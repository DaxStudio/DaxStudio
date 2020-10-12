using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Windows.Data;
using System;
using System.ComponentModel;
using Serilog;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.ViewModels
{

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class QueryHistoryPaneViewModel : ToolWindowBase
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<DatabaseChangedEvent>
        , IHandle<UpdateGlobalOptions>
    {
        private bool _isFilteredByServer = true;
        private bool _isFilteredByDatabase = true;
        private readonly GlobalQueryHistory _globalHistory;
        private readonly ListCollectionView _queryHistory;
        private readonly IEventAggregator _eventAggregator;
        private readonly IConnection _currentConnection;
        private readonly IGlobalOptions _globalOptions;

        [ImportingConstructor]
        public QueryHistoryPaneViewModel(GlobalQueryHistory globalHistory, IEventAggregator eventAggregator, DocumentViewModel currentDocument, IGlobalOptions options)
        {
            Log.Debug("{class} {method} {message}","QueryHistoryPaneViewModel","ctor","start");
            _globalHistory = globalHistory;
            _globalOptions = options;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);            
            _queryHistory = new ListCollectionView(globalHistory.QueryHistory);
            //_queryHistory.PageSize = 50;
            _currentConnection = currentDocument.Connection;
            _queryHistory.Filter = HistoryFilter;
            // sort by StartTime Desc by default
            _queryHistory.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Descending));
            Log.Debug("{class} {method} {message}", "QueryHistoryPaneViewModel", "ctor", "end");
        }

        public BindableCollection<QueryHistoryEvent> QueryHistoryList => _globalHistory.QueryHistory;

        public override string Title => "Query History";

        public string CurrentServer => _currentConnection.ServerName;
        public string CurrentDatabase => _currentConnection.SelectedDatabaseName;

        public bool IsFilteredByServer
        {
            get => _isFilteredByServer;
            set
            {
                _isFilteredByServer = value;
                QueryHistory.Filter = HistoryFilter;
                QueryHistory.Refresh();
                NotifyOfPropertyChange(() => IsFilteredByServer);
                NotifyOfPropertyChange(() => QueryHistory);
            }
        }
        public bool IsFilteredByDatabase
        {
            get { return _isFilteredByDatabase; }
            set
            {
                _isFilteredByDatabase = value;
                QueryHistory.Refresh();
                NotifyOfPropertyChange(() => IsFilteredByDatabase);
            }
        }

        private bool HistoryFilter(object queryHistoryEvent)
        {
            var qhe = queryHistoryEvent as QueryHistoryEvent;
            return qhe != null 
                && (String.Compare( qhe.ServerName, _currentConnection?.ServerName??string.Empty, StringComparison.OrdinalIgnoreCase)==0 || !IsFilteredByServer)
                && (String.Compare(qhe.DatabaseName,  _currentConnection?.SelectedDatabaseName??string.Empty, StringComparison.OrdinalIgnoreCase) == 0 || !IsFilteredByDatabase);
        }

        public ICollectionView QueryHistory => _queryHistory;

        public QueryHistoryEvent SelectedHistoryItem { get; set; }

        public void QueryHistoryDoubleClick()
        {
            QueryHistoryDoubleClick(SelectedHistoryItem);
        }

        public void QueryHistoryDoubleClick(QueryHistoryEvent queryHistoryEvent)
        {
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(queryHistoryEvent.QueryText));
            //_eventAggregator.PublishOnUIThread(new SendTextToEditor(queryText));
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            UpdateHistoryFilters();
        }

        private void UpdateHistoryFilters()
        {
            QueryHistory.Filter = HistoryFilter;
            QueryHistory.Refresh();
            NotifyOfPropertyChange(() => CurrentServer);
            NotifyOfPropertyChange(() => CurrentDatabase);
        }

        public void MouseDoubleClick(object sender)//, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("clicked!");
        }

        public bool ShowTraceColumns { get { return _globalOptions.QueryHistoryShowTraceColumns; } }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(() => ShowTraceColumns);
        }

        public void Handle(DatabaseChangedEvent message)
        {
            UpdateHistoryFilters();
        }
        
    }
}
