using System.ComponentModel.Composition;
using System.Data;
using DaxStudio.UI.Model;
using DaxStudio.UI.Extensions;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using System.Collections.Generic;
using DaxStudio.UI.Interfaces;
using System.Drawing;
using System.Linq;
using System;
using DaxStudio.UI.Views;
using UnitComboLib.ViewModel;
using System.Collections.ObjectModel;
using System.Windows.Media;
using UnitComboLib.Unit.Screen;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    [Export(typeof(IToolWindow))]
    public class QueryResultsPaneViewModel: ToolWindowBase
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<QueryStartedEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<UpdateGlobalOptions>
        , IHandle<SizeUnitsUpdatedEvent>
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
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/icon-table.png") as ImageSource;

            }
        }

        public DataTable ResultsDataTable
        {
            get => _resultsTable;
            set { _resultsTable = value;
            ShowResultsTable = true;
            NotifyOfPropertyChange(()=> ResultsDataView);}
        }

        public DataSet ResultsDataSet
        {
            get { return _resultsDataSet; }
            set {
                _resultsDataSet?.Dispose();
                _resultsDataSet = value;
                ShowResultsTable = true;
                NotifyOfPropertyChange(() => Tables);
                SelectedTableIndex = 0;
                NotifyOfPropertyChange(() => SelectedTableIndex);
            }
        }
        private int _selectedTabIndex = -1;
        public int SelectedTableIndex { get { return _selectedTabIndex; }
            set { _selectedTabIndex = value;
                if (_document != null && value >= 0 ) _document.RowCount = ResultsDataSet.Tables[value].Rows.Count;
                NotifyOfPropertyChange(() => SelectedTableIndex);
            }
        }
        public DataTableCollection Tables
        {
            get {
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
        { get { return _resultsTable==null?new DataTable("blank").AsDataView():  _resultsTable.AsDataView(); } }

        public void OnListViewItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("in OnListViewItemPreviewMouseRightButtonDown");
        }

        private void ResultsAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            
            if ((e.PropertyName.Contains(".")
                || e.PropertyName.Contains("/")
                || e.PropertyName.Contains("(")
                || e.PropertyName.Contains(")")
                || e.PropertyName.Contains("[")
                || e.PropertyName.Contains("]")
                ) && e.Column is DataGridBoundColumn)
            {
                DataGridBoundColumn dataGridBoundColumn = e.Column as DataGridBoundColumn;
                dataGridBoundColumn.Binding = new Binding("[" + e.PropertyName + "]");
            }
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
            get { return !ShowResultsTable; }
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
        public double FontSize {
            get { return _fontSize; }
            set {
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

        public void Handle(QueryResultsPaneMessageEvent message)
        {
            ResultsIcon = message.Target.Icon;
            ResultsMessage = message.Target.Message;
        }

        public IEnumerable<string> Worksheets
        {
            get { return _host.Proxy.Worksheets; }
        }

        public string SelectedWorksheet
        {
            get { return _selectedWorksheet; }
            set { _selectedWorksheet = value;
            _eventAggregator.PublishOnBackgroundThread(new SetSelectedWorksheetEvent(_selectedWorksheet));
            }
        }
        private DocumentViewModel _document;
        public void Handle(ActivateDocumentEvent message)
        {
            _document = message.Document;
            if (_host.IsExcel)
            {
                // refresh workbooks and worksheet properties if the host is excel
                SelectedWorkbook = _host.Proxy.WorkbookName;
                SelectedWorksheet = message.Document.SelectedWorksheet;
                NotifyOfPropertyChange(() => Worksheets);
            }
        }

        public void Handle(NewDocumentEvent message)
        {
            _eventAggregator.PublishOnUIThread(new QueryResultsPaneMessageEvent(message.Target));
            if (message.Target is IActivateResults) { this.Activate(); }
            //ResultsIcon = message.Target.Icon;
            //ResultsMessage = message.Target.Message;
        }

        public bool ShowWorksheets
        {
            get
            {
                // Only show the worksheets option if the output is one of the Excel Targets
                return  _host.IsExcel && (ResultsIcon == OutputTarget.Linked || ResultsIcon == OutputTarget.Static);
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value;
            NotifyOfPropertyChange(() => IsBusy);
            }
        }

        public void Handle(QueryStartedEvent message)
        {
            IsBusy = true;
        }

        public void Handle(CancelQueryEvent message)
        {
            IsBusy = false;
            // clear out any data if the query is cancelled
            ResultsDataTable = new DataTable("Empty");
        }

        public void Handle(QueryFinishedEvent message)
        {
            IsBusy = false;
        }
        private string _selectedWorkbook = "";
        private DataSet _resultsDataSet;

        public string SelectedWorkbook { 
            get { return _selectedWorkbook; } 
            set { _selectedWorkbook = value; NotifyOfPropertyChange(() => SelectedWorkbook); } 
        }

        public void CopyingRowClipboardContent(DataGrid source, DataGridRowClipboardEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.ClipboardRowContent[0]);
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
                    SetAllColumnWidths(source, DataGridLengthUnitType.SizeToCells,50.0);
                }
                else
                {
                    source.ColumnWidth = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                    SetAllColumnWidths(source, DataGridLengthUnitType.Auto,0);
                }
            }
        }

        private void SetAllColumnWidths(DataGrid source, DataGridLengthUnitType lengthType, double minWidth)
        {
            for (int i=0;i < source.Columns.Count;i++)
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
            } else
            {
                dataGridColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.Auto);
                dataGridColumn.MinWidth = 0;
            }
        }

        public System.Windows.Media.Brush TabItemBrush
        {
            get
            {
                return  (System.Windows.Media.Brush)GetValueFromStyle(typeof(TabItem), Control.BackgroundProperty) ?? System.Windows.Media.Brushes.LightSkyBlue;
            }
        }

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

        public void Handle(UpdateGlobalOptions message)
        {
            UpdateSettings();
        }
        
        public void Handle(SizeUnitsUpdatedEvent message)
        {
            if (_options.ScaleResultsFontWithEditor)
            {
                this.Scale = message.Units.Value / 100.0;
                //SizeUnits.Value = message.Units.Value;
                //NotifyOfPropertyChange(() => SizeUnits.ScreenPoints);
            }
        }

        public DataGridClipboardCopyMode ClipboardCopyMode
        {
            get
            {
                if (_options.ExcludeHeadersWhenCopyingResults) return DataGridClipboardCopyMode.ExcludeHeader;
                return DataGridClipboardCopyMode.IncludeHeader;
            }
        }

        protected override void OnViewLoaded(object view)
        {
            UpdateSettings();
        }

        private void UpdateSettings()
        {
            NotifyOfPropertyChange(() => ClipboardCopyMode);

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

    }
}
