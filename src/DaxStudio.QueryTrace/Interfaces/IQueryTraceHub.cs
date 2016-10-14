using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.QueryTrace.Interfaces
{
    public interface IQueryTraceHub
    {
        void StartAsync();
        void Stop();
        void ConfigureTrace(ADOTabular.ADOTabularConnection connection, List<DaxStudioTraceEventClass> events);

        void OnTraceError(string message);
        void OnTraceComplete(IList<DaxStudioTraceEventArgs> capturedEvents);
        void OnTraceComplete(DaxStudioTraceEventArgs[] capturedEvents);
        void OnTraceStarting();
        void OnTraceStarted();
        void OnTraceStopped();
        void GetStatus(QueryTraceStatus status);
        void UpdateEvents (List<DaxStudioTraceEventClass> events);
    }
}
