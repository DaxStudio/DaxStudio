
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

namespace DaxStudio.QueryTrace
{
    public static class QueryTraceEngineFactory
    {
        public static IQueryTrace CreateLocal(ADOTabular.ADOTabularConnection connection, List<DaxStudioTraceEventClass> events, IGlobalOptions globalOptions, bool filterForCurrentSession) {
            var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new QueryTraceEngine(connection.ConnectionString, connection.Type, connection.SessionId, connection.ApplicationName, connection?.Database?.Name, dsEvents, globalOptions, filterForCurrentSession , connection.FileName); 
        }
        public static IQueryTrace CreateRemote(ADOTabular.ADOTabularConnection connection, List<DaxStudioTraceEventClass> events, int port, IGlobalOptions globalOptions, bool filterForCurrentSession) {
            var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new RemoteQueryTraceEngine(connection.ConnectionString,connection.Type,connection.SessionId, dsEvents, port, globalOptions, filterForCurrentSession,connection.FileName);
        }
    }
}
