using System.Collections.Generic;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>
    /// Top level result for a Delta Analyzer run. Contains one entry per analysed table.
    /// </summary>
    public class DeltaAnalyzerResult
    {
        public List<DeltaTableAnalysis> Tables { get; } = new List<DeltaTableAnalysis>();
    }

    /// <summary>
    /// Per-table analysis of a Delta table stored in OneLake.
    /// </summary>
    public class DeltaTableAnalysis
    {
        public string TableName { get; set; }

        /// <summary>The resolved (or manually supplied) OneLake DFS path to the table folder.</summary>
        public string OneLakePath { get; set; }

        /// <summary>Number of active (non-tombstoned) parquet data files.</summary>
        public long FileCount { get; set; }

        /// <summary>Total number of row groups across all active parquet files (requires footer reads).</summary>
        public long RowGroupCount { get; set; }

        /// <summary>Smallest row count of any single row group (requires footer reads). Null when unknown.</summary>
        public long? MinRowsPerRowGroup { get; set; }

        /// <summary>Average row count per row group (requires footer reads). Null when unknown.</summary>
        public long? AvgRowsPerRowGroup { get; set; }

        /// <summary>Largest row count of any single row group (requires footer reads). Null when unknown.</summary>
        public long? MaxRowsPerRowGroup { get; set; }

        /// <summary>Total number of rows (from delta stats, or footer reads if stats unavailable).</summary>
        public long RowCount { get; set; }

        /// <summary>True/False if V-Order could be determined from parquet metadata, null if unknown.</summary>
        public bool? VOrderEnabled { get; set; }

        /// <summary>True when the table uses Delta liquid clustering.</summary>
        public bool LiquidClusteringEnabled { get; set; }

        /// <summary>Best-effort comma separated list of liquid clustering columns.</summary>
        public string ClusteringColumns { get; set; }

        /// <summary>Comma separated list of partition columns (from delta metaData).</summary>
        public string PartitionColumns { get; set; }

        /// <summary>Total compressed bytes across all parquet files (sum of column chunk sizes). 0 when footers were not read.</summary>
        public long CompressedBytes { get; set; }

        /// <summary>Total uncompressed bytes across all parquet files (sum of column chunk sizes). 0 when footers were not read.</summary>
        public long UncompressedBytes { get; set; }

        /// <summary>Sum of the on-disk parquet file sizes (from the delta log 'add.size').</summary>
        public long TotalFileSizeBytes { get; set; }

        /// <summary>Average on-disk parquet file size in bytes. Null when there are no files.</summary>
        public long? AvgFileSizeBytes { get; set; }

        /// <summary>Largest on-disk parquet file size in bytes. Null when there are no files.</summary>
        public long? MaxFileSizeBytes { get; set; }

        /// <summary>Number of "small" parquet files (below the small-file threshold) that may warrant OPTIMIZE.</summary>
        public long SmallFileCount { get; set; }

        /// <summary>True when any active file has a deletion vector (can degrade Direct Lake performance).</summary>
        public bool HasDeletionVectors { get; set; }

        /// <summary>Number of active files carrying a deletion vector.</summary>
        public long DeletionVectorFileCount { get; set; }

        /// <summary>Total rows logically deleted by deletion vectors across the table's files.</summary>
        public long DeletionVectorRowCount { get; set; }

        /// <summary>Populated with a per-table error message when analysis of this table failed (partial data may still be present).</summary>
        public string Error { get; set; }

        /// <summary>The time the table's data was last modified, from the delta log. Null when unknown.</summary>
        public System.DateTimeOffset? LastModifiedUtc { get; set; }

        /// <summary>True when the OneLake path was auto-resolved (vs supplied via manual override).</summary>
        public bool IsResolved { get; set; }

        public List<DeltaColumnAnalysis> Columns { get; } = new List<DeltaColumnAnalysis>();

        /// <summary>Per-row-group detail (across all active files), in file/row-group order. Requires footer reads.</summary>
        public List<DeltaRowGroupAnalysis> RowGroups { get; } = new List<DeltaRowGroupAnalysis>();
    }

    /// <summary>
    /// Statistics for a single row group within one of a table's parquet files.
    /// </summary>
    public class DeltaRowGroupAnalysis
    {
        /// <summary>File the row group belongs to (leaf file name of the delta 'add' path).</summary>
        public string FileName { get; set; }

        /// <summary>Zero-based index of the row group within its file.</summary>
        public int Index { get; set; }

        public long RowCount { get; set; }
        public long CompressedBytes { get; set; }
        public long UncompressedBytes { get; set; }
    }

    /// <summary>
    /// Per-column aggregated statistics gathered from parquet footers.
    /// </summary>
    public class DeltaColumnAnalysis
    {
        public string ColumnName { get; set; }
        public long RowGroupCount { get; set; }
        public long RowCount { get; set; }
        public long CompressedBytes { get; set; }
        public long UncompressedBytes { get; set; }

        /// <summary>Compression codec used for this column (e.g. SNAPPY, ZSTD).</summary>
        public string Codec { get; set; }

        /// <summary>Encoding summary: "Dictionary", "Plain" (high-cardinality fallback) or "Mixed".</summary>
        public string Encoding { get; set; }
    }

    /// <summary>
    /// Progress payload reported while analysing tables/files.
    /// </summary>
    public class DeltaAnalyzerProgress
    {
        public string Message { get; set; }

        /// <summary>Detail count within the current unit of work (e.g. files read for the current table).</summary>
        public long FilesProcessed { get; set; }

        /// <summary>Detail total for the current unit of work (e.g. files in the current table).</summary>
        public long TotalFiles { get; set; }

        /// <summary>Label for the unit being counted in the detail line (e.g. "files"). Shown in the progress stats text.</summary>
        public string ItemLabel { get; set; } = "files";

        /// <summary>
        /// Overall units of work completed across the whole run. The run is modelled as one unit for
        /// metadata retrieval plus (up to) two units per table (delta-log read + parquet-footer read),
        /// so the bar advances continuously rather than restarting for each phase.
        /// </summary>
        public double OverallCompleted { get; set; }

        /// <summary>Total overall units of work for the run. Zero until the overall plan is known.</summary>
        public double OverallTotal { get; set; }

        /// <summary>
        /// Progress as a percentage (0-100). Uses the continuous overall counters when available,
        /// otherwise falls back to the per-unit detail counts.
        /// </summary>
        public double PercentComplete => OverallTotal > 0
            ? System.Math.Min(100.0, OverallCompleted / OverallTotal * 100.0)
            : (TotalFiles > 0 ? (double)FilesProcessed / TotalFiles * 100.0 : 0.0);
    }

    /// <summary>
    /// A single entry returned by the ADLS Gen2 / OneLake DFS path listing API.
    /// </summary>
    public class OneLakePathItem
    {
        public string Name { get; set; }
        public long ContentLength { get; set; }
        public bool IsDirectory { get; set; }
    }
}
