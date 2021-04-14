using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AnalysisServices;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Interfaces
{
    public interface ITraceWatcher
    {
        List<DaxStudioTraceEventClass> MonitoredEvents { get; }
        DocumentViewModel Document { get; set; }
        void Reset();
        bool IsEnabled { get; set; }
        bool IsChecked { get; set; }
        bool IsBusy { get; set; }
        bool FilterForCurrentSession { get; }
        void CheckEnabled(IConnection connection, ITraceWatcher active);
        string DisableReason { get; }
        string ToolTipText { get; }
        bool IsPaused { get; set; }

        void ProcessAllEvents(IList<DaxStudioTraceEventArgs> capturedEvents);

        void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent);
        
        QueryTraceStatus TraceStatus { get; }
        string TraceSuffix { get; }

        Task StopTraceAsync();
    }
}
