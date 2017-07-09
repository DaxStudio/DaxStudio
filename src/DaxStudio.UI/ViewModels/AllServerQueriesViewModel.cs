using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.Interfaces;
using DaxStudio.UI.Models;
using System.IO;
using Newtonsoft.Json;
using System.Text;
using DaxStudio.Controls.DataGridFilter;
using System.Linq;

namespace DaxStudio.UI.ViewModels
{

    class AllServerQueriesViewModel
        : TraceWatcherBaseViewModel, ISaveState //, IViewAware
        
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

            if (IsPaused) return; // exit here if we are paused

            if (Events != null) {
                foreach (var traceEvent in Events) {
                    QueryEvents.Insert(0,new QueryEvent() {
                        QueryType = traceEvent.EventSubclassName.Substring(0,3),
                        StartTime = traceEvent.StartTime,
                        //EndTime = traceEvent.EndTime,
                        Username = traceEvent.NTUserName,
                        Query = traceEvent.TextData,
                        Duration = traceEvent.Duration,
                        DatabaseName = traceEvent.DatabaseName
                        
                    });
                }
                
                Events.Clear();

                NotifyOfPropertyChange(() => QueryEvents);
                NotifyOfPropertyChange(() => CanClearAllEvents);
                NotifyOfPropertyChange(() => CanSendAllQueriesToEditor);
            }
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;
        private bool _isPaused;
        public new bool CanClose { get { return true; } }
        //public event EventHandler<ViewAttachedEventArgs> ViewAttached;

        public IObservableCollection<QueryEvent> QueryEvents 
        {
            get {
                return _queryEvents;
            }
        }

        public string DefaultQueryFilter { get { return "cat"; } }

        // IToolWindow interface
        public override string Title
        {
            get { return "All Queries"; }
            set { }
        }

        public override string ToolTipText
        {
            get
            {
                return "Runs a server trace to record all queries from all users for the current connection";
            }
            set { }
        }

        public override bool FilterForCurrentSession { get { return false; } }

        public void ClearAllEvents()
        {
            QueryEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAllEvents);
            NotifyOfPropertyChange(() => CanSendAllQueriesToEditor);
        }

        public void Pause()
        {
            IsPaused = true;
        }

        public void Start()
        {
            IsPaused = false;
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                _isPaused = value;
                NotifyOfPropertyChange(() => CanPause);
                NotifyOfPropertyChange(() => CanStart);
            }
        }
        public bool CanPause { get { return !IsPaused; } }
        public bool CanStart { get { return IsPaused; } }
        public bool CanClearAllEvents { get { return QueryEvents.Count > 0; } }
        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        public QueryEvent SelectedQuery { get; set; }

        public bool CanSendAllQueriesToEditor { get { return QueryEvents.Count > 0; } }

        public void SendAllQueriesToEditor()
        {
            var sb = new StringBuilder();
            foreach (var q in QueryEvents)
            {
                sb.AppendLine();
                sb.AppendLine($"// {q.QueryType} query against Database: {q.DatabaseName} ");
                sb.AppendLine($"{q.Query}");

            }
            sb.AppendLine();
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(sb.ToString()));
        }

        public void QueryDoubleClick(QueryEvent query)
        {
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(query.Query + "\n", query.DatabaseName));
        }

        #region ISaveState methods
        void ISaveState.Save(string filename)
        {
            var json = JsonConvert.SerializeObject(QueryEvents, Formatting.Indented);
            File.WriteAllText(filename + ".allQueries", json);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".allQueries";
            if (!File.Exists(filename)) return;

            IsChecked = true;
            string data = File.ReadAllText(filename);
            List<QueryEvent> qe = JsonConvert.DeserializeObject<List<QueryEvent>>(data);
            
            _queryEvents.Clear();
            _queryEvents.AddRange(qe);
            NotifyOfPropertyChange(() => QueryEvents);
        }

        private Views.AllServerQueriesView _view;
        public void AttachView(object view, object context = null)
        {
            _view = view as Views.AllServerQueriesView;
        }

        public object GetView(object context = null)
        {
            return _view;
        }

        public void SetDefaultFilter(string column, string value)
        {
            var controller = DataGridExtensions.GetDataGridFilterQueryController(_view.QueryEvents);
            var filters = controller.GetFiltersForColumns();

            var columnFilter = filters.FirstOrDefault(w => w.Key == column);
            if (columnFilter.Key != null)
            {
                columnFilter.Value.QueryString = value;

                controller.SetFiltersForColumns(filters);
            }
        }

        #endregion

    }
}
