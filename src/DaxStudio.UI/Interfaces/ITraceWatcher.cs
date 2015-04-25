using System.Collections.Generic;
using Microsoft.AnalysisServices;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;

namespace DaxStudio.UI.Interfaces
{
    public interface ITraceWatcher
    {
        List<TraceEventClass> MonitoredEvents { get; }
        // todo - need to pass event object as parameter
        //void ProcessEvent(TraceEventArgs traceEvent);
        void Reset();
        bool IsEnabled { get; set; }
        bool IsChecked { get; set; }
        bool IsBusy { get; set; }
        void CheckEnabled(IConnection connection);

        string ToolTipText { get; }


        void ProcessAllEvents(IList<DaxStudioTraceEventArgs> capturedEvents);
    }
}
