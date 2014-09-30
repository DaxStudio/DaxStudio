using System.Collections.Generic;
using Microsoft.AnalysisServices;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.Model
{
    public interface ITraceWatcher
    {
        List<TraceEventClass> MonitoredEvents { get; }
        // todo - need to pass event object as parameter
        void ProcessEvent(TraceEventArgs traceEvent);
        void Reset();
        bool IsEnabled { get; set; }
        bool IsChecked { get; set; }

        void CheckEnabled(IConnection connection);
    }
}
