
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
using DaxStudio.Common.Enums;

namespace DaxStudio.QueryTrace
{
    public static class QueryTraceEngineFactory
    {
        public static IQueryTrace CreateLocal(IConnectionManager connection, Dictionary<DaxStudioTraceEventClass,List<int>> events, IGlobalOptions globalOptions, bool filterForCurrentSession, string traceSuffix) {
            //var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new QueryTraceEngine(connection, events, globalOptions, filterForCurrentSession , connection.FileName, traceSuffix); 
        }
        public static IQueryTrace CreateRemote(IConnectionManager connection, Dictionary<DaxStudioTraceEventClass,List<int>> events, int port, IGlobalOptions globalOptions, bool filterForCurrentSession, string traceSuffix) {
            //var dsEvents = events.Select(e => (DaxStudioTraceEventClass)e).ToList();
            return new RemoteQueryTraceEngine(connection, events, port, globalOptions, filterForCurrentSession,connection.FileName, traceSuffix);
        }
    }
}
