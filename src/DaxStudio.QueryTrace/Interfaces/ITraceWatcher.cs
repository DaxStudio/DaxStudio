using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Common.Enums;
using DaxStudio.Common.Interfaces;
using Microsoft.AnalysisServices;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;


namespace DaxStudio.QueryTrace.Interfaces
{
    public interface ITraceWatcher: IToolWindow
    {
        IDaxDocument Document { get; set; }
        void Reset();
        bool IsEnabled { get; set; }
        bool IsChecked { get; set; }
        bool IsBusy { get; set; }
        bool FilterForCurrentSession { get; }
        void CheckEnabled(IConnection connection, ITraceWatcher active);
        string DisableReason { get; }
        string ToolTipText { get; }
        bool IsPaused { get; set; }

        void ProcessAllEvents();

        void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent);
        
        QueryTraceStatus TraceStatus { get; }
        string TraceSuffix { get; }

        Task StopTraceAsync();
        void StopTrace();
    }
}
