using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Windows.Data;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Serilog;
using DaxStudio.Interfaces;
using System.Threading;
using System.Threading.Tasks;

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
        //private readonly IConnection _currentConnection;
        private readonly IGlobalOptions _globalOptions;
        private readonly DocumentViewModel CurrentDocument;

        [ImportingConstructor]
        public QueryHistoryPaneViewModel(GlobalQueryHistory globalHistory, IEventAggregator eventAggregator, DocumentViewModel currentDocument, IGlobalOptions options)
        {
            Log.Debug("{class} {method} {message}","QueryHistoryPaneViewModel","ctor","start");
            _globalHistory = globalHistory;
            _globalOptions = options;
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnUIThread(this);            
            _queryHistory = new ListCollectionView(globalHistory.QueryHistory);
            //_queryHistory.PageSize = 50;
            CurrentDocument = currentDocument;
            _queryHistory.Filter = HistoryFilter;
            // sort by StartTime Desc by default
            _queryHistory.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Descending));
            Log.Debug("{class} {method} {message}", "QueryHistoryPaneViewModel", "ctor", "end");
        }

        public BindableCollection<QueryHistoryEvent> QueryHistoryList => _globalHistory.QueryHistory;

        public override string Title => "History";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "query-history";

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
            if (qhe == null) return false;
            try
            {
                var conn = CurrentDocument?.Connection;
                var server = conn?.ServerNameForHistory ?? string.Empty;
                var database = conn?.Database?.Caption ?? string.Empty;
                return (string.Equals(qhe.ServerName, server, StringComparison.OrdinalIgnoreCase) || !IsFilteredByServer)
                    && (string.Equals(qhe.DatabaseName, database, StringComparison.OrdinalIgnoreCase) || !IsFilteredByDatabase);
            }
            catch (Exception ex)
            {
                // The connection can be in a transient state during a reconnect or
                // database change (e.g. _dmvConnection has just been reassigned but
                // not yet re-opened). A throw from this filter would abort the
                // entire CollectionView.Refresh() pass; instead show the row and
                // let the next DatabaseChangedEvent re-evaluate the filter once
                // the connection has stabilised.
                Log.Warning(ex, Common.Constants.LogMessageTemplate,
                    nameof(QueryHistoryPaneViewModel), nameof(HistoryFilter),
                    "Connection in transient state, deferring filter");
                return true;
            }
        }

        public ICollectionView QueryHistory => _queryHistory;

        public QueryHistoryEvent SelectedHistoryItem { get; set; }

        public void QueryHistoryDoubleClick()
        {
            QueryHistoryDoubleClick(SelectedHistoryItem);
        }

        public void QueryHistoryDoubleClick(QueryHistoryEvent queryHistoryEvent)
        {
            if (queryHistoryEvent == null) return;  // exit here silently if no history event is selected
            if (!string.IsNullOrEmpty(queryHistoryEvent.QueryBuilderJson))
                _eventAggregator.PublishOnUIThreadAsync(new LoadQueryBuilderEvent(queryHistoryEvent.QueryBuilderJson));
            else
            {
                var text = queryHistoryEvent.QueryText;
                if (!string.IsNullOrWhiteSpace(queryHistoryEvent.Parameters)) text += $"\n{queryHistoryEvent.Parameters}";
                _eventAggregator.PublishOnUIThreadAsync(new SendTextToEditor(text));
            }
        }

        public Task HandleAsync(DocumentConnectionUpdateEvent message, CancellationToken cancellationToken)
        {
            UpdateHistoryFilters();
            return Task.CompletedTask;
        }

        private void UpdateHistoryFilters()
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() => { 
                    QueryHistory.Filter = HistoryFilter;
                    QueryHistory.Refresh();
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(QueryHistoryPaneViewModel), nameof(UpdateHistoryFilters), ex.Message);
                CurrentDocument.OutputWarning("An error occurred while trying to update the filters on the Query History pane due to a connection change");
            }
        }

        public void MouseDoubleClick(object sender)//, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("clicked!");
        }

        public bool ShowTraceColumns { get { return _globalOptions.QueryHistoryShowTraceColumns; } }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(() => ShowTraceColumns);
            return Task.CompletedTask;
        }

        public Task HandleAsync(DatabaseChangedEvent message, CancellationToken cancellationToken)
        {
            UpdateHistoryFilters();
            return Task.CompletedTask;
        }
        
    }
}
