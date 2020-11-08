
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace.Interfaces;
using Microsoft.AnalysisServices;
//using AMOTabular;
//using AMOTabular.AmoWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;

namespace DaxStudio.QueryTrace
{
    public static class QueryTraceEngineFactory
    {
        public static IQueryTrace CreateLocal(IConnectionManager connection, List<DaxStudioTraceEventClass> events, IGlobalOptions globalOptions, bool filterForCurrentSession) {
            var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new QueryTraceEngine(connection, dsEvents, globalOptions, filterForCurrentSession , connection.FileName); 
        }
        public static IQueryTrace CreateRemote(IConnectionManager connection, List<DaxStudioTraceEventClass> events, int port, IGlobalOptions globalOptions, bool filterForCurrentSession) {
            var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new RemoteQueryTraceEngine(connection, dsEvents, port, globalOptions, filterForCurrentSession,connection.FileName);
        }
    }
}
