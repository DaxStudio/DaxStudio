using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Microsoft.AnalysisServices;

namespace DaxStudio.UI.ViewModels
{
    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class QueryPlanTraceViewModel: TraceWatcherBaseViewModel
    {
        [ImportingConstructor]
        public QueryPlanTraceViewModel(IEventAggregator eventAggregator) : base(eventAggregator)
        {
        }

        protected override List<TraceEventClass> GetMonitoredEvents()
        {
            return new List<TraceEventClass> 
                { TraceEventClass.DAXQueryPlan
                , TraceEventClass.QueryEnd };
        }
    

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {

            foreach (var traceEvent in Events)
            {
                if (traceEvent.EventClass == TraceEventClass.DAXQueryPlan && traceEvent.EventSubclass == TraceEventSubclass.DAXVertiPaqLogicalPlan)
                {
                    LogicalQueryPlanText = traceEvent.TextData;
                    NotifyOfPropertyChange(() => LogicalQueryPlanText);
                }
                if (traceEvent.EventClass == TraceEventClass.DAXQueryPlan && traceEvent.EventSubclass == TraceEventSubclass.DAXVertiPaqPhysicalPlan)
                {
                    PhysicalQueryPlanText = traceEvent.TextData;
                    NotifyOfPropertyChange(() => PhysicalQueryPlanText);
                }
                if (traceEvent.EventClass == TraceEventClass.QueryEnd)
                {
                    TotalDuration = traceEvent.Duration;
                    NotifyOfPropertyChange(() => TotalDuration);
                }
            }
        }

        public string PhysicalQueryPlanText { get; set; }
        public string LogicalQueryPlanText { get; set; }
        public long TotalDuration { get; set; }

        // IToolWindow interface
        public override string Title
        {
            get { return "Query Plan"; }
            set { }
        }
        
    }
}
