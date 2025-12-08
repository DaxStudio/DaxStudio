using Caliburn.Micro;
using DaxStudio.Common.Enums;
using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the xmSQL ERD (Entity Relationship Diagram) visualization.
    /// This displays tables, columns, and relationships extracted from Server Timing events.
    /// This is a dockable tool window that can be resized, floated, or maximized.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class XmSqlErdViewModel : ToolWindowBase
    {
        private readonly XmSqlParser _parser;
        private XmSqlAnalysis _analysis;
        private IGlobalOptions _options;

        /// <summary>
        /// Gets the global options instance, lazily loaded via IoC.
        /// </summary>
        private IGlobalOptions Options
        {
            get
            {
                if (_options == null) _options = IoC.Get<IGlobalOptions>();
                return _options;
            }
        }

        [ImportingConstructor]
        public XmSqlErdViewModel()
        {
            _parser = new XmSqlParser();
            _analysis = new XmSqlAnalysis();
            // Initialize heat map mode from persisted options
            _heatMapMode = Options.SEDependenciesHeatMapMode;
        }

        #region ToolWindowBase Implementation

        public override string Title => "Storage Engine Dependencies";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "storage-engine-dependencies";
        public override bool CanHide => true;
        
        /// <summary>
        /// Icon for the tool window (used by AvalonDock).
        /// </summary>
        public ImageSource IconSource => null;

        #endregion

        #region Properties
        /// The analysis results containing all parsed table/column/relationship data.
        /// </summary>
        public XmSqlAnalysis Analysis
        {
            get => _analysis;
            private set
            {
                _analysis = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(Tables));
                NotifyOfPropertyChange(nameof(Relationships));
                NotifyOfPropertyChange(nameof(SummaryText));
                NotifyOfPropertyChange(nameof(HasData));
            }
        }

        /// <summary>
        /// Collection of table view models for display.
        /// </summary>
        public BindableCollection<ErdTableViewModel> Tables { get; } = new BindableCollection<ErdTableViewModel>();

        /// <summary>
        /// Collection of relationship view models for display.
        /// </summary>
        public BindableCollection<ErdRelationshipViewModel> Relationships { get; } = new BindableCollection<ErdRelationshipViewModel>();

        /// <summary>
        /// Summary text showing counts and total CPU.
        /// The SE query count matches Server Timings: parsed queries - cache hits + batch events.
        /// </summary>
        public string SummaryText => _analysis != null
            ? $"{Tables.Count} Tables, {Tables.SelectMany(t => t.Columns).Count()} Columns, {Relationships.Count} Relationships (from {_analysis.SuccessfullyParsedQueries - _analysis.CacheHitQueries + _analysis.BatchEventCount:N0} SE queries" +
              (_analysis.CacheHitQueries > 0 ? $", {_analysis.CacheHitQueries:N0} cache" : "") +
              (_analysis.TotalCpuTimeMs > 0 ? $", {FormatDurationStatic(_analysis.TotalCpuTimeMs)} total CPU)" : ")")
            : "No data";

        /// <summary>
        /// Formats duration in ms with appropriate suffix (static version for summary).
        /// </summary>
        private static string FormatDurationStatic(long ms)
        {
            if (ms >= 60000)
                return $"{ms / 60000.0:0.#}m";
            if (ms >= 1000)
                return $"{ms / 1000.0:0.#}s";
            return $"{ms}ms";
        }

        /// <summary>
        /// Whether there is data to display.
        /// </summary>
        public bool HasData => Tables.Count > 0;

        /// <summary>
        /// Whether there is no data to display (inverse of HasData, for binding).
        /// </summary>
        public bool NoData => !HasData;

        private string _searchText = string.Empty;
        /// <summary>
        /// Search text for filtering tables/columns.
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                NotifyOfPropertyChange();
                ApplySearchFilter();
            }
        }
        
        /// <summary>
        /// Clears the search text.
        /// </summary>
        public void ClearSearch()
        {
            SearchText = string.Empty;
        }
        
        private bool _sortKeyColumnsFirst = true;
        /// <summary>
        /// Whether to sort key/join columns first in the column list.
        /// </summary>
        public bool SortKeyColumnsFirst
        {
            get => _sortKeyColumnsFirst;
            set
            {
                if (_sortKeyColumnsFirst != value)
                {
                    _sortKeyColumnsFirst = value;
                    NotifyOfPropertyChange();
                    // Re-sort columns in all tables
                    foreach (var table in Tables)
                    {
                        table.UpdateColumnSort(_sortKeyColumnsFirst);
                    }
                }
            }
        }

        /// <summary>
        /// Collapses all tables to show only headers and key columns.
        /// </summary>
        public void CollapseAll()
        {
            foreach (var table in Tables)
            {
                table.IsCollapsed = true;
            }
            // Update all relationship paths after collapse
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }
        }
        
        /// <summary>
        /// Expands all tables to show columns.
        /// </summary>
        public void ExpandAll()
        {
            foreach (var table in Tables)
            {
                table.IsCollapsed = false;
            }
            // Update all relationship paths after expand
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }
        }
        
        /// <summary>
        /// Applies the search filter to highlight matching tables/columns.
        /// </summary>
        private void ApplySearchFilter()
        {
            var search = _searchText?.Trim() ?? string.Empty;
            var hasSearch = !string.IsNullOrEmpty(search);
            
            foreach (var table in Tables)
            {
                if (!hasSearch)
                {
                    // No search - clear all search highlighting
                    table.IsSearchMatch = false;
                    table.IsSearchDimmed = false;
                    foreach (var col in table.Columns)
                    {
                        col.IsSearchMatch = false;
                    }
                }
                else
                {
                    // Check if table name matches
                    var tableMatches = table.TableName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                    
                    // Check if any column matches
                    var anyColumnMatches = false;
                    foreach (var col in table.Columns)
                    {
                        col.IsSearchMatch = col.ColumnName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (col.IsSearchMatch) anyColumnMatches = true;
                    }
                    
                    table.IsSearchMatch = tableMatches || anyColumnMatches;
                    table.IsSearchDimmed = !table.IsSearchMatch;
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; NotifyOfPropertyChange(); }
        }

        private double _canvasWidth = 800;
        public double CanvasWidth
        {
            get => _canvasWidth;
            set { _canvasWidth = value; NotifyOfPropertyChange(); }
        }

        private double _canvasHeight = 600;
        public double CanvasHeight
        {
            get => _canvasHeight;
            set { _canvasHeight = value; NotifyOfPropertyChange(); }
        }

        private double _viewWidth = 800;
        /// <summary>
        /// The actual width of the view (set by code-behind).
        /// </summary>
        public double ViewWidth
        {
            get => _viewWidth;
            set { _viewWidth = value; NotifyOfPropertyChange(); }
        }

        private double _viewHeight = 600;
        /// <summary>
        /// The actual height of the view (set by code-behind).
        /// </summary>
        public double ViewHeight
        {
            get => _viewHeight;
            set { _viewHeight = value; NotifyOfPropertyChange(); }
        }

        private bool _showBottlenecks;
        /// <summary>
        /// Whether to highlight bottleneck tables (slowest tables).
        /// </summary>
        public bool ShowBottlenecks
        {
            get => _showBottlenecks;
            set
            {
                _showBottlenecks = value;
                NotifyOfPropertyChange();
                UpdateBottleneckHighlighting();
            }
        }

        #region Heat Map Mode

        private SEDependenciesHeatMapMode _heatMapMode = SEDependenciesHeatMapMode.CpuTime;
        /// <summary>
        /// The metric used for table header heat map coloring.
        /// Persisted to options so the user's preference is remembered.
        /// </summary>
        public SEDependenciesHeatMapMode HeatMapMode
        {
            get => _heatMapMode;
            set
            {
                if (_heatMapMode != value)
                {
                    _heatMapMode = value;
                    // Persist to options
                    Options.SEDependenciesHeatMapMode = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(IsHeatMapModeHitCount));
                    NotifyOfPropertyChange(nameof(IsHeatMapModeCpuTime));
                    NotifyOfPropertyChange(nameof(IsHeatMapModeRowCount));
                    NotifyOfPropertyChange(nameof(HeatMapModeDescription));
                    NotifyOfPropertyChange(nameof(HeatMapLegendText));
                    CalculateHeatLevels();
                }
            }
        }

        /// <summary>
        /// Whether heat map mode is set to Hit Count.
        /// </summary>
        public bool IsHeatMapModeHitCount
        {
            get => _heatMapMode == SEDependenciesHeatMapMode.HitCount;
            set { if (value) HeatMapMode = SEDependenciesHeatMapMode.HitCount; }
        }

        /// <summary>
        /// Whether heat map mode is set to CPU Time.
        /// </summary>
        public bool IsHeatMapModeCpuTime
        {
            get => _heatMapMode == SEDependenciesHeatMapMode.CpuTime;
            set { if (value) HeatMapMode = SEDependenciesHeatMapMode.CpuTime; }
        }

        /// <summary>
        /// Whether heat map mode is set to Row Count.
        /// </summary>
        public bool IsHeatMapModeRowCount
        {
            get => _heatMapMode == SEDependenciesHeatMapMode.RowCount;
            set { if (value) HeatMapMode = SEDependenciesHeatMapMode.RowCount; }
        }

        /// <summary>
        /// Description of the current heat map mode for tooltips.
        /// </summary>
        public string HeatMapModeDescription => _heatMapMode switch
        {
            SEDependenciesHeatMapMode.HitCount => "Table colors show how frequently each table appears in SE queries",
            SEDependenciesHeatMapMode.CpuTime => "Table colors show CPU time consumed by queries on each table",
            SEDependenciesHeatMapMode.RowCount => "Table colors show total rows scanned from each table",
            _ => "Table colors indicate relative activity"
        };

        /// <summary>
        /// Legend text explaining the current heat map coloring.
        /// </summary>
        public string HeatMapLegendText => _heatMapMode switch
        {
            SEDependenciesHeatMapMode.HitCount => "Hits",
            SEDependenciesHeatMapMode.CpuTime => "CPU",
            SEDependenciesHeatMapMode.RowCount => "Rows",
            _ => "Activity"
        };

        /// <summary>
        /// Sets heat map mode to Hit Count.
        /// </summary>
        public void SetHeatMapModeHitCount() => HeatMapMode = SEDependenciesHeatMapMode.HitCount;

        /// <summary>
        /// Sets heat map mode to CPU Time.
        /// </summary>
        public void SetHeatMapModeCpuTime() => HeatMapMode = SEDependenciesHeatMapMode.CpuTime;

        /// <summary>
        /// Sets heat map mode to Row Count.
        /// </summary>
        public void SetHeatMapModeRowCount() => HeatMapMode = SEDependenciesHeatMapMode.RowCount;

        #endregion

        #region Query Plan Integration

        private List<int> _availableQueryIds = new List<int>();
        /// <summary>
        /// Gets the list of available query IDs from the analysis.
        /// </summary>
        public List<int> AvailableQueryIds
        {
            get => _availableQueryIds;
            private set
            {
                _availableQueryIds = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasMultipleQueries));
                NotifyOfPropertyChange(nameof(MaxQueryId));
            }
        }

        /// <summary>
        /// Whether there are multiple queries to filter (shows the filter UI).
        /// </summary>
        public bool HasMultipleQueries => _availableQueryIds.Count > 1;

        /// <summary>
        /// Maximum query ID (for slider range).
        /// </summary>
        public int MaxQueryId => _availableQueryIds.Count > 0 ? _availableQueryIds.Max() : 1;

        private int? _selectedQueryId;
        /// <summary>
        /// The currently selected query ID for filtering. Null means show all queries.
        /// </summary>
        public int? SelectedQueryId
        {
            get => _selectedQueryId;
            set
            {
                _selectedQueryId = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(QueryFilterText));
                NotifyOfPropertyChange(nameof(QueryFilterDetails));
                ApplyQueryFilter();
            }
        }

        /// <summary>
        /// Display text for the current query filter state (e.g., "3 of 7").
        /// </summary>
        public string QueryFilterText
        {
            get
            {
                if (!_selectedQueryId.HasValue)
                    return "All";
                
                var currentIndex = _availableQueryIds.IndexOf(_selectedQueryId.Value);
                return $"{currentIndex + 1} of {_availableQueryIds.Count}";
            }
        }

        /// <summary>
        /// Details about the currently selected query - which tables it accesses.
        /// </summary>
        public string QueryFilterDetails
        {
            get
            {
                if (!_selectedQueryId.HasValue)
                    return $"Showing all {Tables.Count} tables from {_availableQueryIds.Count} SE queries";
                
                var tablesInQuery = Tables.Where(t => t.TableInfo.QueryIds.Contains(_selectedQueryId.Value)).ToList();
                var tableNames = string.Join(", ", tablesInQuery.Select(t => t.TableName).Take(3));
                if (tablesInQuery.Count > 3)
                    tableNames += $" +{tablesInQuery.Count - 3} more";
                
                return $"SE Query #{_selectedQueryId.Value}: {tablesInQuery.Count} table(s) - {tableNames}";
            }
        }

        /// <summary>
        /// Clears the query filter to show all tables.
        /// </summary>
        public void ClearQueryFilter()
        {
            SelectedQueryId = null;
        }

        /// <summary>
        /// Filters to show only the previous query.
        /// </summary>
        public void PreviousQuery()
        {
            if (!_selectedQueryId.HasValue)
            {
                // Currently showing all - go to first query
                if (_availableQueryIds.Count > 0)
                    SelectedQueryId = _availableQueryIds.First();
            }
            else
            {
                var currentIndex = _availableQueryIds.IndexOf(_selectedQueryId.Value);
                if (currentIndex > 0)
                {
                    SelectedQueryId = _availableQueryIds[currentIndex - 1];
                }
            }
        }

        /// <summary>
        /// Filters to show only the next query.
        /// </summary>
        public void NextQuery()
        {
            if (!_selectedQueryId.HasValue)
            {
                // Currently showing all - go to first query
                if (_availableQueryIds.Count > 0)
                    SelectedQueryId = _availableQueryIds.First();
            }
            else
            {
                var currentIndex = _availableQueryIds.IndexOf(_selectedQueryId.Value);
                if (currentIndex >= 0 && currentIndex < _availableQueryIds.Count - 1)
                {
                    SelectedQueryId = _availableQueryIds[currentIndex + 1];
                }
            }
        }

        /// <summary>
        /// Applies query filter to show/hide tables based on which queries accessed them.
        /// </summary>
        private void ApplyQueryFilter()
        {
            foreach (var table in Tables)
            {
                if (!_selectedQueryId.HasValue)
                {
                    // Show all - no query filtering
                    table.IsQueryFiltered = false;
                    table.IsQueryHighlighted = false;
                }
                else
                {
                    // Check if this table was accessed by the selected query
                    var wasAccessedByQuery = table.TableInfo.QueryIds.Contains(_selectedQueryId.Value);
                    table.IsQueryFiltered = !wasAccessedByQuery;
                    table.IsQueryHighlighted = wasAccessedByQuery;
                }
            }

            // Also update relationships visibility
            foreach (var rel in Relationships)
            {
                if (!_selectedQueryId.HasValue)
                {
                    rel.IsQueryFiltered = false;
                }
                else
                {
                    // Show relationship only if both tables are visible
                    var fromTable = Tables.FirstOrDefault(t => t.TableName == rel.FromTable);
                    var toTable = Tables.FirstOrDefault(t => t.TableName == rel.ToTable);
                    rel.IsQueryFiltered = (fromTable?.IsQueryFiltered ?? true) || (toTable?.IsQueryFiltered ?? true);
                }
            }
        }

        #endregion

        #endregion

        #region Methods

        /// <summary>
        /// Analyzes a collection of SE events and builds the ERD model.
        /// Skips Internal and Batch subclass events for hit counting.
        /// </summary>
        public void AnalyzeEvents(IEnumerable<TraceStorageEngineEvent> events)
        {
            IsLoading = true;

            try
            {
                // Clear previous analysis
                _analysis = new XmSqlAnalysis();
                Tables.Clear();
                Relationships.Clear();

                // Calculate total CPU from all scan events (not batch rollups)
                // This is used to calculate CPU percentage per table
                long totalCpu = 0;
                
                // Track batch events separately - they count toward SE query count but don't have parseable queries
                int batchEventCount = 0;

                // Parse each SE event's query
                foreach (var evt in events)
                {
                    // Count batch events toward SE query total (they're counted in Server Timings)
                    // but don't try to parse them as they don't have meaningful query text
                    if (evt.IsBatchEvent)
                    {
                        batchEventCount++;
                        continue;
                    }
                    
                    // Only parse scan events that have query text
                    // Skip Internal events as they don't represent actual user queries
                    if (evt.IsScanEvent && !string.IsNullOrWhiteSpace(evt.Query) 
                        && !evt.IsInternalEvent)
                    {
                        // Track total CPU for percentage calculations
                        if (evt.CpuTime.HasValue && evt.CpuTime.Value > 0)
                        {
                            totalCpu += evt.CpuTime.Value;
                        }

                        // Build full metrics including cache hit status and parallelism data
                        var metrics = new XmSqlParser.SeEventMetrics
                        {
                            QueryId = evt.RowNumber,  // Track which query this is for Query Plan Integration
                            EstimatedRows = evt.EstimatedRows,
                            DurationMs = evt.Duration,
                            IsCacheHit = evt.Class == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch,
                            CpuTimeMs = evt.CpuTime,
                            CpuFactor = evt.CpuFactor,
                            NetParallelDurationMs = evt.NetParallelDuration
                        };
                        _parser.ParseQueryWithMetrics(evt.Query, _analysis, metrics);
                    }
                }
                
                // Add batch event count to analysis for accurate SE query count matching Server Timings
                _analysis.BatchEventCount = batchEventCount;

                // Store total CPU in analysis for reference
                _analysis.TotalCpuTimeMs = totalCpu;

                // Create view models for tables
                CreateTableViewModels();

                // Auto-hide mini-map for small diagrams (fewer than 10 tables)
                ShowMiniMap = Tables.Count >= 10;

                // Calculate CPU percentages for each table
                CalculateCpuPercentages(totalCpu);

                // Calculate heat map levels
                CalculateHeatLevels();

                // Create view models for relationships
                CreateRelationshipViewModels();

                // Layout the diagram
                LayoutDiagram();

                // Gather available query IDs for query plan integration
                GatherAvailableQueryIds();

                NotifyOfPropertyChange(nameof(Analysis));
                NotifyOfPropertyChange(nameof(SummaryText));
                NotifyOfPropertyChange(nameof(HasData));
                NotifyOfPropertyChange(nameof(NoData));
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Calculates CPU percentage for each table based on total CPU time.
        /// </summary>
        private void CalculateCpuPercentages(long totalCpu)
        {
            if (totalCpu <= 0) return;

            foreach (var table in Tables)
            {
                if (table.TotalCpuTimeMs > 0)
                {
                    table.CpuPercentage = (double)table.TotalCpuTimeMs / totalCpu * 100.0;
                }
            }
        }

        /// <summary>
        /// Creates table view models from the analysis.
        /// </summary>
        private void CreateTableViewModels()
        {
            Tables.Clear();

            foreach (var tableInfo in _analysis.Tables.Values
                .Where(t => !IsIntermediateTable(t.TableName))
                .OrderByDescending(t => t.HitCount))
            {
                var tableVm = new ErdTableViewModel(tableInfo, _sortKeyColumnsFirst);
                Tables.Add(tableVm);
            }
        }

        /// <summary>
        /// Determines if a table is an intermediate/temporary table (e.g., $TTable1, $TTable2).
        /// These are internal xmSQL constructs and typically not useful for the user to see.
        /// </summary>
        private bool IsIntermediateTable(string tableName)
        {
            // Filter out tables that match the pattern $TTable followed by digits
            // Also filter out tables starting with $ in general (internal tables)
            if (string.IsNullOrEmpty(tableName)) return true;
            
            // Match $TTable1, $TTable2, etc.
            if (tableName.StartsWith("$TTable", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Could add more patterns here in the future
            // e.g., $SemijoinResult, etc.
            
            return false;
        }

        /// <summary>
        /// Creates relationship view models from the analysis.
        /// </summary>
        private void CreateRelationshipViewModels()
        {
            Relationships.Clear();

            Log.Debug("Creating relationship view models. Analysis has {Count} relationships", _analysis.Relationships.Count);

            foreach (var rel in _analysis.Relationships)
            {
                Log.Debug("Processing relationship: {From}.{FromCol} -> {To}.{ToCol}", 
                    rel.FromTable, rel.FromColumn, rel.ToTable, rel.ToColumn);
                
                var fromTable = Tables.FirstOrDefault(t => t.TableName.Equals(rel.FromTable, StringComparison.OrdinalIgnoreCase));
                var toTable = Tables.FirstOrDefault(t => t.TableName.Equals(rel.ToTable, StringComparison.OrdinalIgnoreCase));

                Log.Debug("  FromTable found: {Found}, ToTable found: {ToFound}", 
                    fromTable != null, toTable != null);

                if (fromTable != null && toTable != null)
                {
                    var relVm = new ErdRelationshipViewModel(rel, fromTable, toTable);
                    Relationships.Add(relVm);
                    Log.Debug("  Added relationship VM. Path: {Path}", relVm.PathData);
                }
            }
            
            Log.Debug("Total relationship VMs created: {Count}", Relationships.Count);
        }

        /// <summary>
        /// Gathers all unique query IDs from the tables for query plan integration.
        /// </summary>
        private void GatherAvailableQueryIds()
        {
            var allQueryIds = new HashSet<int>();
            foreach (var table in Tables)
            {
                foreach (var queryId in table.TableInfo.QueryIds)
                {
                    allQueryIds.Add(queryId);
                }
            }
            AvailableQueryIds = allQueryIds.OrderBy(x => x).ToList();
            
            // Clear any previous query filter
            _selectedQueryId = null;
            NotifyOfPropertyChange(nameof(SelectedQueryId));
            NotifyOfPropertyChange(nameof(QueryFilterText));
        }

        /// <summary>
        /// Lays out the tables on the canvas using Sugiyama-style layered graph drawing algorithm.
        /// Tables connected by relationships are placed in hierarchical layers.
        /// Dimension tables (on the "one" side) go at the top, fact tables (on the "many" side) at the bottom.
        /// Enhanced with SE-specific optimizations for hit counts and CPU usage.
        /// </summary>
        private void LayoutDiagram()
        {
            if (Tables.Count == 0) return;

            const double tableWidth = 220;
            const double tableHeight = 180;
            const double headerHeight = 75; // Fixed header area (title, metrics, etc.)
            const double columnsHeight = tableHeight - headerHeight; // Height for columns area
            const double horizontalSpacing = 100;
            const double verticalSpacing = 120;
            const double padding = 50;

            // Special case: very small diagrams get compact layouts
            if (Tables.Count <= 4 && Relationships.Count <= 3)
            {
                LayoutCompact(tableWidth, tableHeight, columnsHeight, horizontalSpacing, verticalSpacing, padding);
                return;
            }

            // Step 1: Assign tables to layers based on longest path from root nodes
            var layers = AssignLayers();
            
            // Step 2: Handle disconnected tables - integrate them into existing layers
            IntegrateDisconnectedTables(layers);
            
            // Step 3: Order tables within each layer to minimize edge crossings
            MinimizeCrossings(layers);
            
            // Step 4: Assign X coordinates using barycenter method with median fallback
            AssignCoordinates(layers, tableWidth, tableHeight, columnsHeight, horizontalSpacing, verticalSpacing, padding);

            // Step 5: Compact empty spaces between tables
            CompactLayout(layers, tableWidth, horizontalSpacing, padding);

            // Calculate canvas size to fit all tables
            var maxX = Tables.Any() ? Tables.Max(t => t.X + t.Width) : 100;
            var maxY = Tables.Any() ? Tables.Max(t => t.Y + t.Height) : 100;
            CanvasWidth = Math.Max(100, maxX + padding);
            CanvasHeight = Math.Max(100, maxY + padding);

            // Update relationship line positions
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }
        }

        /// <summary>
        /// Compact layout for small diagrams (1-4 tables).
        /// Optimizes for minimal edge crossings and visual clarity.
        /// </summary>
        private void LayoutCompact(double tableWidth, double tableHeight, double columnsHeight, double hSpacing, double vSpacing, double padding)
        {
            var neighbors = BuildNeighborMap();
            
            // Sort tables by connectivity and importance (hit count)
            var sortedTables = Tables
                .OrderByDescending(t => neighbors.TryGetValue(t.TableName, out var n) ? n.Count : 0)
                .ThenByDescending(t => t.HitCount)
                .ThenByDescending(t => t.CpuPercentage)
                .ToList();

            if (Tables.Count == 1)
            {
                // Single table - center it
                sortedTables[0].X = padding;
                sortedTables[0].Y = padding;
                sortedTables[0].Width = tableWidth;
                sortedTables[0].ColumnsHeight = columnsHeight;
            }
            else if (Tables.Count == 2)
            {
                // Two tables - check if connected, layout accordingly
                bool connected = Relationships.Any();
                if (connected)
                {
                    // Vertical layout for connected tables (dimension on top)
                    var dim = sortedTables[0];
                    var fact = sortedTables[1];
                    
                    dim.X = padding;
                    dim.Y = padding;
                    dim.Width = tableWidth;
                    dim.ColumnsHeight = columnsHeight;
                    
                    fact.X = padding;
                    fact.Y = padding + tableHeight + vSpacing;
                    fact.Width = tableWidth;
                    fact.ColumnsHeight = columnsHeight;
                }
                else
                {
                    // Horizontal layout for unconnected tables
                    sortedTables[0].X = padding;
                    sortedTables[0].Y = padding;
                    sortedTables[0].Width = tableWidth;
                    sortedTables[0].ColumnsHeight = columnsHeight;
                    
                    sortedTables[1].X = padding + tableWidth + hSpacing;
                    sortedTables[1].Y = padding;
                    sortedTables[1].Width = tableWidth;
                    sortedTables[1].ColumnsHeight = columnsHeight;
                }
            }
            else if (Tables.Count == 3)
            {
                // Three tables - inverted triangle (2 on top, 1 centered below)
                double totalWidth = 2 * tableWidth + hSpacing;
                
                sortedTables[1].X = padding;
                sortedTables[1].Y = padding;
                sortedTables[1].Width = tableWidth;
                sortedTables[1].ColumnsHeight = columnsHeight;
                
                sortedTables[2].X = padding + tableWidth + hSpacing;
                sortedTables[2].Y = padding;
                sortedTables[2].Width = tableWidth;
                sortedTables[2].ColumnsHeight = columnsHeight;
                
                // Most connected table centered below
                sortedTables[0].X = padding + (totalWidth - tableWidth) / 2;
                sortedTables[0].Y = padding + tableHeight + vSpacing;
                sortedTables[0].Width = tableWidth;
                sortedTables[0].ColumnsHeight = columnsHeight;
            }
            else // 4 tables
            {
                // 2x2 grid, most connected in bottom-left
                sortedTables[1].X = padding;
                sortedTables[1].Y = padding;
                sortedTables[1].Width = tableWidth;
                sortedTables[1].ColumnsHeight = columnsHeight;
                
                sortedTables[2].X = padding + tableWidth + hSpacing;
                sortedTables[2].Y = padding;
                sortedTables[2].Width = tableWidth;
                sortedTables[2].ColumnsHeight = columnsHeight;
                
                sortedTables[0].X = padding;
                sortedTables[0].Y = padding + tableHeight + vSpacing;
                sortedTables[0].Width = tableWidth;
                sortedTables[0].ColumnsHeight = columnsHeight;
                
                sortedTables[3].X = padding + tableWidth + hSpacing;
                sortedTables[3].Y = padding + tableHeight + vSpacing;
                sortedTables[3].Width = tableWidth;
                sortedTables[3].ColumnsHeight = columnsHeight;
            }

            // Calculate canvas size
            var maxX = Tables.Any() ? Tables.Max(t => t.X + t.Width) : 100;
            var maxY = Tables.Any() ? Tables.Max(t => t.Y + t.Height) : 100;
            CanvasWidth = Math.Max(100, maxX + padding);
            CanvasHeight = Math.Max(100, maxY + padding);

            // Update relationship paths
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }
        }

        /// <summary>
        /// Integrates disconnected tables into existing layers based on their importance.
        /// Tables with higher hit counts are placed in more prominent positions.
        /// </summary>
        private void IntegrateDisconnectedTables(List<List<ErdTableViewModel>> layers)
        {
            if (!layers.Any()) return;
            
            // Find all tables that are in layers
            var assignedTables = new HashSet<string>(
                layers.SelectMany(l => l.Select(t => t.TableName)), 
                StringComparer.OrdinalIgnoreCase);
            
            // Find disconnected tables
            var disconnected = Tables
                .Where(t => !assignedTables.Contains(t.TableName))
                .OrderByDescending(t => t.HitCount)
                .ThenByDescending(t => t.CpuPercentage)
                .ToList();
            
            if (!disconnected.Any()) return;
            
            // Find the layer with fewest tables (to balance the layout)
            var targetLayerIndex = 0;
            var minCount = int.MaxValue;
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Count < minCount)
                {
                    minCount = layers[i].Count;
                    targetLayerIndex = i;
                }
            }
            
            // Distribute disconnected tables across layers, starting with the least populated
            int layerIndex = targetLayerIndex;
            foreach (var table in disconnected)
            {
                layers[layerIndex].Add(table);
                // Round-robin to balance layers
                layerIndex = (layerIndex + 1) % layers.Count;
            }
        }

        /// <summary>
        /// Assigns tables to layers using longest-path layering (Sugiyama Step 2).
        /// The "one" side of relationships (dimensions) are placed above the "many" side (facts).
        /// </summary>
        private List<List<ErdTableViewModel>> AssignLayers()
        {
            var layers = new List<List<ErdTableViewModel>>();
            var tableLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            // Build directed adjacency: from "one" side to "many" side (dimension -> fact)
            var adjacencyToMany = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var adjacencyFromMany = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var inDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var table in Tables)
            {
                adjacencyToMany[table.TableName] = new List<string>();
                adjacencyFromMany[table.TableName] = new List<string>();
                inDegree[table.TableName] = 0;
            }
            
            // Direction: from "1" side (dimension) to "*" side (fact)
            foreach (var rel in Relationships)
            {
                string fromTable, toTable;
                
                // Determine direction based on cardinality
                // "1" side points to "*" side (filter flows from one to many)
                if (rel.FromCardinalitySymbol == "1" && rel.ToCardinalitySymbol == "*")
                {
                    fromTable = rel.FromTable;
                    toTable = rel.ToTable;
                }
                else if (rel.ToCardinalitySymbol == "1" && rel.FromCardinalitySymbol == "*")
                {
                    fromTable = rel.ToTable;
                    toTable = rel.FromTable;
                }
                else
                {
                    // Same cardinality on both sides - use alphabetical order for consistency
                    if (string.Compare(rel.FromTable, rel.ToTable, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        fromTable = rel.FromTable;
                        toTable = rel.ToTable;
                    }
                    else
                    {
                        fromTable = rel.ToTable;
                        toTable = rel.FromTable;
                    }
                }
                
                if (adjacencyToMany.ContainsKey(fromTable) && adjacencyFromMany.ContainsKey(toTable))
                {
                    if (!adjacencyToMany[fromTable].Contains(toTable))
                    {
                        adjacencyToMany[fromTable].Add(toTable);
                        adjacencyFromMany[toTable].Add(fromTable);
                        inDegree[toTable]++;
                    }
                }
            }
            
            // Find root nodes (tables with no incoming edges - top-level dimensions)
            var rootNodes = Tables.Where(t => inDegree[t.TableName] == 0).ToList();
            
            // If no clear roots (cyclic), pick tables with most outgoing edges as roots
            if (!rootNodes.Any())
            {
                rootNodes = Tables
                    .OrderByDescending(t => adjacencyToMany[t.TableName].Count)
                    .ThenBy(t => adjacencyFromMany[t.TableName].Count)
                    .Take(Math.Max(1, Tables.Count / 3))
                    .ToList();
            }
            
            // Assign layers using BFS from root nodes (topological ordering)
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<(ErdTableViewModel table, int layer)>();
            
            foreach (var root in rootNodes)
            {
                queue.Enqueue((root, 0));
                visited.Add(root.TableName);
            }
            
            // Process tables level by level
            while (queue.Count > 0)
            {
                var (table, layer) = queue.Dequeue();
                
                // Ensure layer exists
                while (layers.Count <= layer)
                    layers.Add(new List<ErdTableViewModel>());
                
                layers[layer].Add(table);
                tableLayer[table.TableName] = layer;
                
                // Add connected tables at next layer
                foreach (var childName in adjacencyToMany[table.TableName])
                {
                    if (!visited.Contains(childName))
                    {
                        visited.Add(childName);
                        var childTable = Tables.FirstOrDefault(t => t.TableName.Equals(childName, StringComparison.OrdinalIgnoreCase));
                        if (childTable != null)
                        {
                            queue.Enqueue((childTable, layer + 1));
                        }
                    }
                }
            }
            
            // Add any remaining unvisited tables (disconnected components) to a new layer
            var unvisited = Tables.Where(t => !visited.Contains(t.TableName)).ToList();
            if (unvisited.Any())
            {
                layers.Add(unvisited);
            }
            
            return layers;
        }

        /// <summary>
        /// Minimizes edge crossings using barycenter heuristic (Sugiyama Step 4).
        /// Multiple passes: sweep down then up repeatedly.
        /// </summary>
        private void MinimizeCrossings(List<List<ErdTableViewModel>> layers)
        {
            if (layers.Count < 2) return;
            
            // Build adjacency map
            var neighbors = BuildNeighborMap();
            
            // Multiple passes of crossing minimization using barycenter heuristic
            const int maxIterations = 4;
            
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Downward sweep: order layer i based on positions in layer i-1
                for (int i = 1; i < layers.Count; i++)
                {
                    OrderLayerByBarycenter(layers[i], layers[i - 1], neighbors);
                }
                
                // Upward sweep: order layer i based on positions in layer i+1
                for (int i = layers.Count - 2; i >= 0; i--)
                {
                    OrderLayerByBarycenter(layers[i], layers[i + 1], neighbors);
                }
            }
            
            // Post-processing: swap adjacent tables if it reduces crossings
            OptimizeCrossingsWithSwaps(layers, neighbors);
        }

        /// <summary>
        /// Post-processing step to reduce edge crossings by swapping adjacent tables within layers.
        /// </summary>
        private void OptimizeCrossingsWithSwaps(List<List<ErdTableViewModel>> layers, 
            Dictionary<string, HashSet<string>> neighbors)
        {
            bool improved = true;
            int maxPasses = 3;
            int pass = 0;
            
            while (improved && pass < maxPasses)
            {
                improved = false;
                pass++;
                
                // Try swapping adjacent pairs in each layer
                for (int layerIdx = 0; layerIdx < layers.Count; layerIdx++)
                {
                    var layer = layers[layerIdx];
                    
                    // Get adjacent layers for crossing calculation
                    var upperLayer = layerIdx > 0 ? layers[layerIdx - 1] : null;
                    var lowerLayer = layerIdx < layers.Count - 1 ? layers[layerIdx + 1] : null;
                    
                    // Try swapping each adjacent pair
                    for (int i = 0; i < layer.Count - 1; i++)
                    {
                        int currentCrossings = CountCrossingsForPair(layer, i, i + 1, upperLayer, lowerLayer, neighbors);
                        
                        // Swap and count
                        var temp = layer[i];
                        layer[i] = layer[i + 1];
                        layer[i + 1] = temp;
                        
                        int swappedCrossings = CountCrossingsForPair(layer, i, i + 1, upperLayer, lowerLayer, neighbors);
                        
                        if (swappedCrossings < currentCrossings)
                        {
                            // Keep the swap
                            improved = true;
                        }
                        else
                        {
                            // Revert the swap
                            temp = layer[i];
                            layer[i] = layer[i + 1];
                            layer[i + 1] = temp;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Counts edge crossings involving two specific positions in a layer.
        /// </summary>
        private int CountCrossingsForPair(List<ErdTableViewModel> layer, int pos1, int pos2,
            List<ErdTableViewModel> upperLayer, List<ErdTableViewModel> lowerLayer,
            Dictionary<string, HashSet<string>> neighbors)
        {
            int crossings = 0;
            
            var table1 = layer[pos1];
            var table2 = layer[pos2];
            
            // Check crossings with upper layer
            if (upperLayer != null)
            {
                crossings += CountCrossingsBetweenTables(table1, pos1, table2, pos2, upperLayer, neighbors);
            }
            
            // Check crossings with lower layer
            if (lowerLayer != null)
            {
                crossings += CountCrossingsBetweenTables(table1, pos1, table2, pos2, lowerLayer, neighbors);
            }
            
            return crossings;
        }

        /// <summary>
        /// Counts crossings between edges from two tables to an adjacent layer.
        /// </summary>
        private int CountCrossingsBetweenTables(ErdTableViewModel table1, int pos1,
            ErdTableViewModel table2, int pos2,
            List<ErdTableViewModel> adjacentLayer,
            Dictionary<string, HashSet<string>> neighbors)
        {
            int crossings = 0;
            
            // Get neighbors of table1 in adjacent layer
            var neighbors1 = new List<int>();
            if (neighbors.TryGetValue(table1.TableName, out var n1))
            {
                for (int i = 0; i < adjacentLayer.Count; i++)
                {
                    if (n1.Contains(adjacentLayer[i].TableName))
                        neighbors1.Add(i);
                }
            }
            
            // Get neighbors of table2 in adjacent layer
            var neighbors2 = new List<int>();
            if (neighbors.TryGetValue(table2.TableName, out var n2))
            {
                for (int i = 0; i < adjacentLayer.Count; i++)
                {
                    if (n2.Contains(adjacentLayer[i].TableName))
                        neighbors2.Add(i);
                }
            }
            
            // Count crossings: edge from table1 (at pos1) to adj[i] crosses edge from table2 (at pos2) to adj[j]
            // if (pos1 < pos2 and i > j) or (pos1 > pos2 and i < j)
            foreach (int adjPos1 in neighbors1)
            {
                foreach (int adjPos2 in neighbors2)
                {
                    // Crossing occurs when the relative order is different
                    if ((pos1 < pos2 && adjPos1 > adjPos2) || (pos1 > pos2 && adjPos1 < adjPos2))
                    {
                        crossings++;
                    }
                }
            }
            
            return crossings;
        }

        /// <summary>
        /// Orders a layer based on the median position of neighbors in the reference layer.
        /// Uses median instead of mean (barycenter) for more stable results with varying connectivity.
        /// Falls back to hit count for tables with no connections for consistent ordering.
        /// </summary>
        private void OrderLayerByBarycenter(List<ErdTableViewModel> layer, 
            List<ErdTableViewModel> referenceLayer, 
            Dictionary<string, HashSet<string>> neighbors)
        {
            // Calculate median position of neighbors for each table
            var medianPositions = new Dictionary<ErdTableViewModel, double>();
            var neighborPositions = new Dictionary<ErdTableViewModel, List<int>>();
            
            // Collect neighbor positions for each table
            foreach (var table in layer)
            {
                neighborPositions[table] = new List<int>();
                
                if (neighbors.TryGetValue(table.TableName, out var tableNeighbors))
                {
                    for (int i = 0; i < referenceLayer.Count; i++)
                    {
                        if (tableNeighbors.Contains(referenceLayer[i].TableName))
                        {
                            neighborPositions[table].Add(i);
                        }
                    }
                }
            }
            
            // Calculate median for each table
            foreach (var table in layer)
            {
                var positions = neighborPositions[table];
                if (positions.Count == 0)
                {
                    // No neighbors - use a value that preserves relative ordering
                    // Place disconnected high-hit tables more centrally
                    medianPositions[table] = referenceLayer.Count / 2.0 + 
                        (1.0 - (table.HitCount / (double)Math.Max(1, Tables.Max(t => t.HitCount)))) * 0.1;
                }
                else if (positions.Count == 1)
                {
                    medianPositions[table] = positions[0];
                }
                else
                {
                    positions.Sort();
                    int mid = positions.Count / 2;
                    medianPositions[table] = positions.Count % 2 == 0
                        ? (positions[mid - 1] + positions[mid]) / 2.0
                        : positions[mid];
                }
            }
            
            // Sort layer by median position, then by hit count (descending) for ties, then by name
            var sorted = layer
                .OrderBy(t => medianPositions.TryGetValue(t, out var m) ? m : double.MaxValue / 2)
                .ThenByDescending(t => t.HitCount)
                .ThenByDescending(t => t.CpuPercentage)
                .ThenBy(t => t.TableName)
                .ToList();
            
            layer.Clear();
            layer.AddRange(sorted);
        }

        /// <summary>
        /// Builds a map of table name to all connected table names.
        /// </summary>
        private Dictionary<string, HashSet<string>> BuildNeighborMap()
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var table in Tables)
            {
                map[table.TableName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            
            foreach (var rel in Relationships)
            {
                if (map.ContainsKey(rel.FromTable))
                    map[rel.FromTable].Add(rel.ToTable);
                if (map.ContainsKey(rel.ToTable))
                    map[rel.ToTable].Add(rel.FromTable);
            }
            
            return map;
        }

        /// <summary>
        /// Assigns X coordinates to tables, trying to align connected tables (Sugiyama Step 5).
        /// Uses barycenter alignment with priority to minimize total edge length.
        /// </summary>
        private void AssignCoordinates(List<List<ErdTableViewModel>> layers,
            double tableWidth, double tableHeight, double columnsHeight, double hSpacing, double vSpacing, double padding)
        {
            var neighbors = BuildNeighborMap();
            
            // First pass: simple left-to-right placement
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                double y = padding + layerIndex * (tableHeight + vSpacing);
                double x = padding;
                
                foreach (var table in layer)
                {
                    table.X = x;
                    table.Y = y;
                    table.Width = tableWidth;
                    table.ColumnsHeight = columnsHeight;
                    x += tableWidth + hSpacing;
                }
            }
            
            // Second pass: adjust X positions based on connected tables (Brandes-Köpf inspired)
            // Pull tables toward their connected tables in adjacent layers
            const int refinementIterations = 3;
            
            for (int iter = 0; iter < refinementIterations; iter++)
            {
                // Downward pass: align with upper neighbors
                for (int layerIndex = 1; layerIndex < layers.Count; layerIndex++)
                {
                    AdjustLayerPositions(layers[layerIndex], layers[layerIndex - 1], neighbors, tableWidth, hSpacing, padding);
                }
                
                // Upward pass: align with lower neighbors
                for (int layerIndex = layers.Count - 2; layerIndex >= 0; layerIndex--)
                {
                    AdjustLayerPositions(layers[layerIndex], layers[layerIndex + 1], neighbors, tableWidth, hSpacing, padding);
                }
            }
            
            // Final centering: center all layers relative to the widest layer
            CenterAllLayers(layers, tableWidth, hSpacing, padding);
        }

        /// <summary>
        /// Adjusts positions of tables in a layer to align with connected tables.
        /// </summary>
        private void AdjustLayerPositions(List<ErdTableViewModel> layer,
            List<ErdTableViewModel> referenceLayer,
            Dictionary<string, HashSet<string>> neighbors,
            double tableWidth, double hSpacing, double padding)
        {
            // Calculate target X for each table based on average neighbor position
            var targetX = new Dictionary<ErdTableViewModel, double>();
            
            foreach (var table in layer)
            {
                if (neighbors.TryGetValue(table.TableName, out var tableNeighbors))
                {
                    var connectedInRef = referenceLayer
                        .Where(r => tableNeighbors.Contains(r.TableName))
                        .ToList();
                    
                    if (connectedInRef.Any())
                    {
                        // Target is average center X of connected tables
                        targetX[table] = connectedInRef.Average(t => t.X + tableWidth / 2) - tableWidth / 2;
                    }
                }
            }
            
            // Sort layer by current X position
            var sortedLayer = layer.OrderBy(t => t.X).ToList();
            
            // Adjust positions while maintaining order and minimum spacing
            for (int i = 0; i < sortedLayer.Count; i++)
            {
                var table = sortedLayer[i];
                double minX = (i == 0) ? padding : sortedLayer[i - 1].X + tableWidth + hSpacing;
                
                if (targetX.TryGetValue(table, out double target))
                {
                    // Move toward target, but not past minimum
                    double newX = Math.Max(minX, target);
                    
                    // Don't move too far (dampen to avoid oscillation)
                    double maxMove = (tableWidth + hSpacing) / 2;
                    double move = Math.Min(Math.Abs(newX - table.X), maxMove);
                    if (newX > table.X)
                        table.X = Math.Max(minX, table.X + move);
                    else if (newX < table.X)
                        table.X = Math.Max(minX, table.X - move);
                }
                else
                {
                    // No target - just ensure minimum spacing
                    if (table.X < minX)
                        table.X = minX;
                }
            }
        }

        /// <summary>
        /// Centers all layers relative to the widest layer.
        /// </summary>
        private void CenterAllLayers(List<List<ErdTableViewModel>> layers,
            double tableWidth, double hSpacing, double padding)
        {
            if (!layers.Any()) return;
            
            // Find the widest layer
            double maxWidth = 0;
            foreach (var layer in layers)
            {
                if (layer.Any())
                {
                    double layerWidth = layer.Max(t => t.X + tableWidth) - layer.Min(t => t.X);
                    maxWidth = Math.Max(maxWidth, layerWidth);
                }
            }
            
            // Center each layer
            foreach (var layer in layers)
            {
                if (!layer.Any()) continue;
                
                double layerWidth = layer.Max(t => t.X + tableWidth) - layer.Min(t => t.X);
                double offset = (maxWidth - layerWidth) / 2;
                
                foreach (var table in layer)
                {
                    table.X += offset;
                }
            }
        }

        /// <summary>
        /// Compacts the layout by removing unnecessary gaps between tables.
        /// Tables are pulled left while maintaining minimum spacing and alignment.
        /// </summary>
        private void CompactLayout(List<List<ErdTableViewModel>> layers, double tableWidth, double hSpacing, double padding)
        {
            var neighbors = BuildNeighborMap();
            
            foreach (var layer in layers)
            {
                if (layer.Count <= 1) continue;
                
                // Sort by current X position
                var sortedLayer = layer.OrderBy(t => t.X).ToList();
                
                // Pull each table as far left as possible while respecting:
                // 1. Minimum spacing from previous table
                // 2. Alignment with connected tables in other layers
                for (int i = 1; i < sortedLayer.Count; i++)
                {
                    var table = sortedLayer[i];
                    var prevTable = sortedLayer[i - 1];
                    
                    double minX = prevTable.X + tableWidth + hSpacing;
                    
                    // Check if we have a connected table we should align with
                    if (neighbors.TryGetValue(table.TableName, out var tableNeighbors))
                    {
                        var connectedTables = Tables
                            .Where(t => tableNeighbors.Contains(t.TableName) && !layer.Contains(t))
                            .ToList();
                        
                        if (connectedTables.Any())
                        {
                            // Get average X of connected tables
                            double avgConnectedX = connectedTables.Average(t => t.X + tableWidth / 2) - tableWidth / 2;
                            
                            // Only move if it reduces the gap and doesn't violate minimum spacing
                            if (avgConnectedX >= minX && avgConnectedX < table.X)
                            {
                                table.X = avgConnectedX;
                            }
                            else if (table.X > minX + hSpacing)
                            {
                                // Close unnecessary gaps
                                table.X = Math.Max(minX, Math.Min(table.X, avgConnectedX));
                            }
                        }
                        else if (table.X > minX + hSpacing)
                        {
                            // No connected tables - just compact
                            table.X = minX;
                        }
                    }
                    else if (table.X > minX + hSpacing)
                    {
                        // No neighbors - compact to minimum
                        table.X = minX;
                    }
                }
            }
        }

        /// <summary>
        /// Clears all analysis data.
        /// </summary>
        public void Clear()
        {
            _analysis?.Clear();
            Tables.Clear();
            Relationships.Clear();
            NotifyOfPropertyChange(nameof(Analysis));
            NotifyOfPropertyChange(nameof(SummaryText));
            NotifyOfPropertyChange(nameof(HasData));
        }

        /// <summary>
        /// Copies the ERD summary to clipboard.
        /// </summary>
        public void CopyToClipboard()
        {
            if (_analysis == null) return;

            var text = new System.Text.StringBuilder();
            text.AppendLine("=== xmSQL Query Dependencies ===");
            text.AppendLine();
            text.AppendLine($"Tables: {_analysis.UniqueTablesCount}");
            text.AppendLine($"Columns: {_analysis.UniqueColumnsCount}");
            text.AppendLine($"Relationships: {_analysis.UniqueRelationshipsCount}");
            text.AppendLine($"SE Queries Analyzed: {_analysis.TotalSEQueriesAnalyzed}");
            text.AppendLine();

            text.AppendLine("=== Tables ===");
            foreach (var table in _analysis.Tables.Values.OrderByDescending(t => t.HitCount))
            {
                text.AppendLine($"\n[{table.TableName}] (Hit Count: {table.HitCount})");
                foreach (var col in table.Columns.Values.OrderByDescending(c => c.HitCount))
                {
                    var usages = new List<string>();
                    if (col.UsageTypes.HasFlag(XmSqlColumnUsage.Select)) usages.Add("Select");
                    if (col.UsageTypes.HasFlag(XmSqlColumnUsage.Filter)) usages.Add("Filter");
                    if (col.UsageTypes.HasFlag(XmSqlColumnUsage.Join)) usages.Add("Join");
                    if (col.UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate)) usages.Add($"Aggregate({string.Join(",", col.AggregationTypes)})");

                    text.AppendLine($"  [{col.ColumnName}] - {string.Join(", ", usages)} (Hit Count: {col.HitCount})");
                }
            }

            text.AppendLine("\n=== Relationships ===");
            foreach (var rel in _analysis.Relationships.OrderByDescending(r => r.HitCount))
            {
                text.AppendLine($"  [{rel.FromTable}].[{rel.FromColumn}] -> [{rel.ToTable}].[{rel.ToColumn}] ({rel.JoinType}, Hit Count: {rel.HitCount})");
            }

            Clipboard.SetText(text.ToString());
        }

        /// <summary>
        /// Resets the layout by re-running the layout algorithm.
        /// Useful after dragging tables around to restore the auto-arranged layout.
        /// </summary>
        public void ResetLayout()
        {
            if (Tables.Count == 0) return;
            
            LayoutDiagram();
            CalculateHeatLevels();
        }
        
        /// <summary>
        /// Event raised when export to image is requested.
        /// The view handles the actual rendering.
        /// </summary>
        public event EventHandler<string> ExportRequested;
        
        /// <summary>
        /// Requests export of the diagram to an image file.
        /// </summary>
        public void ExportToImage()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Export Diagram to Image",
                FileName = "QueryDependencies.png"
            };
            
            if (dialog.ShowDialog() == true)
            {
                ExportRequested?.Invoke(this, dialog.FileName);
            }
        }

        /// <summary>
        /// Event raised when copy image to clipboard is requested.
        /// The view handles the actual rendering.
        /// </summary>
        public event EventHandler CopyImageRequested;
        
        /// <summary>
        /// Copies the diagram as an image to the clipboard.
        /// </summary>
        public void CopyImageToClipboard()
        {
            CopyImageRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Zoom

        /// <summary>
        /// Zooms in the diagram by increasing the scale.
        /// </summary>
        public void ZoomIn()
        {
            Scale = Math.Min(Scale + 0.1, 3.0); // Max 300%
        }

        /// <summary>
        /// Zooms out the diagram by decreasing the scale.
        /// </summary>
        public void ZoomOut()
        {
            Scale = Math.Max(Scale - 0.1, 0.25); // Min 25%
        }

        /// <summary>
        /// Resets the zoom level to 100%.
        /// </summary>
        public void ResetZoom()
        {
            Scale = 1.0;
        }

        /// <summary>
        /// Zooms to fit all tables within the visible view area.
        /// </summary>
        public void ZoomToFit()
        {
            if (Tables.Count == 0) return;

            // Calculate bounds of all tables
            var minX = Tables.Min(t => t.X);
            var minY = Tables.Min(t => t.Y);
            var maxX = Tables.Max(t => t.X + t.Width);
            var maxY = Tables.Max(t => t.Y + t.Height);

            var contentWidth = maxX - minX + 80; // Add padding
            var contentHeight = maxY - minY + 80;

            // Calculate scale to fit
            var scaleX = ViewWidth / contentWidth;
            var scaleY = ViewHeight / contentHeight;
            var newScale = Math.Min(scaleX, scaleY);

            // Clamp to reasonable bounds
            newScale = Math.Max(0.25, Math.Min(2.0, newScale));

            Scale = newScale;
        }

        #endregion

        #region Mini-map Navigation

        private bool _showMiniMap = false;
        /// <summary>
        /// Whether to show the mini-map overview panel.
        /// Auto-enabled when there are 10 or more tables.
        /// </summary>
        public bool ShowMiniMap
        {
            get => _showMiniMap;
            set 
            { 
                _showMiniMap = value; 
                NotifyOfPropertyChange(); 
            }
        }

        /// <summary>
        /// Toggles the mini-map visibility.
        /// </summary>
        public void ToggleMiniMap()
        {
            ShowMiniMap = !ShowMiniMap;
        }

        private double _viewportX;
        /// <summary>
        /// The X position of the viewport in content coordinates (for mini-map).
        /// </summary>
        public double ViewportX
        {
            get => _viewportX;
            set 
            { 
                _viewportX = value; 
                NotifyOfPropertyChange(); 
            }
        }

        private double _viewportY;
        /// <summary>
        /// The Y position of the viewport in content coordinates (for mini-map).
        /// </summary>
        public double ViewportY
        {
            get => _viewportY;
            set 
            { 
                _viewportY = value; 
                NotifyOfPropertyChange(); 
            }
        }

        private double _contentWidth = 1000;
        /// <summary>
        /// Total width of all content (for mini-map calculations).
        /// </summary>
        public double ContentWidth
        {
            get => _contentWidth;
            set 
            { 
                _contentWidth = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(MiniMapScaleX));
            }
        }

        private double _contentHeight = 1000;
        /// <summary>
        /// Total height of all content (for mini-map calculations).
        /// </summary>
        public double ContentHeight
        {
            get => _contentHeight;
            set 
            { 
                _contentHeight = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(MiniMapScaleY));
            }
        }

        // Mini-map dimensions
        private const double MiniMapWidth = 150;
        private const double MiniMapHeight = 100;

        /// <summary>
        /// Scale factor for X axis in mini-map.
        /// </summary>
        public double MiniMapScaleX => ContentWidth > 0 ? MiniMapWidth / ContentWidth : 1;

        /// <summary>
        /// Scale factor for Y axis in mini-map.
        /// </summary>
        public double MiniMapScaleY => ContentHeight > 0 ? MiniMapHeight / ContentHeight : 1;

        /// <summary>
        /// Updates content bounds for mini-map based on table positions.
        /// </summary>
        public void UpdateContentBounds()
        {
            if (Tables.Count == 0)
            {
                ContentWidth = 1000;
                ContentHeight = 800;
                return;
            }

            var minX = Tables.Min(t => t.X);
            var minY = Tables.Min(t => t.Y);
            var maxX = Tables.Max(t => t.X + t.Width);
            var maxY = Tables.Max(t => t.Y + t.Height);

            ContentWidth = Math.Max(maxX - minX + 100, ViewWidth);
            ContentHeight = Math.Max(maxY - minY + 100, ViewHeight);
        }

        /// <summary>
        /// Called when mini-map is clicked to navigate to that location.
        /// </summary>
        public event EventHandler<Point> MiniMapNavigationRequested;

        /// <summary>
        /// Navigates to a position on the mini-map (triggered by click).
        /// </summary>
        public void NavigateToMiniMapPosition(double miniMapX, double miniMapY)
        {
            // Convert mini-map coordinates to content coordinates
            var contentX = miniMapX / MiniMapScaleX;
            var contentY = miniMapY / MiniMapScaleY;

            MiniMapNavigationRequested?.Invoke(this, new Point(contentX, contentY));
        }

        #endregion

        #region Bottleneck Analysis

        /// <summary>
        /// Toggles bottleneck highlighting on/off.
        /// </summary>
        public void ToggleBottlenecks()
        {
            ShowBottlenecks = !ShowBottlenecks;
        }

        /// <summary>
        /// Updates bottleneck highlighting on all tables based on their duration.
        /// Tables in the top 20% of duration are marked as bottlenecks.
        /// </summary>
        private void UpdateBottleneckHighlighting()
        {
            if (!_showBottlenecks)
            {
                // Clear all bottleneck flags
                foreach (var table in Tables)
                {
                    table.IsBottleneck = false;
                    table.BottleneckRank = 0;
                }
                return;
            }

            // Calculate bottleneck threshold (top 20% or top 3, whichever is smaller)
            var tablesWithDuration = Tables
                .Where(t => t.TotalDurationMs > 0)
                .OrderByDescending(t => t.TotalDurationMs)
                .ToList();

            if (tablesWithDuration.Count == 0) return;

            var bottleneckCount = Math.Max(1, Math.Min(3, (int)Math.Ceiling(tablesWithDuration.Count * 0.2)));
            var threshold = tablesWithDuration.Count > bottleneckCount 
                ? tablesWithDuration[bottleneckCount - 1].TotalDurationMs 
                : 0;

            // Mark bottleneck tables
            var rank = 1;
            foreach (var table in Tables)
            {
                if (tablesWithDuration.Contains(table) && 
                    tablesWithDuration.IndexOf(table) < bottleneckCount)
                {
                    table.IsBottleneck = true;
                    table.BottleneckRank = rank++;
                }
                else
                {
                    table.IsBottleneck = false;
                    table.BottleneckRank = 0;
                }
            }
        }

        #endregion

        #region Selection and Highlighting

        private ErdTableViewModel _selectedTable;
        /// <summary>
        /// The currently selected table (if any).
        /// </summary>
        public ErdTableViewModel SelectedTable
        {
            get => _selectedTable;
            private set
            {
                _selectedTable = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasSelectedTable));
            }
        }

        /// <summary>
        /// Whether a table is currently selected.
        /// </summary>
        public bool HasSelectedTable => _selectedTable != null;

        /// <summary>
        /// Selects a table and highlights related tables and relationships.
        /// </summary>
        public void SelectTable(ErdTableViewModel table)
        {
            // If clicking the same table, deselect
            if (_selectedTable == table)
            {
                ClearSelection();
                return;
            }

            // Clear previous selection
            ClearSelectionState();

            // Set new selection
            SelectedTable = table;
            table.IsSelected = true;

            // Find related tables and relationships
            var relatedTables = new HashSet<ErdTableViewModel>();

            foreach (var rel in Relationships)
            {
                bool isRelated = rel.FromTableViewModel == table || rel.ToTableViewModel == table;
                
                if (isRelated)
                {
                    rel.IsHighlighted = true;
                    rel.IsDimmed = false;
                    
                    // Add the other table to related set
                    if (rel.FromTableViewModel == table)
                        relatedTables.Add(rel.ToTableViewModel);
                    else
                        relatedTables.Add(rel.FromTableViewModel);
                }
                else
                {
                    rel.IsHighlighted = false;
                    rel.IsDimmed = true;
                }
            }

            // Update table highlighting
            foreach (var t in Tables)
            {
                if (t == table)
                {
                    t.IsSelected = true;
                    t.IsHighlighted = false;
                    t.IsDimmed = false;
                }
                else if (relatedTables.Contains(t))
                {
                    t.IsSelected = false;
                    t.IsHighlighted = true;
                    t.IsDimmed = false;
                }
                else
                {
                    t.IsSelected = false;
                    t.IsHighlighted = false;
                    t.IsDimmed = true;
                }
            }
        }

        /// <summary>
        /// Selects a table by name and highlights related tables and relationships.
        /// </summary>
        public void SelectTable(string tableName)
        {
            var table = Tables.FirstOrDefault(t => t.TableName == tableName);
            if (table != null)
            {
                SelectTable(table);
            }
        }

        /// <summary>
        /// Clears the current selection and all highlighting.
        /// </summary>
        public void ClearSelection()
        {
            ClearSelectionState();
            SelectedTable = null;
            SelectedDetailInfo = null;
        }

        private string _selectedDetailInfo;
        /// <summary>
        /// Detail information about the selected item (column or relationship).
        /// Displayed in a panel when user clicks on a column or relationship.
        /// </summary>
        public string SelectedDetailInfo
        {
            get => _selectedDetailInfo;
            set
            {
                _selectedDetailInfo = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasSelectedDetail));
            }
        }

        public bool HasSelectedDetail => !string.IsNullOrEmpty(_selectedDetailInfo);

        private double _detailPanelWidth = 350;
        /// <summary>
        /// Width of the detail panel (resizable by user).
        /// </summary>
        public double DetailPanelWidth
        {
            get => _detailPanelWidth;
            set
            {
                // Clamp between min and max (280 to 600)
                _detailPanelWidth = Math.Max(280, Math.Min(600, value));
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Shows details for a clicked column.
        /// </summary>
        public void SelectColumn(ErdTableViewModel table, ErdColumnViewModel column)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine($"📊 COLUMN: {table.TableName}[{column.ColumnName}]");
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine();
            
            // Usage summary line
            var usages = new List<string>();
            if (column.IsJoinColumn) usages.Add("🔑 Join");
            if (column.IsFilterColumn) usages.Add("🔍 Filter");
            if (column.IsAggregateColumn) usages.Add("📈 Aggregate");
            if (column.IsSelectColumn) usages.Add("✓ Select");
            if (column.HasCallback) usages.Add("⚡ Callback");
            
            sb.AppendLine($"Usage: {(usages.Any() ? string.Join(" | ", usages) : "None detected")}");
            sb.AppendLine($"Hit Count: {column.HitCount}");
            sb.AppendLine();
            
            // Join Key details
            if (column.IsJoinColumn)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("🔑 JOIN KEY DETAILS");
                sb.AppendLine("───────────────────────────────────────────");
                
                // Find relationships using this column
                var columnRelationships = Relationships.Where(r =>
                    (r.FromTable == table.TableName && r.FromColumn == column.ColumnName) ||
                    (r.ToTable == table.TableName && r.ToColumn == column.ColumnName)).ToList();
                
                if (columnRelationships.Any())
                {
                    sb.AppendLine($"Participates in {columnRelationships.Count} relationship(s):");
                    foreach (var rel in columnRelationships)
                    {
                        var isFrom = rel.FromTable == table.TableName;
                        var otherTable = isFrom ? rel.ToTable : rel.FromTable;
                        var otherColumn = isFrom ? rel.ToColumn : rel.FromColumn;
                        var direction = isFrom ? "→" : "←";
                        sb.AppendLine($"  {direction} {otherTable}[{otherColumn}]");
                        sb.AppendLine($"      Type: {rel.JoinTypeText}, Cardinality: {rel.CardinalityText}");
                    }
                }
                sb.AppendLine();
            }
            
            // Filter details
            if (column.IsFilterColumn)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("🔍 FILTER DETAILS");
                sb.AppendLine("───────────────────────────────────────────");
                
                if (column.HasFilterValues)
                {
                    var ops = column.FilterOperators.Any() 
                        ? string.Join(", ", column.FilterOperators) 
                        : "=";
                    sb.AppendLine($"Operators Used: {ops}");
                    sb.AppendLine($"Distinct Values: {column.FilterValues.Count}");
                    sb.AppendLine();
                    
                    // Show values (limit display to first 20)
                    sb.AppendLine("Filter Values:");
                    var values = column.FilterValues.Take(20).ToList();
                    foreach (var value in values)
                    {
                        sb.AppendLine($"  • {value}");
                    }
                    if (column.FilterValues.Count > 20)
                    {
                        sb.AppendLine($"  ... and {column.FilterValues.Count - 20} more values");
                    }
                    
                    // Analysis insight
                    if (column.FilterValues.Count > 100)
                    {
                        sb.AppendLine();
                        sb.AppendLine("💡 TIP: Large number of filter values detected.");
                        sb.AppendLine("   Consider using a dimension table for better performance.");
                    }
                }
                else
                {
                    sb.AppendLine("Filter applied but values not captured.");
                }
                sb.AppendLine();
            }
            
            // Aggregate details
            if (column.IsAggregateColumn)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("📈 AGGREGATION DETAILS");
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine($"Functions: {column.AggregationText}");
                sb.AppendLine();
            }
            
            // Callback warning
            if (column.HasCallback)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("⚡ CALLBACK WARNING");
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine($"Callback Type: {column.CallbackType}");
                sb.AppendLine();
                sb.AppendLine("⚠ DAX callbacks force row-by-row evaluation");
                sb.AppendLine("  and prevent Storage Engine optimization.");
                sb.AppendLine();
                sb.AppendLine("Common causes:");
                sb.AppendLine("  • Calculated columns referencing other tables");
                sb.AppendLine("  • Complex DAX in calculated columns");
                sb.AppendLine("  • Security filters with dynamic expressions");
                sb.AppendLine();
                sb.AppendLine("💡 TIP: Consider replacing calculated columns");
                sb.AppendLine("   with measures or Power Query transformations.");
                sb.AppendLine();
            }
            
            // Parent table context
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("📋 TABLE CONTEXT");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"Table: {table.TableName}");
            sb.AppendLine($"Table Hits: {table.HitCount} | Columns: {table.Columns.Count}");
            if (table.HasRowCountData)
            {
                sb.AppendLine($"Rows Scanned: {table.TotalRowsFormatted}");
            }
            if (table.HasDurationData)
            {
                sb.AppendLine($"Total Duration: {table.TotalDurationFormatted}");
            }
            if (table.IsCpuHotspot)
            {
                sb.AppendLine($"⚡ CPU Hotspot: {table.CpuPercentageFormatted} of total CPU");
            }
            if (table.HasCacheData)
            {
                sb.AppendLine($"Cache Hit Rate: {table.CacheHitRateFormatted}");
            }
            
            SelectedDetailInfo = sb.ToString();
        }

        /// <summary>
        /// Shows details for a clicked table header.
        /// </summary>
        public void SelectTableDetails(ErdTableViewModel table)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine($"📊 TABLE: {table.TableName}");
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine();
            
            // Quick summary line
            var flags = new List<string>();
            if (table.IsFromTable) flags.Add("FROM");
            if (table.IsJoinedTable) flags.Add("JOIN");
            if (table.HasCallbacks) flags.Add("⚡Callbacks");
            if (table.IsCpuHotspot) flags.Add("🔥CPU Hot");
            if (table.HasHighRowCount) flags.Add("⚠High Rows");
            if (flags.Any()) sb.AppendLine($"Flags: {string.Join(" | ", flags)}");
            sb.AppendLine();
            
            // Basic stats
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("📈 STATISTICS");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"SE Query Hits: {table.HitCount}");
            sb.AppendLine($"Distinct SE Queries: {table.QueryCount}");
            sb.AppendLine($"Columns Accessed: {table.Columns.Count}");
            
            // Query IDs
            if (table.HasQueryIds)
            {
                sb.AppendLine($"Query IDs: {table.QueryIdsFormatted}");
            }
            sb.AppendLine();
            
            // Performance metrics
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("⏱ PERFORMANCE");
            sb.AppendLine("───────────────────────────────────────────");
            
            // Row statistics
            if (table.HasRowCountData)
            {
                sb.AppendLine($"Rows Scanned: {table.TotalEstimatedRows:N0} total");
                sb.AppendLine($"Max Single Scan: {table.MaxEstimatedRows:N0} rows");
            }
            
            // Duration
            if (table.HasDurationData)
            {
                sb.AppendLine($"Duration: {table.TotalDurationFormatted} total");
                sb.AppendLine($"Max Single Query: {table.MaxDurationMs}ms");
            }
            
            // CPU
            if (table.TotalCpuTimeMs > 0)
            {
                sb.AppendLine($"CPU Time: {table.TotalCpuTimeMs:N0}ms ({table.CpuPercentageFormatted} of total)");
            }
            
            // Parallelism
            if (table.HasParallelData)
            {
                sb.AppendLine($"Parallelism: {table.ParallelQueryCount} queries, {table.MaxCpuFactor:0.0}x max factor");
            }
            sb.AppendLine();
            
            // Cache statistics
            if (table.HasCacheData)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("💾 CACHE");
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine($"Hit Rate: {table.CacheHitRateFormatted}");
                sb.AppendLine($"Hits: {table.CacheHits} | Misses: {table.CacheMisses}");
                sb.AppendLine();
            }
            
            // Warnings & Recommendations
            var hasWarnings = table.IsCpuHotspot || table.HasHighRowCount || table.HasPoorCacheRate || table.HasCallbacks || table.HasHighDuration;
            if (hasWarnings)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("⚠ WARNINGS & RECOMMENDATIONS");
                sb.AppendLine("───────────────────────────────────────────");
                
                if (table.IsCpuHotspot)
                {
                    sb.AppendLine();
                    sb.AppendLine($"🔥 CPU HOTSPOT ({table.CpuPercentageFormatted})");
                    sb.AppendLine("   This table dominates CPU consumption.");
                    sb.AppendLine("   → Review DAX measures accessing this table");
                    sb.AppendLine("   → Consider pre-aggregating in Power Query");
                    sb.AppendLine("   → Check for unnecessary calculated columns");
                }
                
                if (table.HasHighRowCount)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ HIGH ROW COUNT (100K+ per scan)");
                    sb.AppendLine("   Large scans impact query performance.");
                    sb.AppendLine("   → Add filters to reduce scan size");
                    sb.AppendLine("   → Review report filters and slicers");
                    sb.AppendLine("   → Consider summarizing data upstream");
                }
                
                if (table.HasPoorCacheRate)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ POOR CACHE HIT RATE");
                    sb.AppendLine("   Many queries miss the SE cache.");
                    sb.AppendLine("   → Check for non-deterministic DAX");
                    sb.AppendLine("   → Review calculated column complexity");
                    sb.AppendLine("   → Consider query folding opportunities");
                }
                
                if (table.HasHighDuration)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ HIGH DURATION");
                    sb.AppendLine("   This table takes significant time to scan.");
                    sb.AppendLine("   → Review column cardinality");
                    sb.AppendLine("   → Check dictionary encoding efficiency");
                    sb.AppendLine("   → Consider partitioning strategies");
                }
                
                if (table.HasCallbacks)
                {
                    sb.AppendLine();
                    sb.AppendLine($"⚡ CALLBACKS DETECTED ({table.CallbackColumnCount} column(s))");
                    sb.AppendLine("   Callbacks prevent SE optimization.");
                    var callbackCols = table.Columns.Where(c => c.HasCallback).Take(5);
                    foreach (var col in callbackCols)
                    {
                        sb.AppendLine($"   • {col.ColumnName}");
                    }
                    sb.AppendLine("   → Replace calculated columns with measures");
                    sb.AppendLine("   → Move calculations to Power Query");
                }
                sb.AppendLine();
            }
            
            // Column breakdown
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("📋 COLUMNS ACCESSED");
            sb.AppendLine("───────────────────────────────────────────");
            
            var joinCols = table.Columns.Where(c => c.IsJoinColumn).ToList();
            var filterCols = table.Columns.Where(c => c.IsFilterColumn).ToList();
            var aggCols = table.Columns.Where(c => c.IsAggregateColumn).ToList();
            var selectCols = table.Columns.Where(c => c.IsSelectColumn).ToList();
            
            sb.AppendLine($"🔑 Join Keys: {joinCols.Count}");
            if (joinCols.Any())
            {
                foreach (var col in joinCols.Take(3))
                    sb.AppendLine($"   • {col.ColumnName}");
                if (joinCols.Count > 3) sb.AppendLine($"   ... +{joinCols.Count - 3} more");
            }
            
            sb.AppendLine($"🔍 Filtered: {filterCols.Count}");
            if (filterCols.Any())
            {
                foreach (var col in filterCols.Take(3))
                {
                    var valCount = col.HasFilterValues ? $" ({col.FilterValues.Count} values)" : "";
                    sb.AppendLine($"   • {col.ColumnName}{valCount}");
                }
                if (filterCols.Count > 3) sb.AppendLine($"   ... +{filterCols.Count - 3} more");
            }
            
            sb.AppendLine($"📈 Aggregated: {aggCols.Count}");
            if (aggCols.Any())
            {
                foreach (var col in aggCols.Take(3))
                    sb.AppendLine($"   • {col.ColumnName} ({col.AggregationText})");
                if (aggCols.Count > 3) sb.AppendLine($"   ... +{aggCols.Count - 3} more");
            }
            
            sb.AppendLine($"✓ Selected: {selectCols.Count}");
            sb.AppendLine();
            
            // Relationships
            var tableRelationships = Relationships.Where(r => 
                r.FromTable == table.TableName || r.ToTable == table.TableName).ToList();
            if (tableRelationships.Any())
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("🔗 RELATIONSHIPS");
                sb.AppendLine("───────────────────────────────────────────");
                foreach (var rel in tableRelationships)
                {
                    var isFrom = rel.FromTable == table.TableName;
                    var direction = isFrom ? "→" : "←";
                    var otherTable = isFrom ? rel.ToTable : rel.FromTable;
                    var col = isFrom ? rel.FromColumn : rel.ToColumn;
                    sb.AppendLine($"{direction} {otherTable}");
                    sb.AppendLine($"   via [{col}], {rel.JoinTypeText}, {rel.CardinalityText}");
                }
            }
            
            SelectedDetailInfo = sb.ToString();
        }

        /// <summary>
        /// Shows details for a clicked relationship.
        /// </summary>
        public void SelectRelationship(ErdRelationshipViewModel relationship)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine($"🔗 RELATIONSHIP");
            sb.AppendLine($"═══════════════════════════════════════════");
            sb.AppendLine();
            
            sb.AppendLine($"{relationship.FromTable}  →  {relationship.ToTable}");
            sb.AppendLine();
            
            // Connection details
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("📋 CONNECTION");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"From: {relationship.FromTable}[{relationship.FromColumn}]");
            sb.AppendLine($"To:   {relationship.ToTable}[{relationship.ToColumn}]");
            sb.AppendLine();
            
            // Properties
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("⚙ PROPERTIES");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"Join Type: {relationship.JoinTypeText}");
            sb.AppendLine($"Cardinality: {relationship.CardinalityText}");
            sb.AppendLine($"Cross-Filter: {(relationship.IsBidirectional ? "Bidirectional (Both)" : "Single direction")}");
            sb.AppendLine($"Times Used: {relationship.HitCount}");
            sb.AppendLine();
            
            // Warnings
            if (relationship.IsManyToMany || relationship.IsBidirectional)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("⚠ CONSIDERATIONS");
                sb.AppendLine("───────────────────────────────────────────");
                
                if (relationship.IsManyToMany)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ MANY-TO-MANY RELATIONSHIP");
                    sb.AppendLine("   Can significantly impact performance:");
                    sb.AppendLine("   → Results in larger intermediate tables");
                    sb.AppendLine("   → May cause unexpected aggregations");
                    sb.AppendLine("   → Consider bridge tables if possible");
                }
                
                if (relationship.IsBidirectional)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠ BIDIRECTIONAL CROSS-FILTER");
                    sb.AppendLine("   Filters flow both directions:");
                    sb.AppendLine("   → Can cause ambiguous paths");
                    sb.AppendLine("   → May impact performance on large models");
                    sb.AppendLine("   → Review if bidirectional is necessary");
                }
                sb.AppendLine();
            }
            
            // Table comparison
            var fromTable = Tables.FirstOrDefault(t => t.TableName == relationship.FromTable);
            var toTable = Tables.FirstOrDefault(t => t.TableName == relationship.ToTable);
            
            if (fromTable != null || toTable != null)
            {
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine("📊 TABLE COMPARISON");
                sb.AppendLine("───────────────────────────────────────────");
                sb.AppendLine();
                sb.AppendLine($"{"Metric",-18} {"From",-12} {"To",-12}");
                sb.AppendLine($"{"───────",-18} {"────",-12} {"──",-12}");
                
                var fromHits = fromTable?.HitCount.ToString() ?? "N/A";
                var toHits = toTable?.HitCount.ToString() ?? "N/A";
                sb.AppendLine($"{"Hits",-18} {fromHits,-12} {toHits,-12}");
                
                var fromRows = fromTable?.TotalRowsFormatted ?? "N/A";
                var toRows = toTable?.TotalRowsFormatted ?? "N/A";
                sb.AppendLine($"{"Rows",-18} {fromRows,-12} {toRows,-12}");
                
                var fromDuration = fromTable?.TotalDurationFormatted ?? "N/A";
                var toDuration = toTable?.TotalDurationFormatted ?? "N/A";
                sb.AppendLine($"{"Duration",-18} {fromDuration,-12} {toDuration,-12}");
                
                var fromCpu = fromTable?.CpuPercentageFormatted ?? "N/A";
                var toCpu = toTable?.CpuPercentageFormatted ?? "N/A";
                sb.AppendLine($"{"CPU %",-18} {fromCpu,-12} {toCpu,-12}");
                
                var fromCache = fromTable?.CacheHitRateFormatted ?? "N/A";
                var toCache = toTable?.CacheHitRateFormatted ?? "N/A";
                sb.AppendLine($"{"Cache Rate",-18} {fromCache,-12} {toCache,-12}");
            }
            
            SelectedDetailInfo = sb.ToString();
        }

        /// <summary>
        /// Clears the detail selection.
        /// </summary>
        public void ClearDetailSelection()
        {
            SelectedDetailInfo = null;
        }

        private void ClearSelectionState()
        {
            foreach (var t in Tables)
            {
                t.IsSelected = false;
                t.IsHighlighted = false;
                t.IsDimmed = false;
            }

            foreach (var rel in Relationships)
            {
                rel.IsHighlighted = false;
                rel.IsDimmed = false;
            }
        }

        /// <summary>
        /// Called when a table is dragged to a new position.
        /// Updates all relationship lines connected to the table.
        /// </summary>
        public void OnTablePositionChanged(ErdTableViewModel table)
        {
            // Update all relationships connected to this table
            foreach (var rel in Relationships)
            {
                if (rel.FromTableViewModel == table || rel.ToTableViewModel == table)
                {
                    rel.UpdatePath();
                }
            }

            // Expand canvas if table is dragged beyond current bounds
            double requiredWidth = table.X + table.Width + 50;  // 50px padding
            double requiredHeight = table.Y + 200 + 50;  // Approximate table height + padding

            if (requiredWidth > CanvasWidth)
            {
                CanvasWidth = requiredWidth;
            }
            if (requiredHeight > CanvasHeight)
            {
                CanvasHeight = requiredHeight;
            }
        }

        #endregion

        #region Heat Map

        /// <summary>
        /// Calculates heat levels for all tables based on the selected heat map mode.
        /// </summary>
        private void CalculateHeatLevels()
        {
            if (Tables.Count == 0) return;

            switch (_heatMapMode)
            {
                case SEDependenciesHeatMapMode.CpuTime:
                    {
                        // Calculate heat based on CPU time
                        long maxCpu = Tables.Max(t => t.TotalCpuTimeMs);
                        long minCpu = Tables.Min(t => t.TotalCpuTimeMs);
                        long range = maxCpu - minCpu;

                        foreach (var table in Tables)
                        {
                            table.HeatLevel = range > 0
                                ? (double)(table.TotalCpuTimeMs - minCpu) / range
                                : 0.5;
                        }
                        break;
                    }
                case SEDependenciesHeatMapMode.RowCount:
                    {
                        // Calculate heat based on total rows scanned
                        long maxRows = Tables.Max(t => t.TotalEstimatedRows);
                        long minRows = Tables.Min(t => t.TotalEstimatedRows);
                        long range = maxRows - minRows;

                        foreach (var table in Tables)
                        {
                            table.HeatLevel = range > 0
                                ? (double)(table.TotalEstimatedRows - minRows) / range
                                : 0.5;
                        }
                        break;
                    }
                default: // HitCount
                    {
                        // Calculate heat based on hit count
                        int maxHitCount = Tables.Max(t => t.HitCount);
                        int minHitCount = Tables.Min(t => t.HitCount);
                        int range = maxHitCount - minHitCount;

                        foreach (var table in Tables)
                        {
                            table.HeatLevel = range > 0
                                ? (double)(table.HitCount - minHitCount) / range
                                : 0.5;
                        }
                        break;
                    }
            }
        }

        #endregion

        #region Table Dragging

        private ErdTableViewModel _draggingTable;
        private double _dragStartX;
        private double _dragStartY;
        private double _tableStartX;
        private double _tableStartY;

        /// <summary>
        /// Starts dragging a table.
        /// </summary>
        public void StartTableDrag(ErdTableViewModel table, double mouseX, double mouseY)
        {
            _draggingTable = table;
            _dragStartX = mouseX;
            _dragStartY = mouseY;
            _tableStartX = table.X;
            _tableStartY = table.Y;
            table.IsDragging = true;
        }

        /// <summary>
        /// Updates the position of the dragged table.
        /// </summary>
        public void UpdateTableDrag(double mouseX, double mouseY)
        {
            if (_draggingTable == null) return;

            double deltaX = mouseX - _dragStartX;
            double deltaY = mouseY - _dragStartY;

            _draggingTable.X = Math.Max(0, _tableStartX + deltaX);
            _draggingTable.Y = Math.Max(0, _tableStartY + deltaY);

            // Update relationship lines
            UpdateRelationshipsForTable(_draggingTable);

            // Update canvas size if needed
            UpdateCanvasSize();
        }

        /// <summary>
        /// Ends dragging a table.
        /// </summary>
        public void EndTableDrag()
        {
            if (_draggingTable != null)
            {
                _draggingTable.IsDragging = false;
                _draggingTable = null;
            }
        }

        /// <summary>
        /// Updates all relationships connected to a specific table.
        /// </summary>
        private void UpdateRelationshipsForTable(ErdTableViewModel table)
        {
            foreach (var rel in Relationships)
            {
                if (rel.FromTableViewModel == table || rel.ToTableViewModel == table)
                {
                    rel.UpdatePath();
                }
            }
        }

        /// <summary>
        /// Updates the canvas size to fit all tables.
        /// </summary>
        private void UpdateCanvasSize()
        {
            if (Tables.Count == 0) return;

            double maxX = Tables.Max(t => t.X + t.Width) + 50;
            double maxY = Tables.Max(t => t.Y + t.Height) + 50;

            CanvasWidth = Math.Max(800, maxX);
            CanvasHeight = Math.Max(600, maxY);
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for a table in the ERD diagram.
    /// </summary>
    public class ErdTableViewModel : PropertyChangedBase
    {
        private readonly XmSqlTableInfo _tableInfo;
        private bool _sortKeyColumnsFirst = true;

        public ErdTableViewModel(XmSqlTableInfo tableInfo, bool sortKeyColumnsFirst = true)
        {
            _tableInfo = tableInfo;
            _sortKeyColumnsFirst = sortKeyColumnsFirst;
            Columns = new BindableCollection<ErdColumnViewModel>(GetSortedColumns());
        }

        /// <summary>
        /// Gets columns sorted based on the current sort setting.
        /// </summary>
        private IEnumerable<ErdColumnViewModel> GetSortedColumns()
        {
            var filtered = _tableInfo.Columns.Values
                .Where(c => !IsInternalColumn(c.ColumnName));

            if (_sortKeyColumnsFirst)
            {
                return filtered
                    .OrderByDescending(c => c.UsageTypes.HasFlag(XmSqlColumnUsage.Join))
                    .ThenBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new ErdColumnViewModel(c));
            }
            else
            {
                return filtered
                    .OrderBy(c => c.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new ErdColumnViewModel(c));
            }
        }

        /// <summary>
        /// Updates the column sort order.
        /// </summary>
        public void UpdateColumnSort(bool sortKeyColumnsFirst)
        {
            _sortKeyColumnsFirst = sortKeyColumnsFirst;
            Columns.Clear();
            Columns.AddRange(GetSortedColumns());
            NotifyOfPropertyChange(nameof(KeyColumns));
        }

        /// <summary>
        /// Determines if a column is an internal/measure column that should be hidden.
        /// </summary>
        private static bool IsInternalColumn(string columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return true;
            
            // Filter out $Measure0, $Measure1, etc.
            if (columnName.StartsWith("$Measure", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Filter out $Expr columns
            if (columnName.StartsWith("$Expr", StringComparison.OrdinalIgnoreCase))
                return true;
            
            return false;
        }

        public string TableName => _tableInfo.TableName;
        public int HitCount => _tableInfo.HitCount;
        public bool IsFromTable => _tableInfo.IsFromTable;
        public bool IsJoinedTable => _tableInfo.IsJoinedTable;
        
        /// <summary>
        /// Whether this table is accessed via DirectQuery (vs. VertiPaq).
        /// </summary>
        public bool IsDirectQuery => _tableInfo.IsDirectQuery;
        
        /// <summary>
        /// The DirectQuery source system.
        /// </summary>
        public string DirectQuerySource => _tableInfo.DirectQuerySource;
        
        /// <summary>
        /// Whether this table has columns with DAX callbacks.
        /// </summary>
        public bool HasCallbacks => _tableInfo.HasCallbacks;
        
        /// <summary>
        /// Number of columns with callbacks.
        /// </summary>
        public int CallbackColumnCount => Columns.Count(c => c.HasCallback);
        
        /// <summary>
        /// Total estimated rows scanned for this table across all SE queries.
        /// </summary>
        public long TotalEstimatedRows => _tableInfo.TotalEstimatedRows;
        
        /// <summary>
        /// Maximum rows returned by a single SE query for this table.
        /// </summary>
        public long MaxEstimatedRows => _tableInfo.MaxEstimatedRows;
        
        /// <summary>
        /// Formatted string for total estimated rows (e.g., "1.2M", "500K").
        /// </summary>
        public string TotalRowsFormatted => FormatRowCount(TotalEstimatedRows);
        
        /// <summary>
        /// Whether this table has row count data to display.
        /// </summary>
        public bool HasRowCountData => TotalEstimatedRows > 0;
        
        /// <summary>
        /// Whether this table has a high row count (100k+) which may indicate performance concerns.
        /// </summary>
        public bool HasHighRowCount => MaxEstimatedRows >= 100000;
        
        /// <summary>
        /// Total duration (ms) of all SE queries for this table.
        /// </summary>
        public long TotalDurationMs => _tableInfo.TotalDurationMs;
        
        /// <summary>
        /// Maximum duration (ms) of a single SE query for this table.
        /// </summary>
        public long MaxDurationMs => _tableInfo.MaxDurationMs;
        
        /// <summary>
        /// Formatted string for total duration.
        /// </summary>
        public string TotalDurationFormatted => FormatDuration(TotalDurationMs);
        
        /// <summary>
        /// Whether this table has duration data.
        /// </summary>
        public bool HasDurationData => TotalDurationMs > 0;
        
        /// <summary>
        /// Whether this table has high duration (>100ms total).
        /// </summary>
        public bool HasHighDuration => TotalDurationMs >= 100;

        /// <summary>
        /// Formatted string for total CPU time.
        /// </summary>
        public string TotalCpuFormatted => FormatDuration(TotalCpuTimeMs);
        
        /// <summary>
        /// Whether this table has CPU time data.
        /// </summary>
        public bool HasCpuData => TotalCpuTimeMs > 0;
        
        /// <summary>
        /// Whether this table has high CPU time (>100ms total).
        /// </summary>
        public bool HasHighCpu => TotalCpuTimeMs >= 100;

        #region Cache Hit/Miss Tracking
        
        /// <summary>
        /// Number of cache hits for queries on this table.
        /// </summary>
        public int CacheHits => _tableInfo.CacheHits;
        
        /// <summary>
        /// Number of cache misses for queries on this table.
        /// </summary>
        public int CacheMisses => _tableInfo.CacheMisses;
        
        /// <summary>
        /// Total queries (cache hits + misses) for this table.
        /// </summary>
        public int TotalCacheQueries => CacheHits + CacheMisses;
        
        /// <summary>
        /// Cache hit rate as a percentage (0-100).
        /// </summary>
        public double CacheHitRate => TotalCacheQueries > 0 ? (double)CacheHits / TotalCacheQueries * 100 : 0;
        
        /// <summary>
        /// Formatted cache hit rate string.
        /// </summary>
        public string CacheHitRateFormatted => $"{CacheHitRate:0}%";
        
        /// <summary>
        /// Whether this table has cache data to display.
        /// </summary>
        public bool HasCacheData => TotalCacheQueries > 0;
        
        /// <summary>
        /// Whether cache hit rate is good (>= 50%).
        /// </summary>
        public bool HasGoodCacheRate => CacheHitRate >= 50;
        
        /// <summary>
        /// Whether cache hit rate is poor (< 20%).
        /// </summary>
        public bool HasPoorCacheRate => CacheHitRate < 20 && TotalCacheQueries > 0;
        
        /// <summary>
        /// Cache indicator tooltip text.
        /// </summary>
        public string CacheTooltip => $"Cache: {CacheHits} hits, {CacheMisses} misses ({CacheHitRateFormatted} hit rate)";
        
        #endregion

        #region Query Count Tracking
        
        /// <summary>
        /// Number of distinct SE queries that accessed this table.
        /// </summary>
        public int QueryCount => _tableInfo.QueryCount;
        
        /// <summary>
        /// Whether this table has query count data.
        /// </summary>
        public bool HasQueryCountData => QueryCount > 0;
        
        /// <summary>
        /// Query count tooltip.
        /// </summary>
        public string QueryCountTooltip => $"{QueryCount} SE queries";
        
        #endregion

        #region Parallelism Tracking
        
        /// <summary>
        /// Total CPU time (ms) across all queries for this table.
        /// </summary>
        public long TotalCpuTimeMs => _tableInfo.TotalCpuTimeMs;
        
        /// <summary>
        /// Total parallel duration saved (ms) from parallel execution.
        /// </summary>
        public long TotalParallelDurationMs => _tableInfo.TotalParallelDurationMs;
        
        /// <summary>
        /// Maximum CPU factor observed for this table (higher = more parallelism).
        /// </summary>
        public double MaxCpuFactor => _tableInfo.MaxCpuFactor;
        
        /// <summary>
        /// Number of queries that ran in parallel for this table.
        /// </summary>
        public int ParallelQueryCount => _tableInfo.ParallelQueryCount;
        
        /// <summary>
        /// Whether this table had queries that ran in parallel.
        /// </summary>
        public bool HasParallelData => ParallelQueryCount > 0;
        
        /// <summary>
        /// Whether this table has high parallelism (CpuFactor >= 2).
        /// </summary>
        public bool HasHighParallelism => MaxCpuFactor >= 2.0;
        
        /// <summary>
        /// Formatted CPU factor for display.
        /// </summary>
        public string CpuFactorFormatted => MaxCpuFactor > 0 ? $"{MaxCpuFactor:0.0}x" : "";
        
        /// <summary>
        /// Parallelism indicator tooltip.
        /// </summary>
        public string ParallelismTooltip => HasParallelData 
            ? $"Parallelism: {ParallelQueryCount} parallel queries, max {MaxCpuFactor:0.0}x CPU factor, {FormatDuration(TotalParallelDurationMs)} saved"
            : "No parallel execution detected";
        
        #endregion

        #region CPU Hotspot Tracking
        
        private double _cpuPercentage;
        /// <summary>
        /// Percentage of total CPU time consumed by this table (0-100).
        /// </summary>
        public double CpuPercentage
        {
            get => _cpuPercentage;
            set { _cpuPercentage = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsCpuHotspot)); NotifyOfPropertyChange(nameof(CpuPercentageFormatted)); NotifyOfPropertyChange(nameof(CpuHotspotTooltip)); }
        }
        
        /// <summary>
        /// Whether this table is a CPU hotspot (consumes >= 50% of total CPU).
        /// </summary>
        public bool IsCpuHotspot => CpuPercentage >= 50.0;
        
        /// <summary>
        /// Whether this table has meaningful CPU data (>= 5% of total).
        /// </summary>
        public bool HasSignificantCpu => CpuPercentage >= 5.0;
        
        /// <summary>
        /// Formatted CPU percentage for display.
        /// </summary>
        public string CpuPercentageFormatted => CpuPercentage > 0 ? $"{CpuPercentage:0}%" : "";
        
        /// <summary>
        /// Tooltip for CPU hotspot badge.
        /// </summary>
        public string CpuHotspotTooltip => TotalCpuTimeMs > 0 
            ? $"CPU: {FormatDuration(TotalCpuTimeMs)} ({CpuPercentage:0.0}% of total)"
            : "No CPU data";
        
        #endregion

        private bool _isBottleneck;
        /// <summary>
        /// Whether this table is identified as a performance bottleneck.
        /// </summary>
        public bool IsBottleneck
        {
            get => _isBottleneck;
            set { _isBottleneck = value; NotifyOfPropertyChange(); }
        }

        private int _bottleneckRank;
        /// <summary>
        /// The rank of this bottleneck (1 = slowest, 2 = second slowest, etc.). 0 if not a bottleneck.
        /// </summary>
        public int BottleneckRank
        {
            get => _bottleneckRank;
            set { _bottleneckRank = value; NotifyOfPropertyChange(); }
        }
        
        private bool _isCollapsed;
        /// <summary>
        /// Whether this table's columns are collapsed (header only).
        /// </summary>
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(IsExpanded));
                    // Update Height when collapse state changes
                    NotifyOfPropertyChange(nameof(Height));
                    NotifyOfPropertyChange(nameof(CenterY));
                    NotifyOfPropertyChange(nameof(RightEdgeY));
                    NotifyOfPropertyChange(nameof(LeftEdgeY));
                    NotifyOfPropertyChange(nameof(BottomEdgeY));
                }
            }
        }
        
        /// <summary>
        /// Inverse of IsCollapsed for binding.
        /// </summary>
        public bool IsExpanded => !_isCollapsed;
        
        /// <summary>
        /// Toggles the collapsed state.
        /// </summary>
        public void ToggleCollapse()
        {
            IsCollapsed = !IsCollapsed;
        }
        
        /// <summary>
        /// Formats a row count with K/M/B suffixes for compact display.
        /// </summary>
        private static string FormatRowCount(long rows)
        {
            if (rows >= 1_000_000_000)
                return $"{rows / 1_000_000_000.0:0.#}B";
            if (rows >= 1_000_000)
                return $"{rows / 1_000_000.0:0.#}M";
            if (rows >= 1_000)
                return $"{rows / 1_000.0:0.#}K";
            return rows.ToString();
        }
        
        /// <summary>
        /// Formats duration in ms with appropriate suffix.
        /// </summary>
        private static string FormatDuration(long ms)
        {
            if (ms >= 60000)
                return $"{ms / 60000.0:0.#}m";
            if (ms >= 1000)
                return $"{ms / 1000.0:0.#}s";
            return $"{ms}ms";
        }

        public BindableCollection<ErdColumnViewModel> Columns { get; }

        /// <summary>
        /// Key/join columns (shown when table is collapsed).
        /// </summary>
        public IEnumerable<ErdColumnViewModel> KeyColumns => Columns.Where(c => c.IsJoinColumn);

        /// <summary>
        /// Whether this table has any key/join columns.
        /// </summary>
        public bool HasKeyColumns => Columns.Any(c => c.IsJoinColumn);

        private const double HeaderHeight = 60; // Approximate header height with stats row
        private const double KeyColumnRowHeight = 18; // Height per key column row

        /// <summary>
        /// Calculates the collapsed height based on header + key columns.
        /// </summary>
        public double CollapsedHeight
        {
            get
            {
                int keyCount = Columns.Count(c => c.IsJoinColumn);
                if (keyCount == 0) return HeaderHeight;
                return HeaderHeight + 6 + (keyCount * KeyColumnRowHeight); // 6 = padding
            }
        }

        #region Search Highlighting
        
        private bool _isSearchMatch = true;
        /// <summary>
        /// Whether this table matches the current search query.
        /// </summary>
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set { _isSearchMatch = value; NotifyOfPropertyChange(); }
        }
        
        private bool _isSearchDimmed;
        /// <summary>
        /// Whether this table should be dimmed (doesn't match search).
        /// </summary>
        public bool IsSearchDimmed
        {
            get => _isSearchDimmed;
            set { _isSearchDimmed = value; NotifyOfPropertyChange(); }
        }
        
        #endregion

        #region Heat Map

        private double _heatLevel;
        /// <summary>
        /// Heat level from 0.0 (cold) to 1.0 (hot) based on relative hit count.
        /// </summary>
        public double HeatLevel
        {
            get => _heatLevel;
            set 
            { 
                _heatLevel = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HeatColor));
                NotifyOfPropertyChange(nameof(HeatLevelText));
                NotifyOfPropertyChange(nameof(HeatLevelPercent));
                NotifyOfPropertyChange(nameof(HeatPatternOpacity));
                NotifyOfPropertyChange(nameof(HeatAccessibilityIcon));
            }
        }

        /// <summary>
        /// Gets a text label for the heat level (for accessibility).
        /// </summary>
        public string HeatLevelText
        {
            get
            {
                if (_heatLevel >= 0.8) return "Hot";
                if (_heatLevel >= 0.6) return "Warm";
                if (_heatLevel >= 0.4) return "Med";
                if (_heatLevel >= 0.2) return "Cool";
                return "Low";
            }
        }

        /// <summary>
        /// Gets the heat level as a percentage string (for accessibility).
        /// </summary>
        public string HeatLevelPercent => $"{(_heatLevel * 100):0}%";

        /// <summary>
        /// Gets an accessibility indicator based on heat level.
        /// Shows ascending bars to represent intensity - like a signal strength indicator.
        /// </summary>
        public string HeatAccessibilityIcon
        {
            get
            {
                // Using ascending bar indicator like signal strength
                if (_heatLevel >= 0.8) return "▁▃▅▇"; // Hot - full signal
                if (_heatLevel >= 0.6) return "▁▃▅"; // Warm - 3 bars
                if (_heatLevel >= 0.4) return "▁▃"; // Medium - 2 bars
                if (_heatLevel >= 0.2) return "▁"; // Cool - 1 bar
                return ""; // Low - no indicator
            }
        }

        /// <summary>
        /// Gets pattern opacity for visual differentiation (for accessibility).
        /// Higher heat = more visible diagonal stripes pattern.
        /// </summary>
        public double HeatPatternOpacity => _heatLevel * 0.3; // Max 30% opacity for stripes

        /// <summary>
        /// Gets the heat color brush based on heat level.
        /// Uses green (cool) to yellow to orange to red (hot) gradient matching the legend.
        /// </summary>
        public System.Windows.Media.SolidColorBrush HeatColor
        {
            get
            {
                // Use green (120°) -> yellow (60°) -> orange (30°) -> red (0°)
                // This matches the legend gradient shown in the status bar
                double hue;
                if (_heatLevel <= 0.5)
                {
                    // Green (120) to Yellow (60) for first half
                    hue = 120 - (_heatLevel * 2 * 60); // 120 -> 60
                }
                else
                {
                    // Yellow (60) to Red (0) for second half
                    hue = 60 - ((_heatLevel - 0.5) * 2 * 60); // 60 -> 0
                }
                var color = HslToRgb(hue, 0.7, 0.45);
                return new System.Windows.Media.SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Converts HSL color values to RGB Color.
        /// </summary>
        private static System.Windows.Media.Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360.0);
                b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
            }

            return System.Windows.Media.Color.FromRgb(
                (byte)(r * 255),
                (byte)(g * 255),
                (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        #endregion

        #region Selection and Highlighting

        private bool _isSelected;
        /// <summary>
        /// Whether this table is currently selected (clicked).
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(); }
        }

        private bool _isHighlighted;
        /// <summary>
        /// Whether this table is highlighted (related to selected table).
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; NotifyOfPropertyChange(); }
        }

        private bool _isDimmed;
        /// <summary>
        /// Whether this table is dimmed (not related to selected table).
        /// </summary>
        public bool IsDimmed
        {
            get => _isDimmed;
            set { _isDimmed = value; NotifyOfPropertyChange(); }
        }

        #endregion

        #region Query Plan Integration

        private bool _isQueryFiltered;
        /// <summary>
        /// Whether this table is filtered out (not accessed by selected query).
        /// </summary>
        public bool IsQueryFiltered
        {
            get => _isQueryFiltered;
            set { _isQueryFiltered = value; NotifyOfPropertyChange(); }
        }

        private bool _isQueryHighlighted;
        /// <summary>
        /// Whether this table is highlighted because it was accessed by the selected query.
        /// </summary>
        public bool IsQueryHighlighted
        {
            get => _isQueryHighlighted;
            set { _isQueryHighlighted = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Gets the underlying table info (for accessing QueryIds).
        /// </summary>
        public XmSqlTableInfo TableInfo => _tableInfo;

        /// <summary>
        /// Formatted list of query IDs that accessed this table.
        /// </summary>
        public string QueryIdsFormatted
        {
            get
            {
                if (_tableInfo.QueryIds.Count == 0)
                    return "";
                if (_tableInfo.QueryIds.Count <= 5)
                    return string.Join(", ", _tableInfo.QueryIds.OrderBy(x => x).Select(x => $"#{x}"));
                return $"#{_tableInfo.QueryIds.Min()}...#{_tableInfo.QueryIds.Max()} ({_tableInfo.QueryIds.Count} queries)";
            }
        }

        /// <summary>
        /// Whether this table has query ID data to display.
        /// </summary>
        public bool HasQueryIds => _tableInfo.QueryIds.Count > 0;

        #endregion

        #region Dragging

        private bool _isDragging;
        /// <summary>
        /// Whether the table is currently being dragged.
        /// </summary>
        public bool IsDragging
        {
            get => _isDragging;
            set { _isDragging = value; NotifyOfPropertyChange(); }
        }

        #endregion

        #region Position on canvas

        private double _x;
        public double X
        {
            get => _x;
            set { _x = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CenterX)); }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set { _y = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CenterY)); }
        }

        private double _width = 220;
        public double Width
        {
            get => _width;
            set { _width = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CenterX)); }
        }

        /// <summary>
        /// Calculates the header height based on which info rows are visible.
        /// </summary>
        private double CalculateHeaderHeight()
        {
            // Base: title bar (~30) + resize grip (~8) + padding (~5) = ~43px
            double height = 43;
            
            // Row 1: Row count info (visible when HasRowCountData)
            if (HasRowCountData) height += 20;
            
            // Row 2: Duration-only row (visible when no row count but has duration)
            else if (HasDurationData) height += 20;
            
            // Row 3: SE Event Data row (cache, parallel, CPU)
            if (HasCacheData || HasParallelData || HasSignificantCpu) height += 20;
            
            return height;
        }

        /// <summary>
        /// Current height of the table - calculated based on content for relationship line positioning.
        /// Height varies based on which info rows are visible.
        /// </summary>
        public double Height
        {
            get
            {
                if (IsCollapsed) return CollapsedHeight;
                // Dynamic header height + columns area
                return CalculateHeaderHeight() + _columnsHeight;
            }
        }

        private double _columnsHeight = 105; // Default columns area height
        /// <summary>
        /// Height of the columns area (resizable by user).
        /// </summary>
        public double ColumnsHeight
        {
            get => _columnsHeight;
            set 
            { 
                // Clamp between min and max
                var newValue = Math.Max(40, Math.Min(400, value));
                if (_columnsHeight != newValue)
                {
                    _columnsHeight = newValue;
                    NotifyOfPropertyChange();
                    // Notify height-related properties for relationship line calculations
                    NotifyOfPropertyChange(nameof(Height));
                    NotifyOfPropertyChange(nameof(CenterY));
                    NotifyOfPropertyChange(nameof(RightEdgeY));
                    NotifyOfPropertyChange(nameof(LeftEdgeY));
                    NotifyOfPropertyChange(nameof(BottomEdgeY));
                }
            }
        }

        // Center point for drawing relationship lines
        public double CenterX => X + Width / 2;
        public double CenterY => Y + Height / 2;

        // Right edge for outgoing relationships
        public double RightEdgeX => X + Width;
        public double RightEdgeY => Y + Height / 2;

        // Left edge for incoming relationships
        public double LeftEdgeX => X;
        public double LeftEdgeY => Y + Height / 2;

        // Top edge for relationships
        public double TopEdgeX => X + Width / 2;
        public double TopEdgeY => Y;

        // Bottom edge for relationships
        public double BottomEdgeX => X + Width / 2;
        public double BottomEdgeY => Y + Height;

        #endregion

        #region Mini-map Properties

        // Mini-map scale factor (set by parent ViewModel)
        private double _miniMapScaleX = 0.1;
        private double _miniMapScaleY = 0.1;

        /// <summary>
        /// Updates the mini-map scale factors.
        /// </summary>
        public void SetMiniMapScale(double scaleX, double scaleY)
        {
            _miniMapScaleX = scaleX;
            _miniMapScaleY = scaleY;
            NotifyOfPropertyChange(nameof(MiniMapX));
            NotifyOfPropertyChange(nameof(MiniMapY));
            NotifyOfPropertyChange(nameof(MiniMapWidth));
            NotifyOfPropertyChange(nameof(MiniMapHeight));
        }

        /// <summary>
        /// X position on mini-map.
        /// </summary>
        public double MiniMapX => X * _miniMapScaleX;

        /// <summary>
        /// Y position on mini-map.
        /// </summary>
        public double MiniMapY => Y * _miniMapScaleY;

        /// <summary>
        /// Width on mini-map.
        /// </summary>
        public double MiniMapWidth => Math.Max(4, Width * _miniMapScaleX);

        /// <summary>
        /// Height on mini-map.
        /// </summary>
        public double MiniMapHeight => Math.Max(3, Height * _miniMapScaleY);

        #endregion
    }

    /// <summary>
    /// ViewModel for a column in the ERD diagram.
    /// </summary>
    public class ErdColumnViewModel : PropertyChangedBase
    {
        private readonly XmSqlColumnInfo _columnInfo;

        public ErdColumnViewModel(XmSqlColumnInfo columnInfo)
        {
            _columnInfo = columnInfo;
        }

        public string ColumnName => _columnInfo.ColumnName;
        public int HitCount => _columnInfo.HitCount;

        public bool IsJoinColumn => _columnInfo.UsageTypes.HasFlag(XmSqlColumnUsage.Join);
        public bool IsFilterColumn => _columnInfo.UsageTypes.HasFlag(XmSqlColumnUsage.Filter);
        public bool IsSelectColumn => _columnInfo.UsageTypes.HasFlag(XmSqlColumnUsage.Select);
        public bool IsAggregateColumn => _columnInfo.UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate);

        /// <summary>
        /// Whether this column has a DAX callback.
        /// </summary>
        public bool HasCallback => _columnInfo.HasCallback;

        /// <summary>
        /// The type of callback (CallbackDataID, EncodeCallback, etc.)
        /// </summary>
        public string CallbackType => _columnInfo.CallbackType;

        public string AggregationText => _columnInfo.AggregationTypes.Count > 0
            ? string.Join(", ", _columnInfo.AggregationTypes)
            : string.Empty;

        /// <summary>
        /// Filter values applied to this column.
        /// </summary>
        public IReadOnlyList<string> FilterValues => _columnInfo.FilterValues;

        /// <summary>
        /// Whether this column has filter values.
        /// </summary>
        public bool HasFilterValues => _columnInfo.FilterValues.Count > 0;

        /// <summary>
        /// Filter operators used on this column.
        /// </summary>
        public IEnumerable<string> FilterOperators => _columnInfo.FilterOperators;

        /// <summary>
        /// Formatted filter values for display (first few values).
        /// </summary>
        public string FilterValuesPreview
        {
            get
            {
                if (!HasFilterValues) return string.Empty;
                var values = _columnInfo.FilterValues.Take(3).ToList();
                var preview = string.Join(", ", values);
                if (_columnInfo.FilterValues.Count > 3)
                    preview += $", ... (+{_columnInfo.FilterValues.Count - 3} more)";
                return preview;
            }
        }

        /// <summary>
        /// All filter values formatted for detail panel display.
        /// </summary>
        public string FilterValuesFormatted
        {
            get
            {
                if (!HasFilterValues) return string.Empty;
                var ops = _columnInfo.FilterOperators.Any() 
                    ? string.Join("/", _columnInfo.FilterOperators) 
                    : "=";
                return $"[{ops}] {string.Join(", ", _columnInfo.FilterValues)}";
            }
        }

        /// <summary>
        /// Tooltip text explaining the column usage icons.
        /// </summary>
        public string UsageTooltip
        {
            get
            {
                var tips = new List<string>();
                if (IsJoinColumn) tips.Add("🔑 Join Key");
                if (IsFilterColumn) tips.Add("🔍 Filter");
                if (IsAggregateColumn) tips.Add("📊 Aggregate (" + AggregationText + ")");
                if (IsSelectColumn) tips.Add("✓ Selected");
                if (HasCallback) tips.Add("⚡ Callback (" + CallbackType + ")");
                return tips.Count > 0 ? string.Join("\n", tips) : "No usage detected";
            }
        }
        
        private bool _isSearchMatch = true;
        /// <summary>
        /// Whether this column matches the current search query.
        /// </summary>
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set { _isSearchMatch = value; NotifyOfPropertyChange(); }
        }
    }

    /// <summary>
    /// ViewModel for a relationship line in the ERD diagram.
    /// </summary>
    public class ErdRelationshipViewModel : PropertyChangedBase
    {
        private readonly XmSqlRelationship _relationship;
        private readonly ErdTableViewModel _fromTable;
        private readonly ErdTableViewModel _toTable;

        public ErdRelationshipViewModel(XmSqlRelationship relationship, ErdTableViewModel fromTable, ErdTableViewModel toTable)
        {
            _relationship = relationship;
            _fromTable = fromTable;
            _toTable = toTable;
        }

        public ErdTableViewModel FromTableViewModel => _fromTable;
        public ErdTableViewModel ToTableViewModel => _toTable;

        public string FromTable => _relationship.FromTable;
        public string FromColumn => _relationship.FromColumn;
        public string ToTable => _relationship.ToTable;
        public string ToColumn => _relationship.ToColumn;
        public XmSqlJoinType JoinType => _relationship.JoinType;
        public int HitCount => _relationship.HitCount;
        public XmSqlCardinality Cardinality => _relationship.Cardinality;
        public XmSqlCrossFilterDirection CrossFilterDirection => _relationship.CrossFilterDirection;

        /// <summary>
        /// Whether this relationship is many-to-many.
        /// </summary>
        public bool IsManyToMany => Cardinality == XmSqlCardinality.ManyToMany;

        /// <summary>
        /// Whether this relationship has bi-directional cross-filtering.
        /// </summary>
        public bool IsBidirectional => CrossFilterDirection == XmSqlCrossFilterDirection.Both;

        /// <summary>
        /// Text representation of cardinality for display.
        /// </summary>
        public string CardinalityText => Cardinality switch
        {
            XmSqlCardinality.OneToOne => "1:1",
            XmSqlCardinality.OneToMany => "1:*",
            XmSqlCardinality.ManyToOne => "*:1",
            XmSqlCardinality.ManyToMany => "*:*",
            _ => ""
        };

        /// <summary>
        /// Symbol for "one" side of relationship.
        /// </summary>
        public string FromCardinalitySymbol => Cardinality switch
        {
            XmSqlCardinality.OneToOne => "1",
            XmSqlCardinality.OneToMany => "1",
            XmSqlCardinality.ManyToOne => "*",
            XmSqlCardinality.ManyToMany => "*",
            _ => ""
        };

        /// <summary>
        /// Symbol for "many" side of relationship.
        /// </summary>
        public string ToCardinalitySymbol => Cardinality switch
        {
            XmSqlCardinality.OneToOne => "1",
            XmSqlCardinality.OneToMany => "*",
            XmSqlCardinality.ManyToOne => "1",
            XmSqlCardinality.ManyToMany => "*",
            _ => ""
        };

        public string JoinTypeText => JoinType switch
        {
            XmSqlJoinType.LeftOuterJoin => "LEFT OUTER",
            XmSqlJoinType.InnerJoin => "INNER",
            XmSqlJoinType.RightOuterJoin => "RIGHT OUTER",
            XmSqlJoinType.FullOuterJoin => "FULL OUTER",
            _ => ""
        };

        #region Highlighting

        private bool _isHighlighted;
        /// <summary>
        /// Whether this relationship is highlighted (connected to selected table).
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; NotifyOfPropertyChange(); }
        }

        private bool _isDimmed;
        /// <summary>
        /// Whether this relationship is dimmed (not connected to selected table).
        /// </summary>
        public bool IsDimmed
        {
            get => _isDimmed;
            set { _isDimmed = value; NotifyOfPropertyChange(); }
        }

        private bool _isQueryFiltered;
        /// <summary>
        /// Whether this relationship is filtered out by query filter.
        /// </summary>
        public bool IsQueryFiltered
        {
            get => _isQueryFiltered;
            set { _isQueryFiltered = value; NotifyOfPropertyChange(); }
        }

        #endregion

        // Line path coordinates
        private double _startX;
        public double StartX
        {
            get => _startX;
            set 
            { 
                _startX = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(PathData)); 
                NotifyOfPropertyChange(nameof(LabelX));
            }
        }

        private double _startY;
        public double StartY
        {
            get => _startY;
            set 
            { 
                _startY = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(PathData)); 
                NotifyOfPropertyChange(nameof(LabelY));
            }
        }

        private double _endX;
        public double EndX
        {
            get => _endX;
            set 
            { 
                _endX = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(PathData)); 
                NotifyOfPropertyChange(nameof(LabelX));
            }
        }

        private double _endY;
        public double EndY
        {
            get => _endY;
            set 
            { 
                _endY = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(PathData)); 
                NotifyOfPropertyChange(nameof(LabelY));
            }
        }

        /// <summary>
        /// Enum for edge selection.
        /// </summary>
        private enum EdgeType { Left, Right, Top, Bottom }

        /// <summary>
        /// The edge type used for the start of the relationship line.
        /// </summary>
        private EdgeType _startEdge;

        /// <summary>
        /// The edge type used for the end of the relationship line.
        /// </summary>
        private EdgeType _endEdge;

        /// <summary>
        /// Path data for drawing the relationship line (bezier curve).
        /// Adapts to horizontal or vertical orientation based on edge types.
        /// </summary>
        public string PathData
        {
            get
            {
                // Determine if this is a horizontal or vertical relationship
                bool isVertical = (_startEdge == EdgeType.Top || _startEdge == EdgeType.Bottom);
                
                if (isVertical)
                {
                    // Vertical bezier curve (for top/bottom connections)
                    double midY = (StartY + EndY) / 2;
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "M {0},{1} C {2},{3} {4},{5} {6},{7}",
                        StartX, StartY, StartX, midY, EndX, midY, EndX, EndY);
                }
                else
                {
                    // Horizontal bezier curve (for left/right connections)
                    double midX = (StartX + EndX) / 2;
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "M {0},{1} C {2},{3} {4},{5} {6},{7}",
                        StartX, StartY, midX, StartY, midX, EndY, EndX, EndY);
                }
            }
        }

        /// <summary>
        /// Label position (middle of the line).
        /// </summary>
        public double LabelX => (StartX + EndX) / 2;
        public double LabelY => (StartY + EndY) / 2 - 10;

        /// <summary>
        /// Updates the path based on table positions.
        /// Chooses the optimal edge (top, bottom, left, right) to minimize line crossings.
        /// </summary>
        public void UpdatePath()
        {
            // Calculate the angle between centers to determine primary direction
            double dx = _toTable.CenterX - _fromTable.CenterX;
            double dy = _toTable.CenterY - _fromTable.CenterY;

            // Determine if the relationship is more horizontal or vertical
            bool isMoreHorizontal = Math.Abs(dx) > Math.Abs(dy);

            // Check for overlapping tables (use vertical edges for stacked tables)
            bool tablesOverlapHorizontally = 
                _fromTable.X < _toTable.X + _toTable.Width && 
                _fromTable.X + _fromTable.Width > _toTable.X;
            bool tablesOverlapVertically = 
                _fromTable.Y < _toTable.Y + _toTable.Height && 
                _fromTable.Y + _fromTable.Height > _toTable.Y;

            // Choose edges based on table positions
            if (tablesOverlapHorizontally && !tablesOverlapVertically)
            {
                // Tables are stacked vertically - use top/bottom edges
                if (_fromTable.CenterY < _toTable.CenterY)
                {
                    // From is above To
                    SetEdges(_fromTable.BottomEdgeX, _fromTable.BottomEdgeY, EdgeType.Bottom,
                             _toTable.TopEdgeX, _toTable.TopEdgeY, EdgeType.Top);
                }
                else
                {
                    // From is below To
                    SetEdges(_fromTable.TopEdgeX, _fromTable.TopEdgeY, EdgeType.Top,
                             _toTable.BottomEdgeX, _toTable.BottomEdgeY, EdgeType.Bottom);
                }
            }
            else if (tablesOverlapVertically && !tablesOverlapHorizontally)
            {
                // Tables are side by side - use left/right edges
                if (_fromTable.CenterX < _toTable.CenterX)
                {
                    SetEdges(_fromTable.RightEdgeX, _fromTable.RightEdgeY, EdgeType.Right,
                             _toTable.LeftEdgeX, _toTable.LeftEdgeY, EdgeType.Left);
                }
                else
                {
                    SetEdges(_fromTable.LeftEdgeX, _fromTable.LeftEdgeY, EdgeType.Left,
                             _toTable.RightEdgeX, _toTable.RightEdgeY, EdgeType.Right);
                }
            }
            else if (isMoreHorizontal)
            {
                // Primarily horizontal relationship - prefer left/right edges
                if (dx > 0)
                {
                    SetEdges(_fromTable.RightEdgeX, _fromTable.RightEdgeY, EdgeType.Right,
                             _toTable.LeftEdgeX, _toTable.LeftEdgeY, EdgeType.Left);
                }
                else
                {
                    SetEdges(_fromTable.LeftEdgeX, _fromTable.LeftEdgeY, EdgeType.Left,
                             _toTable.RightEdgeX, _toTable.RightEdgeY, EdgeType.Right);
                }
            }
            else
            {
                // Primarily vertical relationship - prefer top/bottom edges
                if (dy > 0)
                {
                    SetEdges(_fromTable.BottomEdgeX, _fromTable.BottomEdgeY, EdgeType.Bottom,
                             _toTable.TopEdgeX, _toTable.TopEdgeY, EdgeType.Top);
                }
                else
                {
                    SetEdges(_fromTable.TopEdgeX, _fromTable.TopEdgeY, EdgeType.Top,
                             _toTable.BottomEdgeX, _toTable.BottomEdgeY, EdgeType.Bottom);
                }
            }

            // Notify about derived properties
            NotifyOfPropertyChange(nameof(PathData));
            NotifyOfPropertyChange(nameof(LabelX));
            NotifyOfPropertyChange(nameof(LabelY));
        }

        /// <summary>
        /// Helper to set edge positions.
        /// Sets edge types first to ensure derived properties use correct values.
        /// </summary>
        private void SetEdges(double startX, double startY, EdgeType startEdge,
                              double endX, double endY, EdgeType endEdge)
        {
            // Set edge types first (before positions trigger notifications)
            _startEdge = startEdge;
            _endEdge = endEdge;
            
            // Now set positions (which trigger notifications that use edge types)
            StartX = startX;
            StartY = startY;
            EndX = endX;
            EndY = endY;
        }
    }
}
