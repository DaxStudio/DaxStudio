using System.Collections.Generic;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using System.Threading.Tasks;
using DaxStudio.QueryTrace.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using System.Threading;

namespace DaxStudio.UI.Interfaces
{
    public interface ITraceWatcher : IToolWindow, 
        IHandle<UpdateGlobalOptions>,
        IHandle<TraceChangedEvent>,
        IHandle<TraceChangingEvent>
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

        void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent, string errorMessage = null);
        QueryTraceStatus TraceStatus { get; }
        string TraceSuffix { get; }
        bool IsPreview { get; }
        string KeyTip { get; }
        Task StopTraceAsync();
        void StopTrace();

    }

}
