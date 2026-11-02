using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Core;
using DaxStudio.Core.Connections;
using DaxStudio.Core.DeltaAnalyzer;
using DaxStudio.Core.Extensions;
using DaxStudio.Core.Events;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Navigation;
using CoreConnectionManager = DaxStudio.Core.Connections.ConnectionManager;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>Severity of a Delta Analyzer consideration, driving the icon shown for the table.</summary>
    public enum ConsiderationSeverity
    {
        Info,
        Warning
    }

    /// <summary>
    /// A single potential consideration for a Delta table (e.g. enable V-Order, compact small files). These
    /// are guidance rather than hard best practices. Surfaced in the Considerations column via an icon whose
    /// tooltip lists the titles, with the full detail aggregated in the Considerations tab.
    /// </summary>
    public class DeltaConsideration
    {
        public DeltaConsideration(ConsiderationSeverity severity, string title, string detail)
        {
            Severity = severity;
            Title = title;
            Detail = detail;
        }

        public ConsiderationSeverity Severity { get; }
        public string Title { get; }
        public string Detail { get; }
    }

    /// <summary>
    /// Unified tree-grid row for the Delta Analyzer. Represents either a table (top level) or one of its
    /// columns (child). Metrics that don't apply at a given level are left null so they render as blank
    /// cells, keeping the grid clean (e.g. columns have no file/row/V-Order/clustering information).
    /// </summary>
    /// <remarks>
    /// The explicit <see cref="JsonObjectAttribute"/> is required: the base <see cref="PropertyChangedBase"/>
    /// is marked <c>[DataContract]</c> and Json.NET walks the base type chain looking for that attribute, so
    /// without this the type would be treated as opt-in and every row would serialize as an empty "{}".
    /// </remarks>
    [JsonObject(MemberSerialization.OptOut)]
    public class DeltaTreeRow : PropertyChangedBase
    {
        public string Name { get; set; }
        public bool IsTable { get; set; }
        /// <summary>True for the single "Row Groups (N)" grouping node under a table.</summary>
        public bool IsRowGroupGroup { get; set; }
        /// <summary>True for an individual row-group detail row.</summary>
        public bool IsRowGroup { get; set; }
        public string OneLakePath { get; set; }

        /// <summary>
        /// The "Row Groups (N)" grouping node for a table row, held separately so it can be shown or hidden
        /// via the "Show row group details" option without discarding the underlying detail. Not serialized;
        /// it is re-extracted from <see cref="Children"/> on load.
        /// </summary>
        [JsonIgnore] public DeltaTreeRow RowGroupNode { get; set; }

        // Raw values (nullable so column-level rows can omit table-only metrics).
        public long? FileCount { get; set; }
        public long? RowGroupCount { get; set; }
        public long? RowCount { get; set; }
        public long? CompressedBytes { get; set; }
        public long? UncompressedBytes { get; set; }
        public long? MinRowsPerRowGroup { get; set; }
        public long? AvgRowsPerRowGroup { get; set; }
        public long? MaxRowsPerRowGroup { get; set; }
        public long? AvgFileSizeBytes { get; set; }
        public long? MaxFileSizeBytes { get; set; }
        public long? SmallFileCount { get; set; }
        public bool HasDeletionVectors { get; set; }
        public long DeletionVectorFileCount { get; set; }
        public long DeletionVectorRowCount { get; set; }
        public string Codec { get; set; }
        public string Encoding { get; set; }
        public bool? VOrderEnabled { get; set; }
        public bool LiquidClusteringEnabled { get; set; }
        public string ClusteringColumns { get; set; }
        public string PartitionColumns { get; set; }
        public string Error { get; set; }
        public bool IsResolved { get; set; }

        /// <summary>Time the table's data was last modified (from the delta log). Null / table-level only.</summary>
        public DateTimeOffset? LastModifiedUtc { get; set; }

        /// <summary>
        /// Data-type category for a column row ("Number", "Double", "String", "DateTime", "Boolean"),
        /// resolved by matching the column to the connected model so the tree icon matches the Metadata pane.
        /// Empty for tables, row-group nodes, or unmatched columns (which fall back to a generic column icon).
        /// </summary>
        private string _dataTypeCategory;
        public string DataTypeCategory
        {
            get => _dataTypeCategory;
            set { _dataTypeCategory = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IconImage)); }
        }

        /// <summary>
        /// Resource key of the tree icon, matching the Metadata pane: table icon for tables, the data-type
        /// icon for columns, a folder for the row-group node, and none for individual row groups.
        /// </summary>
        [JsonIgnore]
        public string IconResourceKey =>
            IsTable ? "tableDrawingImage"
            : IsRowGroupGroup ? "folderDrawingImage"
            : IsRowGroup ? null
            : DataTypeCategoryToIconKey(DataTypeCategory);

        /// <summary>
        /// The resolved icon image, bound per-row onto the tree column's Icon (the same binding mechanism
        /// the control uses for the row text, so it renders reliably).
        /// </summary>
        [JsonIgnore]
        public ImageSource IconImage
        {
            get
            {
                var key = IconResourceKey;
                if (string.IsNullOrEmpty(key)) return null;
                return Application.Current?.TryFindResource(key) as ImageSource;
            }
        }

        private static string DataTypeCategoryToIconKey(string category)
        {
            switch (category)
            {
                case "Number": return "numberDrawingImage";
                case "Double": return "doubleDrawingImage";
                case "String": return "stringDrawingImage";
                case "DateTime": return "datetimeDrawingImage";
                case "Boolean": return "booleanDrawingImage";
                default: return "columnDrawingImage";
            }
        }

        /// <summary>Child column rows (empty for column-level rows). Bound as the tree ChildrenBindingPath.</summary>
        public List<DeltaTreeRow> Children { get; } = new List<DeltaTreeRow>();

        /// <summary>
        /// Re-declared purely so the inherited Caliburn.Micro notification flag can be excluded from the
        /// persisted state. Json.NET resolves <c>[JsonIgnore]</c> / <c>ShouldSerialize*</c> against the
        /// declaring type, so the attribute has to sit on an override rather than on the base property.
        /// </summary>
        [JsonIgnore]
        public override bool IsNotifying
        {
            get => base.IsNotifying;
            set => base.IsNotifying = value;
        }

        // Display strings - blank when the underlying value is not applicable at this level.
        [JsonIgnore] public string FileCountDisplay => FileCount?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string RowGroupCountDisplay => RowGroupCount?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string RowCountDisplay => RowCount?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string CompressedBytesDisplay => CompressedBytes.HasValue ? FormatBytes(CompressedBytes.Value) : string.Empty;
        [JsonIgnore] public string UncompressedBytesDisplay => UncompressedBytes.HasValue ? FormatBytes(UncompressedBytes.Value) : string.Empty;
        [JsonIgnore] public string MinRowsPerRowGroupDisplay => MinRowsPerRowGroup?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string AvgRowsPerRowGroupDisplay => AvgRowsPerRowGroup?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string MaxRowsPerRowGroupDisplay => MaxRowsPerRowGroup?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string AvgFileSizeDisplay => AvgFileSizeBytes.HasValue ? FormatBytes(AvgFileSizeBytes.Value) : string.Empty;
        [JsonIgnore] public string MaxFileSizeDisplay => MaxFileSizeBytes.HasValue ? FormatBytes(MaxFileSizeBytes.Value) : string.Empty;
        [JsonIgnore] public string SmallFileCountDisplay => SmallFileCount?.ToString("N0") ?? string.Empty;
        [JsonIgnore] public string CodecDisplay => Codec ?? string.Empty;
        [JsonIgnore] public string EncodingDisplay => Encoding ?? string.Empty;
        [JsonIgnore] public string LastModifiedDisplay => LastModifiedUtc.HasValue
            ? LastModifiedUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : string.Empty;

        /// <summary>
        /// Number of rows per row group considered a healthy Direct Lake segment. Direct Lake frames parquet
        /// row groups directly, so row groups averaging fewer than this many rows produce more (sparser)
        /// segments than ideal and are flagged.
        /// </summary>
        private const long IdealSegmentRows = 1000000;

        /// <summary>
        /// Upper bound for rows per row group. Row groups larger than this may negatively impact
        /// Direct Lake performance and are flagged as potentially too large.
        /// </summary>
        private const long MaxSegmentRows = 16000000;

        /// <summary>Fraction of a table's files that must be "small" (below 128 MB) before we suggest OPTIMIZE.</summary>
        private const double SmallFileShareThreshold = 0.30;

        /// <summary>Fraction of a table's rows that must be soft-deleted before purging deletion vectors becomes a warning.</summary>
        private const double DeletionVectorShareThreshold = 0.05;

        /// <summary>Show a green "v-order" pill when V-Order is enabled for the table.</summary>
        [JsonIgnore] public bool ShowVOrderPill => VOrderEnabled == true;

        /// <summary>Muted V-Order text (No / Unknown) shown only at table level when the pill is not shown.</summary>
        [JsonIgnore] public string VOrderText => ShowVOrderPill || !IsTable
            ? string.Empty
            : (VOrderEnabled == false ? "No" : "Unknown");

        /// <summary>Show a blue "liquid" pill when the table uses liquid clustering.</summary>
        [JsonIgnore] public bool ShowLiquidPill => LiquidClusteringEnabled;

        /// <summary>Show an amber "deletion vectors" pill when the table has any deletion vectors.</summary>
        [JsonIgnore] public bool ShowDeletionVectorPill => IsTable && HasDeletionVectors;

        /// <summary>Pill text for deletion vectors, including the deleted row count when known.</summary>
        [JsonIgnore] public string DeletionVectorText => DeletionVectorRowCount > 0
            ? $"del vec ({DeletionVectorRowCount:N0})"
            : "del vec";

        /// <summary>True for a table whose row groups are, on average, smaller than an ideal Direct Lake segment.</summary>
        [JsonIgnore] public bool ShowSegmentWarningPill => IsTable && RowGroupCount > 1
            && AvgRowsPerRowGroup.HasValue && AvgRowsPerRowGroup.Value < IdealSegmentRows;

        /// <summary>Green "ok" pill when a table's average row group is a healthy segment size.</summary>
        [JsonIgnore] public bool ShowSegmentOkPill => IsTable && AvgRowsPerRowGroup.HasValue
            && !ShowSegmentWarningPill;

        /// <summary>Explains why the segments pill shows "small" (used as its tooltip).</summary>
        [JsonIgnore]
        public string SegmentWarningTooltip => ShowSegmentWarningPill
            ? $"Row groups average {AvgRowsPerRowGroup.Value:N0} rows across {RowGroupCount:N0} row group(s), below the ~{IdealSegmentRows:N0}-row Direct Lake segment target. Smaller row groups produce more, sparser segments - rewrite/OPTIMIZE the table to build larger, denser row groups."
            : null;

        [JsonIgnore] public bool HasError => !string.IsNullOrEmpty(Error);

        /// <summary>
        /// True when this table's analysis failed with an HTTP 403 (Forbidden), which for OneLake typically
        /// means OneLake Security (or missing workspace/data access) is blocking the _delta_log and parquet files.
        /// </summary>
        [JsonIgnore]
        public bool IsAccessDenied => IsTable && !string.IsNullOrEmpty(Error)
            && (Error.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0
                || Error.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Tooltip note shown against the access-denied tag in the Considerations column.</summary>
        [JsonIgnore]
        public string AccessDeniedNote =>
            "Access denied (HTTP 403 Forbidden). OneLake Security may be blocking access to this table's _delta_log and parquet files. Ask a workspace admin to grant you OneLake data access for this item.";

        #region Considerations

        private static readonly IReadOnlyList<DeltaConsideration> EmptyConsiderations = new List<DeltaConsideration>();

        /// <summary>
        /// Potential considerations for this table, derived from its metrics (only tables produce
        /// considerations). These are guidance rather than hard best practices - depending on the workload
        /// the ideal choice can differ. Evaluated lazily each time so it always reflects the current metrics.
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<DeltaConsideration> Considerations
        {
            get
            {
                if (!IsTable) return EmptyConsiderations;

                // When the delta log / parquet files are inaccessible (e.g. OneLake Security / RLS), we have no
                // metrics to reason about, so suppress metric considerations. The access-denied tag is surfaced
                // separately in the Considerations column.
                if (IsAccessDenied) return EmptyConsiderations;

                var list = new List<DeltaConsideration>();

                // 1. V-Order improves Direct Lake read/transcode performance. Fabric only writes the "vorder"
                //    parquet metadata key when V-Order is on, so a non-V-Ordered table reads back as false OR
                //    (more commonly) null. Suggest enabling it whenever we inspected the parquet files
                //    (RowGroupCount is only set when footers were read) but could not confirm V-Order is on.
                if (VOrderEnabled != true && RowGroupCount.HasValue)
                {
                    var detail = VOrderEnabled == false
                        ? "V-Order is not enabled. Rewriting the table with V-Order (e.g. running OPTIMIZE with V-Order on) can improve Direct Lake scan and transcoding performance. It adds some cost when writing, so weigh it against your refresh patterns."
                        : "V-Order was not detected on this table's parquet files. Rewriting the table with V-Order enabled (e.g. running OPTIMIZE with V-Order on) can improve Direct Lake scan and transcoding performance. It adds some cost when writing, so weigh it against your refresh patterns.";
                    list.Add(new DeltaConsideration(ConsiderationSeverity.Warning, "Consider enabling V-Order", detail));
                }

                // 2. Many small files increase file-open and transcoding overhead; OPTIMIZE compacts them.
                if (FileCount.HasValue && FileCount.Value > 1 && SmallFileCount.HasValue && SmallFileCount.Value >= 2
                    && SmallFileCount.Value >= FileCount.Value * SmallFileShareThreshold)
                {
                    list.Add(new DeltaConsideration(ConsiderationSeverity.Warning,
                        "Consider compacting small files",
                        $"{SmallFileCount.Value:N0} of {FileCount.Value:N0} files are below 128 MB. Running OPTIMIZE to compact them into fewer, larger files can reduce file-open and Direct Lake transcoding overhead."));
                }

                // 3. Undersized row groups produce more, sparser Direct Lake segments than ideal. This is a
                //    guideline - a handful of row groups (up to ~16) can actually aid scan parallelism, so this
                //    is most relevant when there are many small row groups.
                if (RowGroupCount > 1 && AvgRowsPerRowGroup.HasValue && AvgRowsPerRowGroup.Value < IdealSegmentRows)
                {
                    list.Add(new DeltaConsideration(ConsiderationSeverity.Info,
                        "Row groups are smaller than the target size",
                        $"Row groups average {AvgRowsPerRowGroup.Value:N0} rows, below the ~{IdealSegmentRows:N0}-row guideline (8-16 million rows is often a good target). Denser row groups usually scan more efficiently, though keeping up to ~16 row groups can help scan parallelism, so this is a guideline rather than a hard rule."));
                }

                // 3b. Oversized row groups may negatively impact Direct Lake performance.
                if (MaxRowsPerRowGroup.HasValue && MaxRowsPerRowGroup.Value > MaxSegmentRows)
                {
                    list.Add(new DeltaConsideration(ConsiderationSeverity.Info,
                        "Row groups may be too large",
                        $"The largest row group has {MaxRowsPerRowGroup.Value:N0} rows, above ~{MaxSegmentRows:N0} rows. Very large row groups can potentially have a negative impact on performance and reduce scan parallelism - rewriting/OPTIMIZE so row groups stay around 8-16 million rows may help."));
                }

                // 4. Deletion vectors leave soft-deleted rows that Direct Lake still reads; PURGE removes them.
                if (HasDeletionVectors)
                {
                    var share = RowCount > 0 ? (double)DeletionVectorRowCount / RowCount : 0d;
                    var severity = share >= DeletionVectorShareThreshold ? ConsiderationSeverity.Warning : ConsiderationSeverity.Info;
                    list.Add(new DeltaConsideration(severity,
                        "Consider purging deletion vectors",
                        $"{DeletionVectorRowCount:N0} row(s) across {DeletionVectorFileCount:N0} file(s) are logically deleted via deletion vectors. Running REORG TABLE ... APPLY (PURGE) (or OPTIMIZE) physically removes them - Direct Lake still reads soft-deleted rows until they are purged."));
                }

                // 5. Partitioning fragments a table into more (often smaller) files. Direct Lake generally
                //    prefers fewer, larger files, but over-partitioning on a column that is used as a filter in
                //    most queries (e.g. a date column) can still be beneficial - so this is guidance.
                if (!string.IsNullOrWhiteSpace(PartitionColumns))
                {
                    var manySmallFiles = FileCount.HasValue && FileCount.Value > 1 && SmallFileCount.HasValue
                        && SmallFileCount.Value >= 2 && SmallFileCount.Value >= FileCount.Value * SmallFileShareThreshold;
                    var severity = manySmallFiles ? ConsiderationSeverity.Warning : ConsiderationSeverity.Info;
                    var detail = manySmallFiles
                        ? $"The table is partitioned by [{PartitionColumns}] and {SmallFileCount.Value:N0} of {FileCount.Value:N0} files are below 128 MB. Partitioning has fragmented the data into small files - consider whether liquid clustering + OPTIMIZE would suit better. That said, partitioning on a column used as a filter in most queries (e.g. a date column) can still be worthwhile."
                        : $"The table is partitioned by [{PartitionColumns}]. Direct Lake generally prefers fewer, larger files, so partitioning is worth reviewing. It can still help when the partition column is used as a filter in most queries (e.g. a date column); otherwise liquid clustering is often a good alternative.";
                    list.Add(new DeltaConsideration(severity, "Review partitioning strategy", detail));
                }

                return list;
            }
        }

        [JsonIgnore] public bool HasConsiderations => Considerations.Count > 0;

        /// <summary>Number of considerations (used as the sort key for the Considerations column).</summary>
        [JsonIgnore] public int ConsiderationCount => Considerations.Count;

        /// <summary>True when at least one consideration is a warning (drives the warning vs. info icon).</summary>
        [JsonIgnore] public bool HasWarningConsideration => Considerations.Any(r => r.Severity == ConsiderationSeverity.Warning);

        /// <summary>Consideration count shown next to the icon; blank when there are none.</summary>
        [JsonIgnore] public string ConsiderationCountText => HasConsiderations ? Considerations.Count.ToString("N0") : string.Empty;

        /// <summary>Icon shown in the Considerations column: a warning triangle if any warning, otherwise an info glyph.</summary>
        [JsonIgnore]
        public ImageSource ConsiderationIcon
        {
            get
            {
                if (!HasConsiderations) return null;
                var key = HasWarningConsideration ? "warningDrawingImage" : "infoDrawingImage";
                return Application.Current?.TryFindResource(key) as ImageSource;
            }
        }

        #endregion


        /// <summary>Formats a byte count as a human-readable size (B/KB/MB/GB/TB), matching the Model Diagram view.</summary>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int suffixIndex = 0;
            double size = bytes;
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            // Whole bytes don't need a decimal place; larger units show one.
            return suffixIndex == 0 ? $"{bytes:N0} {suffixes[0]}" : $"{size:N1} {suffixes[suffixIndex]}";
        }

        public static DeltaTreeRow FromAnalysis(DeltaTableAnalysis a)
        {
            var row = new DeltaTreeRow
            {
                Name = a.TableName,
                IsTable = true,
                OneLakePath = a.OneLakePath,
                FileCount = a.FileCount,
                RowGroupCount = a.RowGroupCount,
                RowCount = a.RowCount,
                CompressedBytes = a.CompressedBytes,
                UncompressedBytes = a.UncompressedBytes,
                MinRowsPerRowGroup = a.MinRowsPerRowGroup,
                AvgRowsPerRowGroup = a.AvgRowsPerRowGroup,
                MaxRowsPerRowGroup = a.MaxRowsPerRowGroup,
                AvgFileSizeBytes = a.AvgFileSizeBytes,
                MaxFileSizeBytes = a.MaxFileSizeBytes,
                SmallFileCount = a.FileCount > 0 ? (long?)a.SmallFileCount : null,
                HasDeletionVectors = a.HasDeletionVectors,
                DeletionVectorFileCount = a.DeletionVectorFileCount,
                DeletionVectorRowCount = a.DeletionVectorRowCount,
                VOrderEnabled = a.VOrderEnabled,
                LiquidClusteringEnabled = a.LiquidClusteringEnabled,
                ClusteringColumns = a.ClusteringColumns,
                LastModifiedUtc = a.LastModifiedUtc,
                PartitionColumns = a.PartitionColumns,
                Error = a.Error,
                IsResolved = a.IsResolved
            };
            foreach (var c in a.Columns)
            {
                // Column rows carry only the metrics that make sense per-column; everything else stays blank.
                row.Children.Add(new DeltaTreeRow
                {
                    Name = c.ColumnName,
                    IsTable = false,
                    RowGroupCount = c.RowGroupCount,
                    CompressedBytes = c.CompressedBytes,
                    UncompressedBytes = c.UncompressedBytes,
                    Codec = c.Codec,
                    Encoding = c.Encoding
                });
            }

            // Add an expandable "Row Groups (N)" node with one child per row group so users can inspect
            // the row-count distribution (relevant for Direct Lake segment sizing). Kept under a single
            // node so it doesn't clutter the column list.
            if (a.RowGroups != null && a.RowGroups.Count > 0)
            {
                var groupNode = new DeltaTreeRow
                {
                    Name = $"Row Groups ({a.RowGroups.Count:N0})",
                    IsTable = false,
                    IsRowGroupGroup = true
                };
                foreach (var rg in a.RowGroups)
                {
                    groupNode.Children.Add(new DeltaTreeRow
                    {
                        Name = $"{rg.FileName} \u00B7 RG{rg.Index}",
                        IsTable = false,
                        IsRowGroup = true,
                        RowCount = rg.RowCount,
                        CompressedBytes = rg.CompressedBytes,
                        UncompressedBytes = rg.UncompressedBytes
                    });
                }
                // Held separately - the ViewModel adds it to Children only when row group details are enabled.
                row.RowGroupNode = groupNode;
            }
            return row;
        }
    }

    /// <summary>
    /// Delta Analyzer tool window ViewModel. Reads Delta table metadata from OneLake for Direct Lake
    /// models. Gated behind the ShowDeltaAnalyzer preview option. Displays a progress overlay while
    /// processing, mirroring the Model Diagram loading overlay.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class DeltaAnalyzerViewModel : ToolWindowBase, ISaveState
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;
        private CancellationTokenSource _cts;

        [ImportingConstructor]
        public DeltaAnalyzerViewModel(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _options = options;
        }

        #region ToolWindowBase implementation

        public override string Title => "Delta Analyzer";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "delta-analyzer";
        public override bool CanHide => true;

        #endregion

        #region Properties

        public BindableCollection<DeltaTreeRow> TreeRows { get; } = new BindableCollection<DeltaTreeRow>();

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanAnalyze));
                NotifyOfPropertyChange(nameof(CanCancel));
                NotifyOfPropertyChange(nameof(CanExportCsv));
                NotifyOfPropertyChange(nameof(CanCopyConsiderations));
                NotifyOfPropertyChange(nameof(CanExportConsiderations));
            }
        }

        private string _loadingMessage = "Analyzing delta tables...";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set { _loadingMessage = value; NotifyOfPropertyChange(); }
        }

        private string _loadingStats = string.Empty;
        public string LoadingStats
        {
            get => _loadingStats;
            set { _loadingStats = value; NotifyOfPropertyChange(); }
        }

        private double _progressValue;
        /// <summary>Determinate progress (0-100) based on files processed / total files.</summary>
        public double ProgressValue
        {
            get => _progressValue;
            set { _progressValue = value; NotifyOfPropertyChange(); }
        }

        private string _overridePath = string.Empty;
        /// <summary>
        /// Manual OneLake path override. Should point at a table folder, or (when paths are auto-resolved
        /// but the workspace/lakehouse can't be derived) the <c>.../Tables</c> root used to rebuild paths.
        /// </summary>
        public string OverridePath
        {
            get => _overridePath;
            set { _overridePath = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanAnalyze)); }
        }

        private bool _hasData;
        public bool HasData
        {
            get => _hasData;
            set
            {
                _hasData = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(NoData));
                NotifyOfPropertyChange(nameof(ShowNoDataMessage));
                NotifyOfPropertyChange(nameof(CanExportCsv));
                NotifyOfPropertyChange(nameof(HasAnyConsiderations));
                NotifyOfPropertyChange(nameof(CanCopyConsiderations));
                NotifyOfPropertyChange(nameof(CanExportConsiderations));
            }
        }

        public bool NoData => !HasData && !IsLoading;

        /// <summary>Show the empty-state hint only when there's no data AND no status banner is displayed.</summary>
        public bool ShowNoDataMessage => NoData && !HasStatusMessage;

        private string _summaryText = string.Empty;
        public string SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; NotifyOfPropertyChange(); }
        }

        private FlowDocument _considerationsDocument;
        /// <summary>
        /// A read-only formatted document aggregating the considerations for every analyzed table, shown in
        /// the "Considerations" tab. Rebuilt after each analysis (and on state reload).
        /// </summary>
        public FlowDocument ConsiderationsDocument
        {
            get => _considerationsDocument;
            set { _considerationsDocument = value; NotifyOfPropertyChange(); }
        }

        private string _statusMessage = string.Empty;
        /// <summary>
        /// An inline status/error message shown as a banner inside the tool window (e.g. when the model's
        /// metadata can't be read, or no Direct Lake tables were found), so the user doesn't have to check
        /// the Output pane to see why an analysis produced no results.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasStatusMessage));
                NotifyOfPropertyChange(nameof(ShowNoDataMessage));
            }
        }

        [JsonIgnore] public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

        private bool _statusIsError;
        /// <summary>True when <see cref="StatusMessage"/> represents an error (red) vs. an informational warning (amber).</summary>
        public bool StatusIsError
        {
            get => _statusIsError;
            set { _statusIsError = value; NotifyOfPropertyChange(); }
        }

        /// <summary>Sets the inline banner message and severity in one call.</summary>
        private void SetStatus(string message, bool isError)
        {
            Execute.OnUIThread(() =>
            {
                StatusIsError = isError;
                StatusMessage = message;
            });
        }

        /// <summary>Clears the inline banner (called at the start of each run).</summary>
        private void ClearStatus() => SetStatus(string.Empty, false);

        private bool _readParquetFooters = true;
        /// <summary>When true, read parquet footers for row group counts / V-Order (slower).</summary>
        public bool ReadParquetFooters
        {
            get => _readParquetFooters;
            set { _readParquetFooters = value; NotifyOfPropertyChange(); }
        }

        private bool _readColumnStats = true;
        /// <summary>When true (and footers are read), gather per-column statistics.</summary>
        public bool ReadColumnStats
        {
            get => _readColumnStats;
            set { _readColumnStats = value; NotifyOfPropertyChange(); }
        }

        private bool _showRowGroupDetails;
        /// <summary>When true, each table shows an expandable "Row Groups (N)" node with per-row-group detail.</summary>
        public bool ShowRowGroupDetails
        {
            get => _showRowGroupDetails;
            set { _showRowGroupDetails = value; NotifyOfPropertyChange(); ApplyRowGroupDetails(); }
        }

        /// <summary>
        /// True when there is a usable, open model connection. The connection object can be non-null but
        /// not actually open (the window can be opened without connecting, to analyze a manual OneLake
        /// path), so callers must check the connection state rather than just for null.
        /// </summary>
        private bool HasModelConnection => _lastConnection != null && _lastConnection.IsConnected;

        public bool CanAnalyze => !IsLoading && (HasModelConnection || !string.IsNullOrWhiteSpace(OverridePath));
        public bool CanCancel => IsLoading;
        public bool CanExportCsv => HasData && !IsLoading;

        /// <summary>True when at least one analyzed table produced a consideration.</summary>
        public bool HasAnyConsiderations => TreeRows.Any(t => t.IsTable && t.HasConsiderations);

        public bool CanCopyConsiderations => HasData && !IsLoading && HasAnyConsiderations;
        public bool CanExportConsiderations => CanCopyConsiderations;

        /// <summary>
        /// Names the table from the OneLake folder that was actually found, rather than from the path the
        /// user supplied. A path copied from warehouse table properties wraps the table name in square
        /// brackets as SQL identifier quoting, so the supplied leaf can carry a layer of quoting that the
        /// real folder does not have - the analysis strips it while probing for the folder that exists, and
        /// that resolved name is the one to display. Only applies when the manual path named a single
        /// table; auto-resolved tables keep the authoritative name from the model metadata.
        /// </summary>
        private static void RenameManualTableFromResolvedPath(string overridePath, DeltaAnalyzerResult result)
        {
            if (string.IsNullOrWhiteSpace(overridePath) || result?.Tables == null || result.Tables.Count != 1) return;
            if (!DirectLakePathResolver.TryGetTableFromPath(overridePath, out _)) return;

            var table = result.Tables[0];
            if (DirectLakePathResolver.TryGetTableFromPath(table.OneLakePath, out var resolvedName)
                && !string.IsNullOrEmpty(resolvedName))
            {
                table.TableName = resolvedName;
            }
        }

        /// <summary>
        /// Builds the single status message shown when every table failed. OneLake deliberately answers
        /// "not found" both for a path that does not exist and for one the caller has no access to, so the
        /// message names the account the storage token was issued to and the paths that were actually
        /// requested - otherwise a permissions problem is indistinguishable from a typo.
        /// </summary>
        private string BuildFailureMessage(IReadOnlyList<DeltaTableAnalysis> erroredTables)
        {
            var message = erroredTables.Select(t => t.Error).Distinct().First();

            var isNotFound = message.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0
                          || message.IndexOf("PathNotFound", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isNotFound) return message;

            var sb = new StringBuilder(message);
            sb.Append(" OneLake reports 'not found' both for a path that does not exist and for one your account cannot access.");

            var attempted = erroredTables
                .Select(t => t.OneLakePath)
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .Take(3)
                .ToList();
            if (attempted.Count > 0)
            {
                sb.Append($" Requested: {string.Join(", ", attempted)}.");
            }

            sb.Append(string.IsNullOrEmpty(_storageAccountName)
                ? " No account could be identified for the OneLake connection."
                : $" Signed in as '{_storageAccountName}' - check that this account has access to the workspace.");

            return sb.ToString();
        }

        /// <summary>
        /// Validates a manual OneLake path override. A valid path is an absolute http(s) URL pointing at a
        /// OneLake DFS endpoint (host contains "onelake" or "dfs.fabric"). Returns false with a clean,
        /// user-friendly message in <paramref name="error"/> when the input is not usable, so the caller
        /// can surface a single warning instead of a low-level parse/network exception.
        /// </summary>
        private static bool IsValidOneLakePath(string path, out string error)
        {
            const string example = "https://onelake.dfs.fabric.microsoft.com/<workspace>/<lakehouse>.Lakehouse/Tables/<schema>/<table>";
            if (string.IsNullOrWhiteSpace(path))
            {
                error = $"Enter a OneLake path, for example {example}";
                return false;
            }
            if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = $"'{path}' is not a valid OneLake URL. It should look like {example}";
                return false;
            }
            if (uri.Host.IndexOf("onelake", StringComparison.OrdinalIgnoreCase) < 0
                && uri.Host.IndexOf("dfs.fabric", StringComparison.OrdinalIgnoreCase) < 0)
            {
                error = $"'{uri.Host}' is not a OneLake endpoint. The path should look like {example}";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Adds or removes the per-table "Row Groups (N)" node from each table row's children to match
        /// <see cref="ShowRowGroupDetails"/>, then refreshes the tree so the change is picked up.
        /// </summary>
        private void ApplyRowGroupDetails()
        {
            var rows = TreeRows.ToList();
            var changed = false;
            foreach (var r in rows)
            {
                if (r.RowGroupNode == null) continue;
                var present = r.Children.Contains(r.RowGroupNode);
                if (ShowRowGroupDetails && !present) { r.Children.Add(r.RowGroupNode); changed = true; }
                else if (!ShowRowGroupDetails && present) { r.Children.Remove(r.RowGroupNode); changed = true; }
            }

            if (changed)
            {
                // The tree grid reads the Children list once; reset the root collection to force it to re-read.
                RefreshTreeRows(rows);
            }

            // Preserve the active sort order across row-group show/hide toggles.
            ReapplySort();
        }

        #endregion

        #region Sorting

        private string _sortProperty;
        private bool _sortDescending;

        /// <summary>
        /// Sorts the tree in two levels - first the tables, then the columns within each table - by the given
        /// <see cref="DeltaTreeRow"/> property. Row-group nodes (and their children) keep their original order
        /// and the "Row Groups (N)" node stays at the end of each table's children.
        /// </summary>
        public void SortTree(string propertyName, bool descending)
        {
            var prop = typeof(DeltaTreeRow).GetProperty(propertyName);
            if (prop == null) return;

            _sortProperty = propertyName;
            _sortDescending = descending;

            var sortedTables = StableSort(TreeRows, prop, descending);
            foreach (var table in sortedTables)
            {
                var groupNode = table.Children.FirstOrDefault(c => c.IsRowGroupGroup);
                var columns = table.Children.Where(c => !c.IsRowGroupGroup).ToList();
                var sortedColumns = StableSort(columns, prop, descending);

                table.Children.Clear();
                table.Children.AddRange(sortedColumns);
                if (groupNode != null) table.Children.Add(groupNode);
            }

            // Reset the root collection so the tree grid re-reads the re-ordered rows and children.
            RefreshTreeRows(sortedTables);
        }

        /// <summary>
        /// Replaces the contents of <see cref="TreeRows"/> with the supplied (re-ordered) rows while raising only a
        /// single collection-reset notification. The tree grid rebuilds on Reset but preserves each row's expanded
        /// state by item identity, so re-sorting or toggling row-group details keeps expanded tables expanded.
        /// </summary>
        private void RefreshTreeRows(IEnumerable<DeltaTreeRow> ordered)
        {
            var items = ordered.ToList();
            var wasNotifying = TreeRows.IsNotifying;
            TreeRows.IsNotifying = false;
            try
            {
                TreeRows.Clear();
                foreach (var r in items) TreeRows.Add(r);
            }
            finally
            {
                TreeRows.IsNotifying = wasNotifying;
            }
            // A single Reset fires while the collection already holds the re-ordered items, allowing the grid to
            // rebuild the hierarchy with preserved expansion state instead of collapsing everything.
            TreeRows.Refresh();
        }

        /// <summary>Re-applies the current sort, if one is active (e.g. after a reload or a row-group toggle).</summary>
        private void ReapplySort()
        {
            if (!string.IsNullOrEmpty(_sortProperty)) SortTree(_sortProperty, _sortDescending);
        }

        /// <summary>Stable sort by a <see cref="DeltaTreeRow"/> property, keeping the original order for equal keys.</summary>
        private static List<DeltaTreeRow> StableSort(IEnumerable<DeltaTreeRow> rows, System.Reflection.PropertyInfo prop, bool descending)
        {
            var indexed = rows.Select((r, i) => (row: r, index: i)).ToList();
            indexed.Sort((a, b) =>
            {
                var cmp = CompareValues(prop.GetValue(a.row), prop.GetValue(b.row));
                if (descending) cmp = -cmp;
                return cmp != 0 ? cmp : a.index.CompareTo(b.index);
            });
            return indexed.Select(x => x.row).ToList();
        }

        private static int CompareValues(object a, object b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            if (a is string sa && b is string sb) return string.Compare(sa, sb, StringComparison.OrdinalIgnoreCase);
            if (a is IComparable ca) return ca.CompareTo(b);
            return 0;
        }

        #endregion

        private CoreConnectionManager _lastConnection;
        private IntPtr _lastWindowHandle;

        /// <summary>
        /// A short note describing the Direct Lake storage mode of the analyzed model (Direct Lake on SQL vs.
        /// Direct Lake on OneLake), derived from whether any table's source is a SQL analytics endpoint.
        /// Appended to the summary footer. Empty when the mode could not be determined (e.g. manual override).
        /// </summary>
        private string _directLakeModeNote = string.Empty;

        /// <summary>
        /// The Entra account the OneLake storage token was last issued to. The token is usually acquired
        /// silently, so there is no sign-in prompt to tell the user which identity is in play - this is
        /// reported when a request fails so an access problem can be told apart from a bad path.
        /// </summary>
        private string _storageAccountName;

        /// <summary>
        /// Entry point called from the ribbon. Resolves OneLake paths, acquires a storage token and runs
        /// the analysis. Safe to call multiple times (e.g. from a Refresh button).
        /// </summary>
        public void LoadFromConnection(IConnectionManager connection, IntPtr windowHandle)
        {
            _lastConnection = connection as CoreConnectionManager;
            _lastWindowHandle = windowHandle;
            NotifyOfPropertyChange(nameof(CanAnalyze));
            AnalyzeAsync().FireAndForget();
        }

        /// <summary>Kicks off (or re-runs) the delta analysis on a background task.</summary>
        public async Task AnalyzeAsync()
        {
            if (IsLoading) return;
            if (!HasModelConnection && string.IsNullOrWhiteSpace(OverridePath))
            {
                // No model connection and no manual path - prompt the user rather than raising a warning,
                // since the window can be opened without a connection specifically to enter a OneLake path.
                SetStatus("Connect to a Direct Lake model, or enter a OneLake path above, then click Analyze.", false);
                return;
            }

            // When a manual OneLake path is supplied, normalize it (abfss:// form, stray quotes or square
            // brackets, trailing slash) and validate it up-front so obviously invalid input produces a
            // single clean warning rather than a low-level parse or network exception surfacing later with
            // a stack trace. The normalized value is written back so the user can see what will be used.
            if (!string.IsNullOrWhiteSpace(OverridePath))
            {
                var normalizedPath = DirectLakePathResolver.NormalizeOneLakePath(OverridePath);
                if (!string.Equals(normalizedPath, OverridePath, StringComparison.Ordinal)) OverridePath = normalizedPath;

                if (!IsValidOneLakePath(normalizedPath, out var pathError))
                {
                    SetStatus(pathError, true);
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, pathError));
                    return;
                }
            }

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            IsLoading = true;
            HasData = false;
            ProgressValue = 0;
            LoadingMessage = string.IsNullOrWhiteSpace(OverridePath)
                ? "Resolving Direct Lake table paths..."
                : "Connecting to OneLake...";
            LoadingStats = string.Empty;
            ClearStatus();
            TreeRows.Clear();
            _directLakeModeNote = string.Empty;

            var connection = _lastConnection;
            var overridePath = OverridePath?.Trim();
            var readFooters = ReadParquetFooters;
            var readColumns = ReadColumnStats;
            // The pane can be restored from a saved package (or used with no model connection at all)
            // without ever going through LoadFromConnection, leaving no owner window for the sign-in
            // dialog. Fall back to the main window so an interactive prompt can still be shown when there
            // is no cached token, instead of silently failing to authenticate.
            if (_lastWindowHandle == IntPtr.Zero)
            {
                _lastWindowHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
            var windowHandle = _lastWindowHandle;

            try
            {
                var result = await Task.Run(async () =>
                    await RunAnalysisAsync(connection, overridePath, readFooters, readColumns, windowHandle, ct)
                        .ConfigureAwait(false), ct).ConfigureAwait(true);

                if (result == null) return; // handled/aborted inside (message already shown)

                // If the whole run produced no usable data and every table reported an error (e.g. a manual
                // OneLake path that could not be read), surface the error as a single clean status message
                // rather than populating the grid with an empty error-only row.
                var erroredTables = result.Tables.Where(t => !string.IsNullOrEmpty(t.Error)).ToList();
                if (result.Tables.Count > 0
                    && erroredTables.Count == result.Tables.Count
                    && result.Tables.All(t => t.FileCount == 0))
                {
                    var message = BuildFailureMessage(erroredTables);
                    SetStatus(message, true);
                    await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, message));
                    return;
                }

                RenameManualTableFromResolvedPath(overridePath, result);

                TreeRows.Clear();
                foreach (var t in result.Tables.OrderBy(t => t.TableName))
                {
                    TreeRows.Add(DeltaTreeRow.FromAnalysis(t));
                }

                ApplyColumnDataTypes(connection);

                HasData = TreeRows.Count > 0;
                SummaryText = BuildSummary(TreeRows, _directLakeModeNote);
                ConsiderationsDocument = BuildConsiderationsDocument(TreeRows);
                ApplyRowGroupDetails();
            }
            catch (OperationCanceledException)
            {
                Log.Information(Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(AnalyzeAsync), "Delta analysis was cancelled");
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(AnalyzeAsync), "Error running delta analysis");
                SetStatus($"Error running Delta Analyzer: {ex.Message}", true);
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error running Delta Analyzer: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
                NotifyOfPropertyChange(nameof(NoData));
                NotifyOfPropertyChange(nameof(ShowNoDataMessage));
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Enriches column rows with the data-type category from the connected model so the tree icons
        /// match the Metadata pane (number / string / datetime / etc.). Matching is by table + column name
        /// (case-insensitive, on both Name and Caption). Failures are non-fatal - columns simply keep the
        /// generic column icon.
        /// </summary>
        private void ApplyColumnDataTypes(CoreConnectionManager connection)
        {
            if (connection == null || !connection.IsConnected || TreeRows.Count == 0) return;
            int matched = 0, unmatched = 0;
            try
            {
                // Build a table -> (column -> category) lookup from the connected model metadata.
                var lookup = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var table in connection.GetTables())
                {
                    var cols = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in table.Columns)
                    {
                        var category = MapDataTypeCategory(col.DataType);
                        if (string.IsNullOrEmpty(category)) continue;
                        if (!string.IsNullOrEmpty(col.Name)) cols[col.Name] = category;
                        if (!string.IsNullOrEmpty(col.Caption)) cols[col.Caption] = category;
                    }
                    if (!string.IsNullOrEmpty(table.Name)) lookup[table.Name] = cols;
                    if (!string.IsNullOrEmpty(table.Caption)) lookup[table.Caption] = cols;
                }

                foreach (var tableRow in TreeRows)
                {
                    if (!lookup.TryGetValue(tableRow.Name ?? string.Empty, out var cols)) continue;
                    foreach (var colRow in tableRow.Children)
                    {
                        if (colRow.IsRowGroupGroup || colRow.IsRowGroup) continue;
                        if (cols.TryGetValue(colRow.Name ?? string.Empty, out var category))
                        {
                            colRow.DataTypeCategory = category;
                            matched++;
                        }
                        else
                        {
                            unmatched++;
                        }
                    }
                }

                Log.Information(Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(ApplyColumnDataTypes),
                    $"Resolved column data types for tree icons: {matched} matched, {unmatched} unmatched");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(ApplyColumnDataTypes), "Unable to resolve column data types for tree icons");
            }
        }

        /// <summary>Maps a tabular data type to the icon category used by the Metadata pane tree.</summary>
        private static string MapDataTypeCategory(Microsoft.AnalysisServices.Tabular.DataType dataType)
        {
            switch (dataType)
            {
                case Microsoft.AnalysisServices.Tabular.DataType.Boolean: return "Boolean";
                case Microsoft.AnalysisServices.Tabular.DataType.DateTime: return "DateTime";
                case Microsoft.AnalysisServices.Tabular.DataType.Double:
                case Microsoft.AnalysisServices.Tabular.DataType.Decimal: return "Double";
                case Microsoft.AnalysisServices.Tabular.DataType.Int64: return "Number";
                case Microsoft.AnalysisServices.Tabular.DataType.String: return "String";
                default: return string.Empty;
            }
        }


        private async Task<DeltaAnalyzerResult> RunAnalysisAsync(
            CoreConnectionManager connection, string overridePath, bool readFooters, bool readColumns, IntPtr windowHandle, CancellationToken ct)
        {
            // Progress sink shared across all phases. The overall percentage is driven by the continuous
            // OverallCompleted/OverallTotal counters reported by the analysis service (1 unit for metadata
            // retrieval + up to 2 per table), so the bar never restarts between phases. The detail line
            // shows the per-unit counts (e.g. files within the current table) when available.
            var progress = new Progress<DeltaAnalyzerProgress>(p =>
            {
                LoadingMessage = p.Message;
                if (p.OverallTotal > 0)
                {
                    // Footer reads report progress from multiple threads, so reports can arrive slightly
                    // out of order - clamp to keep the bar monotonic within a run (it is reset to 0 in
                    // AnalyzeAsync before each run starts).
                    ProgressValue = Math.Max(ProgressValue, p.PercentComplete);
                }
                LoadingStats = p.TotalFiles > 0
                    ? $"{p.FilesProcessed:N0} / {p.TotalFiles:N0} {p.ItemLabel}"
                    : string.Empty;
            });

            // 1. Resolve the Direct Lake table paths from TOM metadata.
            var resolver = new DirectLakePathResolver();
            overridePath = DirectLakePathResolver.NormalizeOneLakePath(overridePath);
            bool hasOverride = !string.IsNullOrWhiteSpace(overridePath);

            // A manual path can be either the .../Tables root that every table path is rebuilt from, or a
            // path that already identifies a single table (.../Tables/{schema}/{table}). In the latter case
            // there is nothing to look up in the model, so skip the metadata pass entirely - otherwise we
            // would enumerate every table in the connected model and append its name to a path that is
            // already a table folder, producing paths that can only 404.
            string overrideTableName = null;
            bool overrideIsSingleTable = hasOverride
                && DirectLakePathResolver.TryGetTableFromPath(overridePath, out overrideTableName);

            // The connection object can be non-null but not actually open (e.g. the Delta Analyzer was
            // opened without an active model connection to analyze a manual OneLake path). Reading its
            // metadata / access token in that state throws, so treat an unopened connection as none.
            var effectiveConnection = (connection != null && connection.IsConnected) ? connection : null;
            var resolveResult = overrideIsSingleTable
                ? new DirectLakeResolveResult()
                : resolver.Resolve(effectiveConnection, progress, ct, hasOverride);

            var tables = new List<(string tableName, string dfsBasePath)>();

            if (!hasOverride)
            {
                // 1b. Direct Lake on SQL models reference a SQL analytics endpoint, whose id is NOT the
                // OneLake item that stores the tables. Map those endpoints to the underlying lakehouse item
                // via the Fabric REST API and rewrite the affected table paths. Only needed when we are
                // auto-discovering the paths - a manual override supplies the OneLake root directly.
                await ResolveSqlEndpointPathsAsync(effectiveConnection, resolveResult, windowHandle, progress, ct).ConfigureAwait(false);

                // Determine the Direct Lake storage mode from the resolved tables: a non-empty SqlEndpointId
                // means the table is sourced from a SQL analytics endpoint (Direct Lake on SQL); otherwise the
                // source is a OneLake item (Direct Lake on OneLake). A model can, in principle, mix both.
                _directLakeModeNote = BuildDirectLakeModeNote(resolveResult);
            }

            if (overrideIsSingleTable)
            {
                // The path names the table, so analyze exactly that folder.
                tables.Add((overrideTableName, overridePath));
            }
            else if (resolveResult.Tables.Count > 0)
            {
                foreach (var t in resolveResult.Tables)
                {
                    // The manual override, when supplied, takes precedence over the path discovered from the
                    // data model - rebuild every table path from the override root (which points at the
                    // .../Tables folder) rather than using the auto-resolved DfsBasePath.
                    var path = hasOverride
                        ? DirectLakePathResolver.BuildFromOverrideRoot(overridePath, t.SchemaName, t.EntityName ?? t.TableName)
                        : t.DfsBasePath;
                    tables.Add((t.TableName, path));
                }
            }
            else if (hasOverride)
            {
                // No Direct Lake tables could be enumerated - treat the override as a single table path.
                tables.Add(("(manual)", overridePath));
            }

            if (!resolveResult.ModelHasDirectLakeTables && resolveResult.Tables.Count == 0 && !hasOverride)
            {
                // Distinguish "the model metadata could not be read" (often a permissions / XMLA issue) from
                // "the model genuinely has no Direct Lake tables", so the underlying cause isn't masked.
                var hasResolveError = !string.IsNullOrEmpty(resolveResult.Error);
                var message = hasResolveError
                    ? $"Unable to read Direct Lake table metadata from the model: {resolveResult.Error}. This can happen if you don't have permission to read the semantic model's metadata via the XMLA endpoint. You can enter a manual OneLake path to analyze a specific table."
                    : "No Direct Lake tables were found in the connected model. Delta Analyzer only works with Direct Lake models. You can enter a manual OneLake path to analyze a specific table.";
                if (hasResolveError)
                {
                    Log.Warning(Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(AnalyzeAsync),
                        $"Direct Lake path resolution failed: {resolveResult.Error}");
                }
                SetStatus(message, hasResolveError);
                Execute.OnUIThread(() =>
                {
                    _eventAggregator.PublishAsync(new OutputMessage(
                        hasResolveError ? MessageType.Error : MessageType.Warning, message));
                });
                return new DeltaAnalyzerResult();
            }

            // 2. Acquire a storage-scoped token for OneLake.
            string bearerToken;
            try
            {
                var context = EntraIdHelper.CreateDefaultContext(AccessTokenScope.Storage);
                var hwnd = windowHandle == IntPtr.Zero ? (IntPtr?)null : windowHandle;
                var authResult = await EntraIdHelper.AcquireTokenAsync(hwnd, _options, AccessTokenScope.Storage, context).ConfigureAwait(false);
                if (authResult == null || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    Execute.OnUIThread(() =>
                        _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, "Unable to acquire a storage access token for OneLake.")));
                    return null;
                }
                bearerToken = authResult.AccessToken;

                // OneLake returns 404 for anything the caller cannot see, so the identity actually used is
                // the single most useful thing to know when a path "does not exist". The token is normally
                // acquired silently from the last used account, meaning there is no sign-in prompt to reveal
                // it - record it here so it can be reported.
                _storageAccountName = authResult.Account?.Username;
                Log.Information(Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(RunAnalysisAsync),
                    $"Acquired OneLake storage token for account '{_storageAccountName}' (tenant {authResult.Account?.HomeAccountId?.TenantId})");
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(RunAnalysisAsync), "Error acquiring storage token");
                Execute.OnUIThread(() =>
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error acquiring OneLake access token: {ex.Message}")));
                return null;
            }

            // 3. Run the analysis, marshalling progress to the UI thread.
            var service = new DeltaAnalyzerService();
            return await service.AnalyzeAsync(tables, () => bearerToken, readFooters, readColumns, progress, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// For Direct Lake on SQL models, rewrites the OneLake path of each table whose source is a SQL
        /// analytics endpoint. The endpoint id parsed from the M expression is not a valid OneLake item,
        /// so it is mapped (via the Fabric REST API) to the underlying lakehouse's <c>.../Tables</c> path.
        /// Best-effort: on any failure the provisional path is left unchanged (which still works for
        /// Warehouse sources, where the database id already is the OneLake item).
        /// </summary>
        private async Task ResolveSqlEndpointPathsAsync(
            CoreConnectionManager connection, DirectLakeResolveResult resolveResult, IntPtr windowHandle,
            IProgress<DeltaAnalyzerProgress> progress, CancellationToken ct)
        {
            var endpointTables = resolveResult?.Tables?
                .Where(t => !string.IsNullOrEmpty(t.SqlEndpointId) && !string.IsNullOrEmpty(t.WorkspaceId))
                .ToList();
            if (endpointTables == null || endpointTables.Count == 0) return;

            progress?.Report(new DeltaAnalyzerProgress { Message = "Resolving lakehouse for SQL endpoint..." });

            // The Fabric REST API accepts a Power BI-scoped token. Reuse the connection's token when
            // available (avoids an extra prompt); otherwise acquire one silently/interactively.
            string fabricToken = await AcquireFabricTokenAsync(connection, windowHandle).ConfigureAwait(false);
            if (string.IsNullOrEmpty(fabricToken)) return;

            var fabricClient = new FabricRestClient(() => fabricToken);

            // One lakehouse listing per distinct workspace, reused across its tables.
            var mapsByWorkspace = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in endpointTables)
            {
                ct.ThrowIfCancellationRequested();
                if (!mapsByWorkspace.TryGetValue(t.WorkspaceId, out var map))
                {
                    map = await fabricClient.GetSqlEndpointToTablesPathMapAsync(t.WorkspaceId, ct).ConfigureAwait(false);
                    mapsByWorkspace[t.WorkspaceId] = map;
                }

                if (map != null && map.TryGetValue(t.SqlEndpointId, out var tablesPath) && !string.IsNullOrEmpty(tablesPath))
                {
                    // tablesPath already points at the lakehouse's .../Tables folder; append schema/entity.
                    t.DfsBasePath = DirectLakePathResolver.BuildFromOverrideRoot(tablesPath, t.SchemaName, t.EntityName ?? t.TableName);
                    t.IsResolved = true;
                }
            }
        }

        /// <summary>
        /// Returns a Power BI / Fabric-scoped bearer token for calling the Fabric REST API. Reuses the
        /// connection's token when available (no extra prompt); otherwise acquires one. Returns null on failure.
        /// </summary>
        private async Task<string> AcquireFabricTokenAsync(CoreConnectionManager connection, IntPtr windowHandle)
        {
            var token = connection?.AccessToken.Token;
            if (!string.IsNullOrEmpty(token)) return token;
            try
            {
                var context = EntraIdHelper.CreateDefaultContext(AccessTokenScope.PowerBI);
                var hwnd = windowHandle == IntPtr.Zero ? (IntPtr?)null : windowHandle;
                var authResult = await EntraIdHelper.AcquireTokenAsync(hwnd, _options, AccessTokenScope.PowerBI, context).ConfigureAwait(false);
                return authResult?.AccessToken;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(AcquireFabricTokenAsync), "Error acquiring Power BI token for Fabric REST API");
                return null;
            }
        }

        private static string BuildSummary(IEnumerable<DeltaTreeRow> tables, string directLakeModeNote = null)
        {
            var list = tables.ToList();
            if (list.Count == 0) return string.Empty;
            var totalFiles = list.Sum(t => t.FileCount ?? 0);
            var totalRows = list.Sum(t => t.RowCount ?? 0);
            var errors = list.Count(t => t.HasError);
            var summary = $"{list.Count:N0} table(s), {totalFiles:N0} file(s), {totalRows:N0} row(s)";
            if (errors > 0) summary += $"  -  {errors} table(s) with warnings";
            if (!string.IsNullOrEmpty(directLakeModeNote)) summary += $"  -  {directLakeModeNote}";
            return summary;
        }

        /// <summary>
        /// Builds the Direct Lake storage-mode note for the footer. A table sourced from a SQL analytics
        /// endpoint (non-empty <see cref="DirectLakeTableInfo.SqlEndpointId"/>) is "Direct Lake on SQL";
        /// otherwise it is "Direct Lake on OneLake". Returns an empty string when there are no resolved
        /// tables (e.g. a manual override path) so no mode is asserted.
        /// </summary>
        private static string BuildDirectLakeModeNote(DirectLakeResolveResult resolveResult)
        {
            var tables = resolveResult?.Tables;
            if (tables == null || tables.Count == 0) return string.Empty;

            var sqlCount = tables.Count(t => !string.IsNullOrEmpty(t.SqlEndpointId));
            var oneLakeCount = tables.Count - sqlCount;

            if (sqlCount > 0 && oneLakeCount > 0)
                return $"Direct Lake on SQL + OneLake ({sqlCount:N0} SQL, {oneLakeCount:N0} OneLake table(s))";
            if (sqlCount > 0)
                return "Direct Lake on SQL";
            return "Direct Lake on OneLake";
        }

        /// <summary>Documentation page describing the Delta Analyzer considerations in more detail.</summary>
        private const string ConsiderationsDocUrl = "https://daxstudio.org/docs/features/delta-analyzer/considerations";

        /// <summary>
        /// Builds the aggregated, read-only <see cref="FlowDocument"/> shown on the Considerations tab. Groups
        /// the considerations by table and softens the framing (these are guidance, not hard rules). Uses
        /// theme resource references so the text re-colours correctly when the light/dark theme is switched.
        /// </summary>
        private static FlowDocument BuildConsiderationsDocument(IEnumerable<DeltaTreeRow> tables)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(14, 12, 14, 12)
            };
            doc.SetResourceReference(FlowDocument.ForegroundProperty, "Theme.Brush.Default.Fore");

            var intro = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
            intro.Inlines.Add(new Run("These are potential considerations - guidance based on the analyzed metadata rather than hard best practices. The ideal choice depends on your data volumes and query patterns. "));
            var link = new Hyperlink(new Run("Learn more"))
            {
                NavigateUri = new Uri(ConsiderationsDocUrl)
            };
            link.RequestNavigate += OnConsiderationLinkRequestNavigate;
            intro.Inlines.Add(link);
            intro.Inlines.Add(new Run("."));
            doc.Blocks.Add(intro);

            var tablesWithConsiderations = tables
                .Where(t => t.IsTable && t.HasConsiderations)
                .OrderBy(t => t.Name)
                .ToList();

            if (tablesWithConsiderations.Count == 0)
            {
                var none = new Paragraph(new Run("No considerations were found for the analyzed tables."))
                {
                    FontStyle = FontStyles.Italic
                };
                doc.Blocks.Add(none);
                return doc;
            }

            foreach (var table in tablesWithConsiderations)
            {
                var heading = new Paragraph(new Run(table.Name))
                {
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 10, 0, 4)
                };
                doc.Blocks.Add(heading);

                var listBlock = new List
                {
                    MarkerStyle = TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 0, 0, 4),
                    Padding = new Thickness(20, 0, 0, 0)
                };

                foreach (var consideration in table.Considerations)
                {
                    var item = new ListItem();
                    var titlePara = new Paragraph { Margin = new Thickness(0, 2, 0, 0) };
                    var titleRun = new Run(consideration.Title) { FontWeight = FontWeights.SemiBold };
                    titlePara.Inlines.Add(titleRun);
                    if (consideration.Severity == ConsiderationSeverity.Warning)
                    {
                        titlePara.Inlines.Add(new Run("  (worth reviewing)") { FontStyle = FontStyles.Italic, Foreground = System.Windows.Media.Brushes.Gray });
                    }
                    item.Blocks.Add(titlePara);

                    var detailPara = new Paragraph(new Run(consideration.Detail))
                    {
                        Margin = new Thickness(0, 0, 0, 2),
                        FontSize = 11
                    };
                    item.Blocks.Add(detailPara);

                    listBlock.ListItems.Add(item);
                }

                doc.Blocks.Add(listBlock);
            }

            return doc;
        }

        private static void OnConsiderationLinkRequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(OnConsiderationLinkRequestNavigate), "Error opening considerations documentation link");
            }
        }

        /// <summary>
        /// Builds a plain-text / Markdown rendering of the aggregated considerations (grouped by table),
        /// used by the copy-to-clipboard and export actions on the Considerations tab.
        /// </summary>
        private static string BuildConsiderationsText(IEnumerable<DeltaTreeRow> tables)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Delta Analyzer Considerations");
            sb.AppendLine();
            sb.AppendLine("These are potential considerations - guidance based on the analyzed metadata rather than hard best practices. The ideal choice depends on your data volumes and query patterns.");
            sb.AppendLine();
            sb.AppendLine($"Learn more: {ConsiderationsDocUrl}");
            sb.AppendLine();

            var tablesWithConsiderations = tables
                .Where(t => t.IsTable && t.HasConsiderations)
                .OrderBy(t => t.Name)
                .ToList();

            if (tablesWithConsiderations.Count == 0)
            {
                sb.AppendLine("No considerations were found for the analyzed tables.");
                return sb.ToString();
            }

            foreach (var table in tablesWithConsiderations)
            {
                sb.AppendLine($"## {table.Name}");
                sb.AppendLine();
                foreach (var consideration in table.Considerations)
                {
                    var suffix = consideration.Severity == ConsiderationSeverity.Warning ? " (worth reviewing)" : string.Empty;
                    sb.AppendLine($"- **{consideration.Title}**{suffix}");
                    sb.AppendLine($"  {consideration.Detail}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>Copies the aggregated considerations (as Markdown-style text) to the clipboard.</summary>
        public void CopyConsiderations()
        {
            if (!HasData) return;
            try
            {
                System.Windows.Clipboard.SetText(BuildConsiderationsText(TreeRows));
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Delta Analyzer considerations copied to the clipboard."));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(CopyConsiderations), "Error copying considerations to the clipboard");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error copying considerations to the clipboard: {ex.Message}"));
            }
        }

        /// <summary>Exports the aggregated considerations to a Markdown / text file.</summary>
        public void ExportConsiderations()
        {
            if (!HasData) return;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown files|*.md|Text files|*.txt|All files|*.*",
                Title = "Export Delta Analyzer Considerations",
                FileName = $"DeltaConsiderations_{DateTime.Now:yyyyMMdd_HHmmss}.md"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dialog.FileName, BuildConsiderationsText(TreeRows), new UTF8Encoding(true));
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Information, $"Delta Analyzer considerations exported to {dialog.FileName}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(ExportConsiderations), "Error exporting considerations");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error exporting considerations: {ex.Message}"));
            }
        }

        /// <summary>Cancels an in-progress analysis.</summary>
        public void Cancel()
        {
            try { _cts?.Cancel(); }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(Cancel), "Error cancelling delta analysis");
            }
        }

        /// <summary>Exports the current analysis (tables, columns and any visible row groups) to a CSV file.</summary>
        public void ExportCsv()
        {
            if (!HasData) return;
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files|*.csv|All files|*.*",
                Title = "Export Delta Analysis to CSV",
                FileName = $"DeltaAnalysis_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", new[]
                {
                    "Level","Name","Files","AvgFileBytes","MaxFileBytes","SmallFiles","RowGroups",
                    "MinRowsPerRowGroup","AvgRowsPerRowGroup","MaxRowsPerRowGroup","Rows",
                    "CompressedBytes","UncompressedBytes","Codec","Encoding","VOrder",
                    "DeletionVectors","DeletionVectorRows","LiquidClustering",
                    "ClusteringColumns","PartitionColumns","LastModified","Considerations","Error"
                }));

                foreach (var table in TreeRows)
                {
                    WriteCsvRow(sb, "Table", table);
                    foreach (var child in table.Children)
                    {
                        if (child.IsRowGroupGroup)
                        {
                            foreach (var rg in child.Children) WriteCsvRow(sb, "RowGroup", rg);
                        }
                        else
                        {
                            WriteCsvRow(sb, "Column", child);
                        }
                    }
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Information, $"Delta analysis exported to {dialog.FileName}"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(ExportCsv), "Error exporting delta analysis to CSV");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error exporting Delta Analyzer data to CSV: {ex.Message}"));
            }
        }

        private static void WriteCsvRow(StringBuilder sb, string level, DeltaTreeRow r)
        {
            string Vo() => r.VOrderEnabled == true ? "Yes" : r.VOrderEnabled == false ? "No" : string.Empty;
            string Num(long? v) => v?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

            var values = new[]
            {
                level,
                r.Name,
                Num(r.FileCount),
                Num(r.AvgFileSizeBytes),
                Num(r.MaxFileSizeBytes),
                Num(r.SmallFileCount),
                Num(r.RowGroupCount),
                Num(r.MinRowsPerRowGroup),
                Num(r.AvgRowsPerRowGroup),
                Num(r.MaxRowsPerRowGroup),
                Num(r.RowCount),
                Num(r.CompressedBytes),
                Num(r.UncompressedBytes),
                r.Codec,
                r.Encoding,
                r.IsTable ? Vo() : string.Empty,
                r.IsTable && r.HasDeletionVectors ? "Yes" : string.Empty,
                r.IsTable && r.HasDeletionVectors ? r.DeletionVectorRowCount.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty,
                r.IsTable && r.LiquidClusteringEnabled ? "Yes" : string.Empty,
                r.ClusteringColumns,
                r.PartitionColumns,
                r.IsTable ? r.LastModifiedDisplay : string.Empty,
                r.IsTable && r.HasConsiderations ? string.Join("; ", r.Considerations.Select(x => x.Title)) : string.Empty,
                r.Error
            };
            sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        #region ISaveState - persist the analysis results into the .daxx package so they can be reopened

        /// <summary>Serializable snapshot of the tool window's state written to the .daxx package.</summary>
        private class DeltaAnalyzerState
        {
            public List<DeltaTreeRow> Rows { get; set; } = new List<DeltaTreeRow>();
            public string SummaryText { get; set; }
            public string OverridePath { get; set; }
            public bool ReadParquetFooters { get; set; } = true;
            public bool ReadColumnStats { get; set; } = true;
            public bool ShowRowGroupDetails { get; set; }
        }

        // Satellite (.dax) files are not used for Delta Analyzer state - persistence is via the .daxx package.
        public void Save(string filename) { }
        public void Load(string filename) { }

        public string GetJson()
        {
            var rows = TreeRows.ToList();

            // The "Show row group details" toggle physically removes the row group node from Children, so if
            // details are currently hidden the row group detail would be dropped from the saved file entirely
            // (and the toggle would do nothing when the file is re-opened). Temporarily re-attach the nodes so
            // the snapshot is complete - LoadJson re-extracts them from Children and re-applies the toggle.
            // Children is a plain List<T>, so this raises no change notifications and the UI is unaffected.
            var reattached = new List<DeltaTreeRow>();
            foreach (var r in rows)
            {
                if (r.RowGroupNode != null && !r.Children.Contains(r.RowGroupNode))
                {
                    r.Children.Add(r.RowGroupNode);
                    reattached.Add(r);
                }
            }

            try
            {
                var state = new DeltaAnalyzerState
                {
                    Rows = rows,
                    SummaryText = SummaryText,
                    OverridePath = OverridePath,
                    ReadParquetFooters = ReadParquetFooters,
                    ReadColumnStats = ReadColumnStats,
                    ShowRowGroupDetails = ShowRowGroupDetails
                };
                return JsonConvert.SerializeObject(state, Formatting.Indented);
            }
            finally
            {
                foreach (var r in reattached) r.Children.Remove(r.RowGroupNode);
            }
        }

        public void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var state = JsonConvert.DeserializeObject<DeltaAnalyzerState>(json);
            if (state == null) return;

            TreeRows.Clear();
            if (state.Rows != null)
            {
                foreach (var row in state.Rows)
                {
                    // Re-attach the row group node reference (JsonIgnore, so not restored by the deserializer)
                    // from the persisted children so the "Show row group details" toggle keeps working.
                    var groupNode = row.Children?.FirstOrDefault(c => c.IsRowGroupGroup);
                    if (groupNode != null) row.RowGroupNode = groupNode;
                    TreeRows.Add(row);
                }
            }
            SummaryText = state.SummaryText ?? string.Empty;
            OverridePath = state.OverridePath ?? string.Empty;
            ReadParquetFooters = state.ReadParquetFooters;
            ReadColumnStats = state.ReadColumnStats;
            _showRowGroupDetails = state.ShowRowGroupDetails;
            NotifyOfPropertyChange(nameof(ShowRowGroupDetails));
            IsLoading = false;
            HasData = TreeRows.Count > 0;
            ConsiderationsDocument = BuildConsiderationsDocument(TreeRows);
            ApplyRowGroupDetails();
        }

        public void SavePackage(Package package)
        {
            // Nothing meaningful to persist when the analysis hasn't been run.
            if (!HasData) return;
            try
            {
                var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.DeltaAnalyzer, UriKind.Relative));
                if (package.PartExists(uri)) package.DeletePart(uri);
                using (var strm = package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream())
                using (var writer = new StreamWriter(strm, new UTF8Encoding(false)))
                {
                    writer.Write(GetJson());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(SavePackage), "Error saving Delta Analyzer data to daxx file");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error saving Delta Analyzer data to daxx file\n{ex.Message}"));
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.DeltaAnalyzer, UriKind.Relative));
            if (!package.PartExists(uri)) return;
            try
            {
                var part = package.GetPart(uri);
                string json;
                using (var strm = part.GetStream())
                using (var reader = new StreamReader(strm))
                {
                    json = reader.ReadToEnd();
                }
                LoadJson(json);
                IsVisible = true;
                Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DeltaAnalyzerViewModel), nameof(LoadPackage), "Error loading Delta Analyzer data from daxx file");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error loading Delta Analyzer data from daxx file\n{ex.Message}"));
            }
        }

        #endregion
    }
}

