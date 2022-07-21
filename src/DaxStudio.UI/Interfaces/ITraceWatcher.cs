using System.Collections.Generic;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using System.Threading.Tasks;
using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio.UI.Interfaces
{
    public interface ITraceWatcher : IToolWindow
    {
        IDaxDocument Document { get; set; }
        //List<DaxStudioTraceEventClass> MonitoredEvents { get; }
        
        void Reset();
        bool IsEnabled { get; set; }
        bool IsChecked { get; set; }
        bool IsBusy { get; set; }
        bool FilterForCurrentSession { get; }
        void CheckEnabled(IConnection connection, ITraceWatcher active);
        string DisableReason { get; }
        string ToolTipText { get; }
        bool IsPaused { get; set; }
        string ImageResource { get; }
        int SortOrder { get; }
        void ProcessAllEvents();

        void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent);
        QueryTraceStatus TraceStatus { get; }
        string TraceSuffix { get; }
        bool IsPreview { get; }

        Task StopTraceAsync();
        void StopTrace();
    }

}
