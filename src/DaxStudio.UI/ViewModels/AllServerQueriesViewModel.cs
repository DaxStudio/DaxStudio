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
using System;
using System.Windows;
using DaxStudio.UI.Extensions;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.ObjectModel;

namespace DaxStudio.UI.ViewModels
{

    class AllServerQueriesViewModel
        : TraceWatcherBaseViewModel, ISaveState , IViewAware
        
    {
        [ImportingConstructor]
        public AllServerQueriesViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions) : base(eventAggregator, globalOptions)
        {
            _queryEvents = new BindableCollection<QueryEvent>();
            QueryTypes = new ObservableCollection<string>();
            QueryTypes.Add("DAX");
            QueryTypes.Add("Dmx");
            QueryTypes.Add("Mdx");
            QueryTypes.Add("Sql");
        }
        
        public ObservableCollection<string> QueryTypes { get; set; }

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
                { DaxStudioTraceEventClass.QueryEnd};
        }

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults() {

            //if (IsPaused) return; // exit here if we are paused

            if (Events != null) {
                foreach (var traceEvent in Events) {
                    QueryEvents.Insert(0,new QueryEvent() {
                        QueryType = traceEvent.EventSubclassName.Substring(0,3),
                        StartTime = traceEvent.StartTime,
                        //EndTime = traceEvent.EndTime,
                        Username = traceEvent.NTUserName,
                        Query = traceEvent.TextData.StripLineBreaks(), // strip out newlines
                        Duration = traceEvent.Duration,
                        DatabaseName = traceEvent.DatabaseFriendlyName
                        
                    });
                }
                
                Events.Clear();

                NotifyOfPropertyChange(() => QueryEvents);
                NotifyOfPropertyChange(() => CanClearAll);
                NotifyOfPropertyChange(() => CanCopyAll);
            }
        }
        
 
        private readonly BindableCollection<QueryEvent> _queryEvents;
        
        public new bool CanHide { get { return true; } }
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

        public override void ClearAll()
        {
            QueryEvents.Clear();
            NotifyOfPropertyChange(() => CanClearAll);
            NotifyOfPropertyChange(() => CanCopyAll);
        }

        
        public bool CanClearAll { get { return QueryEvents.Count > 0; } }
        public override void OnReset() {
            IsBusy = false;
            Events.Clear();
            ProcessResults();
        }

        public QueryEvent SelectedQuery { get; set; }

        public override bool IsCopyAllVisible { get { return true; } }
        public override bool IsFilterVisible { get { return true; } }

        public bool CanCopyAll { get { return QueryEvents.Count > 0; } }

        public override void CopyAll()
        {
            //We need to get the default view as that is where any filtering is done
            ICollectionView view = CollectionViewSource.GetDefaultView(QueryEvents);

            var sb = new StringBuilder();
            foreach (var itm in view)
            {
                var q = itm as QueryEvent;
                if (q != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"// {q.QueryType} query against Database: {q.DatabaseName} ");
                    sb.AppendLine($"{q.Query}");
                }

            }
            sb.AppendLine();
            _eventAggregator.PublishOnUIThread(new SendTextToEditor(sb.ToString()));
        }

        public override void ClearFilters()
        {
            var vw = GetView() as Views.AllServerQueriesView;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.QueryEvents);
            controller.ClearFilter();
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

            _eventAggregator.PublishOnUIThread(new ShowTraceWindowEvent(this));
            string data = File.ReadAllText(filename);
            List<QueryEvent> qe = JsonConvert.DeserializeObject<List<QueryEvent>>(data);
            
            _queryEvents.Clear();
            _queryEvents.AddRange(qe);
            NotifyOfPropertyChange(() => QueryEvents);
        }

        

        public void SetDefaultFilter(string column, string value)
        {
            var vw = this.GetView() as Views.AllServerQueriesView;
            var controller = DataGridExtensions.GetDataGridFilterQueryController(vw.QueryEvents);
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
