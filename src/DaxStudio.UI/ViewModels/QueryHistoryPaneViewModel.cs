using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using System.Windows.Data;
using System;
using System.ComponentModel;

namespace DaxStudio.UI.ViewModels
{

    public class QueryHistoryPaneViewModelFactory
    {
        [Export(typeof(Func<GlobalQueryHistory,IEventAggregator,DocumentViewModel,QueryHistoryPaneViewModel>))]
        public QueryHistoryPaneViewModel Create(GlobalQueryHistory globalHistory, IEventAggregator eventAggregator, DocumentViewModel document)
        {
            return new QueryHistoryPaneViewModel(globalHistory, eventAggregator,document);
        }
    }

    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class QueryHistoryPaneViewModel : ToolWindowBase
        , IHandle<DocumentConnectionUpdateEvent>
    {
        private bool _isFilteredByServer = true;
        private bool _isFilteredByDatabase = true;
        private readonly GlobalQueryHistory _globalHistory;
        private readonly ListCollectionView _queryHistory;
        private readonly IEventAggregator _eventAggregator;
        private readonly DocumentViewModel _currentDocument;

        [ImportingConstructor]
        public QueryHistoryPaneViewModel(GlobalQueryHistory globalHistory, IEventAggregator eventAggregator, DocumentViewModel currentDocument)
        {
            _globalHistory = globalHistory;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            //_queryHistory = new ListCollectionView(globalHistory.QueryHistory);
            _queryHistory = new ListCollectionView(globalHistory.QueryHistory);
            //_queryHistory.PageSize = 50;
            _currentDocument = currentDocument;
            _queryHistory.Filter = HistoryFilter;
            // sort by StartTime Desc by default
            _queryHistory.SortDescriptions.Add(new SortDescription("StartTime", ListSortDirection.Descending));
            
        }

        public override string Title
        {
            get { return "Query History"; }
        }

        public bool IsFilteredByServer
        {
            get { return _isFilteredByServer; }
            set
            {
                _isFilteredByServer = value;
                QueryHistory.Filter = HistoryFilter;
                //QueryHistory.Refresh();
                NotifyOfPropertyChange(() => IsFilteredByServer);
                NotifyOfPropertyChange(() => QueryHistory);
            }
        }
        public bool IsFilteredByDatabase
        { 
            get { return _isFilteredByDatabase; }
            set { 
                _isFilteredByDatabase = value;
                QueryHistory.Refresh();
                NotifyOfPropertyChange(() => IsFilteredByDatabase);
            }
        }

        private bool HistoryFilter(object queryHistoryEvent)
        {
            var qhe = queryHistoryEvent as QueryHistoryEvent;
            return (qhe.ServerName == _currentDocument.ServerName || !IsFilteredByServer)
                && (qhe.DatabaseName == _currentDocument.SelectedDatabase || !IsFilteredByDatabase);
        }

        public ICollectionView QueryHistory
        {
            get { return _queryHistory; }
        }

        public void QueryHistoryDoubleClick(QueryHistoryEvent queryHistoryEvent)
        {
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(queryHistoryEvent.QueryText));
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            //QueryHistory.Filter = HistoryFilter;
            QueryHistory.Refresh();
        }
    }
}
