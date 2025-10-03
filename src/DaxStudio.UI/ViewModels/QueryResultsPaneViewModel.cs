using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using ICSharpCode.AvalonEdit.Document;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
//using System.Windows.Forms;
using System.Windows.Input;
using UnitComboLib.Unit.Screen;
using UnitComboLib.ViewModel;

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
                ShowResultsTable = true;
                NotifyOfPropertyChange(() => Tables);
                ShowResultsTable = _resultsDataSet.Tables.Count > 0;
                SelectedTableIndex = 0;
                NotifyOfPropertyChange(() => SelectedTableIndex);
            }
        }
        private int _selectedTabIndex = -1;
        public int SelectedTableIndex
        {
            get { return _selectedTabIndex; }
            set
            {
                _selectedTabIndex = value;
                if (_document != null && value >= 0 && ResultsDataSet != null && ResultsDataSet.Tables.Count > 0) _document.RowCount = ResultsDataSet.Tables[value].Rows.Count;
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
            get { return !ShowResultsTable && !ShowErrorMessage; }
            //private set
            //{
            //    _showResultsMessage = value;
            //    NotifyOfPropertyChange(() => ShowResultsMessage);
            //    NotifyOfPropertyChange(() => ShowResultsTable);
            //}
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
                _eventAggregator.PublishOnBackgroundThreadAsync(new SetSelectedWorksheetEvent(_selectedWorksheet));
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
            await _eventAggregator.PublishOnUIThreadAsync(new QueryResultsPaneMessageEvent(message.Target));
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
            ResultsDataSet?.Tables?.Clear();
            ShowResultsTable = false;
            ResultsMessage = "Results Cleared";
        }

        public void CopyError()
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                Clipboard.SetText(ErrorMessage);
                _eventAggregator.PublishOnCurrentThreadAsync(new OutputMessage(MessageType.Information, "Error message copied to clipboard"));
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
                _eventAggregator.PublishOnUIThreadAsync(
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
