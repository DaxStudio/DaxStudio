using DaxStudio.Common;
using Parquet;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>Aggregated statistics read from a single parquet file's footer.</summary>
    public class ParquetFileStats
    {
        public long RowGroupCount { get; set; }
        public long RowCount { get; set; }

        /// <summary>Total compressed bytes of all column chunks in the file.</summary>
        public long CompressedBytes { get; set; }

        /// <summary>Total uncompressed bytes of all column chunks in the file.</summary>
        public long UncompressedBytes { get; set; }

        /// <summary>V-Order state as read from the file's key/value metadata; null when it can't be determined.</summary>
        public bool? VOrderEnabled { get; set; }

        /// <summary>Per-column aggregated compressed/uncompressed byte sizes keyed by column path.</summary>
        public Dictionary<string, ParquetColumnStats> Columns { get; } = new Dictionary<string, ParquetColumnStats>(StringComparer.Ordinal);

        /// <summary>Per-row-group statistics (in file order), used to surface row-group row counts.</summary>
        public List<ParquetRowGroupStats> RowGroups { get; } = new List<ParquetRowGroupStats>();

        /// <summary>The delta 'add' path of the file these stats were read from (set by the caller).</summary>
        public string SourceFile { get; set; }
    }

    /// <summary>Per-column aggregated statistics from a parquet footer.</summary>
    public class ParquetColumnStats
    {
        public string ColumnName { get; set; }
        public long CompressedBytes { get; set; }
        public long UncompressedBytes { get; set; }
        public long RowCount { get; set; }
        public long RowGroupCount { get; set; }

        /// <summary>Compression codec used for this column's chunks (e.g. SNAPPY, ZSTD).</summary>
        public string Codec { get; set; }

        /// <summary>Number of column chunks that used dictionary encoding.</summary>
        public long DictionaryChunks { get; set; }

        /// <summary>Total number of column chunks seen for this column.</summary>
        public long TotalChunks { get; set; }
    }

    /// <summary>Statistics for a single row group within a parquet file.</summary>
    public class ParquetRowGroupStats
    {
        public long RowCount { get; set; }
        public long CompressedBytes { get; set; }
        public long UncompressedBytes { get; set; }
    }

    /// <summary>
    /// Reads only the footer / metadata of a parquet file (via an <see cref="HttpRangeStream"/>) to
    /// derive row group counts, row counts, V-Order state and per-column size statistics without
    /// downloading the whole file.
    /// </summary>
    public class ParquetFooterReader
    {
        private readonly OneLakeHttpClient _client;

        public ParquetFooterReader(OneLakeHttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<ParquetFileStats> ReadAsync(string fileUrl, bool readColumnStats, CancellationToken ct)
        {
            var stats = new ParquetFileStats();
            using (var stream = HttpRangeStream.Create(_client, fileUrl, ct))
            using (var reader = await ParquetReader.CreateAsync(stream, null, false, ct).ConfigureAwait(false))
            {
                stats.RowGroupCount = reader.RowGroupCount;
                stats.VOrderEnabled = DetectVOrder(reader);

                var metadata = reader.Metadata;
                if (metadata?.RowGroups != null)
                {
                    foreach (var rg in metadata.RowGroups)
                    {
                        stats.RowCount += rg.NumRows;

                        var rgStats = new ParquetRowGroupStats { RowCount = rg.NumRows };

                        if (rg.Columns == null)
                        {
                            stats.RowGroups.Add(rgStats);
                            continue;
                        }
                        foreach (var col in rg.Columns)
                        {
                            var meta = col?.MetaData;
                            if (meta == null) continue;

                            // Always accumulate file-level byte totals (cheap) so table-level
                            // totals are available even when per-column stats are disabled.
                            stats.CompressedBytes += meta.TotalCompressedSize;
                            stats.UncompressedBytes += meta.TotalUncompressedSize;
                            rgStats.CompressedBytes += meta.TotalCompressedSize;
                            rgStats.UncompressedBytes += meta.TotalUncompressedSize;

                            if (readColumnStats)
                            {
                                var name = meta.PathInSchema != null ? string.Join(".", meta.PathInSchema) : "(unknown)";
                                if (!stats.Columns.TryGetValue(name, out var colStats))
                                {
                                    colStats = new ParquetColumnStats { ColumnName = name };
                                    stats.Columns[name] = colStats;
                                }
                                colStats.CompressedBytes += meta.TotalCompressedSize;
                                colStats.UncompressedBytes += meta.TotalUncompressedSize;
                                colStats.RowCount += meta.NumValues;
                                colStats.RowGroupCount += 1;
                                if (string.IsNullOrEmpty(colStats.Codec)) colStats.Codec = meta.Codec.ToString();
                                colStats.TotalChunks += 1;
                                if (IsDictionaryEncoded(meta)) colStats.DictionaryChunks += 1;
                            }
                        }

                        stats.RowGroups.Add(rgStats);
                    }
                }
            }
            return stats;
        }

        /// <summary>
        /// True when a column chunk uses dictionary encoding. Parquet marks dictionary-encoded data with
        /// RLE_DICTIONARY (v2) or PLAIN_DICTIONARY (v1) in its encodings list. A chunk with neither has
        /// fallen back to PLAIN encoding - a strong high-cardinality signal for Direct Lake.
        /// </summary>
        private static bool IsDictionaryEncoded(Parquet.Meta.ColumnMetaData meta)
        {
            var encodings = meta?.Encodings;
            if (encodings == null) return false;
            foreach (var e in encodings)
            {
                if (e == Parquet.Meta.Encoding.RLE_DICTIONARY || e == Parquet.Meta.Encoding.PLAIN_DICTIONARY)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// V-Order is treated as enabled if any file metadata key contains "vorder" (case-insensitive)
        /// with a truthy value. Returns null when no such key is present (unknown).
        /// </summary>
        private static bool? DetectVOrder(ParquetReader reader)
        {
            try
            {
                var found = false;
                bool? result = null;

                if (reader.CustomMetadata != null)
                {
                    foreach (var kvp in reader.CustomMetadata)
                    {
                        if (kvp.Key != null && kvp.Key.IndexOf("vorder", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true;
                            if (IsTruthy(kvp.Value)) result = true;
                            else if (result != true) result = false;
                        }
                    }
                }

                var kvMeta = reader.Metadata?.KeyValueMetadata;
                if (kvMeta != null)
                {
                    foreach (var kv in kvMeta)
                    {
                        if (kv?.Key != null && kv.Key.IndexOf("vorder", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true;
                            if (IsTruthy(kv.Value)) result = true;
                            else if (result != true) result = false;
                        }
                    }
                }

                return found ? result : (bool?)null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(ParquetFooterReader), nameof(DetectVOrder), "Error detecting V-Order");
                return null;
            }
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var v = value.Trim();
            return v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("1", StringComparison.Ordinal)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || v.Equals("enabled", StringComparison.OrdinalIgnoreCase);
        }
    }
}
