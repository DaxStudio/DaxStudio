using DaxStudio.Common;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>
    /// Orchestrates a Delta Analyzer run: reads each table's delta log, then (optionally) the parquet
    /// footers for row group / V-Order / per-column statistics. Everything is defensive - a failure on
    /// one table is captured into that table's <see cref="DeltaTableAnalysis.Error"/> and the run continues.
    /// </summary>
    public class DeltaAnalyzerService
    {
        /// <summary>
        /// Maximum number of parquet footers read concurrently per table. Footer reads are latency-bound
        /// OneLake range requests, so a bounded degree of parallelism greatly speeds up tables with many
        /// files while keeping memory and connection use in check.
        /// </summary>
        private const int MaxFooterConcurrency = 16;

        /// <summary>
        /// Files below this size are counted as "small". Many small files hurt Direct Lake scan/transcode
        /// performance and usually indicate a table that would benefit from OPTIMIZE / compaction.
        /// </summary>
        private const long SmallFileThresholdBytes = 128L * 1024 * 1024; // 128 MB

        /// <param name="tables">The (tableName, dfsBasePath) pairs to analyse.</param>
        /// <param name="tokenProvider">Returns the current storage-scoped bearer token.</param>
        /// <param name="readParquetFooters">When true, reads parquet footers for row groups / V-Order.</param>
        /// <param name="readColumnStats">When true (and footers are read), aggregates per-column stats.</param>
        /// <param name="progress">Progress sink for UI updates.</param>
        public async Task<DeltaAnalyzerResult> AnalyzeAsync(
            IEnumerable<(string tableName, string dfsBasePath)> tables,
            Func<string> tokenProvider,
            bool readParquetFooters,
            bool readColumnStats,
            IProgress<DeltaAnalyzerProgress> progress,
            CancellationToken ct)
        {
            var result = new DeltaAnalyzerResult();
            var tableList = tables?.ToList() ?? new List<(string, string)>();
            if (tableList.Count == 0) return result;

            var httpClient = new OneLakeHttpClient(tokenProvider);
            var logReader = new DeltaLogReader(httpClient);
            var footerReader = new ParquetFooterReader(httpClient);

            // Phase 1: read the delta log for each table (gives active file list, row count from stats,
            // partition columns and clustering info). Also collect the per-file work for phase 2.
            var perTableFiles = new Dictionary<DeltaTableAnalysis, List<DeltaAddFile>>();
            var perTableColumnMap = new Dictionary<DeltaTableAnalysis, DeltaLogResult>();
            long totalFiles = 0;
            long tableIndex = 0;

            // Overall progress plan: one unit for metadata retrieval (already done by the resolver before
            // this method is called) + one unit per table for the delta-log read + (when footers are read)
            // one unit per table for the parquet-footer read. This keeps the progress bar continuous.
            long tableCount = tableList.Count;
            double overallTotal = 1 + tableCount + (readParquetFooters ? tableCount : 0);

            foreach (var (tableName, dfsBasePath) in tableList)
            {
                ct.ThrowIfCancellationRequested();
                tableIndex++;
                var analysis = new DeltaTableAnalysis
                {
                    TableName = tableName,
                    OneLakePath = dfsBasePath,
                    IsResolved = !string.IsNullOrEmpty(dfsBasePath)
                };
                result.Tables.Add(analysis);

                progress?.Report(new DeltaAnalyzerProgress
                {
                    Message = $"Reading delta log for '{tableName}'...",
                    OverallCompleted = 1 + (tableIndex - 1),
                    OverallTotal = overallTotal
                });

                if (string.IsNullOrWhiteSpace(dfsBasePath))
                {
                    analysis.Error = "No OneLake path was resolved for this table.";
                    continue;
                }

                try
                {
                    // Direct Lake on SQL over a non-schema-enabled lakehouse reports schema "dbo" even
                    // though OneLake stores the table directly under /Tables (no schema folder). Try the
                    // path as-is first, then fall back to a schema-less path when the first is not found.
                    DeltaLogResult logResult = null;
                    var usedPath = dfsBasePath;
                    foreach (var candidate in CandidateTablePaths(dfsBasePath))
                    {
                        ct.ThrowIfCancellationRequested();
                        logResult = await logReader.ReadAsync(candidate, ct).ConfigureAwait(false);
                        usedPath = candidate;
                        if (!IsPathNotFound(logResult.Error)) break;
                    }
                    analysis.OneLakePath = usedPath;

                    analysis.FileCount = logResult.ActiveFiles.Count;
                    analysis.RowCount = logResult.RowCountFromStats;
                    analysis.PartitionColumns = logResult.PartitionColumns;
                    analysis.LiquidClusteringEnabled = logResult.LiquidClusteringEnabled;
                    analysis.ClusteringColumns = logResult.ClusteringColumns;
                    analysis.HasDeletionVectors = logResult.HasDeletionVectors;
                    analysis.DeletionVectorFileCount = logResult.DeletionVectorFileCount;
                    analysis.DeletionVectorRowCount = logResult.DeletionVectorRowCount;
                    analysis.LastModifiedUtc = logResult.LastModifiedUtc;

                    // File-size metrics come straight from the delta log 'add.size' (true on-disk sizes).
                    if (logResult.ActiveFiles.Count > 0)
                    {
                        analysis.TotalFileSizeBytes = logResult.ActiveFiles.Sum(f => f.Size);
                        analysis.AvgFileSizeBytes = (long)logResult.ActiveFiles.Average(f => f.Size);
                        analysis.MaxFileSizeBytes = logResult.ActiveFiles.Max(f => f.Size);
                        analysis.SmallFileCount = logResult.ActiveFiles.Count(f => f.Size > 0 && f.Size < SmallFileThresholdBytes);
                    }
                    if (!string.IsNullOrEmpty(logResult.Error)) analysis.Error = logResult.Error;

                    perTableFiles[analysis] = logResult.ActiveFiles;
                    perTableColumnMap[analysis] = logResult;
                    totalFiles += logResult.ActiveFiles.Count;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerService), nameof(AnalyzeAsync), $"Error reading delta log for {tableName}");
                    analysis.Error = AppendError(analysis.Error, ex.Message);
                }
            }

            // Phase 2: read parquet footers (optional). Progress is determinate across ALL files.
            if (readParquetFooters && totalFiles > 0)
            {
                long footerTableIndex = 0;
                foreach (var kvp in perTableFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    footerTableIndex++;
                    // This table's footer read is the (1 + tableCount + footerTableIndex)-th overall unit.
                    double tableBase = 1 + tableCount + (footerTableIndex - 1);
                    var analysis = kvp.Key;
                    var files = kvp.Value;
                    var location = SafeParse(analysis.OneLakePath);
                    if (location == null)
                    {
                        // Can't build file urls - skip footer reads but keep delta-log data.
                        progress?.Report(new DeltaAnalyzerProgress
                        {
                            Message = $"Reading parquet footers for '{analysis.TableName}'...",
                            OverallCompleted = tableBase + 1,
                            OverallTotal = overallTotal
                        });
                        continue;
                    }

                    long rowGroupCount = 0;
                    long footerRowCount = 0;
                    long tableCompressed = 0;
                    long tableUncompressed = 0;
                    bool? vorder = null;
                    var columnStats = new Dictionary<string, DeltaColumnAnalysis>(StringComparer.Ordinal);
                    var dictChunks = new Dictionary<string, long>(StringComparer.Ordinal);
                    var totalChunks = new Dictionary<string, long>(StringComparer.Ordinal);
                    var rowGroupDetail = new List<DeltaRowGroupAnalysis>();
                    bool anyFooterRead = false;

                    // Read the parquet footers concurrently - these are latency-bound OneLake range reads,
                    // so a bounded degree of parallelism is a large win when a table has hundreds of files.
                    // Only the I/O and progress reporting run in parallel; the results are collected into
                    // thread-safe buffers and aggregated single-threaded afterwards to keep the maths simple.
                    var fileResults = new System.Collections.Concurrent.ConcurrentBag<ParquetFileStats>();
                    var fileErrors = new System.Collections.Concurrent.ConcurrentQueue<string>();
                    long processedInTable = 0;
                    long fileTotalInTable = files.Count;

                    using (var throttle = new SemaphoreSlim(MaxFooterConcurrency))
                    {
                        var footerTasks = files.Select(async file =>
                        {
                            await throttle.WaitAsync(ct).ConfigureAwait(false);
                            try
                            {
                                ct.ThrowIfCancellationRequested();
                                var fileUrl = location.BuildFileUrl(CombineRelative(location, analysis.OneLakePath, file.Path));
                                try
                                {
                                    var fileStats = await footerReader.ReadAsync(fileUrl, readColumnStats, ct).ConfigureAwait(false);
                                    fileStats.SourceFile = file.Path;
                                    fileResults.Add(fileStats);
                                }
                                catch (OperationCanceledException)
                                {
                                    throw;
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerService), nameof(AnalyzeAsync), $"Error reading parquet footer {file.Path}");
                                    fileErrors.Enqueue($"footer error ({file.Path}): {ex.Message}");
                                }

                                var done = Interlocked.Increment(ref processedInTable);
                                double fraction = fileTotalInTable > 0 ? (double)done / fileTotalInTable : 1.0;
                                progress?.Report(new DeltaAnalyzerProgress
                                {
                                    Message = $"Reading parquet footers for '{analysis.TableName}'...",
                                    FilesProcessed = done,
                                    TotalFiles = fileTotalInTable,
                                    ItemLabel = "files",
                                    OverallCompleted = tableBase + fraction,
                                    OverallTotal = overallTotal
                                });
                            }
                            finally
                            {
                                throttle.Release();
                            }
                        }).ToList();

                        await Task.WhenAll(footerTasks).ConfigureAwait(false);
                    }

                    // Aggregate the per-file results single-threaded (order-independent sums / max).
                    foreach (var fileStats in fileResults)
                    {
                        anyFooterRead = true;
                        rowGroupCount += fileStats.RowGroupCount;
                        footerRowCount += fileStats.RowCount;
                        tableCompressed += fileStats.CompressedBytes;
                        tableUncompressed += fileStats.UncompressedBytes;
                        if (fileStats.VOrderEnabled == true) vorder = true;
                        else if (fileStats.VOrderEnabled == false && vorder != true) vorder = false;

                        // Capture per-row-group detail (with the originating leaf file name).
                        var leafName = LeafFileName(fileStats.SourceFile);
                        for (int i = 0; i < fileStats.RowGroups.Count; i++)
                        {
                            var rg = fileStats.RowGroups[i];
                            rowGroupDetail.Add(new DeltaRowGroupAnalysis
                            {
                                FileName = leafName,
                                Index = i,
                                RowCount = rg.RowCount,
                                CompressedBytes = rg.CompressedBytes,
                                UncompressedBytes = rg.UncompressedBytes
                            });
                        }

                        if (readColumnStats)
                        {
                            foreach (var c in fileStats.Columns.Values)
                            {
                                if (!columnStats.TryGetValue(c.ColumnName, out var agg))
                                {
                                    agg = new DeltaColumnAnalysis { ColumnName = c.ColumnName };
                                    columnStats[c.ColumnName] = agg;
                                }
                                agg.CompressedBytes += c.CompressedBytes;
                                agg.UncompressedBytes += c.UncompressedBytes;
                                agg.RowCount += c.RowCount;
                                agg.RowGroupCount += c.RowGroupCount;
                                if (string.IsNullOrEmpty(agg.Codec)) agg.Codec = c.Codec;
                                dictChunks.TryGetValue(c.ColumnName, out var dc);
                                totalChunks.TryGetValue(c.ColumnName, out var tc);
                                dictChunks[c.ColumnName] = dc + c.DictionaryChunks;
                                totalChunks[c.ColumnName] = tc + c.TotalChunks;
                            }
                        }
                    }

                    while (fileErrors.TryDequeue(out var fileError))
                    {
                        analysis.Error = AppendError(analysis.Error, fileError);
                    }

                    if (anyFooterRead)
                    {
                        analysis.RowGroupCount = rowGroupCount;
                        analysis.VOrderEnabled = vorder;
                        analysis.CompressedBytes = tableCompressed;
                        analysis.UncompressedBytes = tableUncompressed;
                        // Prefer delta-stats row count; fall back to footer count when stats were missing.
                        if (analysis.RowCount == 0 && footerRowCount > 0) analysis.RowCount = footerRowCount;

                        if (rowGroupDetail.Count > 0)
                        {
                            // Stable ordering (footer reads complete out of order under parallelism).
                            analysis.RowGroups.Clear();
                            analysis.RowGroups.AddRange(rowGroupDetail
                                .OrderBy(r => r.FileName, StringComparer.Ordinal)
                                .ThenBy(r => r.Index));
                            analysis.MinRowsPerRowGroup = rowGroupDetail.Min(r => r.RowCount);
                            analysis.MaxRowsPerRowGroup = rowGroupDetail.Max(r => r.RowCount);
                            analysis.AvgRowsPerRowGroup = (long)Math.Round(rowGroupDetail.Average(r => r.RowCount));
                        }

                        if (readColumnStats)
                        {
                            var logForTable = perTableColumnMap.TryGetValue(analysis, out var lr) ? lr : null;
                            foreach (var kv in columnStats)
                            {
                                var agg = kv.Value;
                                // Summarise the dictionary/plain encoding mix (keyed by the physical name).
                                dictChunks.TryGetValue(kv.Key, out var dc);
                                totalChunks.TryGetValue(kv.Key, out var tc);
                                agg.Encoding = DescribeEncoding(dc, tc);
                                agg.ColumnName = logForTable?.TranslateColumnName(agg.ColumnName) ?? agg.ColumnName;
                            }
                            analysis.Columns.Clear();
                            analysis.Columns.AddRange(columnStats.Values.OrderBy(c => c.ColumnName));
                        }
                    }
                }
            }

            progress?.Report(new DeltaAnalyzerProgress
            {
                Message = "Analysis complete.",
                OverallCompleted = overallTotal,
                OverallTotal = overallTotal
            });

            return result;
        }

        private static OneLakeLocation SafeParse(string dfsUrl)
        {
            try { return OneLakeLocation.Parse(dfsUrl); }
            catch { return null; }
        }

        /// <summary>True when an error message indicates the OneLake path did not exist (HTTP 404).</summary>
        private static bool IsPathNotFound(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            return error.IndexOf("PathNotFound", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0
                || error.IndexOf("(404", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Yields the OneLake paths to try for a table, in order, stopping at the first that is found.
        /// Two independent ambiguities have to be covered:
        /// <list type="bullet">
        /// <item>a non-schema-enabled lakehouse whose SQL endpoint reports a "dbo" schema that has no
        /// corresponding OneLake folder, so the schema segment must be dropped</item>
        /// <item>a path copied from warehouse table properties, where the table name is wrapped in square
        /// brackets as SQL identifier quoting. A name that itself contains brackets then comes back double
        /// wrapped, and the brackets can equally be part of the real folder name - so the name is tried
        /// as supplied first, then with each layer of wrapping brackets removed.</item>
        /// </list>
        /// </summary>
        private static IEnumerable<string> CandidateTablePaths(string dfsBasePath)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var candidate in LeafVariants(dfsBasePath))
            {
                if (seen.Add(candidate)) yield return candidate;

                var stripped = TryStripSchemaSegment(candidate);
                if (!string.IsNullOrEmpty(stripped) && seen.Add(stripped)) yield return stripped;
            }
        }

        /// <summary>
        /// Yields the path as supplied, then a variant for each successive layer of square brackets
        /// wrapping the table name. The brackets are kept in the first candidate because they can be part
        /// of the real folder name - only if that is not found are the unwrapped readings tried.
        /// </summary>
        private static IEnumerable<string> LeafVariants(string dfsBasePath)
        {
            yield return dfsBasePath;
            if (string.IsNullOrEmpty(dfsBasePath)) yield break;

            var lastSlash = dfsBasePath.LastIndexOf('/');
            if (lastSlash < 0 || lastSlash == dfsBasePath.Length - 1) yield break;

            var prefix = dfsBasePath.Substring(0, lastSlash + 1);
            var leaf = dfsBasePath.Substring(lastSlash + 1);

            // Work on the decoded name so an encoded %5B / %5D is recognised as a bracket.
            string decoded;
            try { decoded = Uri.UnescapeDataString(leaf); }
            catch { yield break; }

            while (decoded.Length > 2 && decoded[0] == '[' && decoded[decoded.Length - 1] == ']')
            {
                decoded = decoded.Substring(1, decoded.Length - 2);
                yield return prefix + Uri.EscapeDataString(decoded);
            }
        }

        /// <summary>
        /// If <paramref name="dfsBasePath"/> ends in <c>.../Tables/{schema}/{table}</c> (exactly two
        /// segments after the Tables folder), returns the path with the schema segment removed; otherwise null.
        /// </summary>
        private static string TryStripSchemaSegment(string dfsBasePath)
        {
            if (string.IsNullOrEmpty(dfsBasePath)) return null;
            const string marker = "/Tables/";
            var idx = dfsBasePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var root = dfsBasePath.Substring(0, idx + marker.Length); // includes trailing /Tables/
            var rest = dfsBasePath.Substring(idx + marker.Length).Trim('/');
            var segments = rest.Split('/');
            if (segments.Length != 2) return null; // only strip when exactly schema/table remain
            return root + segments[1];
        }

        /// <summary>
        /// Delta 'add' paths are relative to the table root. Combine the table directory with the file's
        /// relative path (relative to the filesystem) so <see cref="OneLakeLocation.BuildFileUrl"/> works.
        /// </summary>
        private static string CombineRelative(OneLakeLocation location, string tableDfsUrl, string filePath)
        {
            var tableLoc = OneLakeLocation.Parse(tableDfsUrl);
            var tableDir = tableLoc.Directory.TrimEnd('/');
            // If the delta 'add' path is already absolute-ish, use as-is; otherwise treat as relative to the table dir.
            if (filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return filePath;
            return $"{tableDir}/{filePath.TrimStart('/')}";
        }

        private static string AppendError(string existing, string message)
        {
            return string.IsNullOrEmpty(existing) ? message : existing + "; " + message;
        }

        /// <summary>Returns the leaf file name from a delta 'add' path (which may contain '/' separators).</summary>
        private static string LeafFileName(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var trimmed = path.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            return idx >= 0 ? trimmed.Substring(idx + 1) : trimmed;
        }

        /// <summary>Summarises a column's dictionary/plain encoding mix across its chunks.</summary>
        private static string DescribeEncoding(long dictionaryChunks, long totalChunks)
        {
            if (totalChunks <= 0) return null;
            if (dictionaryChunks == totalChunks) return "Dictionary";
            if (dictionaryChunks == 0) return "Plain";
            return "Mixed";
        }
    }
}
