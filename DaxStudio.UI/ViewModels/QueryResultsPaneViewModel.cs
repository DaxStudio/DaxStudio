using System.ComponentModel.Composition;
using System.Data;
using DaxStudio.UI.Model;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Windows.Input;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using System.Collections.Generic;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IToolWindow))]
    public class QueryResultsPaneViewModel: ToolWindowBase
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<RunQueryEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<QueryFinishedEvent>
    {
        private DataTable _resultsTable;
        private string _selectedWorksheet;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;

        [ImportingConstructor]
        public QueryResultsPaneViewModel(IEventAggregator eventAggregator, IDaxStudioHost host) : this(new DataTable("Empty"))
        {
            _eventAggregator = eventAggregator;
            //_eventAggregator.Subscribe(this);
            _host = host;
        }

        public QueryResultsPaneViewModel(DataTable resultsTable)
        {
            _resultsTable = resultsTable;
            
        }

        public override string Title
        {
            get { return "Results"; }
        }

        public DataTable ResultsDataTable
        {
            get { return _resultsTable; }
            set { _resultsTable = value;
            ShowResultsTable = true;
            NotifyOfPropertyChange(()=> ResultsDataView);}
        }

        public void CopyAllResultsToClipboard(object obj)
        {
            System.Diagnostics.Debug.WriteLine(obj);
            Clipboard.SetData("CommaSeparatedValue", ResultsDataTable.ToCsv());
        }

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
                _showResultsMessage = !value;
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
                ShowResultsTable = string.IsNullOrEmpty(_resultsMessage);
                NotifyOfPropertyChange(() => ResultsMessage);
            }
        }

        private bool _showResultsMessage;
        public bool ShowResultsMessage
        {
            get { return _showResultsMessage; }
            private set
            {
                _showResultsMessage = value;
                NotifyOfPropertyChange(() => ShowResultsMessage);
            }
        }
        private OutputTargets _icon;
        public OutputTargets ResultsIcon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                NotifyOfPropertyChange(() => ResultsIcon);
                NotifyOfPropertyChange(() => ShowWorksheets);
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

        public void Handle(ActivateDocumentEvent message)
        {
            if (_host.IsExcel)
            {
            
                SelectedWorksheet = message.Document.SelectedWorksheet;
                //TODO - refresh workbooks and powerpivot conn if the host is excel
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
                return ResultsIcon == OutputTargets.Linked || ResultsIcon == OutputTargets.Static;
            }
        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value;
            NotifyOfPropertyChange(() => IsBusy);
            }
        }

        public void Handle(RunQueryEvent message)
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
    }
}
