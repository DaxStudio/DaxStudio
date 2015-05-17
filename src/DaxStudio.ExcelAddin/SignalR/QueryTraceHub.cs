using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.QueryTrace;
using Microsoft.AnalysisServices;
using DaxStudio.Interfaces;
using DaxStudio.ExcelAddin;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;

namespace DaxStudio
{
    [HubName("QueryTrace")]
    public class QueryTraceHub:Hub<IQueryTraceHub>
    {
        private static QueryTraceEngineExcel _xlEngine;
        private static QueryTraceEngine _engine;

        public void ConstructQueryTraceEngine( ADOTabular.AdomdClientWrappers.AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> eventsToCapture)
        {
            try
            {
                Log.Verbose("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Starting");
                string powerPivotConnStr = "";
                using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
                {
                    powerPivotConnStr = xl.GetPowerPivotConnectionString();
                    // override command type if this is Excel 2013 or later
                    if (xl.IsExcel2013OrLater)
                    {
                        connectionType = ADOTabular.AdomdClientWrappers.AdomdType.Excel;
                        Log.Verbose("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructing QueryTraceEngineExcel");
                        _xlEngine = new QueryTraceEngineExcel(powerPivotConnStr, connectionType, sessionId, "", eventsToCapture);
                        _xlEngine.TraceError += ((o, e) => { Clients.Caller.OnTraceError(e); });
                        _xlEngine.TraceCompleted += ((o, e) => { OnTraceCompleted(e); });
                        _xlEngine.TraceStarted += ((o, e) => { Clients.Caller.OnTraceStarted(); });
                        Log.Verbose("{class} {method} {event} {status}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructed QueryTraceEngineExcel", (_xlEngine != null));
                    }
                    else
                    {
                        connectionType = ADOTabular.AdomdClientWrappers.AdomdType.AnalysisServices;
                        Log.Verbose("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructing QueryTraceEngine");
                        _engine = new QueryTraceEngine(powerPivotConnStr, connectionType, sessionId,"", eventsToCapture);
                        _engine.TraceError += ((o, e) => { Clients.Caller.OnTraceError(e); });
                        _engine.TraceCompleted += ((o, e) => { OnTraceCompleted(e); });
                        _engine.TraceStarted += ((o, e) => { Clients.Caller.OnTraceStarted(); });
                        Log.Verbose("{class} {method} {event} {status}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructed QueryTraceEngine", (_engine != null));
                    }

                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {exception}", "QueryTraceHub", "ConstructQueryTraceEngine", ex.Message);
                Clients.Caller.OnTraceError(string.Format("{0}\n{1}",ex.Message, ex.StackTrace));
            }
        }

        private void OnTraceCompleted(IList<DaxStudioTraceEventArgs> e)
        {
            try
            {
                Clients.Caller.OnTraceComplete(e.ToArray<DaxStudioTraceEventArgs>());
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {exception}", "QueryTraceHub", "OnTraceCompleted", ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        public void StartAsync() {
            
            if (QueryTraceHub._xlEngine != null) {
                // Excel 2013 based traces
                QueryTraceHub._xlEngine.StartAsync().ContinueWith((x) =>
                {
                    if (x.IsFaulted)
                    {
                        // faulted with exception
                        Exception ex = x.Exception;
                        while (ex is AggregateException && ex.InnerException != null)
                            ex = ex.InnerException;
                        Clients.Caller.OnTraceError("Error starting Trace - " + ex.Message);
                    }
                    else if (x.IsCanceled)
                    {
                        Clients.Caller.OnTraceError("Trace cancelled during startup");
                    }
                    else
                    {
                        Clients.Caller.OnTraceStarting();
                    }
                });
                return;
            }

            else if (QueryTraceHub._engine != null)
            {
                // server or Excel 2010 based traces
                QueryTraceHub._engine.StartAsync().ContinueWith((x) =>
                {
                    if (x.IsFaulted)
                    {
                        // faulted with exception
                        Exception ex = x.Exception;
                        while (ex is AggregateException && ex.InnerException != null)
                            ex = ex.InnerException;
                        Clients.Caller.OnTraceError("Error starting Trace - " + ex.Message);
                    }
                    else if (x.IsCanceled)
                    {
                        Clients.Caller.OnTraceError("Trace cancelled during startup");
                    }
                    else
                    {
                        Clients.Caller.OnTraceStarting();
                    }
                });
                return;
            }
            else
            {
                // if neither engine has been created report an error
                Clients.Caller.OnTraceError("QueryTraceEngine not constructed");
            }
        }
        

        public void Stop() {
            if (_xlEngine != null) _xlEngine.Stop();
            Clients.Caller.OnTraceStopped();
        }

        public void Dispose()
        {
            if (_xlEngine != null) _xlEngine.Dispose();
            if (_engine != null) _engine.Dispose();
        }


    }
}
