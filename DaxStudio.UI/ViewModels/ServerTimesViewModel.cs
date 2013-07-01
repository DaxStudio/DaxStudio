using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Microsoft.AnalysisServices;

namespace DaxStudio.UI.ViewModels
{
    //[Export(typeof(ITraceWatcher)),PartCreationPolicy(CreationPolicy.NonShared)]
    class ServerTimesViewModel: TraceWatcherBaseViewModel
    {
        [ImportingConstructor]
        public ServerTimesViewModel(IEventAggregator eventAggregator) : base(eventAggregator)
        {
        }

        protected override List<TraceEventClass> GetMonitoredEvents()
        {
            return new List<TraceEventClass> 
                { TraceEventClass.QuerySubcube
                    , TraceEventClass.VertiPaqSEQueryEnd
                , TraceEventClass.QueryEnd };
        }
    

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected override void ProcessResults()
        {
            StorageEngineDuration = 0;

            foreach (var traceEvent in Events)
            {
                if (traceEvent.EventClass == TraceEventClass.VertiPaqSEQueryEnd && traceEvent.EventSubclass == TraceEventSubclass.VertiPaqScan)
                {
                    StorageEngineDuration += traceEvent.Duration;
                }
                if (traceEvent.EventClass == TraceEventClass.QueryEnd)
                {
                    TotalDuration = traceEvent.Duration;
                }
            }
            NotifyOfPropertyChange(()=> StorageEngineDuration);
            NotifyOfPropertyChange(()=> TotalDuration);
        }


        public double StorageEnginePercent {
            get
            {
                return TotalDuration == 0? 0: StorageEngineDuration / TotalDuration;
            }
        }
        public double FormulaEnginePercent {
            get { return TotalDuration == 0 ? 0:(TotalDuration-StorageEngineDuration)/TotalDuration;}
        }
        public long TotalDuration { get; set; }
        public long StorageEngineDuration { get; set; }

        // IToolWindow interface
        public override string Title
        {
            get { return "Server Timings"; }
            set { }
        }
        
    }
}
