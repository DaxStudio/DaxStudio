using ADOTabular;
using Caliburn.Micro;
using Dax.ViewModel;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// Public enum for edge type, used in edge slot calculations for relationship lines.
    /// Defines which edge of a table a relationship connects to.
    /// </summary>
    public enum EdgeTypePublic { Left, Right, Top, Bottom }

    /// <summary>
    /// ViewModel for the Model Diagram visualization.
    /// This displays tables, columns, and relationships from the connected model's metadata.
    /// This is a dockable tool window that can be resized, floated, or maximized.
    /// </summary>
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class ModelDiagramViewModel : ToolWindowBase, 
        IHandle<MetadataLoadedEvent>,
        IHandle<ViewMetricsCompleteEvent>,
        IHandle<ShowTablesInModelDiagramEvent>
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IMetadataProvider _metadataProvider;
        private readonly IGlobalOptions _options;
        private ADOTabularModel _model;
        private string _currentModelKey;
        private Dax.ViewModel.VpaModel _vpaModel;
        private bool _isOfflineMode;
        private ShowTablesInModelDiagramEvent _pendingTableFilter;

        /// <summary>
        /// Path to the layout cache file.
        /// </summary>
        private static string LayoutCacheFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DaxStudio",
            "ModelDiagramLayouts.json");

        [ImportingConstructor]
        public ModelDiagramViewModel(IEventAggregator eventAggregator, IMetadataProvider metadataProvider, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _metadataProvider = metadataProvider;
            _options = options;
            _eventAggregator.SubscribeOnPublishedThread(this);
        }

        #region ToolWindowBase Implementation

        public override string Title => "Model Diagram";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "model-diagram";
        public override bool CanHide => true;

        /// <summary>
        /// Icon for the tool window (used by AvalonDock).
        /// </summary>
        public ImageSource IconSource => null;

        /// <summary>
        /// Unsubscribe from event aggregator when the window is closed to prevent memory leaks.
        /// </summary>
        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
            {
                _eventAggregator.Unsubscribe(this);
            }
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Collection of table view models for display.
        /// </summary>
        public BindableCollection<ModelDiagramTableViewModel> Tables { get; } = new BindableCollection<ModelDiagramTableViewModel>();

        /// <summary>
        /// Collection of relationship view models for display.
        /// </summary>
        public BindableCollection<ModelDiagramRelationshipViewModel> Relationships { get; } = new BindableCollection<ModelDiagramRelationshipViewModel>();

        /// <summary>
        /// Collection of text annotations for display.
        /// </summary>
        public BindableCollection<ModelDiagramAnnotationViewModel> Annotations { get; } = new BindableCollection<ModelDiagramAnnotationViewModel>();

        /// <summary>
        /// Summary text showing counts and enrichment status.
        /// </summary>
        public string SummaryText
        {
            get
            {
                if (Tables.Count == 0) return "No data";
                
                var summary = $"{Tables.Count} Tables, {Tables.Sum(t => t.ColumnCount)} Columns, {Relationships.Count} Relationships";
                
                // Add enrichment indicators
                if (HasVertipaqData)
                {
                    summary += " 📊"; // VPA stats loaded
                }
                if (HasStorageModeData)
                {
                    summary += " 💾"; // Storage mode info loaded
                }
                
                // Indicate offline mode
                if (_model == null)
                {
                    summary += " (Offline)";
                }
                
                return summary;
            }
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

        /// <summary>
        /// Collapses all tables to show only headers.
        /// </summary>
        public void CollapseAll()
        {
            SaveLayoutForUndo();
            foreach (var table in Tables)
            {
                table.IsCollapsed = true;
            }
            UpdateAllRelationships();
        }

        /// <summary>
        /// Expands all tables to show columns.
        /// </summary>
        public void ExpandAll()
        {
            SaveLayoutForUndo();
            foreach (var table in Tables)
            {
                table.IsCollapsed = false;
            }
            UpdateAllRelationships();
        }

        /// <summary>
        /// Updates all relationship paths (e.g., after collapse/expand all).
        /// </summary>
        public void UpdateAllRelationships()
        {
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
                    table.IsSearchMatch = true;
                    foreach (var col in table.Columns)
                    {
                        col.IsSearchMatch = true;
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
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; NotifyOfPropertyChange(); }
        }

        private string _loadingMessage = "Loading diagram...";
        /// <summary>
        /// Message shown during loading with progress info.
        /// </summary>
        public string LoadingMessage
        {
            get => _loadingMessage;
            set { _loadingMessage = value; NotifyOfPropertyChange(); }
        }

        private string _loadingStats = string.Empty;
        /// <summary>
        /// Statistics about the last load operation (timing info).
        /// </summary>
        public string LoadingStats
        {
            get => _loadingStats;
            set { _loadingStats = value; NotifyOfPropertyChange(); }
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

        private bool _showDataTypes = false;
        /// <summary>
        /// Whether to show data types next to columns.
        /// </summary>
        public bool ShowDataTypes
        {
            get => _showDataTypes;
            set
            {
                _showDataTypes = value;
                NotifyOfPropertyChange();
                foreach (var table in Tables)
                {
                    foreach (var col in table.Columns)
                    {
                        col.ShowDataType = value;
                    }
                }
            }
        }

        private bool _showHiddenObjects = false;
        /// <summary>
        /// Whether to show hidden tables and columns.
        /// </summary>
        public bool ShowHiddenObjects
        {
            get => _showHiddenObjects;
            set
            {
                _showHiddenObjects = value;
                NotifyOfPropertyChange();
                if (_model != null)
                {
                    LoadFromModel(_model, forceReload: true);
                }
            }
        }

        private bool _sortKeyColumnsFirst = false;
        /// <summary>
        /// Whether to sort related columns first in the column list.
        /// </summary>
        public bool SortKeyColumnsFirst
        {
            get => _sortKeyColumnsFirst;
            set
            {
                _sortKeyColumnsFirst = value;
                NotifyOfPropertyChange();
                // Re-sort columns in all tables
                foreach (var table in Tables)
                {
                    table.UpdateColumnSort(_sortKeyColumnsFirst, _options.DiagramColumnSortOrder);
                }
            }
        }

        #region Perspectives

        private ADOTabularModelCollection _availablePerspectives;
        /// <summary>
        /// Collection of available models/perspectives for the current connection.
        /// </summary>
        public ADOTabularModelCollection AvailablePerspectives
        {
            get => _availablePerspectives;
            private set
            {
                _availablePerspectives = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasPerspectives));
            }
        }

        /// <summary>
        /// Whether there are multiple perspectives available (including the base model).
        /// </summary>
        public bool HasPerspectives => _availablePerspectives != null && _availablePerspectives.Count > 1;

        private ADOTabularModel _selectedPerspective;
        /// <summary>
        /// The currently selected perspective/model.
        /// </summary>
        public ADOTabularModel SelectedPerspective
        {
            get => _selectedPerspective;
            set
            {
                if (_selectedPerspective == value) return;
                _selectedPerspective = value;
                NotifyOfPropertyChange();
                
                // Load the selected perspective if it's different from current model
                if (_selectedPerspective != null && _selectedPerspective != _model)
                {
                    LoadFromModel(_selectedPerspective, forceReload: true);
                    
                    // Also notify the metadata provider to sync the selection
                    _ = _metadataProvider.SetSelectedModelAsync(_selectedPerspective);
                }
            }
        }

        /// <summary>
        /// Updates the available perspectives from the metadata provider.
        /// </summary>
        private void RefreshAvailablePerspectives()
        {
            try
            {
                var models = _metadataProvider?.GetModels();
                if (models != null && models.Count > 0)
                {
                    AvailablePerspectives = models;
                    // Set selected perspective to match current model
                    if (_model != null && models.Any(m => m.Name == _model.Name))
                    {
                        _selectedPerspective = models[_model.Name];
                        NotifyOfPropertyChange(nameof(SelectedPerspective));
                    }
                }
                else
                {
                    AvailablePerspectives = null;
                    _selectedPerspective = null;
                    NotifyOfPropertyChange(nameof(SelectedPerspective));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "{class} {method} Error refreshing perspectives", nameof(ModelDiagramViewModel), nameof(RefreshAvailablePerspectives));
                AvailablePerspectives = null;
            }
        }

        #endregion

        private int _tableFilter = 0;
        /// <summary>
        /// Filter tables by type: 0=All, 1=Date Tables Only
        /// </summary>
        public int TableFilter
        {
            get => _tableFilter;
            set
            {
                _tableFilter = value;
                NotifyOfPropertyChange();
                ApplyTableFilter();
            }
        }

        /// <summary>
        /// Applies the table filter to show/hide tables based on type.
        /// Also hides relationships between hidden tables.
        /// </summary>
        private void ApplyTableFilter()
        {
            // First, set table visibility
            foreach (var table in Tables)
            {
                switch (_tableFilter)
                {
                    case 0: // All tables
                        table.IsHidden = false;
                        break;
                    case 1: // Date tables only
                        table.IsHidden = !table.IsDateTable;
                        break;
                }
            }
            
            // Then, hide relationships where either table is hidden
            foreach (var rel in Relationships)
            {
                bool fromVisible = rel.FromTableViewModel != null && !rel.FromTableViewModel.IsHidden;
                bool toVisible = rel.ToTableViewModel != null && !rel.ToTableViewModel.IsHidden;
                rel.IsVisible = fromVisible && toVisible;
            }
            
            RefreshLayout();
        }

        private bool _snapToGrid = false;
        /// <summary>
        /// Whether to snap table positions to a grid when dragging.
        /// </summary>
        public bool SnapToGrid
        {
            get => _snapToGrid;
            set { _snapToGrid = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// The grid size for snapping (in pixels).
        /// </summary>
        public double GridSize => 20;

        /// <summary>
        /// Snaps a value to the grid if snap is enabled.
        /// </summary>
        public double SnapToGridValue(double value)
        {
            if (!SnapToGrid) return value;
            return Math.Round(value / GridSize) * GridSize;
        }

        #region Layout Undo

        private Stack<Dictionary<string, (double X, double Y, bool IsCollapsed)>> _layoutUndoStack = 
            new Stack<Dictionary<string, (double X, double Y, bool IsCollapsed)>>();
        
        private const int MaxUndoLevels = 10;

        /// <summary>
        /// Whether there are layout changes that can be undone.
        /// </summary>
        public bool CanUndoLayout => _layoutUndoStack.Count > 0;

        /// <summary>
        /// Saves the current layout state for undo.
        /// </summary>
        private void SaveLayoutForUndo()
        {
            var state = new Dictionary<string, (double X, double Y, bool IsCollapsed)>();
            foreach (var table in Tables)
            {
                state[table.TableName] = (table.X, table.Y, table.IsCollapsed);
            }
            
            _layoutUndoStack.Push(state);
            
            // Limit stack size
            if (_layoutUndoStack.Count > MaxUndoLevels)
            {
                var items = _layoutUndoStack.ToArray();
                _layoutUndoStack.Clear();
                for (int i = 0; i < MaxUndoLevels; i++)
                {
                    _layoutUndoStack.Push(items[MaxUndoLevels - 1 - i]);
                }
            }
            
            NotifyOfPropertyChange(nameof(CanUndoLayout));
        }

        /// <summary>
        /// Undoes the last layout change (Auto Arrange, etc.).
        /// </summary>
        public void UndoLayout()
        {
            if (_layoutUndoStack.Count == 0) return;

            var state = _layoutUndoStack.Pop();
            
            foreach (var table in Tables)
            {
                if (state.TryGetValue(table.TableName, out var pos))
                {
                    table.X = pos.X;
                    table.Y = pos.Y;
                    table.IsCollapsed = pos.IsCollapsed;
                }
            }
            
            RefreshLayout();
            SaveCurrentLayout();
            NotifyOfPropertyChange(nameof(CanUndoLayout));
        }

        #endregion

        private bool _showMiniMap = false;
        /// <summary>
        /// Whether to show the mini-map overview panel.
        /// </summary>
        public bool ShowMiniMap
        {
            get => _showMiniMap;
            set { _showMiniMap = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Collection of selected tables for multi-selection.
        /// </summary>
        public BindableCollection<ModelDiagramTableViewModel> SelectedTables { get; } = new BindableCollection<ModelDiagramTableViewModel>();

        /// <summary>
        /// Whether multiple tables are currently selected.
        /// </summary>
        public bool HasMultipleSelection => SelectedTables.Count > 1;

        /// <summary>
        /// Whether at least one table is currently selected.
        /// </summary>
        public bool HasSelection => SelectedTables.Count > 0 || _selectedTable != null;

        private ModelDiagramTableViewModel _selectedTable;
        /// <summary>
        /// The currently selected table.
        /// </summary>
        public ModelDiagramTableViewModel SelectedTable
        {
            get => _selectedTable;
            set
            {
                if (_selectedTable != null)
                {
                    _selectedTable.IsSelected = false;
                }
                _selectedTable = value;
                if (_selectedTable != null)
                {
                    _selectedTable.IsSelected = true;
                }
                NotifyOfPropertyChange();
                UpdateRelationshipHighlighting();
            }
        }

        /// <summary>
        /// Updates relationship highlighting based on selected table.
        /// </summary>
        private void UpdateRelationshipHighlighting()
        {
            if (_selectedTable == null)
            {
                // No selection - reset everything to normal
                foreach (var table in Tables)
                {
                    table.IsDimmed = false;
                }
                foreach (var rel in Relationships)
                {
                    rel.IsHighlighted = false;
                    rel.IsDimmed = false;
                }
            }
            else
            {
                // Find connected tables
                var connectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _selectedTable.TableName };
                foreach (var rel in Relationships)
                {
                    if (rel.FromTable == _selectedTable.TableName)
                        connectedTables.Add(rel.ToTable);
                    else if (rel.ToTable == _selectedTable.TableName)
                        connectedTables.Add(rel.FromTable);
                }

                // Dim unconnected tables
                foreach (var table in Tables)
                {
                    table.IsDimmed = !connectedTables.Contains(table.TableName);
                }

                // Highlight connected relationships, dim others
                foreach (var rel in Relationships)
                {
                    var isConnected = rel.FromTable == _selectedTable.TableName || rel.ToTable == _selectedTable.TableName;
                    rel.IsHighlighted = isConnected;
                    rel.IsDimmed = !isConnected;
                }
            }
        }

        /// <summary>
        /// Calculates edge slot positions for all relationships, distributing connection points
        /// along each table edge to prevent overlapping lines.
        /// This must be called AFTER UpdatePath() has set the edge types for each relationship.
        /// </summary>
        private void CalculateParallelRelationshipOffsets()
        {
            // First, call UpdatePath on all relationships to determine which edge they use
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }

            // Now calculate edge slots for each table
            CalculateEdgeSlots();
        }

        /// <summary>
        /// Calculates edge slot positions for relationships on each table edge.
        /// Relationships connecting to the same edge of a table are distributed along that edge.
        /// </summary>
        private void CalculateEdgeSlots()
        {
            CalculateEdgeSlotsCore(null);
        }

        /// <summary>
        /// Calculates edge slot positions for relationships on the specified table edges only.
        /// Only relationships connected to the specified tables are recalculated.
        /// </summary>
        private void CalculateEdgeSlotsForTables(HashSet<string> affectedTableNames)
        {
            CalculateEdgeSlotsCore(affectedTableNames);
        }

        /// <summary>
        /// Core implementation for edge slot calculation.
        /// When affectedTableNames is null, processes all tables (full recalculation).
        /// When provided, only processes the specified tables and their relationships.
        /// </summary>
        private void CalculateEdgeSlotsCore(HashSet<string> affectedTableNames)
        {
            const double EdgePadding = 30; // Minimum distance from table corners
            const double FixedSlotGap = 25;  // Fixed gap between connection points for consistent spacing

            bool processAll = affectedTableNames == null;

            // Reset slot offsets before recalculating
            // Only reset offsets for relationships connected to affected tables
            foreach (var rel in Relationships)
            {
                if (processAll || affectedTableNames.Contains(rel.FromTable))
                    rel.StartEdgeSlotOffset = 0;
                if (processAll || affectedTableNames.Contains(rel.ToTable))
                    rel.EndEdgeSlotOffset = 0;
            }

            // Build a dictionary of table name to table VM for quick lookup
            var tableDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in Tables)
            {
                if (!tableDict.ContainsKey(table.TableName))
                    tableDict[table.TableName] = table;
            }

            // For each table, group relationships by which edge they connect to
            foreach (var table in Tables)
            {
                // Skip tables that are not affected
                if (!processAll && !affectedTableNames.Contains(table.TableName)) continue;

                // Get relationships where this table is the "from" side
                var fromRelsByEdge = Relationships
                    .Where(r => string.Equals(r.FromTable, table.TableName, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(r => r.StartEdgeType)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Get relationships where this table is the "to" side
                var toRelsByEdge = Relationships
                    .Where(r => string.Equals(r.ToTable, table.TableName, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(r => r.EndEdgeType)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Process each edge type
                foreach (var edgeType in new[] { EdgeTypePublic.Left, EdgeTypePublic.Right, EdgeTypePublic.Top, EdgeTypePublic.Bottom })
                {
                    // Combine "from" and "to" relationships for this edge
                    var fromRels = fromRelsByEdge.TryGetValue(edgeType, out var fr) ? fr : new List<ModelDiagramRelationshipViewModel>();
                    var toRels = toRelsByEdge.TryGetValue(edgeType, out var tr) ? tr : new List<ModelDiagramRelationshipViewModel>();

                    int totalCount = fromRels.Count + toRels.Count;
                    if (totalCount == 0) continue; // No relationships on this edge
                    if (totalCount == 1) continue; // Single connection stays centered (offset already 0)

                    // Calculate edge length for reference
                    bool isVerticalEdge = (edgeType == EdgeTypePublic.Left || edgeType == EdgeTypePublic.Right);
                    double edgeLength = isVerticalEdge ? table.Height : table.Width;

                    // Use fixed slot gap for consistent spacing across all tables
                    double slotGap = FixedSlotGap;

                    // Sort relationships by the position of the other table (to minimize crossings)
                    // For vertical edges (Left/Right): sort by Y position of the other table
                    // For horizontal edges (Top/Bottom): sort by X position of the other table
                    var allRelsOnEdge = new List<(ModelDiagramRelationshipViewModel rel, bool isFrom, double otherTablePos)>();

                    foreach (var rel in fromRels)
                    {
                        // Get the "to" table position
                        if (tableDict.TryGetValue(rel.ToTable, out var toTable))
                        {
                            double pos = isVerticalEdge ? toTable.CenterY : toTable.CenterX;
                            allRelsOnEdge.Add((rel, true, pos));
                        }
                    }

                    foreach (var rel in toRels)
                    {
                        // Get the "from" table position
                        if (tableDict.TryGetValue(rel.FromTable, out var fromTable))
                        {
                            double pos = isVerticalEdge ? fromTable.CenterY : fromTable.CenterX;
                            allRelsOnEdge.Add((rel, false, pos));
                        }
                    }

                    // Sort by position of the other table (ascending)
                    allRelsOnEdge = allRelsOnEdge.OrderBy(x => x.otherTablePos).ToList();

                    // Calculate the maximum number of slots that fit within the edge
                    // so that no offset extends beyond the table boundary.
                    double usableLength = Math.Max(edgeLength - 2 * EdgePadding, 0);
                    int maxSlots = Math.Max(1, (int)(usableLength / slotGap) + 1);

                    // Determine actual number of distinct slot positions
                    int numSlots = Math.Min(totalCount, maxSlots);

                    // Distribute slots centered around 0
                    double totalWidth = (numSlots - 1) * slotGap;
                    double startOffset = -totalWidth / 2;

                    // When more relationships than slots, group them evenly across available slots.
                    int relsPerSlot = (int)Math.Ceiling((double)totalCount / numSlots);

                    for (int i = 0; i < allRelsOnEdge.Count; i++)
                    {
                        var (rel, isFrom, otherPos) = allRelsOnEdge[i];
                        int slotIndex = Math.Min(i / relsPerSlot, numSlots - 1);
                        double slotOffset = startOffset + (slotIndex * slotGap);

                        if (isFrom)
                        {
                            rel.StartEdgeSlotOffset = slotOffset;
                        }
                        else
                        {
                            rel.EndEdgeSlotOffset = slotOffset;
                        }
                    }
                }
            }

            // Update relationship paths with the new slot positions
            foreach (var rel in Relationships)
            {
                if (processAll || affectedTableNames.Contains(rel.FromTable) || affectedTableNames.Contains(rel.ToTable))
                {
                    rel.UpdatePathWithSlots();
                }
            }
        }

        /// <summary>
        /// Called when mouse enters a table. Highlights related tables and dims others.
        /// </summary>
        public void OnTableMouseEnter(ModelDiagramTableViewModel hoveredTable)
        {
            if (hoveredTable == null) return;

            hoveredTable.IsHovered = true;

            // Find all tables connected to the hovered table
            var connectedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { hoveredTable.TableName };
            foreach (var rel in Relationships)
            {
                if (rel.FromTable == hoveredTable.TableName)
                    connectedTables.Add(rel.ToTable);
                else if (rel.ToTable == hoveredTable.TableName)
                    connectedTables.Add(rel.FromTable);
            }

            // Dim unconnected tables and relationships
            foreach (var table in Tables)
            {
                table.IsDimmed = !connectedTables.Contains(table.TableName);
            }

            foreach (var rel in Relationships)
            {
                var isConnected = rel.FromTable == hoveredTable.TableName || rel.ToTable == hoveredTable.TableName;
                rel.IsHighlighted = isConnected;
                rel.IsDimmed = !isConnected;
            }
        }

        /// <summary>
        /// Called when mouse leaves a table. Restores normal highlighting.
        /// </summary>
        public void OnTableMouseLeave(ModelDiagramTableViewModel hoveredTable)
        {
            if (hoveredTable == null) return;

            hoveredTable.IsHovered = false;

            // Restore all tables to normal (unless there's a selection)
            if (_selectedTable == null)
            {
                foreach (var table in Tables)
                {
                    table.IsDimmed = false;
                }

                foreach (var rel in Relationships)
                {
                    rel.IsHighlighted = false;
                    rel.IsDimmed = false;
                }
            }
            else
            {
                // Restore selection-based highlighting
                UpdateRelationshipHighlighting();
                foreach (var table in Tables)
                {
                    table.IsDimmed = false; // Tables don't dim on selection, only relationships
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Loads the diagram from an ADOTabularModel.
        /// </summary>
        /// <param name="model">The model to load.</param>
        /// <param name="forceReload">If true, bypasses duplicate load prevention.</param>
        public void LoadFromModel(ADOTabularModel model, bool forceReload = false)
        {
            if (model == null) return;
            
            // Prevent duplicate loads while already loading
            if (IsLoading) return;
            
            // Prevent reloading the same model (unless explicitly requested)
            if (!forceReload && _model == model && Tables.Count > 0) return;

            // Set loading flag BEFORE starting async work to prevent duplicate calls
            IsLoading = true;
            
            // Refresh available perspectives when loading a model
            RefreshAvailablePerspectives();
            
            // Update selected perspective to match the model being loaded
            if (_availablePerspectives != null && _availablePerspectives.Any(m => m.Name == model.Name))
            {
                _selectedPerspective = _availablePerspectives[model.Name];
                NotifyOfPropertyChange(nameof(SelectedPerspective));
            }

            // For large models, load asynchronously
            var tableCount = model.Tables.Count(t => ShowHiddenObjects || t.IsVisible);
            if (tableCount > 20)
            {
                // Run async loading on background thread for large models
                _ = LoadFromModelAsync(model);
            }
            else
            {
                // Small models load synchronously
                LoadFromModelSync(model);
            }
        }

        /// <summary>
        /// Asynchronously loads the diagram from an ADOTabularModel with progress updates.
        /// Used for large models (>20 tables) to keep UI responsive.
        /// </summary>
        private async Task LoadFromModelAsync(ADOTabularModel model)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stageStopwatch = new System.Diagnostics.Stopwatch();

            // IsLoading is already set by LoadFromModel() before calling this method
            LoadingMessage = "Initializing...";
            
            // Allow UI to render the loading indicator before starting work
            await Task.Delay(50);

            try
            {
                _model = model;
                _currentModelKey = GenerateModelKey(model);
                _vpaModel = null;
                _isOfflineMode = false;

                // Stage 1: Clear existing data (must be on UI thread)
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Tables.Clear();
                    Relationships.Clear();
                    Annotations.Clear();
                });

                // Stage 2: Materialize all model data on UI thread first (ADOTabular may log during lazy loading)
                stageStopwatch.Start();
                LoadingMessage = "Reading model metadata...";
                
                var visibleTables = model.Tables
                    .Where(t => ShowHiddenObjects || t.IsVisible)
                    .ToList();
                var totalTables = visibleTables.Count;

                // Pre-load all relationship data on UI thread to avoid logging issues on background thread
                // Also pre-extract all string values to avoid lazy loading on background thread
                var allRelationships = new List<(ADOTabularTable Table, List<ADOTabularRelationship> Relationships)>();
                var relationshipData = new List<(ADOTabularRelationship Rel, string FromTableName, string FromColumn, string ToTableName, string ToColumn)>();
                
                foreach (var table in model.Tables)
                {
                    // Access relationships on UI thread to trigger any lazy loading
                    var rels = table.Relationships.ToList();
                    allRelationships.Add((table, rels));
                    
                    // Pre-extract string values for fast background processing
                    foreach (var rel in rels)
                    {
                        relationshipData.Add((
                            rel,
                            rel.FromTable?.Name ?? "",
                            rel.FromColumn ?? "",
                            rel.ToTable?.Name ?? "",
                            rel.ToColumn ?? ""
                        ));
                    }
                }
                
                var metadataTime = stageStopwatch.ElapsedMilliseconds;

                // Capture values needed for background work
                var showHidden = ShowHiddenObjects;
                var metadataProvider = _metadataProvider;
                var options = _options;
                var sortKeyColumnsFirst = _sortKeyColumnsFirst;

                // Stage 3: Create table VMs (synchronously - faster than async overhead)
                stageStopwatch.Restart();
                LoadingMessage = $"Creating {totalTables} table views...";
                await Task.Yield(); // Allow UI to render progress
                
                // For large models (>50 tables), start with tables collapsed to improve rendering performance
                bool startCollapsed = totalTables > 50;
                
                var tableVms = new List<ModelDiagramTableViewModel>(totalTables);
                for (int i = 0; i < visibleTables.Count; i++)
                {
                    var table = visibleTables[i];
                    var tableVm = new ModelDiagramTableViewModel(table, showHidden, metadataProvider, options, sortKeyColumnsFirst);
                    if (startCollapsed)
                    {
                        tableVm.IsCollapsed = true;
                    }
                    tableVms.Add(tableVm);
                }

                var tablesTime = stageStopwatch.ElapsedMilliseconds;

                // Add all tables to collection - suppress notifications during bulk add
                // WPF rendering happens after this method completes
                stageStopwatch.Restart();
                LoadingMessage = $"Adding {tableVms.Count} tables to diagram...";
                await Task.Yield();
                
                Tables.IsNotifying = false;
                try
                {
                    Tables.AddRange(tableVms);
                }
                finally
                {
                    Tables.IsNotifying = true;
                    Tables.Refresh();
                }
                
                var addTablesTime = stageStopwatch.ElapsedMilliseconds;

                // Stage 4: Create relationships - fast enough to do on UI thread
                stageStopwatch.Restart();
                LoadingMessage = $"Processing {relationshipData.Count} relationships...";
                await Task.Yield(); // Allow UI to render progress

                // Build dictionaries for O(1) lookups
                // Use safe construction to handle potential duplicate keys (e.g., hierarchy levels with same name as columns)
                var tableDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
                foreach (var tableVm in tableVms)
                {
                    if (!tableDict.ContainsKey(tableVm.TableName))
                        tableDict[tableVm.TableName] = tableVm;
                    else
                        Log.Warning("{class} {method} Duplicate table name '{tableName}' - skipping", nameof(ModelDiagramViewModel), nameof(LoadFromModelAsync), tableVm.TableName);
                }
                var columnDicts = new Dictionary<string, Dictionary<string, ModelDiagramColumnViewModel>>(StringComparer.OrdinalIgnoreCase);
                foreach (var tableVm in tableVms)
                {
                    var colDict = new Dictionary<string, ModelDiagramColumnViewModel>(StringComparer.OrdinalIgnoreCase);
                    foreach (var col in tableVm.Columns)
                    {
                        if (!colDict.ContainsKey(col.ColumnName))
                            colDict[col.ColumnName] = col;
                    }
                    columnDicts[tableVm.TableName] = colDict;
                }

                // Track relationship counts per table
                var relationshipCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var tableVm in tableVms)
                {
                    relationshipCounts[tableVm.TableName] = 0;
                }

                var relationshipVms = new List<ModelDiagramRelationshipViewModel>(relationshipData.Count / 2);
                var processedRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (rel, fromTableName, fromColumn, toTableName, toColumn) in relationshipData)
                {
                    // Create a normalized key that handles potential duplicates
                    // Sort table names to ensure same relationship found from either table produces same key
                    var relKey = string.Compare(fromTableName, toTableName, StringComparison.OrdinalIgnoreCase) <= 0
                        ? $"{fromTableName}|{fromColumn}|{toTableName}|{toColumn}"
                        : $"{toTableName}|{toColumn}|{fromTableName}|{fromColumn}";
                    if (processedRelationships.Contains(relKey)) continue;
                    processedRelationships.Add(relKey);

                    tableDict.TryGetValue(fromTableName, out var fromTableVm);
                    tableDict.TryGetValue(toTableName, out var toTableVm);

                    if (fromTableVm != null && toTableVm != null)
                    {
                        var relVm = new ModelDiagramRelationshipViewModel(rel, fromTableVm, toTableVm);
                        relationshipVms.Add(relVm);

                        // Increment relationship counts
                        relationshipCounts[fromTableName]++;
                        relationshipCounts[toTableName]++;

                        if (columnDicts.TryGetValue(fromTableName, out var fromColDict) &&
                            fromColDict.TryGetValue(fromColumn, out var fromCol))
                        {
                            fromCol.IsRelationshipColumn = true;
                        }
                        if (columnDicts.TryGetValue(toTableName, out var toColDict) &&
                            toColDict.TryGetValue(toColumn, out var toCol))
                        {
                            toCol.IsRelationshipColumn = true;
                        }
                    }
                }

                // Apply relationship counts to tables
                foreach (var tableVm in tableVms)
                {
                    if (relationshipCounts.TryGetValue(tableVm.TableName, out var count))
                    {
                        tableVm.RelationshipCount = count;
                    }
                }

                var relationshipsTime = stageStopwatch.ElapsedMilliseconds;

                // Add relationships to collection
                stageStopwatch.Restart();
                LoadingMessage = "Adding relationships to diagram...";

                Relationships.IsNotifying = false;
                try
                {
                    Relationships.AddRange(relationshipVms);
                }
                finally
                {
                    Relationships.IsNotifying = true;
                    Relationships.Refresh();
                }

                var addRelsTime = stageStopwatch.ElapsedMilliseconds;

                // Re-sort columns and recalculate collapsed heights now that IsRelationshipColumn is set
                stageStopwatch.Restart();
                foreach (var table in Tables)
                {
                    // Notify that KeyColumns may have changed (IsRelationshipColumn was just set)
                    table.NotifyKeyColumnsChanged();

                    // Recalculate collapsed height now that we know the key columns
                    if (table.IsCollapsed)
                    {
                        table.RecalculateCollapsedHeight();
                    }
                }
                var resortTime = stageStopwatch.ElapsedMilliseconds;

                // Stage 5: Layout
                stageStopwatch.Restart();
                LoadingMessage = $"Calculating layout for {tableVms.Count} tables...";
                await Task.Yield(); // Allow UI to render progress

                // Try to load saved layout first
                // Pass startCollapsed to ensure we don't override the collapsed state for large models
                bool layoutLoaded = TryLoadSavedLayout(preserveCollapsedState: startCollapsed);

                if (!layoutLoaded)
                {
                    // Use the user's selected layout algorithm (respects dropdown setting)
                    // This includes relationship offset calculation
                    LayoutDiagram();
                }
                else
                {
                    // Saved layout loaded - still need to update relationship paths and offsets
                    CalculateParallelRelationshipOffsets();
                }

                // Safety check: if layout failed (all tables at 0,0), force a grid layout
                if (!ValidateLayoutPositions())
                {
                    Log.Warning("{class} {method} Layout validation failed - all tables at origin. Forcing grid layout.",
                        nameof(ModelDiagramViewModel), nameof(LoadFromModelAsync));
                    LayoutDiagramGrid();
                }

                var layoutTime = stageStopwatch.ElapsedMilliseconds;

                stopwatch.Stop();
                var totalTime = stopwatch.ElapsedMilliseconds;

                // Update stats for display - comprehensive breakdown
                LoadingStats = $"Loaded in {totalTime:N0}ms (Meta:{metadataTime}ms, Create:{tablesTime}ms, Add:{addTablesTime}ms, Rel:{relationshipsTime}ms, AddRel:{addRelsTime}ms, Sort:{resortTime}ms, Layout:{layoutTime}ms)";

                // Capture values for logging on UI thread
                var tableCount = Tables.Count;
                var relCount = Relationships.Count;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    // Log timing info on UI thread (Serilog sink uses IoC which requires UI thread)
                    Log.Information("{class} {method} Loaded {tables} tables, {relationships} relationships in {time}ms " +
                        "(Metadata:{metaTime}ms, CreateTables:{tablesTime}ms, AddTables:{addTablesTime}ms, " +
                        "CreateRels:{relTime}ms, AddRels:{addRelsTime}ms, Resort:{resortTime}ms, Layout:{layoutTime}ms)",
                        nameof(ModelDiagramViewModel), nameof(LoadFromModelAsync),
                        tableCount, relCount, totalTime, 
                        metadataTime, tablesTime, addTablesTime, 
                        relationshipsTime, addRelsTime, resortTime, layoutTime);

                    NotifyOfPropertyChange(nameof(SummaryText));
                    NotifyOfPropertyChange(nameof(HasData));
                    NotifyOfPropertyChange(nameof(NoData));

                    // Apply any pending table filter that arrived while loading
                    ApplyPendingTableFilter();
                });
            }
            catch (Exception ex)
            {
                // Log on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(ModelDiagramViewModel), nameof(LoadFromModelAsync), ex.Message);
                });
            }
            finally
            {
                // Use low priority to set IsLoading=false AFTER WPF finishes rendering
                // This keeps the loading indicator visible during the rendering phase
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoading = false;
                    LoadingMessage = "Loading diagram...";
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        /// <summary>
        /// Calculates layout positions using ViewModels only (no ADOTabular objects needed).
        /// Used by LayoutDiagram() when recalculating layout from existing data.
        /// </summary>
        private Dictionary<string, (double X, double Y, double Width)> CalculateLayoutPositionsFromViewModels()
        {
            var positions = new Dictionary<string, (double X, double Y, double Width)>();
            var tableVms = Tables.ToList();
            
            if (tableVms.Count == 0) return positions;

            const double tableWidth = 200;
            const double tableHeight = 180;
            const double horizontalSpacing = 80;
            const double verticalSpacing = 100;
            const double padding = 50;
            const double clusterGap = 150;

            // Build relationship graph from Relationships ViewModels
            var tableSet = new HashSet<string>(tableVms.Select(t => t.TableName), StringComparer.OrdinalIgnoreCase);
            var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var tableVmDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tableVms)
            {
                if (!tableVmDict.ContainsKey(t.TableName)) tableVmDict[t.TableName] = t;
            }
            
            foreach (var table in tableVms)
            {
                neighbors[table.TableName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // Build neighbor relationships from Relationships collection
            foreach (var rel in Relationships)
            {
                var fromTable = rel.FromTable;
                var toTable = rel.ToTable;
                
                if (string.IsNullOrEmpty(fromTable) || string.IsNullOrEmpty(toTable)) continue;
                
                if (tableSet.Contains(fromTable) && tableSet.Contains(toTable))
                {
                    neighbors[fromTable].Add(toTable);
                    neighbors[toTable].Add(fromTable);
                }
            }

            // Find connected components using BFS
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clusters = new List<List<ModelDiagramTableViewModel>>();
            
            foreach (var table in tableVms)
            {
                if (visited.Contains(table.TableName)) continue;
                
                var cluster = new List<ModelDiagramTableViewModel>();
                var queue = new Queue<string>();
                queue.Enqueue(table.TableName);
                visited.Add(table.TableName);
                
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (tableVmDict.TryGetValue(current, out var tableVm))
                    {
                        cluster.Add(tableVm);
                    }
                    
                    foreach (var neighbor in neighbors[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                
                clusters.Add(cluster);
            }

            // Sort clusters by size (largest first)
            clusters = clusters.OrderByDescending(c => c.Count).ToList();

            // Calculate target aspect ratio
            int totalTables = tableVms.Count;
            int targetCols = (int)Math.Ceiling(Math.Sqrt(totalTables * 1.5));
            targetCols = Math.Max(8, Math.Min(20, targetCols));

            double currentX = padding;
            double currentY = padding;
            double rowMaxHeight = 0;

            foreach (var cluster in clusters)
            {
                int clusterSize = cluster.Count;
                int clusterCols, clusterRows;
                
                if (clusterSize <= 3)
                {
                    clusterCols = clusterSize;
                    clusterRows = 1;
                }
                else if (clusterSize <= 12)
                {
                    clusterCols = (int)Math.Ceiling(Math.Sqrt(clusterSize));
                    clusterRows = (int)Math.Ceiling((double)clusterSize / clusterCols);
                }
                else
                {
                    clusterCols = Math.Min(6, (int)Math.Ceiling(Math.Sqrt(clusterSize)));
                    clusterRows = (int)Math.Ceiling((double)clusterSize / clusterCols);
                }

                double clusterWidth = clusterCols * (tableWidth + horizontalSpacing) - horizontalSpacing;
                double clusterHeight = clusterRows * (tableHeight + verticalSpacing) - verticalSpacing;

                if (currentX + clusterWidth > targetCols * (tableWidth + horizontalSpacing) && currentX > padding)
                {
                    currentX = padding;
                    currentY += rowMaxHeight + clusterGap;
                    rowMaxHeight = 0;
                }

                int col = 0;
                int row = 0;
                
                var sortedCluster = cluster
                    .OrderByDescending(t => neighbors[t.TableName].Count)
                    .ThenBy(t => t.TableName)
                    .ToList();

                foreach (var tableVm in sortedCluster)
                {
                    double x = currentX + col * (tableWidth + horizontalSpacing);
                    double y = currentY + row * (tableHeight + verticalSpacing);
                    
                    positions[tableVm.TableName] = (x, y, tableWidth);

                    col++;
                    if (col >= clusterCols)
                    {
                        col = 0;
                        row++;
                    }
                }

                currentX += clusterWidth + clusterGap;
                rowMaxHeight = Math.Max(rowMaxHeight, clusterHeight);
            }

            return positions;
        }

        /// <summary>
        /// Calculates layout positions on a background thread without touching UI.
        /// Uses a relationship-aware layout that groups connected tables into clusters.
        /// Returns a dictionary of table name to (X, Y, Width) positions.
        /// </summary>
        private Dictionary<string, (double X, double Y, double Width)> CalculateLayoutPositions(
            List<ModelDiagramTableViewModel> tableVms,
            List<(ADOTabularTable Table, List<ADOTabularRelationship> Relationships)> allRelationships)
        {
            var positions = new Dictionary<string, (double X, double Y, double Width)>();
            
            if (tableVms.Count == 0) return positions;

            const double tableWidth = 200;
            const double tableHeight = 180;
            const double horizontalSpacing = 80;
            const double verticalSpacing = 100;
            const double padding = 50;
            const double clusterGap = 150; // Extra gap between clusters

            // Build relationship graph from the pre-loaded relationship data
            var tableSet = new HashSet<string>(tableVms.Select(t => t.TableName), StringComparer.OrdinalIgnoreCase);
            var neighbors = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var tableVmDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tableVms)
            {
                if (!tableVmDict.ContainsKey(t.TableName)) tableVmDict[t.TableName] = t;
            }
            
            foreach (var table in tableVms)
            {
                neighbors[table.TableName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            // Build neighbor relationships from pre-loaded relationship data
            var processedRels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (table, relationships) in allRelationships)
            {
                foreach (var rel in relationships)
                {
                    var fromTable = rel.FromTable?.Name;
                    var toTable = rel.ToTable?.Name;
                    
                    if (string.IsNullOrEmpty(fromTable) || string.IsNullOrEmpty(toTable)) continue;
                    
                    var relKey = $"{fromTable}|{toTable}";
                    if (processedRels.Contains(relKey)) continue;
                    processedRels.Add(relKey);
                    processedRels.Add($"{toTable}|{fromTable}");
                    
                    if (tableSet.Contains(fromTable) && tableSet.Contains(toTable))
                    {
                        neighbors[fromTable].Add(toTable);
                        neighbors[toTable].Add(fromTable);
                    }
                }
            }

            // Find connected components using BFS
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clusters = new List<List<ModelDiagramTableViewModel>>();
            
            foreach (var table in tableVms)
            {
                if (visited.Contains(table.TableName)) continue;
                
                // BFS to find all tables in this connected component
                var cluster = new List<ModelDiagramTableViewModel>();
                var queue = new Queue<string>();
                queue.Enqueue(table.TableName);
                visited.Add(table.TableName);
                
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (tableVmDict.TryGetValue(current, out var tableVm))
                    {
                        cluster.Add(tableVm);
                    }
                    
                    foreach (var neighbor in neighbors[current])
                    {
                        if (!visited.Contains(neighbor))
                        {
                            visited.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                
                clusters.Add(cluster);
            }

            // Sort clusters by size (largest first) to place important groups prominently
            clusters = clusters.OrderByDescending(c => c.Count).ToList();

            // Calculate target aspect ratio - aim for something reasonably square
            int totalTables = tableVms.Count;
            int targetCols = (int)Math.Ceiling(Math.Sqrt(totalTables * 1.5)); // Slightly wider than square
            targetCols = Math.Max(8, Math.Min(20, targetCols)); // Between 8 and 20 columns

            double currentX = padding;
            double currentY = padding;
            double rowMaxHeight = 0;
            double maxX = 0;

            foreach (var cluster in clusters)
            {
                // Calculate cluster dimensions
                int clusterSize = cluster.Count;
                int clusterCols, clusterRows;
                
                if (clusterSize <= 3)
                {
                    // Small clusters: single row
                    clusterCols = clusterSize;
                    clusterRows = 1;
                }
                else if (clusterSize <= 12)
                {
                    // Medium clusters: roughly square
                    clusterCols = (int)Math.Ceiling(Math.Sqrt(clusterSize));
                    clusterRows = (int)Math.Ceiling((double)clusterSize / clusterCols);
                }
                else
                {
                    // Large clusters: limit width, allow more rows
                    clusterCols = Math.Min(6, (int)Math.Ceiling(Math.Sqrt(clusterSize)));
                    clusterRows = (int)Math.Ceiling((double)clusterSize / clusterCols);
                }

                double clusterWidth = clusterCols * (tableWidth + horizontalSpacing) - horizontalSpacing;
                double clusterHeight = clusterRows * (tableHeight + verticalSpacing) - verticalSpacing;

                // Check if this cluster fits in the current row
                if (currentX + clusterWidth > targetCols * (tableWidth + horizontalSpacing) && currentX > padding)
                {
                    // Move to next row
                    currentX = padding;
                    currentY += rowMaxHeight + clusterGap;
                    rowMaxHeight = 0;
                }

                // Place tables within this cluster
                int col = 0;
                int row = 0;
                
                // Sort tables within cluster: more connected tables first (central position)
                var sortedCluster = cluster
                    .OrderByDescending(t => neighbors[t.TableName].Count)
                    .ThenBy(t => t.TableName)
                    .ToList();

                foreach (var tableVm in sortedCluster)
                {
                    double x = currentX + col * (tableWidth + horizontalSpacing);
                    double y = currentY + row * (tableHeight + verticalSpacing);
                    
                    positions[tableVm.TableName] = (x, y, tableWidth);
                    maxX = Math.Max(maxX, x + tableWidth);

                    col++;
                    if (col >= clusterCols)
                    {
                        col = 0;
                        row++;
                    }
                }

                // Update position for next cluster
                currentX += clusterWidth + clusterGap;
                rowMaxHeight = Math.Max(rowMaxHeight, clusterHeight);
            }

            return positions;
        }

        /// <summary>
        /// Validates that the layout positions are sensible (not all at origin).
        /// Returns true if layout is valid, false if all tables are at (0,0).
        /// </summary>
        private bool ValidateLayoutPositions()
        {
            if (Tables.Count <= 1) return true;
            
            // Check that at least half the tables have been positioned away from the origin
            int positioned = Tables.Count(t => t.X > 0 || t.Y > 0);
            return positioned > Tables.Count / 2;
        }

        /// <summary>
        /// Applies pre-calculated layout positions to tables.
        /// Must be called on UI thread.
        /// </summary>
        private void ApplyLayoutPositions(Dictionary<string, (double X, double Y, double Width)> positions)
        {
            double maxX = 0, maxY = 0;

            foreach (var table in Tables)
            {
                if (positions.TryGetValue(table.TableName, out var pos))
                {
                    table.X = pos.X;
                    table.Y = pos.Y;
                    table.Width = pos.Width;

                    maxX = Math.Max(maxX, pos.X + pos.Width);
                    maxY = Math.Max(maxY, pos.Y + 180); // Approximate height
                }
            }

            CanvasWidth = Math.Max(800, maxX + 50);
            CanvasHeight = Math.Max(600, maxY + 50);
        }

        /// <summary>
        /// Synchronously loads the diagram from an ADOTabularModel.
        /// Used for small models (<=20 tables) where async overhead isn't needed.
        /// </summary>
        private void LoadFromModelSync(ADOTabularModel model)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // IsLoading is already set by LoadFromModel() before calling this method

            try
            {
                _model = model;
                _currentModelKey = GenerateModelKey(model);
                _vpaModel = null;
                _isOfflineMode = false;
                Tables.Clear();
                Relationships.Clear();
                Annotations.Clear();

                // Create view models for tables
                foreach (var table in model.Tables)
                {
                    if (!ShowHiddenObjects && !table.IsVisible) continue;

                    var tableVm = new ModelDiagramTableViewModel(table, ShowHiddenObjects, _metadataProvider, _options, _sortKeyColumnsFirst);
                    Tables.Add(tableVm);
                }

                // Create a dictionary for O(1) table lookups (performance optimization for large models)
                // Use safe construction to handle potential duplicate keys
                var tableDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
                foreach (var tv in Tables)
                {
                    if (!tableDict.ContainsKey(tv.TableName))
                        tableDict[tv.TableName] = tv;
                    else
                        Log.Warning("{class} {method} Duplicate table name '{tableName}' - skipping", nameof(ModelDiagramViewModel), nameof(LoadFromModelSync), tv.TableName);
                }

                // Create view models for relationships
                // Relationships are stored on individual tables, so we need to gather from all tables
                var processedRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var table in model.Tables)
                {
                    foreach (var rel in table.Relationships)
                    {
                        // Create a normalized key to avoid duplicates (relationships appear on both ends)
                        // Sort table names to ensure same relationship found from either table produces same key
                        var fromName = rel.FromTable?.Name ?? "";
                        var toName = rel.ToTable?.Name ?? "";
                        var relKey = string.Compare(fromName, toName, StringComparison.OrdinalIgnoreCase) <= 0
                            ? $"{fromName}|{rel.FromColumn}|{toName}|{rel.ToColumn}"
                            : $"{toName}|{rel.ToColumn}|{fromName}|{rel.FromColumn}";
                        if (processedRelationships.Contains(relKey)) continue;
                        processedRelationships.Add(relKey);

                        tableDict.TryGetValue(rel.FromTable?.Name ?? "", out var fromTableVm);
                        tableDict.TryGetValue(rel.ToTable?.Name ?? "", out var toTableVm);

                        if (fromTableVm != null && toTableVm != null)
                        {
                            var relVm = new ModelDiagramRelationshipViewModel(rel, fromTableVm, toTableVm);
                            Relationships.Add(relVm);

                            // Track relationship count for each table
                            fromTableVm.RelationshipCount++;
                            toTableVm.RelationshipCount++;

                            // Mark columns as relationship columns
                            var fromCol = fromTableVm.Columns.FirstOrDefault(c => c.ColumnName == rel.FromColumn);
                            var toCol = toTableVm.Columns.FirstOrDefault(c => c.ColumnName == rel.ToColumn);
                            if (fromCol != null) fromCol.IsRelationshipColumn = true;
                            if (toCol != null) toCol.IsRelationshipColumn = true;
                        }
                    }
                }

                // Try to load saved layout, otherwise use auto-layout
                // NOTE: Both branches call CalculateParallelRelationshipOffsets AFTER positions are set
                if (!TryLoadSavedLayout())
                {
                    // LayoutDiagram() calls CalculateParallelRelationshipOffsets() internally at the end
                    LayoutDiagram();
                }
                else
                {
                    // Saved layout loaded - need to calculate offsets now that positions are set
                    CalculateParallelRelationshipOffsets();
                }

                stopwatch.Stop();
                Log.Information("{class} {method} Loaded {tables} tables, {relationships} relationships in {time}ms",
                    nameof(ModelDiagramViewModel), nameof(LoadFromModelSync),
                    Tables.Count, Relationships.Count, stopwatch.ElapsedMilliseconds);

                LoadingStats = $"Loaded in {stopwatch.ElapsedMilliseconds}ms";

                NotifyOfPropertyChange(nameof(SummaryText));
                NotifyOfPropertyChange(nameof(HasData));
                NotifyOfPropertyChange(nameof(NoData));

                // Apply any pending table filter that arrived while loading
                ApplyPendingTableFilter();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(ModelDiagramViewModel), nameof(LoadFromModelSync), ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads the diagram from VPA (VertiPaq Analyzer) data when offline.
        /// This allows viewing the model diagram from .daxx files without a live connection.
        /// </summary>
        /// <param name="vpaModel">The VPA model containing table, column, and relationship metadata.</param>
        public void LoadFromVpaModel(VpaModel vpaModel)
        {
            if (vpaModel?.Tables == null)
            {
                Log.Warning("{class} {method} VpaModel or Tables is null", nameof(ModelDiagramViewModel), nameof(LoadFromVpaModel));
                return;
            }

            if (IsLoading) return;

            try
            {
                IsLoading = true;
                LoadingMessage = "Loading from offline data...";

                Log.Information("{class} {method} Loading diagram from VPA data with {tableCount} tables",
                    nameof(ModelDiagramViewModel), nameof(LoadFromVpaModel), vpaModel.Tables.Count());

                // Clear existing data
                Tables.Clear();
                Relationships.Clear();
                Annotations.Clear();

                // Store VPA model reference for debugging
                _vpaModel = vpaModel;
                _isOfflineMode = true;;

                // Generate a model key from VPA data
                var tableNames = string.Join("|", vpaModel.Tables.OrderBy(t => t.TableName).Select(t => t.TableName));
                var hash = tableNames.GetHashCode().ToString("X8");
                _currentModelKey = $"VPA_{hash}";

                // Create table ViewModels from VPA tables
                var tableDict = new Dictionary<string, ModelDiagramTableViewModel>(StringComparer.OrdinalIgnoreCase);
                var showHidden = ShowHiddenObjects;
                var sortKeyColumnsFirst = _sortKeyColumnsFirst;

                foreach (var vpaTable in vpaModel.Tables)
                {
                    // Note: VpaTable doesn't expose IsHidden, so we show all tables in offline mode
                    var tableVm = new ModelDiagramTableViewModel(vpaTable, showHidden, _options, sortKeyColumnsFirst);
                    Tables.Add(tableVm);
                    tableDict[vpaTable.TableName] = tableVm;
                }

                // Create relationship ViewModels from VPA relationships
                var processedRelationships = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var relationshipVms = new List<ModelDiagramRelationshipViewModel>();

                foreach (var vpaTable in vpaModel.Tables)
                {
                    foreach (var vpaRel in vpaTable.RelationshipsFrom)
                    {
                        // Parse DAX column names: 'TableName'[ColumnName]
                        var (fromTableName, fromColumnName) = ParseDaxColumnName(vpaRel.FromColumnName);
                        var (toTableName, toColumnName) = ParseDaxColumnName(vpaRel.ToColumnName);

                        if (string.IsNullOrEmpty(fromTableName) || string.IsNullOrEmpty(toTableName)) continue;

                        // Create a normalized key to prevent duplicates
                        var relKey = string.Compare(fromTableName, toTableName, StringComparison.OrdinalIgnoreCase) <= 0
                            ? $"{fromTableName}|{fromColumnName}|{toTableName}|{toColumnName}"
                            : $"{toTableName}|{toColumnName}|{fromTableName}|{fromColumnName}";

                        if (processedRelationships.Contains(relKey)) continue;
                        processedRelationships.Add(relKey);

                        // Find the table ViewModels
                        if (!tableDict.TryGetValue(fromTableName, out var fromTableVm)) continue;
                        if (!tableDict.TryGetValue(toTableName, out var toTableVm)) continue;

                        // Find column ViewModels - if missing (hidden columns), create them dynamically
                        var fromColumnVm = fromTableVm.Columns.FirstOrDefault(c =>
                            string.Equals(c.ColumnName, fromColumnName, StringComparison.OrdinalIgnoreCase));
                        var toColumnVm = toTableVm.Columns.FirstOrDefault(c =>
                            string.Equals(c.ColumnName, toColumnName, StringComparison.OrdinalIgnoreCase));

                        // If relationship columns are hidden in VPA, create placeholder columns for the diagram
                        // This ensures relationships are visible even when the underlying columns are hidden
                        if (fromColumnVm == null)
                        {
                            // Try to find the VPA column to get its type info
                            var vpaCol = vpaTable.Columns.FirstOrDefault(c => 
                                string.Equals(c.ColumnName, fromColumnName, StringComparison.OrdinalIgnoreCase));
                            fromColumnVm = new ModelDiagramColumnViewModel(fromColumnName, vpaCol, _options);
                            fromColumnVm.IsRelationshipColumn = true;
                            fromTableVm.Columns.Add(fromColumnVm);
                            Log.Debug("VPA: Created placeholder column {Table}.{Column} for relationship", fromTableName, fromColumnName);
                        }
                        
                        if (toColumnVm == null)
                        {
                            // Try to find the VPA column in the target table
                            var targetVpaTable = vpaModel.Tables.FirstOrDefault(t => 
                                string.Equals(t.TableName, toTableName, StringComparison.OrdinalIgnoreCase));
                            var vpaCol = targetVpaTable?.Columns.FirstOrDefault(c => 
                                string.Equals(c.ColumnName, toColumnName, StringComparison.OrdinalIgnoreCase));
                            toColumnVm = new ModelDiagramColumnViewModel(toColumnName, vpaCol, _options);
                            toColumnVm.IsRelationshipColumn = true;
                            toTableVm.Columns.Add(toColumnVm);
                            Log.Debug("VPA: Created placeholder column {Table}.{Column} for relationship", toTableName, toColumnName);
                        }

                        // Create an ADOTabularRelationship to wrap the VPA data
                        // Parse RelationshipFromToName to extract cardinality and cross-filter direction
                        // Format: "Table[Column] <from_card><direction><to_card> Table[Column]"
                        // Where: ∞ = Many, 1 = One, ↔ = BiDi, ← = Single direction
                        var relName = vpaRel.RelationshipFromToName ?? "";
                        var isBiDi = relName.Contains("↔");
                        var crossFilterDirection = isBiDi ? "Both" : "Single";
                        
                        // Extract cardinality from RelationshipFromToName
                        // The cardinality symbols appear between ] and [ in the format: ] symbol←symbol [
                        // ∞ (infinity) indicates Many, 1 indicates One
                        var fromMultiplicity = "*"; // Default to Many
                        var toMultiplicity = "1";   // Default to One
                        
                        // Look for the cardinality symbols in the relationship name
                        // Format examples: "Table[Col] ∞←1 Table[Col]" or "Table[Col] 1↔1 Table[Col]" or "Table[Col] ∞↔∞ Table[Col]"
                        if (relName.Contains("∞←1") || relName.Contains("∞↔1"))
                        {
                            fromMultiplicity = "*";
                            toMultiplicity = "1";
                        }
                        else if (relName.Contains("1←∞") || relName.Contains("1↔∞"))
                        {
                            fromMultiplicity = "1";
                            toMultiplicity = "*";
                        }
                        else if (relName.Contains("∞←∞") || relName.Contains("∞↔∞"))
                        {
                            // Many-to-Many relationship
                            fromMultiplicity = "*";
                            toMultiplicity = "*";
                        }
                        else if (relName.Contains("1←1") || relName.Contains("1↔1"))
                        {
                            // One-to-One relationship
                            fromMultiplicity = "1";
                            toMultiplicity = "1";
                        }
                        
                        var adoRel = new ADOTabularRelationship
                        {
                            FromTable = null, // Not needed for diagram - we use table VMs
                            ToTable = null,
                            FromColumn = fromColumnName,
                            ToColumn = toColumnName,
                            FromColumnMultiplicity = fromMultiplicity,
                            ToColumnMultiplicity = toMultiplicity,
                            CrossFilterDirection = crossFilterDirection,
                            IsActive = vpaRel.IsActive
                        };

                        var relVm = new ModelDiagramRelationshipViewModel(adoRel, fromTableVm, toTableVm);
                        relationshipVms.Add(relVm);

                        // Track relationship counts per table
                        fromTableVm.RelationshipCount++;
                        toTableVm.RelationshipCount++;
                    }
                }

                // Add all relationships
                Relationships.AddRange(relationshipVms);

                // Now enrich with full VPA stats since we already have the data
                EnrichFromVertipaq(vpaModel);

                // Try to load saved layout, otherwise use auto-layout
                // NOTE: Both branches call CalculateParallelRelationshipOffsets AFTER positions are set
                if (!TryLoadSavedLayout())
                {
                    // LayoutDiagram() calls CalculateParallelRelationshipOffsets() internally at the end
                    LayoutDiagram();
                }
                else
                {
                    // Saved layout loaded - need to calculate offsets now that positions are set
                    CalculateParallelRelationshipOffsets();
                }

                Log.Information("{class} {method} Loaded {tableCount} tables and {relCount} relationships from VPA data",
                    nameof(ModelDiagramViewModel), nameof(LoadFromVpaModel), Tables.Count, Relationships.Count);

                NotifyOfPropertyChange(nameof(SummaryText));
                NotifyOfPropertyChange(nameof(HasData));
                NotifyOfPropertyChange(nameof(NoData));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(ModelDiagramViewModel), nameof(LoadFromVpaModel), ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error loading Model Diagram from offline data: {ex.Message}"));
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Parses a DAX column name in the format 'TableName'[ColumnName] into its components.
        /// </summary>
        private (string TableName, string ColumnName) ParseDaxColumnName(string daxColumnName)
        {
            if (string.IsNullOrEmpty(daxColumnName)) return (null, null);

            try
            {
                // Format: 'TableName'[ColumnName]
                var match = System.Text.RegularExpressions.Regex.Match(daxColumnName, @"^'([^']+)'\[([^\]]+)\]$");
                if (match.Success)
                {
                    return (match.Groups[1].Value, match.Groups[2].Value);
                }

                // Alternative format without quotes: TableName[ColumnName]
                match = System.Text.RegularExpressions.Regex.Match(daxColumnName, @"^([^\[]+)\[([^\]]+)\]$");
                if (match.Success)
                {
                    return (match.Groups[1].Value, match.Groups[2].Value);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "{class} {method} Failed to parse DAX column name: {name}",
                    nameof(ModelDiagramViewModel), nameof(ParseDaxColumnName), daxColumnName);
            }

            return (null, null);
        }

        /// <summary>
        /// Generates a unique key for the model based on server and database.
        /// </summary>
        private string GenerateModelKey(ADOTabularModel model)
        {
            // Create a key from database name and table count/names hash
            // This identifies the same model even across different connections
            var tableNames = string.Join("|", model.Tables.OrderBy(t => t.Caption).Select(t => t.Caption));
            var hash = tableNames.GetHashCode().ToString("X8");
            return $"{model.Database?.Name ?? "Unknown"}_{hash}";
        }

        /// <summary>
        /// Layouts the diagram using the appropriate algorithm based on user selection or model size.
        /// Algorithm selection:
        /// - Auto: Hierarchy (≤15 tables), Grid (16-50 tables), Clustered (>50 tables)
        /// - Hierarchy: Sugiyama-style layered algorithm - best for showing relationship hierarchy
        /// - Grid: Compact grid layout - best for medium models with many disconnected tables
        /// - Clustered: Cluster-based compact algorithm - best for large models
        /// </summary>
        private void LayoutDiagram()
        {
            if (Tables.Count == 0) return;

            var algorithm = _options?.DiagramLayoutAlgorithm ?? DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm.Auto;

            // Determine which algorithm to use
            if (algorithm == DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm.Auto)
            {
                // Auto-select based on table count
                if (Tables.Count <= 15)
                {
                    LayoutDiagramSugiyama(); // Hierarchy - best for small models
                }
                else if (Tables.Count <= 50)
                {
                    LayoutDiagramGrid(); // Grid - best for medium models
                }
                else
                {
                    LayoutDiagramClustered(); // Clustered - best for large models
                }
            }
            else
            {
                // Use explicitly selected algorithm
                switch (algorithm)
                {
                    case DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm.Hierarchy:
                        LayoutDiagramSugiyama();
                        break;
                    case DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm.Grid:
                        LayoutDiagramGrid();
                        break;
                    case DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm.Clustered:
                        LayoutDiagramClustered();
                        break;
                    default:
                        LayoutDiagramSugiyama();
                        break;
                }
            }
        }

        /// <summary>
        /// Layouts the diagram using Sugiyama-style layered algorithm.
        /// Better for smaller models where relationship hierarchy is important.
        /// </summary>
        private void LayoutDiagramSugiyama()
        {
            const double tableWidth = 200;
            const double tableHeight = 180;
            const double horizontalSpacing = 100;
            const double verticalSpacing = 120;
            const double padding = 50;

            // Step 1: Assign tables to layers based on longest path from root nodes
            var layers = AssignLayers();
            
            // Step 2: Order tables within each layer to minimize edge crossings
            MinimizeCrossings(layers);

            // Step 3: Assign X coordinates using barycenter method
            AssignCoordinates(layers, tableWidth, tableHeight, horizontalSpacing, verticalSpacing, padding);

            // Calculate canvas size to fit all tables
            var maxX = Tables.Any() ? Tables.Max(t => t.X + t.Width) : 100;
            var maxY = Tables.Any() ? Tables.Max(t => t.Y + t.Height) : 100;
            CanvasWidth = Math.Max(100, maxX + padding);
            CanvasHeight = Math.Max(100, maxY + padding);

            // Update relationship line positions and edge slot distribution
            CalculateParallelRelationshipOffsets();
        }

        /// <summary>
        /// Layouts the diagram using a compact grid algorithm.
        /// Better for medium-sized models (15-50 tables) where many tables may be disconnected.
        /// Places related tables near each other but uses a grid structure to avoid excessive horizontal spread.
        /// </summary>
        private void LayoutDiagramGrid()
        {
            const double tableWidth = 200;
            const double tableHeight = 180;
            const double horizontalSpacing = 80;
            const double verticalSpacing = 100;
            const double padding = 50;

            // Build neighbor map for relationship awareness
            var neighbors = BuildNeighborMap();
            
            // Group tables by their connectivity
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var groups = new List<List<ModelDiagramTableViewModel>>();
            
            // Find connected components
            foreach (var table in Tables)
            {
                if (visited.Contains(table.TableName)) continue;
                
                var group = new List<ModelDiagramTableViewModel>();
                var queue = new Queue<ModelDiagramTableViewModel>();
                queue.Enqueue(table);
                visited.Add(table.TableName);
                
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    group.Add(current);
                    
                    if (neighbors.TryGetValue(current.TableName, out var currentNeighbors))
                    {
                        foreach (var neighborName in currentNeighbors)
                        {
                            if (!visited.Contains(neighborName))
                            {
                                var neighbor = Tables.FirstOrDefault(t => 
                                    string.Equals(t.TableName, neighborName, StringComparison.OrdinalIgnoreCase));
                                if (neighbor != null)
                                {
                                    visited.Add(neighborName);
                                    queue.Enqueue(neighbor);
                                }
                            }
                        }
                    }
                }
                
                if (group.Count > 0)
                {
                    groups.Add(group);
                }
            }
            
            // Sort groups by size (largest first) for better placement
            groups = groups.OrderByDescending(g => g.Count).ToList();
            
            // Calculate optimal grid dimensions based on total tables
            // Aim for a roughly square layout with slight preference for wider
            int totalTables = Tables.Count;
            int columns = (int)Math.Ceiling(Math.Sqrt(totalTables * 1.5)); // Slightly wider than square
            columns = Math.Max(3, Math.Min(columns, 8)); // Between 3 and 8 columns
            
            // Place groups in the grid, keeping related tables together
            double currentX = padding;
            double currentY = padding;
            int columnIndex = 0;
            double maxHeightInRow = 0;
            
            foreach (var group in groups)
            {
                // Sort tables within group: fact tables (more relationships) in center
                var sortedGroup = group
                    .OrderByDescending(t => neighbors.TryGetValue(t.TableName, out var n) ? n.Count : 0)
                    .ToList();
                
                foreach (var table in sortedGroup)
                {
                    // Check if we need to wrap to next row
                    if (columnIndex >= columns)
                    {
                        columnIndex = 0;
                        currentX = padding;
                        currentY += maxHeightInRow + verticalSpacing;
                        maxHeightInRow = 0;
                    }
                    
                    table.X = currentX;
                    table.Y = currentY;
                    table.Width = tableWidth;
                    table.Height = tableHeight;
                    
                    maxHeightInRow = Math.Max(maxHeightInRow, tableHeight);
                    currentX += tableWidth + horizontalSpacing;
                    columnIndex++;
                }
            }

            // Calculate canvas size
            var maxX = Tables.Any() ? Tables.Max(t => t.X + t.Width) : 100;
            var maxY = Tables.Any() ? Tables.Max(t => t.Y + t.Height) : 100;
            CanvasWidth = Math.Max(100, maxX + padding);
            CanvasHeight = Math.Max(100, maxY + padding);

            // Update relationship line positions and edge slot distribution
            CalculateParallelRelationshipOffsets();
        }

        /// <summary>
        /// Layouts the diagram using cluster-based algorithm.
        /// Better for larger models where compact layout is more important.
        /// </summary>
        private void LayoutDiagramClustered()
        {
            // Calculate positions using the cluster-based algorithm
            var positions = CalculateLayoutPositionsFromViewModels();

            // Apply positions
            ApplyLayoutPositions(positions);

            // Update relationship line positions and edge slot distribution
            CalculateParallelRelationshipOffsets();
        }

        /// <summary>
        /// Assigns tables to layers using longest-path layering (Sugiyama Step 2).
        /// The "one" side of relationships (dimensions) are placed above the "many" side (facts).
        /// </summary>
        private List<List<ModelDiagramTableViewModel>> AssignLayers()
        {
            var layers = new List<List<ModelDiagramTableViewModel>>();
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
                if (rel.FromCardinality == "1" && (rel.ToCardinality == "*" || rel.ToCardinality == "M"))
                {
                    fromTable = rel.FromTable;
                    toTable = rel.ToTable;
                }
                else if (rel.ToCardinality == "1" && (rel.FromCardinality == "*" || rel.FromCardinality == "M"))
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
            var queue = new Queue<(ModelDiagramTableViewModel table, int layer)>();
            
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
                    layers.Add(new List<ModelDiagramTableViewModel>());
                
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
        private void MinimizeCrossings(List<List<ModelDiagramTableViewModel>> layers)
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
        private void OptimizeCrossingsWithSwaps(List<List<ModelDiagramTableViewModel>> layers, 
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
        private int CountCrossingsForPair(List<ModelDiagramTableViewModel> layer, int pos1, int pos2,
            List<ModelDiagramTableViewModel> upperLayer, List<ModelDiagramTableViewModel> lowerLayer,
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
        private int CountCrossingsBetweenTables(ModelDiagramTableViewModel table1, int pos1,
            ModelDiagramTableViewModel table2, int pos2,
            List<ModelDiagramTableViewModel> adjacentLayer,
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
        /// Orders a layer based on the average position of neighbors in the reference layer (barycenter).
        /// </summary>
        private void OrderLayerByBarycenter(List<ModelDiagramTableViewModel> layer, 
            List<ModelDiagramTableViewModel> referenceLayer, 
            Dictionary<string, HashSet<string>> neighbors)
        {
            // Calculate barycenter for each table in the layer
            var barycenters = new Dictionary<ModelDiagramTableViewModel, double>();
            
            for (int i = 0; i < referenceLayer.Count; i++)
            {
                // Position index of reference table
                var refTable = referenceLayer[i];
                if (!neighbors.ContainsKey(refTable.TableName)) continue;
                
                foreach (var neighborName in neighbors[refTable.TableName])
                {
                    var neighborTable = layer.FirstOrDefault(t => t.TableName.Equals(neighborName, StringComparison.OrdinalIgnoreCase));
                    if (neighborTable != null)
                    {
                        if (!barycenters.ContainsKey(neighborTable))
                            barycenters[neighborTable] = 0;
                        barycenters[neighborTable] += i;
                    }
                }
            }
            
            // Normalize by number of neighbors
            foreach (var table in layer)
            {
                if (barycenters.ContainsKey(table) && neighbors.ContainsKey(table.TableName))
                {
                    int neighborCount = neighbors[table.TableName].Count(n => 
                        referenceLayer.Any(r => r.TableName.Equals(n, StringComparison.OrdinalIgnoreCase)));
                    if (neighborCount > 0)
                        barycenters[table] /= neighborCount;
                }
                else
                {
                    // Tables with no neighbors get a high barycenter (placed at end)
                    barycenters[table] = double.MaxValue / 2;
                }
            }
            
            // Sort layer by barycenter
            var sorted = layer.OrderBy(t => barycenters.TryGetValue(t, out var bc) ? bc : double.MaxValue / 2)
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
        private void AssignCoordinates(List<List<ModelDiagramTableViewModel>> layers,
            double tableWidth, double tableHeight, double hSpacing, double vSpacing, double padding)
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
                    table.Height = tableHeight;
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
        private void AdjustLayerPositions(List<ModelDiagramTableViewModel> layer,
            List<ModelDiagramTableViewModel> referenceLayer,
            Dictionary<string, HashSet<string>> neighbors,
            double tableWidth, double hSpacing, double padding)
        {
            // Calculate target X for each table based on average neighbor position
            var targetX = new Dictionary<ModelDiagramTableViewModel, double>();
            
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
        private void CenterAllLayers(List<List<ModelDiagramTableViewModel>> layers,
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
                double minX = layer.Min(t => t.X);
                double targetMinX = padding + offset;
                double shift = targetMinX - minX;
                
                foreach (var table in layer)
                {
                    table.X += shift;
                }
            }
        }

        /// <summary>
        /// Re-layouts the diagram (can be called after table positions are changed).
        /// </summary>
        public void RefreshLayout()
        {
            // Update relationship visibility based on table visibility
            UpdateRelationshipVisibility();

            // Recalculate edge slot distribution for parallel relationships
            // This must be done BEFORE UpdatePath() so that relationships connecting 
            // to the same edge are properly sorted and spaced based on current table positions
            CalculateParallelRelationshipOffsets();

            // Update relationship line positions
            foreach (var rel in Relationships)
            {
                rel.UpdatePath();
            }

            // Recalculate canvas size
            UpdateCanvasSize();
        }

        /// <summary>
        /// Updates relationship paths connected to a specific table.
        /// Call this after a table's position or size changes.
        /// </summary>
        public void UpdateRelationshipsForTable(ModelDiagramTableViewModel table)
        {
            if (table == null) return;

            foreach (var rel in Relationships)
            {
                if (rel.FromTableViewModel == table || rel.ToTableViewModel == table)
                {
                    rel.UpdatePath();
                }
            }
        }

        /// <summary>
        /// Targeted layout refresh for a single dragged table.
        /// Only recalculates edge slots and paths for the dragged table and its direct neighbors,
        /// avoiding a full O(tables × relationships) recalculation on every mouse move.
        /// </summary>
        public void RefreshLayoutForTable(ModelDiagramTableViewModel draggedTable)
        {
            if (draggedTable == null) return;

            // Find all relationships connected to the dragged table
            var affectedRels = new List<ModelDiagramRelationshipViewModel>();
            foreach (var rel in Relationships)
            {
                if (rel.FromTableViewModel == draggedTable || rel.ToTableViewModel == draggedTable)
                {
                    affectedRels.Add(rel);
                }
            }

            // Update edge types for affected relationships only
            foreach (var rel in affectedRels)
            {
                rel.UpdatePath();
            }

            // Build the set of affected tables: dragged table + all neighbors connected via relationships
            var affectedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { draggedTable.TableName };
            foreach (var rel in affectedRels)
            {
                affectedTableNames.Add(rel.FromTable);
                affectedTableNames.Add(rel.ToTable);
            }

            // Recalculate edge slots only for affected tables
            CalculateEdgeSlotsForTables(affectedTableNames);

            // Update canvas size
            UpdateCanvasSize();
        }

        /// <summary>
        /// Targeted layout refresh for multiple dragged tables (multi-select drag).
        /// Only recalculates edge slots and paths for the dragged tables and their direct neighbors.
        /// </summary>
        public void RefreshLayoutForTables(IEnumerable<ModelDiagramTableViewModel> draggedTables)
        {
            if (draggedTables == null) return;

            var draggedSet = new HashSet<ModelDiagramTableViewModel>(draggedTables);
            if (draggedSet.Count == 0) return;

            // Find all relationships connected to any dragged table
            var affectedRels = new List<ModelDiagramRelationshipViewModel>();
            foreach (var rel in Relationships)
            {
                if (draggedSet.Contains(rel.FromTableViewModel) || draggedSet.Contains(rel.ToTableViewModel))
                {
                    affectedRels.Add(rel);
                }
            }

            // Update edge types for affected relationships only
            foreach (var rel in affectedRels)
            {
                rel.UpdatePath();
            }

            // Build the set of affected tables: all dragged tables + all their neighbors
            var affectedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in draggedSet)
                affectedTableNames.Add(t.TableName);
            foreach (var rel in affectedRels)
            {
                affectedTableNames.Add(rel.FromTable);
                affectedTableNames.Add(rel.ToTable);
            }

            // Recalculate edge slots only for affected tables
            CalculateEdgeSlotsForTables(affectedTableNames);

            // Update canvas size
            UpdateCanvasSize();
        }

        /// <summary>
        /// Updates CanvasWidth and CanvasHeight based on current table positions.
        /// </summary>
        private void UpdateCanvasSize()
        {
            if (Tables.Count > 0)
            {
                var maxX = Tables.Max(t => t.X + t.Width);
                var maxY = Tables.Max(t => t.Y + t.Height);
                CanvasWidth = Math.Max(100, maxX + 40);
                CanvasHeight = Math.Max(100, maxY + 40);
            }
        }

        /// <summary>
        /// Updates relationship visibility based on connected table visibility.
        /// Relationships are hidden if either connected table is hidden.
        /// </summary>
        private void UpdateRelationshipVisibility()
        {
            foreach (var rel in Relationships)
            {
                bool fromVisible = rel.FromTableViewModel != null && !rel.FromTableViewModel.IsHidden;
                bool toVisible = rel.ToTableViewModel != null && !rel.ToTableViewModel.IsHidden;
                rel.IsVisible = fromVisible && toVisible;
            }
        }

        /// <summary>
        /// Zooms to fit all tables in the view.
        /// </summary>
        public void ZoomToFit()
        {
            if (Tables.Count == 0) return;

            var contentWidth = Tables.Max(t => t.X + t.Width) + 40;
            var contentHeight = Tables.Max(t => t.Y + t.Height) + 40;

            var scaleX = ViewWidth / contentWidth;
            var scaleY = ViewHeight / contentHeight;
            var newScale = Math.Min(scaleX, scaleY) * 0.95; // 95% to add some margin

            Scale = Math.Max(0.1, Math.Min(2.0, newScale));
        }

        /// <summary>
        /// Zooms to fit only selected tables in the view.
        /// </summary>
        public void ZoomToSelection()
        {
            var tablesToFit = SelectedTables.Count > 0 ? SelectedTables.ToList() : (_selectedTable != null ? new List<ModelDiagramTableViewModel> { _selectedTable } : null);
            if (tablesToFit == null || tablesToFit.Count == 0) return;

            var minX = tablesToFit.Min(t => t.X);
            var minY = tablesToFit.Min(t => t.Y);
            var maxX = tablesToFit.Max(t => t.X + t.Width);
            var maxY = tablesToFit.Max(t => t.Y + t.Height);

            var contentWidth = maxX - minX + 80;
            var contentHeight = maxY - minY + 80;

            var scaleX = ViewWidth / contentWidth;
            var scaleY = ViewHeight / contentHeight;
            var newScale = Math.Min(scaleX, scaleY) * 0.9; // 90% to add some margin

            Scale = Math.Max(0.1, Math.Min(2.0, newScale));
            
            // Request scroll to the selection center (handled by view)
            OnScrollToRequested?.Invoke(minX + contentWidth / 2 - 40, minY + contentHeight / 2 - 40);
        }

        /// <summary>
        /// Event raised when scrolling to a position is requested.
        /// </summary>
        public event Action<double, double> OnScrollToRequested;

        /// <summary>
        /// Resets zoom to 100%.
        /// </summary>
        public void ResetZoom()
        {
            Scale = 1.0;
        }

        /// <summary>
        /// Zooms in by 10%.
        /// </summary>
        public void ZoomIn()
        {
            Scale = Math.Min(2.0, Scale + 0.1);
        }

        /// <summary>
        /// Zooms out by 10%.
        /// </summary>
        public void ZoomOut()
        {
            Scale = Math.Max(0.1, Scale - 0.1);
        }

        /// <summary>
        /// Sets zoom to a specific level.
        /// </summary>
        public void SetZoom(double scale)
        {
            Scale = Math.Max(0.1, Math.Min(2.0, scale));
        }

        /// <summary>
        /// Auto-arranges tables in a grid layout.
        /// </summary>
        public void AutoArrange()
        {
            SaveLayoutForUndo();
            LayoutDiagram();
            RefreshLayout();
            SaveCurrentLayout();
        }

        /// <summary>
        /// Clears the saved layout for this model and re-applies auto-arrange.
        /// </summary>
        public void ClearSavedLayout()
        {
            if (string.IsNullOrEmpty(_currentModelKey)) return;

            try
            {
                var layouts = LoadLayoutCache();
                if (layouts.Remove(_currentModelKey))
                {
                    // Save the updated cache
                    var directory = Path.GetDirectoryName(LayoutCacheFilePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    var json = JsonConvert.SerializeObject(layouts, Formatting.None);
                    File.WriteAllText(LayoutCacheFilePath, json);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to clear saved layout");
            }

            // Re-apply auto layout
            LayoutDiagram();
            RefreshLayout();
        }

        /// <summary>
        /// Saves the current table positions after a drag operation.
        /// Called from the view when tables are moved.
        /// </summary>
        public void SaveLayoutAfterDrag()
        {
            SaveCurrentLayout();
        }

        #region Copy Operations

        /// <summary>
        /// Copies the table name to clipboard.
        /// </summary>
        public void CopyTableName(ModelDiagramTableViewModel table)
        {
            if (table == null) return;
            try
            {
                ClipboardManager.SetText(table.TableName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy table name to clipboard");
            }
        }

        /// <summary>
        /// Copies the table DAX reference to clipboard (e.g., 'TableName').
        /// </summary>
        public void CopyTableDaxName(ModelDiagramTableViewModel table)
        {
            if (table == null) return;
            try
            {
                ClipboardManager.SetText($"'{table.TableName}'");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy table DAX name to clipboard");
            }
        }

        /// <summary>
        /// Copies the column name to clipboard.
        /// </summary>
        public void CopyColumnName(ModelDiagramColumnViewModel column)
        {
            if (column == null) return;
            try
            {
                ClipboardManager.SetText(column.ColumnName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy column name to clipboard");
            }
        }

        /// <summary>
        /// Copies the column DAX reference to clipboard (e.g., 'TableName'[ColumnName]).
        /// </summary>
        public void CopyColumnDaxName(ModelDiagramColumnViewModel column, ModelDiagramTableViewModel table)
        {
            if (column == null || table == null) return;
            try
            {
                var daxName = column.IsMeasure
                    ? $"[{column.ColumnName}]"
                    : $"'{table.TableName}'[{column.ColumnName}]";
                ClipboardManager.SetText(daxName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy column DAX name to clipboard");
            }
        }

        #endregion

        #region Multi-Selection Operations

        /// <summary>
        /// Toggles selection of a table (for Ctrl+click multi-select).
        /// </summary>
        public void ToggleTableSelection(ModelDiagramTableViewModel table)
        {
            if (table == null) return;

            if (SelectedTables.Contains(table))
            {
                SelectedTables.Remove(table);
                table.IsSelected = false;
            }
            else
            {
                SelectedTables.Add(table);
                table.IsSelected = true;
            }
            NotifyOfPropertyChange(nameof(HasMultipleSelection));
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanHighlightPath));
        }

        /// <summary>
        /// Selects a single table, clearing any multi-selection.
        /// </summary>
        public void SelectSingleTable(ModelDiagramTableViewModel table)
        {
            ClearSelection();
            if (table != null)
            {
                SelectedTables.Add(table);
                table.IsSelected = true;
                _selectedTable = table;
                NotifyOfPropertyChange(nameof(SelectedTable));
            }
            NotifyOfPropertyChange(nameof(HasMultipleSelection));
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanHighlightPath));
            UpdateRelationshipHighlighting();
        }

        private ModelDiagramRelationshipViewModel _selectedRelationship;
        /// <summary>
        /// The currently selected relationship.
        /// </summary>
        public ModelDiagramRelationshipViewModel SelectedRelationship
        {
            get => _selectedRelationship;
            set
            {
                if (_selectedRelationship != null)
                {
                    _selectedRelationship.IsSelected = false;
                }
                _selectedRelationship = value;
                if (_selectedRelationship != null)
                {
                    _selectedRelationship.IsSelected = true;
                }
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Selects a relationship and highlights the connected tables and columns.
        /// </summary>
        public void SelectRelationship(ModelDiagramRelationshipViewModel relationship)
        {
            // Clear table selection and column highlighting
            ClearSelection();
            ClearColumnHighlighting();
            
            // Select the relationship
            SelectedRelationship = relationship;
            
            if (relationship != null)
            {
                // Highlight the connected tables
                foreach (var table in Tables)
                {
                    var isConnected = table.TableName == relationship.FromTable || table.TableName == relationship.ToTable;
                    table.IsDimmed = !isConnected;
                    
                    // Highlight the specific columns used in this relationship
                    if (table.TableName == relationship.FromTable)
                    {
                        var col = FindColumnByName(table, relationship.FromColumn);
                        if (col != null) col.IsHighlighted = true;
                    }
                    // Note: Don't use else - a self-join would have both columns in the same table
                    if (table.TableName == relationship.ToTable)
                    {
                        var col = FindColumnByName(table, relationship.ToColumn);
                        if (col != null) col.IsHighlighted = true;
                    }
                }
                
                // Highlight this relationship, dim others
                foreach (var rel in Relationships)
                {
                    rel.IsHighlighted = rel == relationship;
                    rel.IsDimmed = rel != relationship;
                }
            }
        }

        /// <summary>
        /// Clears highlighting from all columns.
        /// </summary>
        private void ClearColumnHighlighting()
        {
            foreach (var table in Tables)
            {
                foreach (var col in table.Columns)
                {
                    col.IsHighlighted = false;
                }
            }
        }

        /// <summary>
        /// Finds a column in a table by name, trying multiple matching strategies.
        /// Handles differences between internal names (with underscores) and display names (with spaces).
        /// </summary>
        private ModelDiagramColumnViewModel FindColumnByName(ModelDiagramTableViewModel table, string columnName)
        {
            if (table == null || string.IsNullOrEmpty(columnName)) return null;
            
            // Try exact match on ColumnName first
            var col = table.Columns.FirstOrDefault(c => 
                string.Equals(c.ColumnName, columnName, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col;
            
            // Try matching on Caption
            col = table.Columns.FirstOrDefault(c => 
                string.Equals(c.Caption, columnName, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col;
            
            // Try with underscores replaced by spaces (internal name -> display name)
            var nameWithSpaces = columnName.Replace("_", " ");
            col = table.Columns.FirstOrDefault(c => 
                string.Equals(c.ColumnName, nameWithSpaces, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Caption, nameWithSpaces, StringComparison.OrdinalIgnoreCase));
            if (col != null) return col;
            
            // Try with spaces replaced by underscores (display name -> internal name)
            var nameWithUnderscores = columnName.Replace(" ", "_");
            col = table.Columns.FirstOrDefault(c => 
                string.Equals(c.ColumnName, nameWithUnderscores, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Caption, nameWithUnderscores, StringComparison.OrdinalIgnoreCase));
            
            return col;
        }

        /// <summary>
        /// Highlights the columns involved in a relationship when hovering over it.
        /// Does not select the relationship or dim other tables.
        /// </summary>
        public void HoverRelationship(ModelDiagramRelationshipViewModel relationship)
        {
            // Don't change hover highlighting if a relationship is already selected
            if (SelectedRelationship != null) return;
            
            ClearColumnHighlighting();
            
            if (relationship != null)
            {
                // Highlight the specific columns used in this relationship
                foreach (var table in Tables)
                {
                    if (table.TableName == relationship.FromTable)
                    {
                        var col = FindColumnByName(table, relationship.FromColumn);
                        if (col != null) col.IsHighlighted = true;
                    }
                    // Note: Don't use else - a self-join would have both columns in the same table
                    if (table.TableName == relationship.ToTable)
                    {
                        var col = FindColumnByName(table, relationship.ToColumn);
                        if (col != null) col.IsHighlighted = true;
                    }
                }
            }
        }

        /// <summary>
        /// Clears column highlighting when mouse leaves a relationship.
        /// Only clears if no relationship is selected.
        /// </summary>
        public void UnhoverRelationship()
        {
            // Don't clear highlighting if a relationship is selected
            if (SelectedRelationship != null) return;
            
            ClearColumnHighlighting();
        }

        /// <summary>
        /// Clears all table selections.
        /// </summary>
        public void ClearSelection()
        {
            foreach (var table in SelectedTables)
            {
                table.IsSelected = false;
            }
            SelectedTables.Clear();
            _selectedTable = null;
            SelectedRelationship = null; // Also clear relationship selection
            ClearColumnHighlighting(); // Clear column highlighting
            NotifyOfPropertyChange(nameof(SelectedTable));
            NotifyOfPropertyChange(nameof(HasMultipleSelection));
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanHighlightPath));
            UpdateRelationshipHighlighting(); // Reset all dimming
        }

        /// <summary>
        /// Selects all tables in the diagram.
        /// </summary>
        public void SelectAllTables()
        {
            ClearSelection();
            foreach (var table in Tables)
            {
                SelectedTables.Add(table);
                table.IsSelected = true;
            }
            NotifyOfPropertyChange(nameof(HasMultipleSelection));
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanHighlightPath));
        }

        /// <summary>
        /// Nudges selected tables by the specified delta.
        /// </summary>
        public void NudgeSelectedTables(double deltaX, double deltaY)
        {
            if (SelectedTables.Count == 0 && _selectedTable != null)
            {
                _selectedTable.X = Math.Max(0, _selectedTable.X + deltaX);
                _selectedTable.Y = Math.Max(0, _selectedTable.Y + deltaY);
            }
            else
            {
                foreach (var table in SelectedTables)
                {
                    table.X = Math.Max(0, table.X + deltaX);
                    table.Y = Math.Max(0, table.Y + deltaY);
                }
            }
            RefreshLayout();
            SaveCurrentLayout();
        }

        /// <summary>
        /// Hides all selected tables from the diagram.
        /// </summary>
        public void HideSelectedTables()
        {
            var tablesToHide = SelectedTables.Count > 0 
                ? SelectedTables.ToList() 
                : (_selectedTable != null ? new List<ModelDiagramTableViewModel> { _selectedTable } : new List<ModelDiagramTableViewModel>());
            
            foreach (var table in tablesToHide)
            {
                table.IsHidden = true;
            }
            
            ClearSelection();
            RefreshLayout();
        }

        /// <summary>
        /// Hides a single table from the diagram.
        /// </summary>
        public void HideTable(ModelDiagramTableViewModel table)
        {
            if (table != null)
            {
                table.IsHidden = true;
                
                // Deselect if selected
                if (_selectedTable == table)
                {
                    _selectedTable = null;
                    NotifyOfPropertyChange(nameof(SelectedTable));
                }
                SelectedTables.Remove(table);
                
                RefreshLayout();
            }
        }

        /// <summary>
        /// Finds all tables reachable from the specified table via relationships (transitive closure).
        /// Uses BFS to traverse the relationship graph.
        /// </summary>
        private HashSet<string> FindRelatedTablesClosure(string startTableName)
        {
            var relatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(startTableName)) return relatedTables;

            var neighbors = BuildNeighborMap();
            var queue = new Queue<string>();
            queue.Enqueue(startTableName);
            relatedTables.Add(startTableName);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (neighbors.TryGetValue(current, out var currentNeighbors))
                {
                    foreach (var neighbor in currentNeighbors)
                    {
                        if (relatedTables.Add(neighbor))
                        {
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return relatedTables;
        }

        /// <summary>
        /// Finds all tables directly related (1-hop) to the specified table.
        /// </summary>
        private HashSet<string> FindDirectlyRelatedTables(string tableName)
        {
            var relatedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(tableName)) return relatedTables;

            relatedTables.Add(tableName);
            foreach (var rel in Relationships)
            {
                if (string.Equals(rel.FromTable, tableName, StringComparison.OrdinalIgnoreCase))
                    relatedTables.Add(rel.ToTable ?? "");
                else if (string.Equals(rel.ToTable, tableName, StringComparison.OrdinalIgnoreCase))
                    relatedTables.Add(rel.FromTable ?? "");
            }

            return relatedTables;
        }

        /// <summary>
        /// Shows tables that are directly related to the specified table (unhides them).
        /// </summary>
        public void ShowRelatedTables(ModelDiagramTableViewModel table)
        {
            if (table == null) return;

            var relatedTables = FindDirectlyRelatedTables(table.TableName);

            foreach (var t in Tables)
            {
                if (relatedTables.Contains(t.TableName))
                    t.IsHidden = false;
            }

            RefreshLayout();
        }

        /// <summary>
        /// Isolates the specified table and all tables reachable through relationships,
        /// hiding everything else. Uses transitive closure to find the full connected subgraph.
        /// </summary>
        public void IsolateRelatedTables(ModelDiagramTableViewModel table)
        {
            if (table == null) return;

            SaveLayoutForUndo();
            var relatedTables = FindRelatedTablesClosure(table.TableName);

            foreach (var t in Tables)
            {
                t.IsHidden = !relatedTables.Contains(t.TableName);
            }

            ClearSelection();
            RefreshLayout();
            SaveCurrentLayout();
        }

        /// <summary>
        /// Shows only the specified set of tables, hiding all others.
        /// Optionally includes tables that bridge them via relationships.
        /// Used by Server Timings integration to show query-dependent tables.
        /// </summary>
        /// <param name="tableNames">The table names to show.</param>
        /// <param name="includeRelated">If true, also show tables connected via relationships to form a complete subgraph.</param>
        public void ShowOnlyTables(IEnumerable<string> tableNames, bool includeRelated = false)
        {
            if (tableNames == null) return;

            SaveLayoutForUndo();

            var tablesToShow = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);

            if (includeRelated)
            {
                // Also include tables that lie on relationship paths between the specified tables
                var neighbors = BuildNeighborMap();
                var expanded = new HashSet<string>(tablesToShow, StringComparer.OrdinalIgnoreCase);

                // For each specified table, add its direct neighbors that are also neighbors of other specified tables
                // This connects the subgraph without pulling in unrelated tables
                foreach (var tableName in tablesToShow)
                {
                    if (neighbors.TryGetValue(tableName, out var tableNeighbors))
                    {
                        foreach (var neighbor in tableNeighbors)
                        {
                            expanded.Add(neighbor);
                        }
                    }
                }
                tablesToShow = expanded;
            }

            int hiddenCount = 0;
            foreach (var table in Tables)
            {
                bool shouldShow = tablesToShow.Contains(table.TableName);
                table.IsHidden = !shouldShow;
                if (!shouldShow) hiddenCount++;
            }

            // Reset filter dropdown (we're doing a custom filter)
            _tableFilter = 0;
            NotifyOfPropertyChange(nameof(TableFilter));

            ClearSelection();
            RefreshLayout();

            Log.Information("{class} {method} Showing {shown} of {total} tables ({hidden} hidden)",
                nameof(ModelDiagramViewModel), nameof(ShowOnlyTables),
                Tables.Count - hiddenCount, Tables.Count, hiddenCount);
        }

        /// <summary>
        /// Hides all tables that are NOT selected, keeping only the selected tables visible.
        /// </summary>
        public void HideNonSelectedTables()
        {
            var selectedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Collect selected table names
            if (SelectedTables.Count > 0)
            {
                foreach (var table in SelectedTables)
                {
                    selectedTableNames.Add(table.TableName);
                }
            }
            else if (_selectedTable != null)
            {
                selectedTableNames.Add(_selectedTable.TableName);
            }
            
            // Hide all non-selected tables
            if (selectedTableNames.Count > 0)
            {
                foreach (var table in Tables)
                {
                    if (!selectedTableNames.Contains(table.TableName))
                    {
                        table.IsHidden = true;
                    }
                }
                
                ClearSelection();
                RefreshLayout();
            }
        }

        /// <summary>
        /// Shows all hidden tables.
        /// </summary>
        public void ShowAllTables()
        {
            foreach (var table in Tables)
            {
                table.IsHidden = false;
            }
            // Reset filter to "All Tables"
            _tableFilter = 0;
            NotifyOfPropertyChange(nameof(TableFilter));
            RefreshLayout();
        }

        #region Annotations

        private ModelDiagramAnnotationViewModel _selectedAnnotation;
        /// <summary>
        /// The currently selected annotation.
        /// </summary>
        public ModelDiagramAnnotationViewModel SelectedAnnotation
        {
            get => _selectedAnnotation;
            set
            {
                if (_selectedAnnotation != null)
                    _selectedAnnotation.IsSelected = false;
                _selectedAnnotation = value;
                if (_selectedAnnotation != null)
                    _selectedAnnotation.IsSelected = true;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Adds a new annotation at the specified position.
        /// </summary>
        public void AddAnnotation(double x, double y)
        {
            var annotation = new ModelDiagramAnnotationViewModel
            {
                X = x,
                Y = y,
                Text = "Double-click to edit",
                Width = 150,
                Height = 60
            };
            Annotations.Add(annotation);
            SelectedAnnotation = annotation;
            annotation.IsEditing = true;
        }

        /// <summary>
        /// Adds a new annotation at the center of the visible area.
        /// Called from context menu.
        /// </summary>
        public void AddAnnotationAtCenter()
        {
            // Place in visible area - use a reasonable default position
            double x = 100;
            double y = 100;
            
            // Try to offset from existing annotations to avoid overlap
            if (Annotations.Count > 0)
            {
                var lastAnnotation = Annotations.Last();
                x = lastAnnotation.X + 20;
                y = lastAnnotation.Y + 20;
            }
            
            AddAnnotation(x, y);
        }

        /// <summary>
        /// Deletes the selected annotation.
        /// </summary>
        public void DeleteSelectedAnnotation()
        {
            if (_selectedAnnotation != null)
            {
                Annotations.Remove(_selectedAnnotation);
                SelectedAnnotation = null;
            }
        }

        /// <summary>
        /// Deletes a specific annotation.
        /// </summary>
        public void DeleteAnnotation(ModelDiagramAnnotationViewModel annotation)
        {
            if (annotation != null)
            {
                if (_selectedAnnotation == annotation)
                    SelectedAnnotation = null;
                Annotations.Remove(annotation);
            }
        }

        /// <summary>
        /// Clears all annotations.
        /// </summary>
        public void ClearAllAnnotations()
        {
            SelectedAnnotation = null;
            Annotations.Clear();
        }

        /// <summary>
        /// Sets the background color of the specified annotation.
        /// </summary>
        public void SetAnnotationColor(ModelDiagramAnnotationViewModel annotation, string color)
        {
            if (annotation != null)
            {
                annotation.BackgroundColor = color;
                SaveLayoutAfterDrag();
            }
        }

        #endregion

        /// <summary>
        /// Highlights the path between two selected tables.
        /// Uses BFS to find the shortest relationship path.
        /// </summary>
        public void HighlightPath()
        {
            if (SelectedTables.Count != 2) return;
            
            var startTable = SelectedTables[0].TableName;
            var endTable = SelectedTables[1].TableName;
            
            // Build adjacency list
            var adjacency = new Dictionary<string, List<(string neighbor, ModelDiagramRelationshipViewModel rel)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var table in Tables)
            {
                adjacency[table.TableName] = new List<(string, ModelDiagramRelationshipViewModel)>();
            }
            foreach (var rel in Relationships)
            {
                if (adjacency.ContainsKey(rel.FromTable) && adjacency.ContainsKey(rel.ToTable))
                {
                    adjacency[rel.FromTable].Add((rel.ToTable, rel));
                    adjacency[rel.ToTable].Add((rel.FromTable, rel));
                }
            }
            
            // BFS to find shortest path
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<(string table, List<string> path, List<ModelDiagramRelationshipViewModel> rels)>();
            queue.Enqueue((startTable, new List<string> { startTable }, new List<ModelDiagramRelationshipViewModel>()));
            visited.Add(startTable);
            
            List<string> foundPath = null;
            List<ModelDiagramRelationshipViewModel> foundRels = null;
            
            while (queue.Count > 0)
            {
                var (current, path, rels) = queue.Dequeue();
                
                if (current.Equals(endTable, StringComparison.OrdinalIgnoreCase))
                {
                    foundPath = path;
                    foundRels = rels;
                    break;
                }
                
                foreach (var (neighbor, rel) in adjacency[current])
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        var newPath = new List<string>(path) { neighbor };
                        var newRels = new List<ModelDiagramRelationshipViewModel>(rels) { rel };
                        queue.Enqueue((neighbor, newPath, newRels));
                    }
                }
            }
            
            if (foundPath != null)
            {
                // Highlight the path tables and relationships
                var pathTables = new HashSet<string>(foundPath, StringComparer.OrdinalIgnoreCase);
                var pathRels = new HashSet<ModelDiagramRelationshipViewModel>(foundRels);
                
                // Clear current selection and add all path tables to selection
                SelectedTables.Clear();
                
                foreach (var table in Tables)
                {
                    table.IsDimmed = !pathTables.Contains(table.TableName);
                    table.IsSelected = pathTables.Contains(table.TableName);
                    
                    // Add path tables to selection (including intermediary tables)
                    if (pathTables.Contains(table.TableName))
                    {
                        SelectedTables.Add(table);
                    }
                }
                
                // Update selected table reference
                if (SelectedTables.Count > 0)
                {
                    _selectedTable = SelectedTables[0];
                    NotifyOfPropertyChange(nameof(SelectedTable));
                }
                NotifyOfPropertyChange(nameof(HasMultipleSelection));
                NotifyOfPropertyChange(nameof(HasSelection));
                NotifyOfPropertyChange(nameof(CanHighlightPath));
                
                foreach (var rel in Relationships)
                {
                    rel.IsHighlighted = pathRels.Contains(rel);
                    rel.IsDimmed = !pathRels.Contains(rel);
                }
            }
        }

        /// <summary>
        /// Whether path highlighting is available (exactly 2 tables selected).
        /// </summary>
        public bool CanHighlightPath => SelectedTables.Count == 2;

        /// <summary>
        /// Selects all tables within a rectangle (for drag-select).
        /// </summary>
        public void SelectTablesInRect(double left, double top, double right, double bottom)
        {
            ClearSelection();
            foreach (var table in Tables)
            {
                // Check if table overlaps with selection rectangle
                if (table.X < right && table.X + table.Width > left &&
                    table.Y < bottom && table.Y + table.Height > top)
                {
                    SelectedTables.Add(table);
                    table.IsSelected = true;
                }
            }
            if (SelectedTables.Count == 1)
            {
                _selectedTable = SelectedTables[0];
                NotifyOfPropertyChange(nameof(SelectedTable));
            }
            NotifyOfPropertyChange(nameof(HasMultipleSelection));
            NotifyOfPropertyChange(nameof(HasSelection));
            NotifyOfPropertyChange(nameof(CanHighlightPath));
        }

        /// <summary>
        /// Moves all selected tables by the given delta.
        /// </summary>
        public void MoveSelectedTables(double deltaX, double deltaY)
        {
            foreach (var table in SelectedTables)
            {
                table.X = Math.Max(0, SnapToGridValue(table.X + deltaX));
                table.Y = Math.Max(0, SnapToGridValue(table.Y + deltaY));
            }
            RefreshLayout();
        }

        #endregion

        #region Grouping

        /// <summary>
        /// Available group names for tables.
        /// </summary>
        public BindableCollection<string> AvailableGroups { get; } = new BindableCollection<string>();

        /// <summary>
        /// Groups tables by their name prefix (schema-like grouping).
        /// Tables named like "Dim_Customer" or "Dim.Customer" will be grouped as "Dim".
        /// </summary>
        public void AutoGroupByPrefix()
        {
            AvailableGroups.Clear();
            var groups = new HashSet<string>();

            foreach (var table in Tables)
            {
                var name = table.TableName;
                var prefix = ExtractPrefix(name);
                if (!string.IsNullOrEmpty(prefix))
                {
                    table.Group = prefix;
                    groups.Add(prefix);
                }
                else
                {
                    table.Group = "Other";
                    groups.Add("Other");
                }
            }

            foreach (var group in groups.OrderBy(g => g))
            {
                AvailableGroups.Add(group);
            }
            NotifyOfPropertyChange(nameof(AvailableGroups));
        }

        /// <summary>
        /// Extracts a prefix from a table name (e.g., "Dim" from "Dim_Customer").
        /// </summary>
        private string ExtractPrefix(string tableName)
        {
            // Try common separators
            foreach (var sep in new[] { '_', '.', ' ' })
            {
                var idx = tableName.IndexOf(sep);
                if (idx > 0 && idx < tableName.Length - 1)
                {
                    var prefix = tableName.Substring(0, idx);
                    // Only use if it looks like a prefix (short, starts with letter)
                    if (prefix.Length <= 10 && char.IsLetter(prefix[0]))
                    {
                        return prefix;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Sets the group for all selected tables.
        /// </summary>
        public void SetGroupForSelectedTables(string groupName)
        {
            foreach (var table in SelectedTables)
            {
                table.Group = groupName;
            }

            if (!string.IsNullOrEmpty(groupName) && !AvailableGroups.Contains(groupName))
            {
                AvailableGroups.Add(groupName);
            }
        }

        /// <summary>
        /// Clears all table groups.
        /// </summary>
        public void ClearAllGroups()
        {
            foreach (var table in Tables)
            {
                table.Group = null;
            }
            AvailableGroups.Clear();
        }

        /// <summary>
        /// Arranges tables into rows by their group.
        /// </summary>
        public void ArrangeByGroup()
        {
            if (!Tables.Any(t => !string.IsNullOrEmpty(t.Group))) return;

            const double tableWidth = 200;
            const double tableHeight = 180;
            const double horizontalSpacing = 100;
            const double verticalSpacing = 100;
            const double groupSpacing = 50;
            const double padding = 40;

            var groupedTables = Tables
                .GroupBy(t => t.Group ?? "Ungrouped")
                .OrderBy(g => g.Key)
                .ToList();

            double currentY = padding;

            foreach (var group in groupedTables)
            {
                var tablesInGroup = group.OrderBy(t => t.TableName).ToList();
                double x = padding;

                foreach (var table in tablesInGroup)
                {
                    table.X = x;
                    table.Y = currentY;
                    table.Width = tableWidth;
                    table.Height = tableHeight;
                    x += tableWidth + horizontalSpacing;
                }

                currentY += tableHeight + verticalSpacing + groupSpacing;
            }

            // Recalculate canvas size
            RefreshLayout();
            SaveCurrentLayout();
        }

        #endregion

        #region Keyboard Navigation

        /// <summary>
        /// Navigates to the next/previous table using arrow keys.
        /// </summary>
        public void NavigateToTable(string direction)
        {
            if (Tables.Count == 0) return;

            var current = _selectedTable ?? Tables.FirstOrDefault();
            if (current == null) return;

            ModelDiagramTableViewModel target = null;
            double bestDistance = double.MaxValue;

            foreach (var table in Tables)
            {
                if (table == current) continue;

                double dx = table.CenterX - current.CenterX;
                double dy = table.CenterY - current.CenterY;

                bool isCandidate = false;
                switch (direction.ToLower())
                {
                    case "left":
                        isCandidate = dx < -10 && Math.Abs(dy) < Math.Abs(dx);
                        break;
                    case "right":
                        isCandidate = dx > 10 && Math.Abs(dy) < Math.Abs(dx);
                        break;
                    case "up":
                        isCandidate = dy < -10 && Math.Abs(dx) < Math.Abs(dy);
                        break;
                    case "down":
                        isCandidate = dy > 10 && Math.Abs(dx) < Math.Abs(dy);
                        break;
                }

                if (isCandidate)
                {
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        target = table;
                    }
                }
            }

            if (target != null)
            {
                SelectSingleTable(target);
            }
        }

        /// <summary>
        /// Cycles through connected tables (Tab key).
        /// </summary>
        public void CycleConnectedTables(bool reverse = false)
        {
            if (_selectedTable == null || Relationships.Count == 0)
            {
                // If nothing selected, select first table
                if (Tables.Count > 0)
                {
                    SelectSingleTable(Tables[0]);
                }
                return;
            }

            // Get all tables connected to current selection
            var connectedTables = new List<ModelDiagramTableViewModel>();
            foreach (var rel in Relationships)
            {
                if (rel.FromTable == _selectedTable.TableName)
                {
                    var toTable = Tables.FirstOrDefault(t => t.TableName == rel.ToTable);
                    if (toTable != null && !connectedTables.Contains(toTable))
                        connectedTables.Add(toTable);
                }
                else if (rel.ToTable == _selectedTable.TableName)
                {
                    var fromTable = Tables.FirstOrDefault(t => t.TableName == rel.FromTable);
                    if (fromTable != null && !connectedTables.Contains(fromTable))
                        connectedTables.Add(fromTable);
                }
            }

            if (connectedTables.Count == 0) return;

            // Sort by name for consistent ordering
            connectedTables = connectedTables.OrderBy(t => t.TableName).ToList();

            // Find current position and move to next
            int currentIndex = -1;
            for (int i = 0; i < connectedTables.Count; i++)
            {
                if (connectedTables[i] == _selectedTable)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = reverse
                ? (currentIndex <= 0 ? connectedTables.Count - 1 : currentIndex - 1)
                : ((currentIndex + 1) % connectedTables.Count);

            SelectSingleTable(connectedTables[nextIndex]);
        }

        /// <summary>
        /// Navigates to the selected table in the metadata pane.
        /// </summary>
        public void JumpToMetadataPane()
        {
            if (_selectedTable != null)
            {
                _eventAggregator.PublishOnUIThreadAsync(new NavigateToMetadataItemEvent(_selectedTable.TableName));
            }
        }

        #endregion

        /// <summary>
        /// Saves the current layout to the cache file.
        /// </summary>
        private void SaveCurrentLayout()
        {
            if (string.IsNullOrEmpty(_currentModelKey) || Tables.Count == 0) return;

            // Don't save if most tables are at origin - the layout hasn't been applied yet
            if (Tables.Count > 1)
            {
                int atOrigin = Tables.Count(t => t.X == 0 && t.Y == 0);
                if (atOrigin > Tables.Count / 2)
                {
                    Log.Warning("{class} {method} Refusing to save layout: {atOrigin}/{total} tables at origin",
                        nameof(ModelDiagramViewModel), nameof(SaveCurrentLayout), atOrigin, Tables.Count);
                    return;
                }
            }

            try
            {
                // Load existing layouts
                var layouts = LoadLayoutCache();

                // Create layout data for current model
                var layoutData = new ModelLayoutData
                {
                    ModelKey = _currentModelKey,
                    LastModified = DateTime.UtcNow,
                    TablePositions = Tables.ToDictionary(
                        t => t.TableName,
                        t => new TablePosition 
                        { 
                            X = t.X, 
                            Y = t.Y, 
                            Width = t.Width, 
                            Height = t.Height, 
                            IsCollapsed = t.IsCollapsed,
                            ExpandedHeight = t.ExpandedHeight
                        }
                    ),
                    Annotations = Annotations.Select(a => a.ToData()).ToList()
                };

                // Update or add
                layouts[_currentModelKey] = layoutData;

                // Prune old entries (keep last 50 models)
                if (layouts.Count > 50)
                {
                    var oldest = layouts.OrderBy(kvp => kvp.Value.LastModified).Take(layouts.Count - 50).Select(kvp => kvp.Key).ToList();
                    foreach (var key in oldest)
                    {
                        layouts.Remove(key);
                    }
                }

                // Save to file
                var directory = Path.GetDirectoryName(LayoutCacheFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(layouts, Formatting.None);
                File.WriteAllText(LayoutCacheFilePath, json);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to save model diagram layout");
            }
        }

        /// <summary>
        /// Tries to load a saved layout for the current model.
        /// </summary>
        /// <returns>True if layout was loaded, false otherwise.</returns>
        private bool TryLoadSavedLayout(bool preserveCollapsedState = false)
        {
            if (string.IsNullOrEmpty(_currentModelKey)) return false;

            try
            {
                var layouts = LoadLayoutCache();
                if (!layouts.TryGetValue(_currentModelKey, out var layoutData)) return false;

                // Validate saved layout isn't corrupted (most positions at 0,0)
                if (layoutData.TablePositions.Count > 1)
                {
                    int atOrigin = layoutData.TablePositions.Values.Count(p => p.X == 0 && p.Y == 0);
                    // If more than half the tables are at the origin, the layout is corrupted
                    if (atOrigin > layoutData.TablePositions.Count / 2)
                    {
                        Log.Warning("{class} {method} Saved layout has {atOrigin}/{total} tables at origin (0,0) - discarding corrupted layout for model '{model}'",
                            nameof(ModelDiagramViewModel), nameof(TryLoadSavedLayout), atOrigin, layoutData.TablePositions.Count, _currentModelKey);
                        return false;
                    }
                }

                // Apply saved positions
                bool anyApplied = false;
                foreach (var table in Tables)
                {
                    if (layoutData.TablePositions.TryGetValue(table.TableName, out var pos))
                    {
                        table.X = pos.X;
                        table.Y = pos.Y;
                        table.Width = pos.Width > 0 ? pos.Width : 200;
                        
                        // If preserveCollapsedState is true (large model), keep tables collapsed
                        // Otherwise, restore from saved layout
                        if (preserveCollapsedState)
                        {
                            // Keep current collapsed state, just update position
                            // Height was already set when IsCollapsed was set
                        }
                        else
                        {
                            table.Height = pos.Height > 0 ? pos.Height : 180;
                            // Use SetCollapsedState to properly restore collapsed state with expanded height
                            if (pos.IsCollapsed)
                            {
                                table.SetCollapsedState(true, pos.ExpandedHeight > 0 ? pos.ExpandedHeight : pos.Height);
                            }
                        }
                        anyApplied = true;
                    }
                }

                // Restore annotations
                if (layoutData.Annotations != null && layoutData.Annotations.Count > 0)
                {
                    Annotations.Clear();
                    foreach (var annotData in layoutData.Annotations)
                    {
                        Annotations.Add(new ModelDiagramAnnotationViewModel(annotData));
                    }
                }

                if (anyApplied)
                {
                    // Calculate canvas size including annotations
                    var maxX = Tables.Max(t => t.X + t.Width);
                    var maxY = Tables.Max(t => t.Y + t.Height);
                    
                    if (Annotations.Count > 0)
                    {
                        maxX = Math.Max(maxX, Annotations.Max(a => a.X + a.Width));
                        maxY = Math.Max(maxY, Annotations.Max(a => a.Y + a.Height));
                    }
                    
                    CanvasWidth = Math.Max(100, maxX + 40);
                    CanvasHeight = Math.Max(100, maxY + 40);

                    // Update relationship paths
                    foreach (var rel in Relationships)
                    {
                        rel.UpdatePath();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load model diagram layout");
            }

            return false;
        }

        /// <summary>
        /// Loads the layout cache from disk.
        /// </summary>
        private Dictionary<string, ModelLayoutData> LoadLayoutCache()
        {
            try
            {
                if (File.Exists(LayoutCacheFilePath))
                {
                    var json = File.ReadAllText(LayoutCacheFilePath);
                    return JsonConvert.DeserializeObject<Dictionary<string, ModelLayoutData>>(json)
                           ?? new Dictionary<string, ModelLayoutData>();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read layout cache file");
            }
            return new Dictionary<string, ModelLayoutData>();
        }

        #endregion

        #region Event Handlers

        public System.Threading.Tasks.Task HandleAsync(MetadataLoadedEvent message, System.Threading.CancellationToken cancellationToken)
        {
            if (message?.Model != null && IsVisible)
            {
                LoadFromModel(message.Model);
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Handles the ViewMetricsCompleteEvent to enrich the diagram with Vertipaq Analyzer data.
        /// </summary>
        public System.Threading.Tasks.Task HandleAsync(ViewMetricsCompleteEvent message, System.Threading.CancellationToken cancellationToken)
        {
            if (message?.VpaModel != null && Tables.Count > 0)
            {
                Log.Information("{class} {method} Enriching diagram with VPA data", nameof(ModelDiagramViewModel), nameof(HandleAsync));
                EnrichFromVertipaq(message.VpaModel);
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Handles the ShowTablesInModelDiagramEvent from Server Timings or SE Dependencies.
        /// Filters the model diagram to show only the specified tables (and optionally their relationship neighbors).
        /// </summary>
        public System.Threading.Tasks.Task HandleAsync(ShowTablesInModelDiagramEvent message, System.Threading.CancellationToken cancellationToken)
        {
            if (message?.TableNames == null) return System.Threading.Tasks.Task.CompletedTask;

            if (Tables.Count > 0)
            {
                Log.Information("{class} {method} Filtering diagram to {count} tables",
                    nameof(ModelDiagramViewModel), nameof(HandleAsync), message.TableNames.Count());
                ShowOnlyTables(message.TableNames, message.IncludeRelated);
            }
            else
            {
                // Diagram is still loading — store the filter to apply when loading completes
                Log.Information("{class} {method} Diagram not loaded yet, storing pending table filter ({count} tables)",
                    nameof(ModelDiagramViewModel), nameof(HandleAsync), message.TableNames.Count());
                _pendingTableFilter = message;
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Applies any pending table filter that was received while the diagram was still loading.
        /// </summary>
        private void ApplyPendingTableFilter()
        {
            if (_pendingTableFilter != null)
            {
                Log.Information("{class} {method} Applying pending table filter ({count} tables)",
                    nameof(ModelDiagramViewModel), nameof(ApplyPendingTableFilter), _pendingTableFilter.TableNames.Count());
                ShowOnlyTables(_pendingTableFilter.TableNames, _pendingTableFilter.IncludeRelated);
                _pendingTableFilter = null;
            }
        }

        #endregion

        #region Enrichment (Admin/VPA Data)

        private bool _hasVertipaqData;
        /// <summary>
        /// Whether Vertipaq Analyzer data has been loaded into the diagram.
        /// </summary>
        public bool HasVertipaqData
        {
            get => _hasVertipaqData;
            private set { _hasVertipaqData = value; NotifyOfPropertyChange(); }
        }

        private bool _hasStorageModeData;
        /// <summary>
        /// Whether storage mode data (from TOM/BIM) has been loaded into the diagram.
        /// </summary>
        public bool HasStorageModeData
        {
            get => _hasStorageModeData;
            private set { _hasStorageModeData = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Gets or sets which statistic to display on columns after VPA enrichment.
        /// This property persists to user options.
        /// </summary>
        public DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay ColumnStatDisplay
        {
            get => _options.DiagramColumnStatDisplay;
            set
            {
                if (_options.DiagramColumnStatDisplay != value)
                {
                    _options.DiagramColumnStatDisplay = value;
                    NotifyOfPropertyChange();
                    // Update all columns to reflect the change
                    foreach (var table in Tables)
                    {
                        foreach (var col in table.Columns)
                        {
                            col.NotifyStatDisplayChanged();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the sort order for columns in tables.
        /// This property persists to user options.
        /// </summary>
        public DaxStudio.Interfaces.Enums.DiagramColumnSortOrder ColumnSortOrder
        {
            get => _options.DiagramColumnSortOrder;
            set
            {
                if (_options.DiagramColumnSortOrder != value)
                {
                    _options.DiagramColumnSortOrder = value;
                    NotifyOfPropertyChange();
                    // Re-sort columns in all tables
                    foreach (var table in Tables)
                    {
                        table.UpdateColumnSort(_sortKeyColumnsFirst, value);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets the layout algorithm for table arrangement.
        /// This property persists to user options.
        /// </summary>
        public DaxStudio.Interfaces.Enums.DiagramLayoutAlgorithm LayoutAlgorithm
        {
            get => _options.DiagramLayoutAlgorithm;
            set
            {
                if (_options.DiagramLayoutAlgorithm != value)
                {
                    _options.DiagramLayoutAlgorithm = value;
                    NotifyOfPropertyChange();
                    
                    // Re-layout the diagram with the new algorithm
                    if (Tables.Count > 0)
                    {
                        SaveLayoutForUndo(); // Save current state for undo
                        LayoutDiagram();
                        CalculateParallelRelationshipOffsets();
                    }
                }
            }
        }

        /// <summary>
        /// Exports detailed debug information about the Model Diagram to a text file.
        /// This includes table, column, and relationship data for troubleshooting.
        /// </summary>
        public void ExportDebugInfo()
        {
            if (Tables.Count == 0)
            {
                System.Windows.MessageBox.Show("No model data to export. Load a model first.", "Export Debug Info", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Text files|*.txt|All files|*.*",
                Title = "Export Model Diagram Debug Info",
                FileName = $"ModelDiagram_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("========================================");
                    sb.AppendLine("MODEL DIAGRAM DEBUG EXPORT");
                    sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    sb.AppendLine("========================================");
                    sb.AppendLine();

                    // Summary
                    sb.AppendLine("=== SUMMARY ===");
                    sb.AppendLine($"Model Key: {_currentModelKey}");
                    sb.AppendLine($"Is Offline (VPA): {_isOfflineMode}");
                    sb.AppendLine($"Tables: {Tables.Count}");
                    sb.AppendLine($"Relationships: {Relationships.Count}");
                    sb.AppendLine($"Has VPA Data: {HasVertipaqData}");
                    sb.AppendLine($"Has Storage Mode Data: {HasStorageModeData}");
                    sb.AppendLine($"Layout Algorithm: {_options?.DiagramLayoutAlgorithm}");
                    sb.AppendLine($"CSDL Version: {_model?.CSDLVersion ?? 0}");
                    sb.AppendLine($"Raw ADOTabular Relationship Total: {_model?.Tables?.Sum(t => t.Relationships?.Count ?? 0) ?? -1}");
                    sb.AppendLine($"Model-level Relationships: {_model?.Relationships?.Count ?? -1}");
                    sb.AppendLine();

                    // Tables
                    sb.AppendLine("=== TABLES ===");
                    foreach (var table in Tables.OrderBy(t => t.TableName))
                    {
                        sb.AppendLine($"  [{table.TableName}]");
                        sb.AppendLine($"    IsVisible: {table.IsVisible}, IsDateTable: {table.IsDateTable}");
                        sb.AppendLine($"    IsCollapsed: {table.IsCollapsed}, RelationshipCount: {table.RelationshipCount}");
                        sb.AppendLine($"    Position: X={table.X:F0}, Y={table.Y:F0}, W={table.Width:F0}, H={table.Height:F0}");
                        sb.AppendLine($"    StorageMode: {table.StorageMode}");
                        sb.AppendLine($"    RowCount: {table.RowCount}, TableSizeBytes: {table.TableSizeBytes}");
                        sb.AppendLine($"    Columns ({table.Columns.Count}):");
                        foreach (var col in table.Columns.Take(20)) // Limit to first 20 columns
                        {
                            sb.AppendLine($"      - {col.ColumnName} ({col.DataTypeName}) [IsKey:{col.IsKey}, IsMeasure:{col.IsMeasure}, IsRelCol:{col.IsRelationshipColumn}]");
                        }
                        if (table.Columns.Count > 20)
                        {
                            sb.AppendLine($"      ... and {table.Columns.Count - 20} more columns");
                        }
                    }
                    sb.AppendLine();

                    // Relationships
                    sb.AppendLine("=== RELATIONSHIPS ===");
                    foreach (var rel in Relationships)
                    {
                        sb.AppendLine($"  [{rel.FromTable}].[{rel.FromColumn}] --> [{rel.ToTable}].[{rel.ToColumn}]");
                        sb.AppendLine($"    Cardinality: {rel.FromCardinality}:{rel.ToCardinality}");
                        sb.AppendLine($"    IsActive: {rel.IsActive}");
                        sb.AppendLine($"    IsBidirectional: {rel.IsBidirectional}");
                        sb.AppendLine($"    IsManyToMany: {rel.IsManyToMany}");
                        sb.AppendLine($"    IsVisible: {rel.IsVisible}");
                        sb.AppendLine($"    HasCenterIndicators: {rel.HasCenterIndicators}");
                        sb.AppendLine($"    CrossFilterDirection (raw): {rel.CrossFilterDirectionRaw}");
                        sb.AppendLine($"    StartEdge: {rel.StartEdgeType}, EndEdge: {rel.EndEdgeType}");
                        sb.AppendLine($"    StartSlotOffset: {rel.StartEdgeSlotOffset:F1}, EndSlotOffset: {rel.EndEdgeSlotOffset:F1}");
                        sb.AppendLine($"    FromCardinalityY: {rel.FromCardinalityY:F1}, ToCardinalityY: {rel.ToCardinalityY:F1}");
                        sb.AppendLine($"    ActualStartY: {rel.ActualStartY:F1}, ActualEndY: {rel.ActualEndY:F1}");
                    }
                    sb.AppendLine();

                    // Edge slot diagnostics
                    sb.AppendLine("=== EDGE SLOT DIAGNOSTICS ===");
                    foreach (var table in Tables.OrderBy(t => t.TableName))
                    {
                        // Group relationships by edge for this table
                        var fromRelsByEdge = Relationships
                            .Where(r => string.Equals(r.FromTable, table.TableName, StringComparison.OrdinalIgnoreCase))
                            .GroupBy(r => r.StartEdgeType)
                            .ToDictionary(g => g.Key, g => g.ToList());
                        var toRelsByEdge = Relationships
                            .Where(r => string.Equals(r.ToTable, table.TableName, StringComparison.OrdinalIgnoreCase))
                            .GroupBy(r => r.EndEdgeType)
                            .ToDictionary(g => g.Key, g => g.ToList());

                        foreach (var edgeType in new[] { EdgeTypePublic.Left, EdgeTypePublic.Right, EdgeTypePublic.Top, EdgeTypePublic.Bottom })
                        {
                            var fromRels = fromRelsByEdge.TryGetValue(edgeType, out var fr) ? fr : new List<ModelDiagramRelationshipViewModel>();
                            var toRels = toRelsByEdge.TryGetValue(edgeType, out var tr) ? tr : new List<ModelDiagramRelationshipViewModel>();
                            int totalCount = fromRels.Count + toRels.Count;
                            if (totalCount > 1)
                            {
                                sb.AppendLine($"  [{table.TableName}] {edgeType} edge: {totalCount} relationships");
                                sb.AppendLine($"    Table Position: Y={table.Y:F0}, Height={table.Height:F0}, CenterY={table.CenterY:F0}");

                                // Sort and show relationships with their positions
                                bool isVerticalEdge = (edgeType == EdgeTypePublic.Left || edgeType == EdgeTypePublic.Right);
                                var allRels = new List<(ModelDiagramRelationshipViewModel rel, bool isFrom, string otherTable, double otherPos, double slotOffset)>();

                                var tableDict = Tables.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);
                                foreach (var rel in fromRels)
                                {
                                    if (tableDict.TryGetValue(rel.ToTable, out var otherTbl))
                                    {
                                        double pos = isVerticalEdge ? otherTbl.CenterY : otherTbl.CenterX;
                                        allRels.Add((rel, true, rel.ToTable, pos, rel.StartEdgeSlotOffset));
                                    }
                                }
                                foreach (var rel in toRels)
                                {
                                    if (tableDict.TryGetValue(rel.FromTable, out var otherTbl))
                                    {
                                        double pos = isVerticalEdge ? otherTbl.CenterY : otherTbl.CenterX;
                                        allRels.Add((rel, false, rel.FromTable, pos, rel.EndEdgeSlotOffset));
                                    }
                                }

                                // Sort by other table position (ascending)
                                allRels = allRels.OrderBy(x => x.otherPos).ToList();

                                for (int i = 0; i < allRels.Count; i++)
                                {
                                    var (rel, isFrom, otherTable, otherPos, offset) = allRels[i];
                                    var actualY = isFrom ? rel.ActualStartY : rel.ActualEndY;
                                    var cardinalityY = isFrom ? rel.FromCardinalityY : rel.ToCardinalityY;
                                    sb.AppendLine($"      #{i}: {otherTable} (CenterY={otherPos:F0}) -> Offset={offset:F1}, ActualY={actualY:F1}, CardinalityY={cardinalityY:F1}");
                                }
                            }
                        }
                    }
                    sb.AppendLine();

                    // Raw ADOTabular relationship diagnostics
                    sb.AppendLine("=== RAW ADOTABULAR RELATIONSHIP DIAGNOSTICS ===");
                    if (_model != null)
                    {
                        sb.AppendLine($"  CSDL Version: {_model.CSDLVersion}");
                        sb.AppendLine($"  CSDL Version >= 2.5 (required for relationships): {_model.CSDLVersion >= 2.5}");
                        sb.AppendLine($"  Model-level Relationships.Count: {_model.Relationships?.Count ?? -1}");
                        sb.AppendLine($"  Model.Tables.Count: {_model.Tables?.Count ?? -1}");
                        sb.AppendLine();
                        
                        int totalRawRels = 0;
                        int tablesWithRels = 0;
                        try
                        {
                            foreach (var table in _model.Tables)
                            {
                                var relCount = table.Relationships?.Count ?? 0;
                                totalRawRels += relCount;
                                if (relCount > 0)
                                {
                                    tablesWithRels++;
                                    sb.AppendLine($"  Table [{table.Name}]: {relCount} relationship(s)");
                                    foreach (var rel in table.Relationships)
                                    {
                                        sb.AppendLine($"    - InternalName: {rel.InternalName}");
                                        sb.AppendLine($"      FromTable: {rel.FromTable?.Name ?? "(null)"}, FromColumn: {rel.FromColumn ?? "(null)"}");
                                        sb.AppendLine($"      ToTable: {rel.ToTable?.Name ?? "(null)"}, ToColumn: {rel.ToColumn ?? "(null)"}");
                                        sb.AppendLine($"      IsActive: {rel.IsActive}, CrossFilter: {rel.CrossFilterDirection}");
                                        sb.AppendLine($"      FromMultiplicity: {rel.FromColumnMultiplicity ?? "(null)"}, ToMultiplicity: {rel.ToColumnMultiplicity ?? "(null)"}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  Error reading raw table relationships: {ex.Message}");
                        }
                        
                        sb.AppendLine();
                        sb.AppendLine($"  TOTALS: {totalRawRels} raw relationships across {tablesWithRels} tables");
                        
                        if (totalRawRels == 0)
                        {
                            sb.AppendLine();
                            sb.AppendLine("  *** WARNING: Zero raw relationships found in ADOTabular model ***");
                            if (_model.CSDLVersion < 2.5)
                            {
                                sb.AppendLine("  *** LIKELY CAUSE: CSDLVersion < 2.5 — relationship parsing is skipped ***");
                                sb.AppendLine("  *** The CSDL metadata from this server may not include relationship info ***");
                            }
                            else
                            {
                                sb.AppendLine("  *** CSDLVersion is >= 2.5 so relationships SHOULD have been parsed ***");
                                sb.AppendLine("  *** Possible causes: CSDL XML missing AssociationSet elements, ***");
                                sb.AppendLine("  *** or table name resolution failed in BuildRelationshipFromAssociationSet ***");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine("  _model is null (offline mode or model not loaded)");
                    }
                    sb.AppendLine();

                    // VPA Relationship info (if available and offline)
                    if (_isOfflineMode && _vpaModel != null)
                    {
                        sb.AppendLine("=== VPA RELATIONSHIP RAW DATA ===");
                        try
                        {
                            foreach (var vpaTable in _vpaModel.Tables)
                            {
                                foreach (var vpaRel in vpaTable.RelationshipsFrom)
                                {
                                    sb.AppendLine($"  VpaRelationship:");
                                    sb.AppendLine($"    FromColumnName: {vpaRel.FromColumnName}");
                                    sb.AppendLine($"    ToColumnName: {vpaRel.ToColumnName}");
                                    sb.AppendLine($"    RelationshipFromToName: {vpaRel.RelationshipFromToName}");
                                    sb.AppendLine($"    IsActive: {vpaRel.IsActive}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            sb.AppendLine($"  Error reading VPA relationships: {ex.Message}");
                        }
                    }

                    System.IO.File.WriteAllText(dialog.FileName, sb.ToString());
                    
                    // Open the file
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to export debug info: {ex.Message}", "Export Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Clears all enrichment data from tables and columns.
        /// Called when loading a new model.
        /// </summary>
        public void ClearEnrichmentData()
        {
            foreach (var table in Tables)
            {
                table.ClearEnrichmentData();
                foreach (var col in table.Columns)
                {
                    col.ClearEnrichmentData();
                }
            }
            HasVertipaqData = false;
            HasStorageModeData = false;
            NotifyOfPropertyChange(nameof(SummaryText));
        }

        /// <summary>
        /// Enriches the diagram with Vertipaq Analyzer statistics.
        /// Called when VPA completes or when user requests stats refresh.
        /// </summary>
        /// <param name="vpaModel">The Vertipaq Analyzer model with statistics.</param>
        public void EnrichFromVertipaq(VpaModel vpaModel)
        {
            if (vpaModel?.Tables == null)
            {
                Log.Warning("{class} {method} VpaModel or Tables is null", nameof(ModelDiagramViewModel), nameof(EnrichFromVertipaq));
                return;
            }

            int enrichedTables = 0;
            int enrichedColumns = 0;

            foreach (var table in Tables)
            {
                // Find matching VPA table (by name)
                var vpaTable = vpaModel.Tables.FirstOrDefault(t => 
                    string.Equals(t.TableName, table.TableName, StringComparison.OrdinalIgnoreCase));

                if (vpaTable != null)
                {
                    // Enrich table with VPA stats
                    table.RowCount = vpaTable.RowsCount;
                    table.TableSizeBytes = vpaTable.TableSize;
                    table.PercentOfDatabase = vpaTable.PercentageDatabase;
                    table.SegmentCount = vpaTable.SegmentsNumber;
                    table.PartitionCount = (int)vpaTable.PartitionsNumber;
                    table.ReferentialIntegrityViolations = vpaTable.ReferentialIntegrityViolationCount;

                    // Determine storage mode from partitions
                    var storageMode = DetermineStorageMode(vpaTable);
                    if (!string.IsNullOrEmpty(storageMode))
                    {
                        table.StorageMode = storageMode;
                    }

                    enrichedTables++;

                    // Enrich columns
                    foreach (var column in table.Columns)
                    {
                        // Skip measures and hierarchies - they don't have VPA column data
                        if (column.IsMeasure || column.IsHierarchy) continue;

                        var vpaColumn = vpaTable.Columns.FirstOrDefault(c =>
                            string.Equals(c.ColumnName, column.ColumnName, StringComparison.OrdinalIgnoreCase));

                        if (vpaColumn != null)
                        {
                            column.Cardinality = vpaColumn.ColumnCardinality;
                            column.ColumnSizeBytes = vpaColumn.TotalSize;
                            column.Encoding = vpaColumn.Encoding;
                            column.PercentOfTable = vpaColumn.PercentageTable;
                            column.DataSizeBytes = vpaColumn.DataSize;
                            column.DictionarySizeBytes = vpaColumn.DictionarySize;
                            enrichedColumns++;
                        }
                    }
                }
            }

            HasVertipaqData = enrichedTables > 0;
            if (enrichedTables > 0)
            {
                HasStorageModeData = Tables.Any(t => t.HasStorageModeInfo);
                
                // Notify all columns to refresh their displayed stat
                foreach (var table in Tables)
                {
                    foreach (var col in table.Columns)
                    {
                        col.NotifyStatDisplayChanged();
                    }
                }
            }

            Log.Information("{class} {method} Enriched {tables} tables and {columns} columns with VPA data",
                nameof(ModelDiagramViewModel), nameof(EnrichFromVertipaq), enrichedTables, enrichedColumns);

            // Update summary to reflect enrichment
            NotifyOfPropertyChange(nameof(SummaryText));
        }

        /// <summary>
        /// Determines the storage mode for a table from its VPA partitions.
        /// </summary>
        private string DetermineStorageMode(VpaTable vpaTable)
        {
            if (vpaTable?.Partitions == null || !vpaTable.Partitions.Any())
                return null;

            string mode = null;
            foreach (var partition in vpaTable.Partitions)
            {
                var partitionMode = partition.PartitionMode?.ParseStorageMode();
                
                if (string.IsNullOrEmpty(mode))
                {
                    mode = partitionMode;
                }
                else if (!string.Equals(mode, partitionMode, StringComparison.OrdinalIgnoreCase))
                {
                    // Mixed modes = Hybrid
                    return "Hybrid";
                }
            }

            return mode;
        }

        #endregion

        #region Export

        /// <summary>
        /// Event raised when export is requested. The view subscribes to render the canvas to an image.
        /// </summary>
        public event EventHandler<string> ExportRequested;

        /// <summary>
        /// Exports the diagram to a PNG file.
        /// </summary>
        public void ExportToImage()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png",
                Title = "Export Model Diagram",
                FileName = $"ModelDiagram_{DateTime.Now:yyyyMMdd_HHmmss}.png"
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
    }

    /// <summary>
    /// ViewModel for a table in the Model Diagram.
    /// </summary>
    public class ModelDiagramTableViewModel : PropertyChangedBase
    {
        private readonly ADOTabularTable _table;
        private readonly bool _showHiddenObjects;
        private readonly IMetadataProvider _metadataProvider;
        private readonly IGlobalOptions _options;
        private bool _sortKeyColumnsFirst = false;
        
        // Backing fields for VPA-only construction (when _table is null)
        private readonly string _vpaTableName;
        private readonly string _vpaCaption;
        private readonly string _vpaDescription;
        private readonly bool _vpaIsVisible;
        private readonly bool _vpaIsDateTable;
        private readonly string _vpaDataCategory;
        private readonly bool _vpaIsPrivate;
        private readonly bool _isFromVpa;

        public ModelDiagramTableViewModel(ADOTabularTable table, bool showHiddenObjects, IMetadataProvider metadataProvider, IGlobalOptions options, bool sortKeyColumnsFirst = false)
        {
            _table = table;
            _showHiddenObjects = showHiddenObjects;
            _metadataProvider = metadataProvider;
            _options = options;
            _sortKeyColumnsFirst = sortKeyColumnsFirst;
            _isFromVpa = false;
            Columns = new BindableCollection<ModelDiagramColumnViewModel>(GetSortedColumns());
        }

        /// <summary>
        /// Constructor for creating a table from VPA (VertiPaq Analyzer) data when offline.
        /// </summary>
        public ModelDiagramTableViewModel(VpaTable vpaTable, bool showHiddenObjects, IGlobalOptions options, bool sortKeyColumnsFirst = false)
        {
            _table = null; // No ADOTabular table when loading from VPA
            _showHiddenObjects = showHiddenObjects;
            _metadataProvider = null;
            _options = options;
            _sortKeyColumnsFirst = sortKeyColumnsFirst;
            _isFromVpa = true;
            
            // Store VPA data in backing fields
            _vpaTableName = vpaTable.TableName;
            _vpaCaption = vpaTable.TableName; // VPA doesn't distinguish caption
            _vpaDescription = null; // VPA doesn't have description
            _vpaIsVisible = true; // VPA doesn't expose IsHidden at table level, assume visible
            _vpaIsDateTable = vpaTable.IsDateTable == true; // VpaTable has nullable bool IsDateTable
            _vpaDataCategory = null; // VPA doesn't have data category
            _vpaIsPrivate = false; // VPA doesn't track private
            
            // Create columns from VPA data
            Columns = new BindableCollection<ModelDiagramColumnViewModel>(GetColumnsFromVpa(vpaTable, showHiddenObjects, options));
            
            Log.Information("{class} {method} Created table from VPA: '{table}' with {colCount} columns",
                nameof(ModelDiagramTableViewModel), ".ctor(VPA)", _vpaTableName, Columns.Count);
        }

        /// <summary>
        /// Creates column ViewModels from VPA table data.
        /// </summary>
        private IEnumerable<ModelDiagramColumnViewModel> GetColumnsFromVpa(VpaTable vpaTable, bool showHiddenObjects, IGlobalOptions options)
        {
            var result = new List<ModelDiagramColumnViewModel>();
            
            foreach (var vpaCol in vpaTable.Columns)
            {
                // Skip RowNumber columns
                if (vpaCol.ColumnName.StartsWith("RowNumber-")) continue;
                
                // Skip hidden columns if not showing hidden
                if (!showHiddenObjects && vpaCol.IsHidden) continue;
                
                var colVm = new ModelDiagramColumnViewModel(vpaCol, options);
                result.Add(colVm);
            }
            
            // Sort: columns first (by name), then measures (by name)
            return result
                .OrderBy(c => c.IsMeasure ? 1 : 0)
                .ThenBy(c => c.Caption);
        }

        /// <summary>
        /// Gets columns sorted based on the current sort setting.
        /// Note: IsRelationshipColumn is NOT set here during initial load - it's set later
        /// by the relationship processing loop for much better performance.
        /// Hierarchy levels are shown indented under their parent hierarchy.
        /// </summary>
        private IEnumerable<ModelDiagramColumnViewModel> GetSortedColumns()
        {
            var filtered = _table.Columns
                .Where(c => c.Contents != "RowNumber" && (_showHiddenObjects || c.IsVisible));

            IEnumerable<ADOTabularColumn> sorted;
            if (_sortKeyColumnsFirst)
            {
                // During initial load, we can't sort by relationship columns yet (not determined)
                // After relationships are processed, UpdateColumnSort can be called to re-sort
                sorted = filtered
                    .OrderBy(c => c.ObjectType == ADOTabularObjectType.Column ? 0 : 1) // Columns first, then measures
                    .ThenBy(c => c.Caption);
            }
            else
            {
                sorted = filtered
                    .OrderBy(c => c.ObjectType == ADOTabularObjectType.Column ? 0 : 1) // Columns first, then measures
                    .ThenBy(c => c.Caption);
            }

            // Create ViewModels - IsRelationshipColumn will be set later by relationship processing
            // For hierarchies, also add the hierarchy levels as indented children
            // First, collect all column names that are used in hierarchy levels so we can exclude them from main list
            var hierarchyLevelColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in sorted)
            {
                if (col is ADOTabularHierarchy hierarchy && hierarchy.Levels != null)
                {
                    foreach (var level in hierarchy.Levels)
                    {
                        hierarchyLevelColumnNames.Add(level.Column.Name);
                        // DEBUG: Log hierarchy level columns
                        Log.Information("{class} {method} Table '{table}': Hierarchy '{hier}' has level column '{col}'",
                            nameof(ModelDiagramTableViewModel), nameof(GetSortedColumns), 
                            _table.Caption, hierarchy.Caption, level.Column.Name);
                    }
                }
            }
            
            // DEBUG: Log all hierarchy level column names collected
            if (hierarchyLevelColumnNames.Count > 0)
            {
                Log.Information("{class} {method} Table '{table}': HierarchyLevelColumnNames = [{cols}]",
                    nameof(ModelDiagramTableViewModel), nameof(GetSortedColumns),
                    _table.Caption, string.Join(", ", hierarchyLevelColumnNames));
            }

            var result = new List<ModelDiagramColumnViewModel>();
            foreach (var col in sorted)
            {
                // Skip columns that will appear as hierarchy levels (to avoid duplicates like Date)
                if (hierarchyLevelColumnNames.Contains(col.Name) && 
                    col.ObjectType != ADOTabularObjectType.Hierarchy && 
                    col.ObjectType != ADOTabularObjectType.UnnaturalHierarchy)
                {
                    Log.Information("{class} {method} Table '{table}': SKIPPING column '{col}' (Name='{name}', ObjectType={type}) - it's a hierarchy level",
                        nameof(ModelDiagramTableViewModel), nameof(GetSortedColumns),
                        _table.Caption, col.Caption, col.Name, col.ObjectType);
                    continue;
                }

                result.Add(new ModelDiagramColumnViewModel(col, _metadataProvider, _options));
                
                // If this is a hierarchy, add its levels as indented children
                if (col is ADOTabularHierarchy hierarchy && hierarchy.Levels != null)
                {
                    Log.Information("{class} {method} Table '{table}': Adding {count} levels for hierarchy '{hier}'",
                        nameof(ModelDiagramTableViewModel), nameof(GetSortedColumns),
                        _table.Caption, hierarchy.Levels.Count, hierarchy.Caption);
                        
                    foreach (var level in hierarchy.Levels)
                    {
                        // Create a column VM for the level, marked as a hierarchy level
                        result.Add(new ModelDiagramColumnViewModel(level.Column, _metadataProvider, _options, 
                            isHierarchyLevel: true, hierarchyLevelName: level.Caption));
                    }
                }
            }
            
            // DEBUG: Log final column list with IsHierarchyLevel status
            Log.Information("{class} {method} Table '{table}': Final column list ({count} items):",
                nameof(ModelDiagramTableViewModel), nameof(GetSortedColumns), _table.Caption, result.Count);
            foreach (var vm in result)
            {
                if (vm.IsHierarchyLevel)
                {
                    Log.Information("  - '{caption}' (IsHierarchyLevel=TRUE, Name='{name}')", vm.Caption, vm.ColumnName);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Checks if a column is used in a relationship (foreign key or referenced key).
        /// WARNING: This is expensive - iterates all relationships. Use sparingly.
        /// </summary>
        private bool IsRelationshipColumn(string columnName)
        {
            // Check if this column participates in any relationship (either side)
            return _table.Model?.Relationships?.Any(r => 
                (r.FromTable?.Name == _table.Name && r.FromColumn == columnName) ||
                (r.ToTable?.Name == _table.Name && r.ToColumn == columnName)) ?? false;
        }

        /// <summary>
        /// Updates the column sort order.
        /// Note: Hierarchy levels are NOT sorted - they remain attached to their parent hierarchy.
        /// This method re-sorts the EXISTING columns to preserve VPA enrichment data.
        /// </summary>
        public void UpdateColumnSort(bool sortKeyColumnsFirst, DaxStudio.Interfaces.Enums.DiagramColumnSortOrder sortOrder = DaxStudio.Interfaces.Enums.DiagramColumnSortOrder.Name)
        {
            _sortKeyColumnsFirst = sortKeyColumnsFirst;
            
            // Get the existing columns (preserving all their enrichment data)
            var existingColumns = Columns.ToList();
            
            // Build a mapping of hierarchy levels to their parent hierarchy
            // Levels appear immediately after their parent hierarchy in the list
            var hierarchyLevelToParent = new Dictionary<ModelDiagramColumnViewModel, ModelDiagramColumnViewModel>();
            ModelDiagramColumnViewModel currentHierarchy = null;
            foreach (var col in existingColumns)
            {
                if (col.IsHierarchy)
                {
                    currentHierarchy = col;
                }
                else if (col.IsHierarchyLevel && currentHierarchy != null)
                {
                    hierarchyLevelToParent[col] = currentHierarchy;
                }
                else
                {
                    // Reset if we hit a non-hierarchy-level column
                    currentHierarchy = null;
                }
            }
            
            // Separate hierarchy levels from regular columns
            var hierarchyLevelColumns = existingColumns.Where(c => c.IsHierarchyLevel).ToList();
            var nonHierarchyLevelColumns = existingColumns.Where(c => !c.IsHierarchyLevel).ToList();
            
            // Apply the sort based on sortOrder parameter
            List<ModelDiagramColumnViewModel> sortedNonHierarchy;
            
            // First level sort: Columns -> Hierarchies -> Measures
            // Second level sort depends on sortKeyColumnsFirst and sortOrder
            var baseSort = nonHierarchyLevelColumns
                .OrderBy(c => c.ObjectType == ADOTabularObjectType.Column ? 0 : 
                              c.ObjectType == ADOTabularObjectType.Hierarchy || c.ObjectType == ADOTabularObjectType.UnnaturalHierarchy ? 1 : 2);
            
            if (sortKeyColumnsFirst)
            {
                // Sort relationship columns first
                baseSort = baseSort.ThenByDescending(c => c.IsRelationshipColumn);
            }
            
            // Apply the stat-based or name-based sort
            switch (sortOrder)
            {
                case DaxStudio.Interfaces.Enums.DiagramColumnSortOrder.CardinalityDesc:
                    // Sort by cardinality descending (nulls go to end), then by name
                    sortedNonHierarchy = baseSort
                        .ThenByDescending(c => c.Cardinality ?? 0)
                        .ThenBy(c => c.Caption)
                        .ToList();
                    break;
                    
                case DaxStudio.Interfaces.Enums.DiagramColumnSortOrder.SizeDesc:
                    // Sort by size descending (nulls go to end), then by name
                    sortedNonHierarchy = baseSort
                        .ThenByDescending(c => c.ColumnSizeBytes ?? 0)
                        .ThenBy(c => c.Caption)
                        .ToList();
                    break;
                    
                case DaxStudio.Interfaces.Enums.DiagramColumnSortOrder.Name:
                default:
                    // Sort alphabetically by caption
                    sortedNonHierarchy = baseSort
                        .ThenBy(c => c.Caption)
                        .ToList();
                    break;
            }
            
            // Rebuild the final list, inserting hierarchy levels after their parent hierarchies
            var finalColumns = new List<ModelDiagramColumnViewModel>();
            foreach (var col in sortedNonHierarchy)
            {
                finalColumns.Add(col);
                
                // If this is a hierarchy, find and add its levels right after
                if (col.IsHierarchy)
                {
                    // Find levels that belong to this hierarchy
                    var levels = hierarchyLevelColumns
                        .Where(l => hierarchyLevelToParent.TryGetValue(l, out var parent) && parent == col)
                        .ToList();
                    
                    foreach (var level in levels)
                    {
                        finalColumns.Add(level);
                    }
                }
            }
            
            // Add any remaining hierarchy levels at the end (fallback for orphaned levels)
            foreach (var level in hierarchyLevelColumns)
            {
                if (!finalColumns.Contains(level))
                {
                    finalColumns.Add(level);
                }
            }
            
            // Clear and re-add to force UI update
            Columns.Clear();
            foreach (var col in finalColumns)
            {
                Columns.Add(col);
            }
            
            NotifyOfPropertyChange(nameof(Columns));
            NotifyOfPropertyChange(nameof(KeyColumns));
        }

        public string TableName => _isFromVpa ? _vpaTableName : _table.Name;
        public string DaxName => _isFromVpa ? _vpaTableName : _table.DaxName;
        public string Caption => _isFromVpa ? _vpaCaption : _table.Caption;
        public string Description => _isFromVpa ? _vpaDescription : _table.Description;
        public bool ShowDescription => !string.IsNullOrEmpty(Description);
        public string TreeviewImage
        {
            get
            {
                if (!IsVisible || IsPrivate) return "tableHiddenDrawingImage";
                if (IsDateTable) return "date_tableDrawingImage";
                return "tableDrawingImage";
            }
        }
        public bool IsVisible => _isFromVpa ? _vpaIsVisible : _table.IsVisible;
        public bool IsDateTable => _isFromVpa ? _vpaIsDateTable : _table.IsDateTable;
        public string DataCategory => _isFromVpa ? _vpaDataCategory : _table.DataCategory;
        public int ColumnCount => Columns.Count(c => c.ObjectType == ADOTabularObjectType.Column || (!c.IsMeasure && !c.IsHierarchy));
        public int MeasureCount => Columns.Count(c => c.ObjectType == ADOTabularObjectType.Measure || c.IsMeasure);
        public int HierarchyCount => Columns.Count(c => c.IsHierarchy);

        /// <summary>
        /// Whether this table has any hierarchies.
        /// </summary>
        public bool HasHierarchies => HierarchyCount > 0;

        /// <summary>
        /// Whether this is a "measure table" (contains only measures, no data columns).
        /// Alias for IsMeasureTable for XAML binding compatibility.
        /// </summary>
        public bool IsMeasureOnlyTable => MeasureCount > 0 && ColumnCount == 0;

        /// <summary>
        /// Whether this is a "measure table" (contains only measures, no data columns).
        /// </summary>
        public bool IsMeasureTable => MeasureCount > 0 && ColumnCount == 0;

        /// <summary>
        /// Whether this table is marked as private.
        /// </summary>
        public bool IsPrivate => _isFromVpa ? _vpaIsPrivate : _table.Private;

        /// <summary>
        /// Whether this table is a Field Parameter table.
        /// Detection: ShowAsVariationsOnly property OR any column has Variations defined,
        /// OR follows the field parameter pattern (1 column + 1 measure with specific naming).
        /// </summary>
        public bool IsFieldParameterTable
        {
            get
            {
                // VPA mode: We don't have enough metadata to detect field parameters
                if (_isFromVpa) return false;
                
                // Primary detection methods
                if (_table.ShowAsVariationsOnly) return true;
                if (_table.Columns.Any(c => c.Variations != null && c.Variations.Count > 0)) return true;
                
                // Heuristic: Field parameter tables typically have:
                // - Exactly 1 column (the parameter name)
                // - 1 or more measures (typically named "{TableName} Value" or similar)
                // - The column often has the same name as the table
                var columns = _table.Columns.Where(c => c.ObjectType == ADOTabularObjectType.Column).ToList();
                var measures = _table.Columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure).ToList();
                
                if (columns.Count == 1 && measures.Count >= 1)
                {
                    // Check if column name matches table name (common field parameter pattern)
                    var colName = columns[0].Name;
                    var tableName = _table.Name;
                    if (colName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    
                    // Check if there's a measure with "Value" in the name
                    if (measures.Any(m => m.Name.IndexOf("Value", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return true;
                    }
                }
                
                return false;
            }
        }

        /// <summary>
        /// Whether this table has field parameters (variations) - checks columns.
        /// </summary>
        public bool HasFieldParameters => IsFieldParameterTable;

        /// <summary>
        /// Whether this table is a Calculation Group.
        /// Detection: Has an "Ordinal" column (calc groups always have this) AND 
        /// has exactly 2 columns (the Name column and Ordinal column).
        /// </summary>
        public bool IsCalculationGroup
        {
            get
            {
                // VPA mode: Check using columns in our collection
                if (_isFromVpa)
                {
                    var hasOrdinalColumn = Columns.Any(c => c.ColumnName.Equals("Ordinal", StringComparison.OrdinalIgnoreCase));
                    return hasOrdinalColumn && ColumnCount == 2;
                }
                
                // Calculation groups have a specific structure:
                // 1. An "Ordinal" column (always present)
                // 2. A "Name" column (named after the calc group or just "Name")
                // 3. Typically only 2 columns total
                var columns = _table.Columns.Where(c => c.ObjectType == ADOTabularObjectType.Column).ToList();
                var hasOrdinal = columns.Any(c => c.Name.Equals("Ordinal", StringComparison.OrdinalIgnoreCase));
                
                // Calc groups have exactly 2 columns: the name column and ordinal
                // They may also have calculation items as measures
                return hasOrdinal && columns.Count == 2;
            }
        }

        private int _relationshipCount;
        /// <summary>
        /// Number of relationships this table participates in.
        /// Set during relationship processing.
        /// </summary>
        public int RelationshipCount
        {
            get => _relationshipCount;
            set 
            { 
                _relationshipCount = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsHubTable));
                NotifyOfPropertyChange(nameof(IsLeafTable));
            }
        }

        /// <summary>
        /// Whether this is a "hub" table (many relationships, likely a dimension).
        /// </summary>
        public bool IsHubTable => RelationshipCount >= 3;

        /// <summary>
        /// Whether this is a "leaf" table (0-1 relationships, likely a fact or standalone).
        /// </summary>
        public bool IsLeafTable => RelationshipCount <= 1 && !IsMeasureTable;

        private bool _isHidden;
        /// <summary>
        /// Whether this table is manually hidden from the diagram (separate from model IsVisible).
        /// </summary>
        public bool IsHidden
        {
            get => _isHidden;
            set { _isHidden = value; NotifyOfPropertyChange(); }
        }

        private string _group;
        /// <summary>
        /// The group this table belongs to (for visual grouping).
        /// </summary>
        public string Group
        {
            get => _group;
            set 
            { 
                _group = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HasGroup));
                NotifyOfPropertyChange(nameof(GroupColor));
            }
        }

        /// <summary>
        /// Whether this table has a group assigned.
        /// </summary>
        public bool HasGroup => !string.IsNullOrEmpty(Group);

        /// <summary>
        /// Color for the group indicator bar.
        /// Uses a hash of the group name to generate consistent colors.
        /// </summary>
        public string GroupColor
        {
            get
            {
                if (string.IsNullOrEmpty(Group)) return "#CCCCCC";
                
                // Generate a consistent color from the group name
                var hash = Group.GetHashCode();
                var colors = new[]
                {
                    "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3",
                    "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A",
                    "#CDDC39", "#FFC107", "#FF9800", "#FF5722", "#795548"
                };
                return colors[Math.Abs(hash) % colors.Length];
            }
        }

        public BindableCollection<ModelDiagramColumnViewModel> Columns { get; }

        private const int CollapsedFallbackColumnCount = 3;

        /// <summary>
        /// Key and relationship columns (for collapsed view).
        /// Shows columns marked as keys OR columns used in relationships.
        /// Falls back to showing the first few non-measure columns if no key/relationship columns exist,
        /// so collapsed tables aren't empty rectangles.
        /// </summary>
        public IEnumerable<ModelDiagramColumnViewModel> KeyColumns
        {
            get
            {
                var keyAndRelCols = Columns.Where(c => c.IsKey || c.IsRelationshipColumn).ToList();
                if (keyAndRelCols.Count > 0) return keyAndRelCols;

                // Fallback: show first few non-measure columns so collapsed tables aren't empty
                return Columns.Where(c => !c.IsMeasure).Take(CollapsedFallbackColumnCount);
            }
        }

        /// <summary>
        /// Whether this table has columns to show in collapsed view.
        /// True when there are key/relationship columns, or any non-measure columns to use as fallback.
        /// </summary>
        public bool HasKeyColumns => Columns.Any(c => c.IsKey || c.IsRelationshipColumn) || Columns.Any(c => !c.IsMeasure);

        /// <summary>
        /// Tooltip with table details matching metadata pane style.
        /// Includes enrichment data when available.
        /// </summary>
        public string Tooltip
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(Caption);
                
                if (Caption != TableName)
                    sb.AppendLine($"Name: {TableName}");
                
                // Enrichment: Storage mode (if available)
                if (HasStorageModeInfo)
                    sb.AppendLine($"{StorageModeIcon} {StorageMode}");
                
                sb.AppendLine($"Columns: {ColumnCount}");
                sb.AppendLine($"Measures: {MeasureCount}");
                if (HierarchyCount > 0)
                    sb.AppendLine($"Hierarchies: {HierarchyCount}");
                sb.AppendLine($"Relationships: {RelationshipCount}");
                
                // Enrichment: Vertipaq stats (if available)
                if (HasVertipaqStats)
                {
                    sb.AppendLine();
                    sb.AppendLine("─── Statistics ───");
                    if (_rowCount.HasValue)
                        sb.AppendLine($"Rows: {FormattedRowCount}");
                    if (_tableSizeBytes.HasValue)
                        sb.AppendLine($"Size: {FormattedTableSize}");
                    if (_percentOfDatabase.HasValue)
                        sb.AppendLine($"% of Database: {FormattedPercentOfDatabase}");
                    if (_partitionCount.HasValue)
                        sb.AppendLine($"Partitions: {_partitionCount.Value}");
                    if (_segmentCount.HasValue)
                        sb.AppendLine($"Segments: {_segmentCount.Value}");
                    if (HasReferentialIntegrityIssues)
                        sb.AppendLine($"⚠️ RI Violations: {_referentialIntegrityViolations.Value:N0}");
                }
                
                // Table type indicators
                if (IsDateTable)
                    sb.AppendLine("📅 Date Table");
                if (IsFieldParameterTable)
                    sb.AppendLine("⚡ Field Parameter Table");
                if (IsCalculationGroup)
                    sb.AppendLine("🧮 Calculation Group");
                if (IsMeasureTable)
                    sb.AppendLine("📊 Measure Table");
                if (!string.IsNullOrEmpty(DataCategory) && DataCategory != "Time" && DataCategory != "CalculationGroup")
                    sb.AppendLine($"Category: {DataCategory}");
                    
                if (IsPrivate)
                    sb.AppendLine("🔒 Private");
                if (!IsVisible)
                    sb.AppendLine("👁 Hidden");
                    
                if (!string.IsNullOrEmpty(Description))
                {
                    sb.AppendLine();
                    sb.Append(Description);
                }
                
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Header background color based on table type and storage mode.
        /// When storage mode is available (admin), uses storage mode colors.
        /// Otherwise falls back to: Date Tables = green, hidden = gray, default = blue.
        /// </summary>
        public string HeaderColor
        {
            get
            {
                // If storage mode is available, use storage mode colors
                if (HasStorageModeInfo)
                {
                    return StorageModeColor;
                }
                
                // Fallback to type-based colors
                if (IsDateTable) return "#4CAF50"; // Green for date tables
                if (!IsVisible) return "#9E9E9E"; // Gray for hidden tables
                return "#2196F3"; // Blue default
            }
        }

        /// <summary>
        /// Icon to show in the table header based on table type.
        /// </summary>
        public string TableTypeIcon
        {
            get
            {
                if (IsDateTable) return "📅";
                if (IsMeasureTable) return "📊";
                if (HasFieldParameters) return "🔀";
                if (IsHubTable) return "⭐";
                if (IsLeafTable && RelationshipCount == 0) return "📄"; // Standalone/calculated table
                return "";
            }
        }

        /// <summary>
        /// Whether the table has a type icon to display.
        /// </summary>
        public bool HasTableTypeIcon => !string.IsNullOrEmpty(TableTypeIcon);

        #region Position Properties

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

        private double _width = 200;
        public double Width
        {
            get => _width;
            set 
            { 
                _width = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(CenterX)); 
                NotifyOfPropertyChange(nameof(RightEdgeX)); 
            }
        }

        private double _height = 180;
        public double Height
        {
            get => _height;
            set 
            { 
                _height = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(CenterY)); 
                NotifyOfPropertyChange(nameof(RightEdgeY));
                NotifyOfPropertyChange(nameof(LeftEdgeY));
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

        #region State Properties

        private bool _isCollapsed;
        private double _expandedHeight;
        private const double CollapsedHeaderHeight = 40; // Height for just the header when collapsed
        private const double KeyColumnRowHeight = 18; // Height per key column row

        /// <summary>
        /// Calculate collapsed height based on header + key columns + relationship columns.
        /// </summary>
        private double CalculateCollapsedHeight()
        {
            // Count key columns AND relationship columns (shown in collapsed view)
            int keyCount = Columns.Count(c => c.IsKey || c.IsRelationshipColumn);
            if (keyCount == 0)
            {
                // Fallback: count first few non-measure columns
                keyCount = Math.Min(CollapsedFallbackColumnCount, Columns.Count(c => !c.IsMeasure));
            }
            if (keyCount == 0) return CollapsedHeaderHeight;
            return CollapsedHeaderHeight + 6 + (keyCount * KeyColumnRowHeight); // 6 = padding
        }

        /// <summary>
        /// Recalculates the collapsed height after IsRelationshipColumn has been set.
        /// Should be called after relationships are processed.
        /// </summary>
        public void RecalculateCollapsedHeight()
        {
            if (_isCollapsed)
            {
                Height = CalculateCollapsedHeight();
            }
        }

        /// <summary>
        /// Notifies that KeyColumns collection may have changed.
        /// Should be called after IsRelationshipColumn has been set on columns.
        /// </summary>
        public void NotifyKeyColumnsChanged()
        {
            NotifyOfPropertyChange(nameof(KeyColumns));
            NotifyOfPropertyChange(nameof(HasKeyColumns));
        }

        public bool IsCollapsed
        {
            get => _isCollapsed;
            set 
            { 
                if (_isCollapsed == value) return;
                
                if (value)
                {
                    // Collapsing: save current height and set to collapsed height
                    _expandedHeight = Height;
                    Height = CalculateCollapsedHeight();
                }
                else
                {
                    // Expanding: restore saved height (or calculate default)
                    Height = _expandedHeight > CalculateCollapsedHeight() ? _expandedHeight : CalculateDefaultHeight();
                }
                
                _isCollapsed = value; 
                NotifyOfPropertyChange(); 
            }
        }

        /// <summary>
        /// The height when expanded (for saving/restoring collapse state).
        /// </summary>
        public double ExpandedHeight
        {
            get => _expandedHeight > CalculateCollapsedHeight() ? _expandedHeight : Height;
            set => _expandedHeight = value;
        }

        /// <summary>
        /// Sets the collapsed state without triggering height changes (for loading saved state).
        /// </summary>
        public void SetCollapsedState(bool isCollapsed, double expandedHeight)
        {
            _expandedHeight = expandedHeight;
            _isCollapsed = isCollapsed;
            NotifyOfPropertyChange(nameof(IsCollapsed));
        }

        /// <summary>
        /// Calculate a reasonable default height based on column/measure count.
        /// </summary>
        private double CalculateDefaultHeight()
        {
            int itemCount = Columns?.Count ?? 0;
            return Math.Max(80, Math.Min(50 + itemCount * 20, 400));
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(); }
        }

        private bool _isSearchMatch = true;
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set { _isSearchMatch = value; NotifyOfPropertyChange(); }
        }

        private bool _isDimmed;
        /// <summary>
        /// Whether this table should be visually dimmed (e.g., when another table is hovered).
        /// </summary>
        public bool IsDimmed
        {
            get => _isDimmed;
            set { _isDimmed = value; NotifyOfPropertyChange(); }
        }

        private bool _isHovered;
        /// <summary>
        /// Whether the mouse is currently hovering over this table.
        /// </summary>
        public bool IsHovered
        {
            get => _isHovered;
            set { _isHovered = value; NotifyOfPropertyChange(); }
        }

        #endregion

        #region Optional Enrichment Properties (Admin/VPA)

        // These properties are optionally populated when:
        // - User has admin access (for storage mode from BIM/TOM)
        // - User runs Vertipaq Analyzer (for row counts, sizes, etc.)
        // If not populated, the UI shows the base model without these details.

        private string _storageMode;
        /// <summary>
        /// Storage mode for this table (Import, DirectQuery, DirectLake, Dual, Hybrid).
        /// Requires admin access to BIM/TOM to determine.
        /// </summary>
        public string StorageMode
        {
            get => _storageMode;
            set 
            { 
                _storageMode = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HasStorageModeInfo));
                NotifyOfPropertyChange(nameof(StorageModeIcon));
                NotifyOfPropertyChange(nameof(StorageModeColor));
                NotifyOfPropertyChange(nameof(HeaderColor));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Whether storage mode information is available.
        /// </summary>
        public bool HasStorageModeInfo => !string.IsNullOrEmpty(_storageMode);

        /// <summary>
        /// Icon representing the storage mode.
        /// </summary>
        public string StorageModeIcon
        {
            get
            {
                return _storageMode?.ToUpperInvariant() switch
                {
                    "IMPORT" => "💾",
                    "DIRECTQUERY" => "⚡",
                    "DIRECTLAKE" => "🌊",
                    "DUAL" => "🔄",
                    "HYBRID" => "🔀",
                    _ => ""
                };
            }
        }

        /// <summary>
        /// Color associated with the storage mode (for badges/indicators).
        /// </summary>
        public string StorageModeColor
        {
            get
            {
                return _storageMode?.ToUpperInvariant() switch
                {
                    "IMPORT" => "#4CAF50",      // Green - data is local
                    "DIRECTQUERY" => "#FF9800", // Orange - live query
                    "DIRECTLAKE" => "#2196F3",  // Blue - DirectLake
                    "DUAL" => "#9C27B0",        // Purple - both modes
                    "HYBRID" => "#E91E63",      // Pink - mixed partitions
                    _ => "#9E9E9E"              // Gray - unknown
                };
            }
        }

        private int? _partitionCount;
        /// <summary>
        /// Number of partitions in this table.
        /// Requires admin access to BIM/TOM.
        /// </summary>
        public int? PartitionCount
        {
            get => _partitionCount;
            set 
            { 
                _partitionCount = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        private long? _rowCount;
        /// <summary>
        /// Number of rows in this table.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? RowCount
        {
            get => _rowCount;
            set 
            { 
                _rowCount = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HasVertipaqStats));
                NotifyOfPropertyChange(nameof(FormattedRowCount));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted row count for display (e.g., "1.2M rows").
        /// </summary>
        public string FormattedRowCount => _rowCount.HasValue ? FormatNumber(_rowCount.Value) : null;

        private long? _tableSizeBytes;
        /// <summary>
        /// Total size of this table in bytes (memory footprint).
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? TableSizeBytes
        {
            get => _tableSizeBytes;
            set 
            { 
                _tableSizeBytes = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(FormattedTableSize));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted table size for display (e.g., "12.5 MB").
        /// </summary>
        public string FormattedTableSize => _tableSizeBytes.HasValue ? FormatBytes(_tableSizeBytes.Value) : null;

        private double? _percentOfDatabase;
        /// <summary>
        /// Percentage of total database size this table represents.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public double? PercentOfDatabase
        {
            get => _percentOfDatabase;
            set 
            { 
                _percentOfDatabase = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(FormattedPercentOfDatabase));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted percentage of database for display.
        /// </summary>
        public string FormattedPercentOfDatabase => _percentOfDatabase.HasValue 
            ? $"{_percentOfDatabase.Value:P1}" 
            : null;

        private int? _segmentCount;
        /// <summary>
        /// Number of segments in this table.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public int? SegmentCount
        {
            get => _segmentCount;
            set 
            { 
                _segmentCount = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        private long? _referentialIntegrityViolations;
        /// <summary>
        /// Number of referential integrity violations for relationships from this table.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? ReferentialIntegrityViolations
        {
            get => _referentialIntegrityViolations;
            set 
            { 
                _referentialIntegrityViolations = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HasReferentialIntegrityIssues));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Whether this table has referential integrity violations.
        /// </summary>
        public bool HasReferentialIntegrityIssues => _referentialIntegrityViolations.HasValue && _referentialIntegrityViolations.Value > 0;

        /// <summary>
        /// Whether Vertipaq Analyzer statistics are available for this table.
        /// </summary>
        public bool HasVertipaqStats => _rowCount.HasValue || _tableSizeBytes.HasValue;

        /// <summary>
        /// Clears all enrichment data (e.g., when loading a new model).
        /// </summary>
        public void ClearEnrichmentData()
        {
            _storageMode = null;
            _partitionCount = null;
            _rowCount = null;
            _tableSizeBytes = null;
            _percentOfDatabase = null;
            _segmentCount = null;
            _referentialIntegrityViolations = null;
            
            NotifyOfPropertyChange(nameof(StorageMode));
            NotifyOfPropertyChange(nameof(HasStorageModeInfo));
            NotifyOfPropertyChange(nameof(HasVertipaqStats));
            NotifyOfPropertyChange(nameof(HeaderColor));
            NotifyOfPropertyChange(nameof(Tooltip));
        }

        /// <summary>
        /// Formats a byte count for display.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return $"{size:N1} {suffixes[suffixIndex]}";
        }

        /// <summary>
        /// Formats a large number for compact display.
        /// </summary>
        private static string FormatNumber(long number)
        {
            if (number >= 1_000_000_000)
                return $"{number / 1_000_000_000.0:N1}B";
            if (number >= 1_000_000)
                return $"{number / 1_000_000.0:N1}M";
            if (number >= 1_000)
                return $"{number / 1_000.0:N1}K";
            return number.ToString("N0");
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for a column in the Model Diagram.
    /// </summary>
    public class ModelDiagramColumnViewModel : PropertyChangedBase
    {
        private readonly ADOTabularColumn _column;
        private readonly IMetadataProvider _metadataProvider;
        private readonly IGlobalOptions _options;
        private List<string> _sampleData;
        private bool _updatingSampleData;
        private bool _sampleDataLoadFailed; // Prevent retrying on error
        private const int SAMPLE_ROWS = 10;
        
        // Backing fields for VPA-only construction
        private readonly string _vpaColumnName;
        private readonly string _vpaCaption;
        private readonly string _vpaDataTypeName;
        private readonly bool _vpaIsVisible;
        private readonly bool _vpaIsMeasure;
        private readonly bool _vpaIsKey;
        private readonly bool _isFromVpa;

        public ModelDiagramColumnViewModel(ADOTabularColumn column, IMetadataProvider metadataProvider, IGlobalOptions options, bool isHierarchyLevel = false, string hierarchyLevelName = null)
        {
            _column = column;
            _metadataProvider = metadataProvider;
            _options = options;
            _sampleData = new List<string>();
            IsHierarchyLevel = isHierarchyLevel;
            _isFromVpa = false;
            HierarchyLevelName = hierarchyLevelName;
        }

        /// <summary>
        /// Constructor for creating a column from VPA (VertiPaq Analyzer) data when offline.
        /// </summary>
        public ModelDiagramColumnViewModel(VpaColumn vpaColumn, IGlobalOptions options)
        {
            _column = null;
            _metadataProvider = null;
            _options = options;
            _sampleData = new List<string>();
            _isFromVpa = true;
            
            // Store VPA data in backing fields
            _vpaColumnName = vpaColumn.ColumnName;
            _vpaCaption = vpaColumn.ColumnName; // VPA doesn't distinguish caption
            _vpaDataTypeName = vpaColumn.DataType;
            _vpaIsVisible = !vpaColumn.IsHidden;
            // VpaColumn doesn't have ColumnType - detect measures by checking if ColumnExpression exists
            // and DataType is empty/null (measures don't have a data type in VPA)
            _vpaIsMeasure = !string.IsNullOrEmpty(vpaColumn.ColumnExpression) && string.IsNullOrEmpty(vpaColumn.DataType);
            _vpaIsKey = vpaColumn.IsKey; // VpaColumn does have IsKey
            
            // Pre-populate VPA stats
            Cardinality = vpaColumn.ColumnCardinality;
            ColumnSizeBytes = vpaColumn.TotalSize;
            Encoding = vpaColumn.Encoding;
            PercentOfTable = vpaColumn.PercentageTable;
            DataSizeBytes = vpaColumn.DataSize;
            DictionarySizeBytes = vpaColumn.DictionarySize;
        }

        /// <summary>
        /// Constructor for creating a placeholder column for hidden relationship columns in VPA mode.
        /// When columns involved in relationships are hidden, we still need to create them for the diagram.
        /// </summary>
        /// <param name="columnName">The column name from the relationship definition</param>
        /// <param name="vpaColumn">Optional VPA column data if available (may be null for completely hidden columns)</param>
        /// <param name="options">Global options</param>
        public ModelDiagramColumnViewModel(string columnName, VpaColumn vpaColumn, IGlobalOptions options)
        {
            _column = null;
            _metadataProvider = null;
            _options = options;
            _sampleData = new List<string>();
            _isFromVpa = true;
            
            // Store column info - use VPA data if available, otherwise use just the name
            _vpaColumnName = columnName;
            _vpaCaption = columnName;
            _vpaIsVisible = false; // These are hidden relationship columns
            _vpaIsMeasure = false; // Relationship columns are never measures
            
            if (vpaColumn != null)
            {
                _vpaDataTypeName = vpaColumn.DataType;
                _vpaIsKey = vpaColumn.IsKey;
                Cardinality = vpaColumn.ColumnCardinality;
                ColumnSizeBytes = vpaColumn.TotalSize;
                Encoding = vpaColumn.Encoding;
                PercentOfTable = vpaColumn.PercentageTable;
                DataSizeBytes = vpaColumn.DataSize;
                DictionarySizeBytes = vpaColumn.DictionarySize;
            }
            else
            {
                _vpaDataTypeName = null;
                _vpaIsKey = false;
            }
        }

        /// <summary>
        /// Whether this column represents a level within a hierarchy (should be indented).
        /// </summary>
        public bool IsHierarchyLevel { get; }

        /// <summary>
        /// The display name for this hierarchy level (if IsHierarchyLevel is true).
        /// </summary>
        public string HierarchyLevelName { get; }

        public string ColumnName => _isFromVpa ? _vpaColumnName : _column.Name;
        public string Caption => _isFromVpa ? _vpaCaption : _column.Caption;
        public string Description => _isFromVpa ? null : _column.Description;
        public bool IsVisible => _isFromVpa ? _vpaIsVisible : _column.IsVisible;
        public ADOTabularObjectType ObjectType => _isFromVpa 
            ? (_vpaIsMeasure ? ADOTabularObjectType.Measure : ADOTabularObjectType.Column) 
            : _column.ObjectType;
        public string DataTypeName => _isFromVpa ? _vpaDataTypeName : _column.DataTypeName;
        public bool IsKey => _isFromVpa ? _vpaIsKey : _column.IsKey;

        /// <summary>
        /// Whether this column has a "Sort By" column defined.
        /// </summary>
        public bool HasSortByColumn => !_isFromVpa && _column?.OrderBy != null;

        /// <summary>
        /// The name of the column this is sorted by (if any).
        /// </summary>
        public string SortByColumnName => _isFromVpa ? null : _column?.OrderBy?.Name;

        /// <summary>
        /// Whether this column has variations (field parameters).
        /// </summary>
        public bool HasVariations => !_isFromVpa && _column?.Variations != null && _column.Variations.Count > 0;

        /// <summary>
        /// The measure expression (for measures only).
        /// </summary>
        public string MeasureExpression => _isFromVpa ? null : _column?.MeasureExpression;
        
        private bool _isRelationshipColumn;
        /// <summary>
        /// Whether this column is used in a relationship.
        /// Set by the parent table when relationships are loaded.
        /// </summary>
        public bool IsRelationshipColumn
        {
            get => _isRelationshipColumn;
            set { _isRelationshipColumn = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Whether this is a measure (vs column/hierarchy).
        /// </summary>
        public bool IsMeasure => ObjectType == ADOTabularObjectType.Measure 
                              || ObjectType == ADOTabularObjectType.KPI
                              || ObjectType == ADOTabularObjectType.KPIGoal
                              || ObjectType == ADOTabularObjectType.KPIStatus;

        /// <summary>
        /// Whether this is a hierarchy.
        /// </summary>
        public bool IsHierarchy => ObjectType == ADOTabularObjectType.Hierarchy 
                                || ObjectType == ADOTabularObjectType.UnnaturalHierarchy;

        /// <summary>
        /// The object type name for display (Column, Measure, Hierarchy).
        /// Matches the MetadataPaneView tooltip binding.
        /// </summary>
        public string ObjectTypeName
        {
            get
            {
                if (IsMeasure) return "Measure";
                if (IsHierarchy) return "Hierarchy";
                if (IsHierarchyLevel) return "Column";
                return "Column";
            }
        }

        /// <summary>
        /// The accent-colored object type image resource key.
        /// Matches the MetadataPaneView tooltip binding (e.g., "measure_accentDrawingImage").
        /// </summary>
        public string ObjectTypeImage => ObjectTypeName.ToLower() + "_accentDrawingImage";

        /// <summary>
        /// The data-type-specific image resource key for the Data Type row icon.
        /// Matches the MetadataPaneView tooltip binding using SourceAccentResourceKey.
        /// </summary>
        public string TreeviewImage
        {
            get
            {
                var suffix = IsVisible ? "DrawingImage" : "HiddenDrawingImage";

                if (IsHierarchy)
                    return "hierarchyDrawingImage";

                if (_isFromVpa)
                    return GetIconFromDataTypeName(_vpaDataTypeName, suffix);

                return _column?.DataType switch
                {
                    Microsoft.AnalysisServices.Tabular.DataType.Boolean => $"boolean{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.DateTime => $"datetime{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Double => $"double{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Decimal => $"double{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Int64 => $"number{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.String => $"string{suffix}",
                    _ => $"column{suffix}"
                };
            }
        }

        /// <summary>
        /// Icon resource key based on column type and data type.
        /// Uses the same icons as the metadata pane.
        /// </summary>
        public string IconResourceKey
        {
            get
            {
                var suffix = IsVisible ? "DrawingImage" : "HiddenDrawingImage";
                
                // Measures
                if (IsMeasure)
                    return $"measure{suffix}";

                // Hierarchies
                if (IsHierarchy)
                    return "hierarchyDrawingImage";

                // Regular columns - based on data type
                if (_isFromVpa)
                {
                    // Use string-based data type for VPA
                    return GetIconFromDataTypeName(_vpaDataTypeName, suffix);
                }
                
                return _column.DataType switch
                {
                    Microsoft.AnalysisServices.Tabular.DataType.Boolean => $"boolean{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.DateTime => $"datetime{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Double => $"double{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Decimal => $"double{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.Int64 => $"number{suffix}",
                    Microsoft.AnalysisServices.Tabular.DataType.String => $"string{suffix}",
                    _ => $"column{suffix}"
                };
            }
        }

        /// <summary>
        /// Gets icon resource key from data type name string (for VPA mode).
        /// </summary>
        private string GetIconFromDataTypeName(string dataTypeName, string suffix)
        {
            if (string.IsNullOrEmpty(dataTypeName)) return $"column{suffix}";
            
            var lowerType = dataTypeName.ToLowerInvariant();
            if (lowerType.Contains("bool")) return $"boolean{suffix}";
            if (lowerType.Contains("date") || lowerType.Contains("time")) return $"datetime{suffix}";
            if (lowerType.Contains("double") || lowerType.Contains("decimal") || lowerType.Contains("float") || lowerType.Contains("currency")) return $"double{suffix}";
            if (lowerType.Contains("int") || lowerType.Contains("whole")) return $"number{suffix}";
            if (lowerType.Contains("string") || lowerType.Contains("text")) return $"string{suffix}";
            
            return $"column{suffix}";
        }

        /// <summary>
        /// Short data type indicator for compact display.
        /// </summary>
        public string DataTypeIndicator
        {
            get
            {
                if (IsMeasure) return "fx";
                if (IsHierarchy) return "H";
                
                if (_isFromVpa)
                {
                    return GetDataTypeIndicatorFromName(_vpaDataTypeName);
                }
                
                return _column.DataType switch
                {
                    Microsoft.AnalysisServices.Tabular.DataType.Boolean => "T/F",
                    Microsoft.AnalysisServices.Tabular.DataType.DateTime => "📅",
                    Microsoft.AnalysisServices.Tabular.DataType.Double => "#.#",
                    Microsoft.AnalysisServices.Tabular.DataType.Decimal => "#.#",
                    Microsoft.AnalysisServices.Tabular.DataType.Int64 => "123",
                    Microsoft.AnalysisServices.Tabular.DataType.String => "abc",
                    _ => ""
                };
            }
        }

        /// <summary>
        /// Gets data type indicator from type name string (for VPA mode).
        /// </summary>
        private string GetDataTypeIndicatorFromName(string dataTypeName)
        {
            if (string.IsNullOrEmpty(dataTypeName)) return "";
            
            var lowerType = dataTypeName.ToLowerInvariant();
            if (lowerType.Contains("bool")) return "T/F";
            if (lowerType.Contains("date") || lowerType.Contains("time")) return "📅";
            if (lowerType.Contains("double") || lowerType.Contains("decimal") || lowerType.Contains("float") || lowerType.Contains("currency")) return "#.#";
            if (lowerType.Contains("int") || lowerType.Contains("whole")) return "123";
            if (lowerType.Contains("string") || lowerType.Contains("text")) return "abc";
            
            return "";
        }

        /// <summary>
        /// Background color for the data type indicator.
        /// </summary>
        public string DataTypeColor
        {
            get
            {
                if (IsMeasure) return "#FF9800";  // Orange for measures
                if (IsHierarchy) return "#9C27B0";  // Purple for hierarchies
                if (IsKey) return "#F44336";  // Red for keys
                
                if (_isFromVpa)
                {
                    return GetDataTypeColorFromName(_vpaDataTypeName);
                }
                
                return _column.DataType switch
                {
                    Microsoft.AnalysisServices.Tabular.DataType.Boolean => "#4CAF50",  // Green
                    Microsoft.AnalysisServices.Tabular.DataType.DateTime => "#2196F3",  // Blue
                    Microsoft.AnalysisServices.Tabular.DataType.Double => "#9C27B0",  // Purple
                    Microsoft.AnalysisServices.Tabular.DataType.Decimal => "#9C27B0",  // Purple
                    Microsoft.AnalysisServices.Tabular.DataType.Int64 => "#673AB7",  // Deep Purple
                    Microsoft.AnalysisServices.Tabular.DataType.String => "#607D8B",  // Blue Grey
                    _ => "#9E9E9E"  // Grey
                };
            }
        }

        /// <summary>
        /// Gets data type color from type name string (for VPA mode).
        /// </summary>
        private string GetDataTypeColorFromName(string dataTypeName)
        {
            if (string.IsNullOrEmpty(dataTypeName)) return "#9E9E9E";
            
            var lowerType = dataTypeName.ToLowerInvariant();
            if (lowerType.Contains("bool")) return "#4CAF50";  // Green
            if (lowerType.Contains("date") || lowerType.Contains("time")) return "#2196F3";  // Blue
            if (lowerType.Contains("double") || lowerType.Contains("decimal") || lowerType.Contains("float") || lowerType.Contains("currency")) return "#9C27B0";  // Purple
            if (lowerType.Contains("int") || lowerType.Contains("whole")) return "#673AB7";  // Deep Purple
            if (lowerType.Contains("string") || lowerType.Contains("text")) return "#607D8B";  // Blue Grey
            
            return "#9E9E9E";  // Grey
        }

        /// <summary>
        /// Tooltip with detailed column information matching metadata pane.
        /// </summary>
        public string Tooltip
        {
            get
            {
                // Trigger sample data loading if enabled and not already loaded/failed
                if (ShowSampleData && !HasSampleData && !_updatingSampleData && !_sampleDataLoadFailed)
                {
                    LoadSampleDataAsync();
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine(Caption);
                
                if (Caption != ColumnName)
                    sb.AppendLine($"Name: {ColumnName}");
                
                if (IsKey) 
                    sb.AppendLine("🔑 Key Column");
                if (IsRelationshipColumn && !IsKey)
                    sb.AppendLine("🔗 Relationship Column");
                    
                if (IsMeasure) 
                    sb.AppendLine("📊 Measure");
                else if (IsHierarchy) 
                    sb.AppendLine("📚 Hierarchy");
                else 
                    sb.AppendLine($"📋 Column");
                
                if (!string.IsNullOrEmpty(DataTypeName))
                    sb.AppendLine($"Data Type: {DataTypeName}");
                
                // Add format string if available
                if (!string.IsNullOrEmpty(FormatString))
                    sb.AppendLine($"Format: {FormatString}");
                
                // Sort by column indicator
                if (HasSortByColumn)
                    sb.AppendLine($"↕️ Sorted by: {SortByColumnName}");
                
                // Field parameter indicator
                if (HasVariations)
                    sb.AppendLine("🔀 Field Parameter");
                
                // Add statistics from metadata (like the metadata pane tooltip)
                // Use Vertipaq stats if available, otherwise fall back to CSDL values
                if (!IsMeasure && !IsHierarchy)
                {
                    // Prefer Vertipaq cardinality if available
                    if (_cardinality.HasValue)
                        sb.AppendLine($"Cardinality: {FormattedCardinality}");
                    else if (DistinctValues > 0)
                        sb.AppendLine($"Distinct Values: {DistinctValues:N0}");
                    
                    if (!string.IsNullOrEmpty(MinValue))
                        sb.AppendLine($"Min Value: {MinValue}");
                    
                    if (!string.IsNullOrEmpty(MaxValue))
                        sb.AppendLine($"Max Value: {MaxValue}");
                }
                
                // Enrichment: Vertipaq stats (if available)
                if (HasVertipaqStats)
                {
                    sb.AppendLine();
                    sb.AppendLine("─── Statistics ───");
                    if (_columnSizeBytes.HasValue)
                        sb.AppendLine($"Size: {FormattedColumnSize}");
                    if (_percentOfTable.HasValue)
                        sb.AppendLine($"% of Table: {FormattedPercentOfTable}");
                    if (!string.IsNullOrEmpty(_encoding))
                        sb.AppendLine($"Encoding: {_encoding}");
                }
                
                // Add measure expression preview (first 100 chars)
                if (IsMeasure && !string.IsNullOrEmpty(MeasureExpression))
                {
                    sb.AppendLine();
                    var expr = MeasureExpression.Length > 100 
                        ? MeasureExpression.Substring(0, 100) + "..." 
                        : MeasureExpression;
                    sb.AppendLine($"Expression: {expr}");
                }
                
                // Add sample data if available and enabled
                if (ShowSampleData)
                {
                    if (_updatingSampleData)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Sample Data: Loading...");
                    }
                    else if (HasSampleData)
                    {
                        sb.AppendLine();
                        sb.AppendLine("Sample Data:");
                        foreach (var sample in _sampleData)
                        {
                            sb.AppendLine($"  {sample}");
                        }
                    }
                }
                
                if (!IsVisible)
                    sb.AppendLine("👁 Hidden");
                    
                if (!string.IsNullOrEmpty(Description))
                {
                    sb.AppendLine();
                    sb.Append(Description);
                }
                    
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Whether to show sample data in the tooltip (based on options).
        /// For Model Diagram, we disable sample data loading to avoid performance issues
        /// and errors with calculated columns. Users can see sample data in the metadata pane.
        /// </summary>
        private bool ShowSampleData => false; // Disabled for Model Diagram - too many columns to query

        /// <summary>
        /// Whether sample data has been loaded.
        /// </summary>
        private bool HasSampleData => _sampleData != null && _sampleData.Count > 0;

        /// <summary>
        /// Asynchronously loads sample data for the column.
        /// This is intentionally async void as it's a fire-and-forget operation triggered by the Tooltip getter.
        /// Errors are caught and logged to prevent application crashes.
        /// </summary>
        private async void LoadSampleDataAsync()
        {
            // Skip sample data for hierarchies and measures - they don't have queryable data
            // Also skip if we already tried and failed (prevents retry loops)
            if (_metadataProvider == null || _updatingSampleData || _sampleDataLoadFailed || IsHierarchy || IsMeasure) return;
            
            _updatingSampleData = true;
            
            try
            {
                // Notify tooltip that we're loading (shows "Loading..." state)
                NotifyOfPropertyChange(nameof(Tooltip));
                
                var samples = await _metadataProvider.GetColumnSampleData(_column, SAMPLE_ROWS).ConfigureAwait(false);
                _sampleData = samples ?? new List<string>();
            }
            catch (Exception ex)
            {
                // Log but don't crash - sample data is not critical
                // Mark as failed to prevent retry loops on error columns
                Log.Warning(ex, "Error loading sample data for column {ColumnName}", ColumnName);
                _sampleData = new List<string>();
                _sampleDataLoadFailed = true;
            }
            finally
            {
                _updatingSampleData = false;
                // Marshal back to UI thread for property change notification
                await Application.Current.Dispatcher.InvokeAsync(() => NotifyOfPropertyChange(nameof(Tooltip)));
            }
        }

        // Expose additional column metadata properties for tooltip
        public string FormatString => _isFromVpa ? null : _column?.FormatString;
        public long DistinctValues => _isFromVpa ? 0 : _column?.DistinctValues ?? 0;
        public string MinValue => _isFromVpa ? null : _column?.MinValue;
        public string MaxValue => _isFromVpa ? null : _column?.MaxValue;

        private bool _showDataType;
        public bool ShowDataType
        {
            get => _showDataType;
            set { _showDataType = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(DisplayText)); }
        }

        public string DisplayText => ShowDataType && !string.IsNullOrEmpty(DataTypeName)
            ? $"{Caption} ({DataTypeName})"
            : Caption;

        private bool _isSearchMatch = true;
        public bool IsSearchMatch
        {
            get => _isSearchMatch;
            set { _isSearchMatch = value; NotifyOfPropertyChange(); }
        }

        private bool _isHighlighted;
        /// <summary>
        /// Whether this column is highlighted (e.g., when its relationship is selected).
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; NotifyOfPropertyChange(); }
        }

        #region Optional Enrichment Properties (VPA)

        // These properties are optionally populated when Vertipaq Analyzer is run.
        // If not populated, the UI shows the base column information.

        private long? _cardinality;
        /// <summary>
        /// Cardinality (distinct value count) from Vertipaq Analyzer.
        /// More accurate than the CSDL DistinctValues for some scenarios.
        /// </summary>
        public long? Cardinality
        {
            get => _cardinality;
            set 
            { 
                _cardinality = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(HasVertipaqStats));
                NotifyOfPropertyChange(nameof(FormattedCardinality));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted cardinality for display.
        /// </summary>
        public string FormattedCardinality => _cardinality.HasValue ? FormatNumber(_cardinality.Value) : null;

        private long? _columnSizeBytes;
        /// <summary>
        /// Total size of this column in bytes (memory footprint).
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? ColumnSizeBytes
        {
            get => _columnSizeBytes;
            set 
            { 
                _columnSizeBytes = value; 
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(nameof(FormattedColumnSize));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted column size for display (e.g., "1.5 MB").
        /// </summary>
        public string FormattedColumnSize => _columnSizeBytes.HasValue ? FormatBytes(_columnSizeBytes.Value) : null;

        private string _encoding;
        /// <summary>
        /// Column encoding type (Hash, Value, RunLength).
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public string Encoding
        {
            get => _encoding;
            set 
            { 
                _encoding = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        private double? _percentOfTable;
        /// <summary>
        /// Percentage of table size this column represents.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public double? PercentOfTable
        {
            get => _percentOfTable;
            set 
            { 
                _percentOfTable = value; 
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(FormattedPercentOfTable));
                NotifyOfPropertyChange(nameof(Tooltip));
            }
        }

        /// <summary>
        /// Formatted percentage of table for display.
        /// </summary>
        public string FormattedPercentOfTable => _percentOfTable.HasValue 
            ? $"{_percentOfTable.Value:P1}" 
            : null;

        private long? _dataSizeBytes;
        /// <summary>
        /// Data size portion of the column (vs dictionary).
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? DataSizeBytes
        {
            get => _dataSizeBytes;
            set 
            { 
                _dataSizeBytes = value; 
                NotifyOfPropertyChange();
            }
        }

        private long? _dictionarySizeBytes;
        /// <summary>
        /// Dictionary size portion of the column.
        /// Populated from Vertipaq Analyzer statistics.
        /// </summary>
        public long? DictionarySizeBytes
        {
            get => _dictionarySizeBytes;
            set 
            { 
                _dictionarySizeBytes = value; 
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Whether Vertipaq Analyzer statistics are available for this column.
        /// </summary>
        public bool HasVertipaqStats => _cardinality.HasValue || _columnSizeBytes.HasValue;

        /// <summary>
        /// Gets the currently displayed statistic text based on user preference.
        /// Returns the appropriate formatted value based on Options.DiagramColumnStatDisplay.
        /// </summary>
        public string DisplayedStat
        {
            get
            {
                if (!HasVertipaqStats) return null;
                
                switch (_options?.DiagramColumnStatDisplay ?? DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Cardinality)
                {
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.None:
                        return null;
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Size:
                        return FormattedColumnSize;
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Cardinality:
                    default:
                        return FormattedCardinality;
                }
            }
        }

        /// <summary>
        /// Gets the tooltip for the displayed stat.
        /// </summary>
        public string DisplayedStatTooltip
        {
            get
            {
                if (!HasVertipaqStats) return null;
                
                switch (_options?.DiagramColumnStatDisplay ?? DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Cardinality)
                {
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.None:
                        return null;
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Size:
                        return $"Column size: {FormattedColumnSize}";
                    case DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Cardinality:
                    default:
                        return $"Cardinality: {FormattedCardinality}";
                }
            }
        }

        /// <summary>
        /// Should the stat display be visible based on user preference?
        /// </summary>
        public bool ShowStatDisplay
        {
            get
            {
                if (!HasVertipaqStats) return false;
                return (_options?.DiagramColumnStatDisplay ?? DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.Cardinality) 
                    != DaxStudio.Interfaces.Enums.DiagramColumnStatDisplay.None;
            }
        }

        /// <summary>
        /// Called when the stat display preference changes to refresh bindings.
        /// </summary>
        public void NotifyStatDisplayChanged()
        {
            NotifyOfPropertyChange(nameof(DisplayedStat));
            NotifyOfPropertyChange(nameof(DisplayedStatTooltip));
            NotifyOfPropertyChange(nameof(ShowStatDisplay));
        }

        /// <summary>
        /// Clears all enrichment data.
        /// </summary>
        public void ClearEnrichmentData()
        {
            _cardinality = null;
            _columnSizeBytes = null;
            _encoding = null;
            _percentOfTable = null;
            _dataSizeBytes = null;
            _dictionarySizeBytes = null;
            
            NotifyOfPropertyChange(nameof(Cardinality));
            NotifyOfPropertyChange(nameof(ColumnSizeBytes));
            NotifyOfPropertyChange(nameof(Encoding));
            NotifyOfPropertyChange(nameof(PercentOfTable));
            NotifyOfPropertyChange(nameof(HasVertipaqStats));
            NotifyOfPropertyChange(nameof(Tooltip));
        }

        /// <summary>
        /// Formats a byte count for display.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return $"{size:N1} {suffixes[suffixIndex]}";
        }

        /// <summary>
        /// Formats a large number for compact display.
        /// </summary>
        private static string FormatNumber(long number)
        {
            if (number >= 1_000_000_000)
                return $"{number / 1_000_000_000.0:N1}B";
            if (number >= 1_000_000)
                return $"{number / 1_000_000.0:N1}M";
            if (number >= 1_000)
                return $"{number / 1_000.0:N1}K";
            return number.ToString("N0");
        }

        #endregion
    }

    /// <summary>
    /// ViewModel for a relationship line in the Model Diagram.
    /// </summary>
    public class ModelDiagramRelationshipViewModel : PropertyChangedBase
    {
        private readonly ADOTabularRelationship _relationship;
        private readonly ModelDiagramTableViewModel _fromTable;
        private readonly ModelDiagramTableViewModel _toTable;

        public ModelDiagramRelationshipViewModel(ADOTabularRelationship relationship, ModelDiagramTableViewModel fromTable, ModelDiagramTableViewModel toTable)
        {
            _relationship = relationship;
            _fromTable = fromTable;
            _toTable = toTable;
        }

        public ModelDiagramTableViewModel FromTableViewModel => _fromTable;
        public ModelDiagramTableViewModel ToTableViewModel => _toTable;

        public string FromTable => _relationship.FromTable?.Name ?? _fromTable?.TableName;
        public string FromColumn => _relationship.FromColumn;
        public string ToTable => _relationship.ToTable?.Name ?? _toTable?.TableName;
        public string ToColumn => _relationship.ToColumn;
        public bool IsActive => _relationship.IsActive;

        /// <summary>
        /// The "From" side cardinality symbol (1 or *)
        /// </summary>
        public string FromCardinality => _relationship.FromColumnMultiplicity == "*" ? "*" : "1";

        /// <summary>
        /// Font size for the "From" cardinality - larger for * symbol
        /// </summary>
        public double FromCardinalityFontSize => FromCardinality == "*" ? 16 : 12;

        /// <summary>
        /// Margin for the "From" cardinality - negative top/bottom for * to maintain consistent border height
        /// </summary>
        public Thickness FromCardinalityMargin => FromCardinality == "*" ? new Thickness(0, -2, 0, -4) : new Thickness(0,-2,0,-2);

        /// <summary>
        /// The "To" side cardinality symbol (1 or *)
        /// </summary>
        public string ToCardinality => _relationship.ToColumnMultiplicity == "*" ? "*" : "1";

        /// <summary>
        /// Font size for the "To" cardinality - larger for * symbol
        /// </summary>
        public double ToCardinalityFontSize => ToCardinality == "*" ? 16 : 12;

        /// <summary>
        /// Margin for the "To" cardinality - negative top/bottom for * to maintain consistent border height
        /// </summary>
        public Thickness ToCardinalityMargin => ToCardinality == "*" ? new Thickness(0, -4, 0, -4) : new Thickness(0);

        /// <summary>
        /// Tooltip for the "From" side cardinality.
        /// </summary>
        public string FromCardinalityTooltip => $"{FromTable}[{FromColumn}] ({(FromCardinality == "*" ? "Many" : "One")})";

        /// <summary>
        /// Tooltip for the "To" side cardinality.
        /// </summary>
        public string ToCardinalityTooltip => $"{ToTable}[{ToColumn}] ({(ToCardinality == "*" ? "Many" : "One")})";

        /// <summary>
        /// Whether this relationship is many-to-many.
        /// </summary>
        public bool IsManyToMany =>
            _relationship.FromColumnMultiplicity == "*" && _relationship.ToColumnMultiplicity == "*";

        /// <summary>
        /// Whether this relationship has bi-directional cross-filtering.
        /// </summary>
        public bool IsBidirectional =>
            string.Equals(_relationship.CrossFilterDirection, "Both", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Raw cross-filter direction value from the relationship (for debugging).
        /// </summary>
        public string CrossFilterDirectionRaw => _relationship.CrossFilterDirection;

        /// <summary>
        /// Inverse of IsActive for binding convenience.
        /// </summary>
        public bool IsInactive => !IsActive;

        /// <summary>
        /// Whether the center indicator panel should be shown (has BiDi, M:M, or Inactive).
        /// </summary>
        public bool HasCenterIndicators => IsBidirectional || IsManyToMany || IsInactive;

        /// <summary>
        /// Whether the filter flows from "From" to "To" (single direction or both)
        /// </summary>
        public bool FiltersToEnd => true; // Filter always flows from "From" side

        /// <summary>
        /// Whether the filter flows from "To" to "From" (bi-directional only)
        /// </summary>
        public bool FiltersToStart => IsBidirectional;

        /// <summary>
        /// Text representation of cardinality for display.
        /// </summary>
        public string CardinalityText
        {
            get
            {
                var from = _relationship.FromColumnMultiplicity == "*" ? "*" : "1";
                var to = _relationship.ToColumnMultiplicity == "*" ? "*" : "1";
                return $"{from}:{to}";
            }
        }

        /// <summary>
        /// Tooltip with relationship details.
        /// </summary>
        public string Tooltip => $"{FromTable}[{FromColumn}] → {ToTable}[{ToColumn}]\n" +
                                 $"Cardinality: {CardinalityText}\n" +
                                 $"Cross-filter: {_relationship.CrossFilterDirection}\n" +
                                 $"Active: {(IsActive ? "Yes" : "No")}";

        #region Visibility

        private bool _isVisible = true;
        /// <summary>
        /// Whether this relationship line should be visible (both connected tables are visible).
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; NotifyOfPropertyChange(); }
        }

        #endregion

        #region Highlighting

        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; NotifyOfPropertyChange(); }
        }

        private bool _isDimmed;
        public bool IsDimmed
        {
            get => _isDimmed;
            set { _isDimmed = value; NotifyOfPropertyChange(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Tracks whether UpdatePath has been called at least once.
        /// </summary>
        private bool _isPathInitialized;

        /// <summary>
        /// Offset for parallel relationships between the same table pair.
        /// 0 = centered, negative = offset left/up, positive = offset right/down.
        /// </summary>
        private double _parallelOffset;
        public double ParallelOffset
        {
            get => _parallelOffset;
            set { _parallelOffset = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(PathData)); }
        }

        /// <summary>
        /// Slot offset for the start point (on the from table's edge).
        /// Used to distribute multiple relationships along a table edge.
        /// </summary>
        private double _startEdgeSlotOffset;
        public double StartEdgeSlotOffset
        {
            get => _startEdgeSlotOffset;
            set { _startEdgeSlotOffset = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// Slot offset for the end point (on the to table's edge).
        /// Used to distribute multiple relationships along a table edge.
        /// </summary>
        private double _endEdgeSlotOffset;
        public double EndEdgeSlotOffset
        {
            get => _endEdgeSlotOffset;
            set { _endEdgeSlotOffset = value; NotifyOfPropertyChange(); }
        }

        /// <summary>
        /// The edge type used for the start of the relationship line (publicly accessible).
        /// </summary>
        public EdgeTypePublic StartEdgeType => ConvertToPublicEdgeType(_startEdge);

        /// <summary>
        /// The edge type used for the end of the relationship line (publicly accessible).
        /// </summary>
        public EdgeTypePublic EndEdgeType => ConvertToPublicEdgeType(_endEdge);

        /// <summary>
        /// Converts internal EdgeType to public EdgeTypePublic enum.
        /// </summary>
        private EdgeTypePublic ConvertToPublicEdgeType(EdgeType edgeType)
        {
            return edgeType switch
            {
                EdgeType.Left => EdgeTypePublic.Left,
                EdgeType.Right => EdgeTypePublic.Right,
                EdgeType.Top => EdgeTypePublic.Top,
                EdgeType.Bottom => EdgeTypePublic.Bottom,
                _ => EdgeTypePublic.Right
            };
        }

        #endregion

        #region Line Path

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
        /// Path data for drawing the relationship line (bezier curve).
        /// Uses the actual start/end positions which include edge slot offsets.
        /// </summary>
        public string PathData
        {
            get
            {
                // Return empty if positions not initialized
                if (!HasValidPositions) return string.Empty;

                // Determine if this is a horizontal or vertical relationship
                bool isVertical = (_startEdge == EdgeType.Top || _startEdge == EdgeType.Bottom);

                // Use actual positions (already include slot offsets)
                double startX = ActualStartX;
                double startY = ActualStartY;
                double endX = ActualEndX;
                double endY = ActualEndY;

                if (isVertical)
                {
                    // Vertical bezier curve (for top/bottom connections)
                    double midY = (startY + endY) / 2;
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "M {0},{1} C {2},{3} {4},{5} {6},{7}",
                        startX, startY, startX, midY, endX, midY, endX, endY);
                }
                else
                {
                    // Horizontal bezier curve (for left/right connections)
                    double midX = (startX + endX) / 2;
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "M {0},{1} C {2},{3} {4},{5} {6},{7}",
                        startX, startY, midX, startY, midX, endY, endX, endY);
                }
            }
        }

        /// <summary>
        /// Actual start X position (base position + slot offset for vertical edges).
        /// </summary>
        public double ActualStartX
        {
            get
            {
                bool isVertical = (_startEdge == EdgeType.Top || _startEdge == EdgeType.Bottom);
                return isVertical ? StartX + _startEdgeSlotOffset : StartX;
            }
        }

        /// <summary>
        /// Actual start Y position (base position + slot offset for horizontal edges).
        /// </summary>
        public double ActualStartY
        {
            get
            {
                bool isVertical = (_startEdge == EdgeType.Top || _startEdge == EdgeType.Bottom);
                return isVertical ? StartY : StartY + _startEdgeSlotOffset;
            }
        }

        /// <summary>
        /// Actual end X position (base position + slot offset for vertical edges).
        /// </summary>
        public double ActualEndX
        {
            get
            {
                bool isVertical = (_endEdge == EdgeType.Top || _endEdge == EdgeType.Bottom);
                return isVertical ? EndX + _endEdgeSlotOffset : EndX;
            }
        }

        /// <summary>
        /// Actual end Y position (base position + slot offset for horizontal edges).
        /// </summary>
        public double ActualEndY
        {
            get
            {
                bool isVertical = (_endEdge == EdgeType.Top || _endEdge == EdgeType.Bottom);
                return isVertical ? EndY : EndY + _endEdgeSlotOffset;
            }
        }

        /// <summary>
        /// Label position (middle of the line), using actual positions.
        /// </summary>
        public double LabelX => (ActualStartX + ActualEndX) / 2;

        public double LabelY => (ActualStartY + ActualEndY) / 2;

        /// <summary>
        /// Rotation angle for filter direction arrow pointing toward "From" table (where filter originates).
        /// 0 = right, 90 = down, 180 = left, 270 = up
        /// </summary>
        public double FilterDirectionAngle
        {
            get
            {
                // Point toward the From table (filter flows from "one" side to "many" side)
                double dx = ActualStartX - ActualEndX;
                double dy = ActualStartY - ActualEndY;
                double angleRadians = Math.Atan2(dy, dx);
                return angleRadians * 180 / Math.PI;
            }
        }

        /// <summary>
        /// Rotation angle for reverse filter direction (BiDi) pointing toward "To" table.
        /// </summary>
        public double ReverseFilterDirectionAngle => FilterDirectionAngle + 180;

        // Label dimensions - must match XAML fixed dimensions (Height=18, Width=16)
        private const double CardinalityLabelWidth = 16;
        private const double CardinalityLabelHeight = 18;
        private const double CardinalityGap = 2; // Gap between label and table edge

        /// <summary>
        /// Position for the "From" cardinality label (adjacent to the table edge at the line endpoint).
        /// For left/right edges: X touches the table, Y centers on the connection point (with slot offset).
        /// For top/bottom edges: Y touches the table, X centers on the connection point (with slot offset).
        /// </summary>
        public double FromCardinalityX
        {
            get
            {
                switch (_startEdge)
                {
                    case EdgeType.Top:
                    case EdgeType.Bottom:
                        // Vertical edge: center horizontally on the connection point (includes slot offset)
                        return ActualStartX - CardinalityLabelWidth / 2;
                    case EdgeType.Left:
                        // Left edge: position to the left of the table edge (touching)
                        return StartX - CardinalityLabelWidth - CardinalityGap;
                    case EdgeType.Right:
                        // Right edge: position to the right of the table edge (touching)
                        return StartX + CardinalityGap;
                    default:
                        return ActualStartX - CardinalityLabelWidth / 2;
                }
            }
        }

        public double FromCardinalityY
        {
            get
            {
                switch (_startEdge)
                {
                    case EdgeType.Top:
                        // Top edge: position above the table edge (touching)
                        return StartY - CardinalityLabelHeight - CardinalityGap;
                    case EdgeType.Bottom:
                        // Bottom edge: position below the table edge (touching)
                        return StartY + CardinalityGap;
                    case EdgeType.Left:
                    case EdgeType.Right:
                        // Horizontal edge: center vertically on the connection point (includes slot offset)
                        return ActualStartY - CardinalityLabelHeight / 2;
                    default:
                        return ActualStartY - CardinalityLabelHeight / 2;
                }
            }
        }

        /// <summary>
        /// Position for the "To" cardinality label (adjacent to the table edge at the line endpoint).
        /// For left/right edges: X touches the table, Y centers on the connection point (with slot offset).
        /// For top/bottom edges: Y touches the table, X centers on the connection point (with slot offset).
        /// </summary>
        public double ToCardinalityX
        {
            get
            {
                switch (_endEdge)
                {
                    case EdgeType.Top:
                    case EdgeType.Bottom:
                        // Vertical edge: center horizontally on the connection point (includes slot offset)
                        return ActualEndX - CardinalityLabelWidth / 2;
                    case EdgeType.Left:
                        // Left edge: position to the left of the table edge (touching)
                        return EndX - CardinalityLabelWidth - CardinalityGap;
                    case EdgeType.Right:
                        // Right edge: position to the right of the table edge (touching)
                        return EndX + CardinalityGap;
                    default:
                        return ActualEndX - CardinalityLabelWidth / 2;
                }
            }
        }

        public double ToCardinalityY
        {
            get
            {
                switch (_endEdge)
                {
                    case EdgeType.Top:
                        // Top edge: position above the table edge (touching)
                        return EndY - CardinalityLabelHeight - CardinalityGap;
                    case EdgeType.Bottom:
                        // Bottom edge: position below the table edge (touching)
                        return EndY + CardinalityGap;
                    case EdgeType.Left:
                    case EdgeType.Right:
                        // Horizontal edge: center vertically on the connection point (includes slot offset)
                        return ActualEndY - CardinalityLabelHeight / 2;
                    default:
                        return ActualEndY - CardinalityLabelHeight / 2;
                }
            }
        }

        /// <summary>
        /// Returns true when the relationship path has been initialized via UpdatePath().
        /// </summary>
        public bool HasValidPositions => _isPathInitialized;

        /// <summary>
        /// Arrow path data pointing toward the "To" table (filter direction).
        /// Returns empty string - arrows removed to avoid visual artifacts.
        /// Filter direction is indicated by the relationship flowing from "From" to "To" table.
        /// </summary>
        public string ArrowToPathData => string.Empty;

        /// <summary>
        /// Arrow path data pointing toward the "From" table (bi-directional filter).
        /// Returns empty string - arrows removed to avoid visual artifacts.
        /// BiDi is indicated by the "BiDi" label in the center of the relationship line.
        /// </summary>
        public string ArrowFromPathData => string.Empty;

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
        /// Updates the path based on table positions.
        /// Chooses the optimal edge (top, bottom, left, right) to minimize line crossings.
        /// </summary>
        public void UpdatePath()
        {
            // Null check to prevent NullReferenceException
            if (_fromTable == null || _toTable == null)
            {
                _isPathInitialized = false;
                return;
            }

            // Calculate distances for each edge combination and pick the shortest
            var fromCenter = new Point(_fromTable.CenterX, _fromTable.CenterY);
            var toCenter = new Point(_toTable.CenterX, _toTable.CenterY);

            // Calculate the angle between centers to determine primary direction
            double dx = toCenter.X - fromCenter.X;
            double dy = toCenter.Y - fromCenter.Y;

            // Determine if the relationship is more horizontal or vertical
            bool isMoreHorizontal = Math.Abs(dx) > Math.Abs(dy);

            // Check for overlapping tables (use vertical edges for stacked tables)
            bool tablesOverlapHorizontally = 
                _fromTable.X < _toTable.X + _toTable.Width && 
                _fromTable.X + _fromTable.Width > _toTable.X;
            bool tablesOverlapVertically = 
                _fromTable.Y < _toTable.Y + _toTable.Height && 
                _fromTable.Y + _fromTable.Height > _toTable.Y;

            if (tablesOverlapHorizontally && !tablesOverlapVertically)
            {
                // Tables are stacked vertically - use top/bottom edges
                if (fromCenter.Y < toCenter.Y)
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
                if (fromCenter.X < toCenter.X)
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

            // Mark as initialized so paths will render
            _isPathInitialized = true;

            // Notify about derived properties
            NotifyOfPropertyChange(nameof(HasValidPositions));
            NotifyOfPropertyChange(nameof(PathData));
            NotifyOfPropertyChange(nameof(FromCardinalityX));
            NotifyOfPropertyChange(nameof(FromCardinalityY));
            NotifyOfPropertyChange(nameof(ToCardinalityX));
            NotifyOfPropertyChange(nameof(ToCardinalityY));
            NotifyOfPropertyChange(nameof(ArrowToPathData));
            NotifyOfPropertyChange(nameof(ArrowFromPathData));
            NotifyOfPropertyChange(nameof(FilterDirectionAngle));
            NotifyOfPropertyChange(nameof(ReverseFilterDirectionAngle));
            NotifyOfPropertyChange(nameof(StartEdgeType));
            NotifyOfPropertyChange(nameof(EndEdgeType));
        }

        /// <summary>
        /// Updates the path after slot offsets have been calculated.
        /// Called by CalculateEdgeSlots after assigning slot positions.
        /// </summary>
        public void UpdatePathWithSlots()
        {
            // Re-notify all derived properties that depend on actual positions
            NotifyOfPropertyChange(nameof(PathData));
            NotifyOfPropertyChange(nameof(LabelX));
            NotifyOfPropertyChange(nameof(LabelY));
            NotifyOfPropertyChange(nameof(FromCardinalityX));
            NotifyOfPropertyChange(nameof(FromCardinalityY));
            NotifyOfPropertyChange(nameof(ToCardinalityX));
            NotifyOfPropertyChange(nameof(ToCardinalityY));
            NotifyOfPropertyChange(nameof(ActualStartX));
            NotifyOfPropertyChange(nameof(ActualStartY));
            NotifyOfPropertyChange(nameof(ActualEndX));
            NotifyOfPropertyChange(nameof(ActualEndY));
            NotifyOfPropertyChange(nameof(FilterDirectionAngle));
            NotifyOfPropertyChange(nameof(ReverseFilterDirectionAngle));
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

        /// <summary>
        /// Simple Point struct for calculations.
        /// </summary>
        private struct Point
        {
            public double X, Y;
            public Point(double x, double y) { X = x; Y = y; }
        }

        #endregion
    }

    #region Layout Persistence Classes

    /// <summary>
    /// Lightweight data structure for persisting model diagram layouts.
    /// </summary>
    public class ModelLayoutData
    {
        public string ModelKey { get; set; }
        public DateTime LastModified { get; set; }
        public Dictionary<string, TablePosition> TablePositions { get; set; } = new Dictionary<string, TablePosition>();
        public List<AnnotationData> Annotations { get; set; } = new List<AnnotationData>();
    }

    /// <summary>
    /// Position data for a single table.
    /// </summary>
    public class TablePosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsCollapsed { get; set; }
        public double ExpandedHeight { get; set; }
    }

    /// <summary>
    /// Data for a single annotation.
    /// </summary>
    public class AnnotationData
    {
        public string Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Text { get; set; }
        public string BackgroundColor { get; set; }
        public double FontSize { get; set; } = 11;
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
    }

    #endregion

    #region Annotation ViewModel

    /// <summary>
    /// ViewModel for a text annotation in the Model Diagram.
    /// </summary>
    public class ModelDiagramAnnotationViewModel : PropertyChangedBase
    {
        public ModelDiagramAnnotationViewModel()
        {
            Id = Guid.NewGuid().ToString("N");
        }

        public ModelDiagramAnnotationViewModel(AnnotationData data)
        {
            Id = data.Id ?? Guid.NewGuid().ToString("N");
            _x = data.X;
            _y = data.Y;
            _width = data.Width > 0 ? data.Width : 150;
            _height = data.Height > 0 ? data.Height : 60;
            _text = data.Text ?? "";
            _backgroundColor = data.BackgroundColor ?? "#FFFDE7"; // Light yellow default
            _fontSize = data.FontSize > 0 ? data.FontSize : 11;
            _isBold = data.IsBold;
            _isItalic = data.IsItalic;
        }

        public string Id { get; }

        private double _x;
        public double X
        {
            get => _x;
            set { _x = value; NotifyOfPropertyChange(); }
        }

        private double _y;
        public double Y
        {
            get => _y;
            set { _y = value; NotifyOfPropertyChange(); }
        }

        private double _width = 150;
        public double Width
        {
            get => _width;
            set { _width = Math.Max(80, value); NotifyOfPropertyChange(); }
        }

        private double _height = 60;
        public double Height
        {
            get => _height;
            set { _height = Math.Max(30, value); NotifyOfPropertyChange(); }
        }

        private string _text = "";
        public string Text
        {
            get => _text;
            set { _text = value; NotifyOfPropertyChange(); }
        }

        private string _backgroundColor = "#FFFDE7"; // Light yellow
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; NotifyOfPropertyChange(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; NotifyOfPropertyChange(); }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; NotifyOfPropertyChange(); }
        }

        private double _fontSize = 11;
        public double FontSize
        {
            get => _fontSize;
            set { _fontSize = value; NotifyOfPropertyChange(); }
        }

        private bool _isBold;
        public bool IsBold
        {
            get => _isBold;
            set { _isBold = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(FontWeight)); }
        }

        public System.Windows.FontWeight FontWeight => _isBold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal;

        private bool _isItalic;
        public bool IsItalic
        {
            get => _isItalic;
            set { _isItalic = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(FontStyle)); }
        }

        public System.Windows.FontStyle FontStyle => _isItalic ? System.Windows.FontStyles.Italic : System.Windows.FontStyles.Normal;

        /// <summary>
        /// Converts this annotation to persistence data.
        /// </summary>
        public AnnotationData ToData()
        {
            return new AnnotationData
            {
                Id = Id,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                Text = Text,
                BackgroundColor = BackgroundColor,
                FontSize = FontSize,
                IsBold = IsBold,
                IsItalic = IsItalic
            };
        }
    }

    #endregion
}
