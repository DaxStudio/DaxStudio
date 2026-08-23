using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using ICSharpCode.AvalonEdit.Document;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
//using System.Windows.Forms;
using System.Windows.Input;
using UnitComboLib.Unit.Screen;
using UnitComboLib.ViewModel;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core;
using DaxStudio.Core.Model;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    [Export(typeof(IToolWindow))]
    public class QueryResultsPaneViewModel : ToolWindowBase
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<QueryStartedEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<UpdateGlobalOptions>
        , IHandle<SizeUnitsUpdatedEvent>
        , IHandle<CopyWithHeadersEvent>
        , ISaveState
    {
        private DataTable _resultsTable;
        private string _selectedWorksheet;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;
        private readonly IGlobalOptions _options;

        [ImportingConstructor]
        public QueryResultsPaneViewModel(IEventAggregator eventAggregator, IDaxStudioHost host, IGlobalOptions options) : this(new DataTable("Empty"))
        {
            _eventAggregator = eventAggregator;
            //_eventAggregator.Subscribe(this);
            _host = host;
            _options = options;
            var items = new ObservableCollection<ListItem>(ScreenUnitsHelper.GenerateScreenUnitList());
            SizeUnits = new UnitViewModel(items, new ScreenConverter(_options.ResultFontSizePx), 0);
            //UpdateSettings();
        }

        public QueryResultsPaneViewModel(DataTable resultsTable)
        {
            _resultsTable = resultsTable;

        }

        public override string Title => "Results";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "results";


        public DataSet ResultsDataSet
        {
            get { return _resultsDataSet; }
            set
            {
                _resultsDataSet?.Dispose();
                _resultsDataSet = value;
                // Rebuild the visible tabs to contain one data-grid tab per result table. This is the
                // classic / back-compat path (e.g. a cancelled query resetting to an empty DataSet);
                // the richer interspersed path (data + SHOW tabs) goes through SetResultTabs.
                RebuildDataTabsFromDataSet();
                ShowResultsTable = _resultTabs.Count > 0;
                SelectedTableIndex = 0;
                NotifyOfPropertyChange(() => SelectedTableIndex);
            }
        }

        /// <summary>The heterogeneous set of tabs shown in the Results TabControl: query-result data
        /// grids and Comment Script <c>--&gt; SHOW</c> tree-grids interspersed in execution order.</summary>
        private readonly BindableCollection<ResultTabViewModel> _resultTabs = new BindableCollection<ResultTabViewModel>();
        public BindableCollection<ResultTabViewModel> ResultTabs => _resultTabs;

        private void RebuildDataTabsFromDataSet()
        {
            Execute.OnUIThread(() =>
            {
                _resultTabs.Clear();
                if (_resultsDataSet != null)
                {
                    foreach (DataTable table in _resultsDataSet.Tables)
                    {
                        _resultTabs.Add(new DataTableResultTab(table));
                    }
                }
                NotifyOfPropertyChange(() => Tables);
                NotifyOfPropertyChange(() => ShowResultsMessage);
            });
        }

        /// <summary>
        /// Populates the Results pane with an ordered set of tabs - a mix of query-result data grids
        /// and Comment Script <c>--&gt; SHOW</c> tree-grids - preserving batch execution order. This
        /// replaces the previous approach of assigning <see cref="ResultsDataSet"/> and separately
        /// overlaying a single SHOW tree.
        /// </summary>
        public void SetResultTabs(IList<DaxStudio.Core.Model.ResultTabDescriptor> tabs)
        {
            Execute.OnUIThread(() =>
            {
                // The DataSet is still kept in sync (row-counts, exports and other consumers read it)
                // and holds only the query-result tables in their execution order.
                _resultsDataSet?.Dispose();
                _resultsDataSet = new DataSet();

                _resultTabs.Clear();
                if (tabs != null)
                {
                    foreach (var tab in tabs)
                    {
                        if (tab.IsShowTree)
                        {
                            _resultTabs.Add(new ShowTreeResultTab(tab.ShowTreeRoots, tab.ShowType));
                        }
                        else if (tab.Table != null)
                        {
                            if (tab.Table.DataSet != null) tab.Table.DataSet.Tables.Remove(tab.Table);
                            _resultsDataSet.Tables.Add(tab.Table);
                            _resultTabs.Add(new DataTableResultTab(tab.Table));
                        }
                    }
                }

                ShowResultsTable = _resultTabs.Count > 0;
                NotifyOfPropertyChange(() => Tables);
                SelectedTableIndex = 0;
                NotifyOfPropertyChange(() => SelectedTableIndex);
                NotifyOfPropertyChange(() => ShowResultsMessage);
            });
        }

        private int _selectedTabIndex = -1;
        public int SelectedTableIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                _selectedTabIndex = value;
                if (_document != null && value >= 0 && value < _resultTabs.Count)
                {
                    // a SHOW tree tab has no row data, so it reports a zero row-count
                    _document.RowCount = _resultTabs[value] is DataTableResultTab dataTab ? dataTab.RowCount : 0;
                }
                NotifyOfPropertyChange(() => SelectedTableIndex);
            }
        }
        public DataTableCollection Tables
        {
            get
            {
                if (_resultsDataSet == null) return null;
                return _resultsDataSet.Tables;
            }
        }

        //public void CopyAllResultsToClipboard(object obj)
        //{
        //    System.Diagnostics.Debug.WriteLine(obj);
        //    Clipboard.SetData("CommaSeparatedValue", ResultsDataTable.ToCsv());
        //}

        public DataView ResultsDataView
        { get { return _resultsTable == null ? new DataTable("blank").AsDataView() : _resultsTable.AsDataView(); } }

        /// <summary>
        /// The <see cref="DataTable"/> backing the currently selected result tab (or the first
        /// data-result tab if the selection isn't a data grid). Returns null when there are no
        /// query results. Used to build a <c>--&gt; ASSERT TABLE</c> block from the live results.
        /// </summary>
        public DataTable ActiveResultsTable
        {
            get
            {
                if (_selectedTabIndex >= 0 && _selectedTabIndex < _resultTabs.Count
                    && _resultTabs[_selectedTabIndex] is DataTableResultTab selected)
                {
                    return selected.Table;
                }

                foreach (var tab in _resultTabs)
                {
                    if (tab is DataTableResultTab dataTab) return dataTab.Table;
                }

                return _resultsTable;
            }
        }

        public void OnListViewItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("in OnListViewItemPreviewMouseRightButtonDown");
        }


        private bool _showResultsTable;
        public bool ShowResultsTable
        {
            get
            {
                return _showResultsTable;
            }
            private set
            {
                _showResultsTable = value;
                if (value) ResultsMessage = string.Empty;
                NotifyOfPropertyChange(() => ShowResultsTable);
                NotifyOfPropertyChange(() => ShowResultsMessage);
            }
        }

        private string _resultsMessage;
        public string ResultsMessage
        {
            get { return _resultsMessage; }
            set
            {
                _resultsMessage = value;
                NotifyOfPropertyChange(() => ResultsMessage);
            }
        }


        //private bool _showResultsMessage;
        public bool ShowResultsMessage
        {
            get { return !ShowResultsTable && !ShowErrorMessage && _resultTabs.Count == 0; }
        }
        private OutputTarget _icon;
        public OutputTarget ResultsIcon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                NotifyOfPropertyChange(() => ResultsIcon);
                NotifyOfPropertyChange(() => ShowWorksheets);
            }
        }

        private double _fontSize = 20;
        public double FontSize
        {
            get { return _fontSize; }
            set
            {
                _fontSize = value;
                NotifyOfPropertyChange(() => FontSize);
            }
        }

        private string _fontFamily = "Arial";
        public string FontFamily
        {
            get { return _fontFamily; }
            set
            {
                _fontFamily = value;
                NotifyOfPropertyChange(() => FontFamily);
            }
        }

        public Task HandleAsync(QueryResultsPaneMessageEvent message, CancellationToken cancellationToken)
        {
            if (message.Target == null) return Task.CompletedTask;
            ResultsIcon = message.Target.Icon;
            ResultsMessage = message.Target.Message;
            return Task.CompletedTask;
        }

        public IEnumerable<string> Worksheets
        {
            get { return _host.Proxy.Worksheets; }
        }

        public string SelectedWorksheet
        {
            get { return _selectedWorksheet; }
            set
            {
                _selectedWorksheet = value;
                _eventAggregator.PublishAsync(new SetSelectedWorksheetEvent(_selectedWorksheet));
            }
        }
        private DocumentViewModel _document;
        public Task HandleAsync(ActivateDocumentEvent message, CancellationToken cancellationToken)
        {
            _document = message.Document;
            if (_host.IsExcel)
            {
                // refresh workbooks and worksheet properties if the host is excel
                SelectedWorkbook = _host.Proxy.WorkbookName;
                SelectedWorksheet = message.Document.SelectedWorksheet;
                NotifyOfPropertyChange(() => Worksheets);
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(NewDocumentEvent message, CancellationToken cancellationToken)
        {
            await _eventAggregator.PublishAsync(new QueryResultsPaneMessageEvent(message.Target));
            if (message.Target is IActivateResults) { this.Activate(); }
            //ResultsIcon = message.Target.Icon;
            //ResultsMessage = message.Target.Message;
        }

        public bool ShowWorksheets
        {
            get
            {
                // Only show the worksheets option if the output is one of the Excel Targets
                return _host.IsExcel && (ResultsIcon == OutputTarget.Linked || ResultsIcon == OutputTarget.Static);
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                NotifyOfPropertyChange(() => IsBusy);
            }
        }

        public Task HandleAsync(QueryStartedEvent message, CancellationToken cancellation)
        {
            // clear any tabs (including SHOW trees) from a previous query when a new query starts
            ClearShowTabs();
            // if we are not outputting to the grid it should be cleared
            if (!ShowResultsTable) Clear();
            IsBusy = true;
            return Task.CompletedTask;
        }

        public Task HandleAsync(CancelQueryEvent message, CancellationToken cancellationToken)
        {
            IsBusy = false;
            // clear out any data if the query is cancelled
            ResultsDataSet?.Dispose();
            ResultsDataSet = new DataSet("Empty");
            return Task.CompletedTask;
        }

        public Task HandleAsync(QueryFinishedEvent message, CancellationToken cancellationToken)
        {
            IsBusy = false;
            return Task.CompletedTask;
        }
        private string _selectedWorkbook = "";
        private DataSet _resultsDataSet;

        public string SelectedWorkbook
        {
            get { return _selectedWorkbook; }
            set { _selectedWorkbook = value; NotifyOfPropertyChange(() => SelectedWorkbook); }
        }

        private bool ShouldCopyHeader;
        public void CopyWithHeaders(RoutedEventArgs args)
        {
            ShouldCopyHeader = true;
            CheckSelectionAndCopy(args.Source);
        }

        public void CopyData(RoutedEventArgs args)
        {
            ShouldCopyHeader = false;
            CheckSelectionAndCopy(args.Source);
        }

        private void CheckSelectionAndCopy(object source)
        {
            var selectionSet = false;
            if (source == null) return;
            if (source is MenuItem menu)
            {
                if (menu.Parent is ContextMenu ctxMenu)
                {
                    if (ctxMenu.PlacementTarget is DataGrid grid)
                    {
                        if (grid.SelectedCells.Count == 0)
                        {
                            // if this is a grid and nothing is selected
                            // then select all cells
                            grid.SelectAllCells();
                            grid.Focus();
                            selectionSet = true;
                        }

                        ApplicationCommands.Copy.Execute(null, null);

                        if (selectionSet)
                        {
                            // if we set the selection as part of the copy command 
                            // then we should clear it
                            grid.SelectedCells.Clear();
                        }
                    }
                }
            }


        }

        public void CopyAsTableAssertion(RoutedEventArgs args)
        {
            if (args?.Source is MenuItem menu
                && menu.Parent is ContextMenu ctxMenu
                && ctxMenu.PlacementTarget is DataGrid grid)
            {
                var dt = BuildAssertionDataTable(grid);
                if (dt == null) return;

                var text = DaxStudio.Parsers.CommentScript.TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: true, includeTypeRow: true);

                try
                {
                    Clipboard.SetText(text);
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(QueryResultsPaneViewModel), nameof(CopyAsTableAssertion), "Error setting clipboard text for table assertion");
                }
            }
        }

        private static DataTable BuildAssertionDataTable(DataGrid grid)
        {
            // the grid is bound to a DataView, so the underlying source is its Table
            var sourceTable = (grid.ItemsSource as DataView)?.Table;
            if (sourceTable == null) return null;

            var totalCellCount = sourceTable.Rows.Count * sourceTable.Columns.Count;
            var isProperSubset = grid.SelectedCells.Count > 0 && grid.SelectedCells.Count < totalCellCount;

            if (!isProperSubset)
            {
                // nothing selected, or all cells selected - use the full result table
                return sourceTable;
            }

            // build the list of distinct selected columns in visual column order. The grid column
            // Header is the friendly Caption while SortMemberPath holds the underlying (possibly
            // escaped) ColumnName used to index the source table.
            var selectedColumns = new List<DataColumn>();
            foreach (var col in grid.Columns.OrderBy(c => c.DisplayIndex))
            {
                var columnName = col.SortMemberPath;
                if (string.IsNullOrEmpty(columnName)) continue;
                if (!sourceTable.Columns.Contains(columnName)) continue;
                var sourceColumn = sourceTable.Columns[columnName];
                if (grid.SelectedCells.Any(sc => ReferenceEquals(sc.Column, col)) && !selectedColumns.Contains(sourceColumn))
                {
                    selectedColumns.Add(sourceColumn);
                }
            }
            if (selectedColumns.Count == 0) return sourceTable;

            // build the set of rows that have at least one selected cell, preserving row order
            var selectedRowViews = new List<DataRowView>();
            var seenRows = new HashSet<DataRow>();
            foreach (var cell in grid.SelectedCells)
            {
                if (cell.Item is DataRowView drv && seenRows.Add(drv.Row))
                {
                    selectedRowViews.Add(drv);
                }
            }
            // re-order the selected rows to match the underlying table row order
            selectedRowViews = selectedRowViews
                .OrderBy(drv => sourceTable.Rows.IndexOf(drv.Row))
                .ToList();

            var result = new DataTable();
            foreach (var sourceColumn in selectedColumns)
            {
                // preserve the real CLR type so the formatter emits the correct DAX type, and the
                // Caption so the formatter emits the friendly header (with spaces) rather than the
                // escaped ColumnName.
                var newColumn = result.Columns.Add(sourceColumn.ColumnName, sourceColumn.DataType);
                newColumn.Caption = sourceColumn.Caption;
            }

            foreach (var drv in selectedRowViews)
            {
                var values = new object[selectedColumns.Count];
                for (int i = 0; i < selectedColumns.Count; i++)
                {
                    values[i] = drv.Row[selectedColumns[i].ColumnName];
                }
                result.Rows.Add(values);
            }

            return result;
        }

        public void CopyingRowClipboardContent(object sender, DataGridRowClipboardEventArgs e)
        {

            System.Diagnostics.Debug.WriteLine("Clipboard Copy Content");
            if (e.IsColumnHeadersRow)
            {
                if (ShouldCopyHeader)
                {
                    ShouldCopyHeader = false;
                }
                else
                {
                    e.ClipboardRowContent.Clear();
                }
            }

        }
        public void ResizeGridColumns(DataGrid source, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("DoubleClick fired");
            string dataContext = string.Empty;
            if (e.OriginalSource is TextBlock block) { dataContext = block.DataContext as string; }
            if (e.OriginalSource is Border border) { dataContext = border.DataContext as string; }

            if (!string.IsNullOrEmpty(dataContext))
            {
                for (var i = 0; i < source.Columns.Count; i++)
                {
                    if ((string)source.Columns[i].Header == dataContext)
                    {
                        ToggleSizing(source.Columns[i]);
                        break;
                    }
                }
            }



            if (e.OriginalSource is System.Windows.Shapes.Rectangle)
            {
                if (source.ColumnWidth.UnitType != DataGridLengthUnitType.SizeToCells)
                {
                    source.ColumnWidth = new DataGridLength(1.0, DataGridLengthUnitType.SizeToCells);
                    SetAllColumnWidths(source, DataGridLengthUnitType.SizeToCells, 50.0);
                }
                else
                {
                    source.ColumnWidth = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                    SetAllColumnWidths(source, DataGridLengthUnitType.Auto, 0);
                }
            }
        }

        private void SetAllColumnWidths(DataGrid source, DataGridLengthUnitType lengthType, double minWidth)
        {
            for (int i = 0; i < source.Columns.Count; i++)
            {
                source.Columns[i].Width = new DataGridLength(1.0, lengthType);
                source.Columns[i].MinWidth = minWidth;
            }
        }

        private void ToggleSizing(DataGridColumn dataGridColumn)
        {
            if (dataGridColumn.Width.UnitType != DataGridLengthUnitType.SizeToCells)
            {
                dataGridColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.SizeToCells);
                dataGridColumn.MinWidth = 50.0;
            }
            else
            {
                dataGridColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                dataGridColumn.MinWidth = 0;
            }
        }

        //public System.Windows.Media.Brush TabItemBrush
        //{
        //    get
        //    {
        //        return  (System.Windows.Media.Brush)GetValueFromStyle(typeof(TabItem), Control.BackgroundProperty) ?? System.Windows.Media.Brushes.LightSkyBlue;
        //    }
        //}

        private static object GetValueFromStyle(object styleKey, DependencyProperty property)
        {
            Style style = Application.Current.TryFindResource(styleKey) as Style;
            while (style != null)
            {
                var setter =
                    style.Setters
                        .OfType<Setter>()
                        .FirstOrDefault(s => s.Property == property);

                if (setter != null)
                {
                    return setter.Value;
                }

                style = style.BasedOn;
            }
            return null;
        }

        public UnitViewModel SizeUnits { get; set; }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            UpdateSettings();
            return Task.CompletedTask;
        }

        public Task HandleAsync(SizeUnitsUpdatedEvent message, CancellationToken cancellationToken)
        {
            if (_options.ScaleResultsFontWithEditor)
            {
                this.Scale = message.Units.Value / 100.0;
                //SizeUnits.Value = message.Units.Value;
                //NotifyOfPropertyChange(() => SizeUnits.ScreenPoints);
            }
            return Task.CompletedTask;
        }

        protected override void OnViewLoaded(object view)
        {
            UpdateSettings();
        }

        private void UpdateSettings()
        {

            if (FontSize != _options.ResultFontSizePx)
            {
                FontSize = _options.ResultFontSizePx;
                this.SizeUnits.SetOneHundredPercentFontSize(_options.ResultFontSizePx);
                this.SizeUnits.Value = 100;
                NotifyOfPropertyChange(() => SizeUnits);
            }
            if (FontFamily != _options.ResultFontFamily)
            {
                FontFamily = _options.ResultFontFamily;
            }
        }

        public Task HandleAsync(CopyWithHeadersEvent message, CancellationToken cancellationToken)
        {
            if (GridHasFocus)
            {
                ShouldCopyHeader = true;
                ApplicationCommands.Copy.Execute(null, null);
            }
            return Task.CompletedTask;
        }

        private bool _gridHasFocus;
        public bool GridHasFocus
        {
            get => _gridHasFocus;
            set
            {
                _gridHasFocus = value;
                NotifyOfPropertyChange();
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                if (!string.IsNullOrWhiteSpace(_errorMessage)) Clear();
                ErrorLocation = RegexHelper.GetQueryErrorLocation(_errorMessage);
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(() => ShowErrorMessage);
                NotifyOfPropertyChange(() => ShowResultsMessage);

            }
        }
        private (int Line, int Column) _errorLocation = (0, 0);
        public (int Line, int Column) ErrorLocation
        {
            get => _errorLocation;
            set
            {
                _errorLocation = value;
                NotifyOfPropertyChange(nameof(ShowGotoError));
            }
        }
        public bool ShowGotoError { get => ErrorLocation.Line > 0 || ErrorLocation.Column > 0; }

        public void GridGotFocus() { GridHasFocus = true; }
        public void GridLostFocus() { GridHasFocus = false; }

        public void Clear()
        {
            Execute.OnUIThread(() =>
            {
                ResultsDataSet?.Tables?.Clear();
                // Remove every tab (query-result data grids AND SHOW trees) so an error / cleared state
                // hides the TabControl entirely rather than leaving stale grids visible behind the error
                // overlay. ShowResultsTable is bound to the grid's visibility.
                _resultTabs.Clear();
                ShowResultsTable = false;
                ResultsMessage = "Results Cleared";
                NotifyOfPropertyChange(() => Tables);
                NotifyOfPropertyChange(() => ShowResultsMessage);
            });
        }

        #region SHOW command tabs

        /// <summary>
        /// Appends a Comment Script <c>--&gt; SHOW</c> tree-grid as a new tab at the end of the results.
        /// SHOW output is now a first-class tab interspersed with the query-result grids rather than a
        /// full-pane overlay.
        /// </summary>
        public void AddShowTreeTab(IList<ShowTreeNode> roots, DaxStudio.Parsers.CommentScript.ShowType showType)
        {
            Execute.OnUIThread(() =>
            {
                _resultTabs.Add(new ShowTreeResultTab(roots, showType));
                ShowResultsTable = _resultTabs.Count > 0;
                NotifyOfPropertyChange(() => ShowResultsMessage);
            });
        }

        /// <summary>Removes any SHOW tree tabs, leaving the query-result data tabs untouched.</summary>
        private void ClearShowTabs()
        {
            Execute.OnUIThread(() =>
            {
                var showTabs = _resultTabs.OfType<ShowTreeResultTab>().ToList();
                foreach (var tab in showTabs) _resultTabs.Remove(tab);
                ShowResultsTable = _resultTabs.Count > 0;
                NotifyOfPropertyChange(() => ShowResultsMessage);
            });
        }

        #endregion

        #region ISaveState - persist only the SHOW tree tabs into the .daxx package (query-result grids are never saved)

        /// <summary>Serializable snapshot of a single SHOW tree tab written to the .daxx package. The
        /// <see cref="TabIndex"/> records the tab's position within the interspersed results collection
        /// so the relative order of multiple SHOW tabs is preserved on reload.</summary>
        private class ShowTreeTabState
        {
            public int TabIndex { get; set; }
            public DaxStudio.Parsers.CommentScript.ShowType ShowType { get; set; }
            public List<ShowTreeNode> Roots { get; set; } = new List<ShowTreeNode>();
        }

        /// <summary>The original (pre-array) schema: a single SHOW tree object. Retained so existing
        /// .daxx files written before SHOW became a tab can still be read.</summary>
        private class ShowTreeState
        {
            public List<ShowTreeNode> Roots { get; set; } = new List<ShowTreeNode>();
            public DaxStudio.Parsers.CommentScript.ShowType ShowType { get; set; }
        }

        // Satellite (.dax) files do not persist SHOW output - persistence is only via the .daxx package.
        public void Save(string filename) { }
        public void Load(string filename) { }

        public string GetJson()
        {
            var states = new List<ShowTreeTabState>();
            for (int i = 0; i < _resultTabs.Count; i++)
            {
                if (_resultTabs[i] is ShowTreeResultTab showTab)
                {
                    states.Add(new ShowTreeTabState
                    {
                        TabIndex = i,
                        ShowType = showTab.ShowType,
                        Roots = showTab.ShowTreeRoots.ToList()
                    });
                }
            }
            return JsonConvert.SerializeObject(states, Formatting.Indented);
        }

        public void LoadJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            List<ShowTreeTabState> states;
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith("["))
            {
                states = JsonConvert.DeserializeObject<List<ShowTreeTabState>>(json) ?? new List<ShowTreeTabState>();
            }
            else
            {
                // backward-compat: the original schema persisted a single SHOW tree object
                var legacy = JsonConvert.DeserializeObject<ShowTreeState>(json);
                states = new List<ShowTreeTabState>();
                if (legacy?.Roots != null && legacy.Roots.Count > 0)
                {
                    states.Add(new ShowTreeTabState { TabIndex = 0, ShowType = legacy.ShowType, Roots = legacy.Roots });
                }
            }

            if (states.Count == 0) return;

            Execute.OnUIThread(() =>
            {
                // Only SHOW tabs are persisted, so on reload we recreate them at their saved positions.
                // The index is clamped because the query-result data tabs they were interspersed with
                // are never saved (so the collection is shorter than when they were written).
                foreach (var state in states.OrderBy(s => s.TabIndex))
                {
                    var index = state.TabIndex;
                    if (index < 0 || index > _resultTabs.Count) index = _resultTabs.Count;
                    _resultTabs.Insert(index, new ShowTreeResultTab(state.Roots, state.ShowType));
                }
                ShowResultsTable = _resultTabs.Count > 0;
                NotifyOfPropertyChange(() => Tables);
                NotifyOfPropertyChange(() => ShowResultsMessage);
                SelectedTableIndex = 0;
                NotifyOfPropertyChange(() => SelectedTableIndex);
            });
        }

        public void SavePackage(Package package)
        {
            // Only the SHOW tree tabs are persisted. The query-result data grids are intentionally
            // never saved into the .daxx file.
            if (!_resultTabs.OfType<ShowTreeResultTab>().Any()) return;
            try
            {
                var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ShowResults, UriKind.Relative));
                if (package.PartExists(uri)) package.DeletePart(uri);
                using (var strm = package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream())
                using (var writer = new StreamWriter(strm, new UTF8Encoding(false)))
                {
                    writer.Write(GetJson());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(QueryResultsPaneViewModel), nameof(SavePackage), "Error saving SHOW results to daxx file");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error saving SHOW results to daxx file\n{ex.Message}"));
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.ShowResults, UriKind.Relative));
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
                Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(QueryResultsPaneViewModel), nameof(LoadPackage), "Error loading SHOW results from daxx file");
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error loading SHOW results from daxx file\n{ex.Message}"));
            }
        }

        #endregion


        public void CopyError()
        {
            // exit early if the ErrorMessage is emtpty
            if (string.IsNullOrWhiteSpace(ErrorMessage)) return;

            try
            {
                Clipboard.SetText(ErrorMessage);
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Error message copied to clipboard"));
            }
            catch (Exception ex)
            {
                var msg = $"Unable to copy error message to clipboard: {ex.Message}";
                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, msg));
                Log.Error(Constants.LogMessageTemplate, nameof(QueryResultsPaneViewModel), nameof(CopyError), msg);
            }
            
        }

        public bool ShowErrorMessage { get => !string.IsNullOrEmpty(ErrorMessage); }
        public TextLocation SelectionLocation { get; internal set; }
        private string _resultsFullFileName;
        public string ResultsFullFileName { get => _resultsFullFileName; 
            internal set {
                _resultsFullFileName = value;
                var fileInfo = new System.IO.FileInfo(_resultsFullFileName);
                ResultsFilePath = fileInfo.DirectoryName;
                NotifyOfPropertyChange();
            } 
        }

        private string _resultsFileName;
        public string ResultsFileName
        {
            get => _resultsFileName;
            internal set
            {
                _resultsFileName = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(() => ShowOpenFileLocation);
            }
        }

        private string _resultsFilePath;
        public string ResultsFilePath { get => _resultsFilePath; 
            internal set { 
                _resultsFilePath = value;
                if (!_resultsFilePath.EndsWith("\\")) _resultsFilePath += "\\";
                NotifyOfPropertyChange(); 
                NotifyOfPropertyChange(() => ShowOpenFileLocation);
            } 
        }

        public bool ShowOpenFileLocation { get => !string.IsNullOrEmpty(ResultsFilePath) && System.IO.Directory.Exists(ResultsFilePath); }

        public void GotoError()
        {
            if (ErrorLocation.Line >= 0 && ErrorLocation.Column >= 0)
            {
                var lineOffset = 0;
                var columnOffset = 0;
                if(SelectionLocation.Line > 0 
                   && SelectionLocation.Column > 0)
                {
                    // need to -1 to make the offset 0 based
                    lineOffset = SelectionLocation.Line-1;
                    // only offset the column if the error is on line 1
                    columnOffset = SelectionLocation.Line == 1 ? SelectionLocation.Column -1: 0;
                }
                _eventAggregator.PublishAsync(
                    new NavigateToLocationEvent(ErrorLocation.Line + lineOffset
                                               , ErrorLocation.Column + columnOffset));
            }
        }

        public void OpenResultsFileLocation()
        {
            if (!string.IsNullOrEmpty(ResultsFilePath) && System.IO.Directory.Exists(ResultsFilePath))
            {
                System.Diagnostics.Process.Start("explorer.exe", ResultsFilePath);
            }
        }
    }
}
