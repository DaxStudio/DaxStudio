using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
//using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Models;

namespace DaxStudio.UI.ViewModels
{

    class AllServerQueriesViewModel
        : TraceWatcherBaseViewModel, ISaveState
        
    {
        [ImportingConstructor]
        public AllServerQueriesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base(eventAggregator, globalOptions)
        {
            _queryEvents = new BindableCollection<QueryEvent>();
        }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QueryEnd};
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults() {

            if (Events != null) {
                foreach (var traceEvent in Events) {
                    QueryEvents.Add(new QueryEvent() {
                        StartTime = traceEvent.StartTime,
                        //EndTime = traceEvent.EndTime,
                        Username = traceEvent.NTUserName,
                        Query = traceEvent.TextData,
                        Duration = traceEvent.Duration });
                }
                
                Events.Clear();

                NotifyOfPropertyChange(() => QueryEvents);
            }
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;

        public IObservableCollection<QueryEvent> QueryEvents 
        {
            get {
                return _queryEvents;
            }
        }

        

        // IToolWindow interface
        public override string Title
        {
            get { return "All Server Queries"; }
            set { }
        }

        public override string ToolTipText
        {
            get
            {
                return "Runs a server trace to record all queries from all users";
            }
            set { }
        }

        public override bool FilterForCurrentSession
        {
            get
            {
                return false;
            }
        }

        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        public QueryEvent SelectedQuery { get; set; }

        public void QueryDoubleClick(QueryEvent query)
        {
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(query.Query + "\n"));
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            
        }

        void ISaveState.Load(string filename)
        {
            
        }

        #endregion

    }
}
