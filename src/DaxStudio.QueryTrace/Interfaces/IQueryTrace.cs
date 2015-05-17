using ADOTabular.AdomdClientWrappers;
using Microsoft.AnalysisServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.QueryTrace.Interfaces
{
    public enum QueryTraceStatus
    {
        Stopped,
        Stopping,
        Started,
        Starting,
        Unknown
    }

    public interface IQueryTrace
    {
        Task StartAsync();
        void Stop();

        void ConfigureTrace(string connectionString, AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> events);

        //event TraceEventHandler TraceEvent;
        event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;
        event EventHandler TraceStarted;
        event EventHandler<string> TraceError;
        /*
        public void ClearEvents();
        void OnQueryEnd();
        void OnTraceError();
        void OnTracedStarted();
        */
        QueryTraceStatus Status {get;}

        void Dispose();
    }
}
