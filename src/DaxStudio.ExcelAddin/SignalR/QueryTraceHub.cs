using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DaxStudio.QueryTrace;
using DaxStudio.ExcelAddin;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;
using DaxStudio.SignalR;
using ADOTabular.Enums;

namespace DaxStudio
{


    [HubName("QueryTrace")]
        public class QueryTraceHub : Hub<IQueryTraceHub>
        {
            internal delegate void VoidDelegate();

            private static QueryTraceEngineExcel _xlEngine;
            private static QueryTraceEngine _engine;
            //public void ConstructQueryTraceEngine(ADOTabular.AdomdClientWrappers.AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> eventsToCapture)
            //{
            //    var stubGlobalOptions =  new StubGlobalOptions();
            //    ConstructQueryTraceEngine(connectionType, sessionId, eventsToCapture, stubGlobalOptions);
            //}

            public void ConstructQueryTraceEngine(AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> eventsToCapture, bool filterForCurrentSession, string powerBIFileName) //, IGlobalOptions globalOptions)
            {
                try
                {
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Starting");
                    string powerPivotConnStr = "";
                    using (var xl = new ExcelHelper(Globals.ThisAddIn.Application))
                    {
                        powerPivotConnStr = xl.GetPowerPivotConnectionString();
                        // override command type if this is Excel 2013 or later
                        if (xl.IsExcel2013OrLater)
                        {
                            connectionType = AdomdType.Excel;
                            Log.Debug("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructing QueryTraceEngineExcel");
                            // Anonymouse delegate stops .Net from trying to load MIcrosoft.Excel.Amo.dll when we are running inside Excel 2010
                            VoidDelegate f = delegate
                            {
                                _xlEngine = new QueryTraceEngineExcel(powerPivotConnStr, connectionType, sessionId, "", eventsToCapture, filterForCurrentSession);
                                _xlEngine.TraceError += ((o, e) => { Clients.Caller.OnTraceError(e); });
                                _xlEngine.TraceCompleted += ((o, e) => { OnTraceCompleted(e); });
                                _xlEngine.TraceStarted += ((o, e) => { Clients.Caller.OnTraceStarted(); });

                            };
                            f();
                            Log.Debug("{class} {method} {event} {status}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructed QueryTraceEngineExcel", (_xlEngine != null));
                        }
                        else
                        {
                            connectionType = AdomdType.AnalysisServices;
                            Log.Debug("{class} {method} {event}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructing QueryTraceEngine");
                            _engine = new QueryTraceEngine(powerPivotConnStr, connectionType, sessionId, "", "", eventsToCapture, new StubGlobalOptions(), filterForCurrentSession, powerBIFileName);
                            _engine.TraceError += ((o, e) => { Clients.Caller.OnTraceError(e); });
                            _engine.TraceWarning += ((o, e) => { Clients.Caller.OnTraceWarning(e); });
                            _engine.TraceCompleted += ((o, e) => { OnTraceCompleted(e); });
                            _engine.TraceStarted += ((o, e) => { Clients.Caller.OnTraceStarted(); });
                            Log.Debug("{class} {method} {event} {status}", "QueryTraceHub", "ConstructQueryTraceEngine", "Constructed QueryTraceEngine", (_engine != null));
                        }

                    }
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {method} {exception}", "QueryTraceHub", "ConstructQueryTraceEngine", ex.Message);
                    Clients.Caller.OnTraceError(string.Format("{0}\n{1}", ex.Message, ex.StackTrace));
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

            public void StartAsync(int startTimeoutSecs)
            {

                if (QueryTraceHub._xlEngine != null)
                {
                    // Excel 2013 based traces
                    // Anonymouse delegate stops .Net from trying to load MIcrosoft.Excel.Amo.dll when we are running inside Excel 2010
                    VoidDelegate f = delegate
                    {
                        QueryTraceHub._xlEngine.StartAsync(startTimeoutSecs).ContinueWith((x) =>
                        {
                            if (x.IsFaulted)
                            {
                            // faulted with exception
                            Exception ex = x.Exception;
                                while (ex is AggregateException && ex.InnerException != null)
                                    ex = ex.InnerException;
                                Log.Error("{class} {method} {message}", "QueryTraceHub", "StartAsync", "ExcelEngine: " + ex.Message);
                                Clients.Caller.OnTraceError("Error starting Trace - " + ex.Message);
                            }
                            else if (x.IsCanceled)
                            {
                                Log.Warning("{class} {method} {message}", "QueryTraceHub", "StartAsync", "Trace Cancelled during startup");
                                Clients.Caller.OnTraceError("Trace cancelled during startup");
                            }
                            else
                            {
                                Log.Debug("{class} {method} {message}", "QueryTraceHub", "StartAsync", "Trace Starting");
                                Clients.Caller.OnTraceStarting();
                            }
                        }, TaskScheduler.Default);
                    };
                    f();
                    return;
                }

                else if (QueryTraceHub._engine != null)
                {
                    // server or Excel 2010 based traces
                    QueryTraceHub._engine.StartAsync(startTimeoutSecs).ContinueWith((x) =>
                    {
                        if (x.IsFaulted)
                        {
                        // faulted with exception
                        Exception ex = x.Exception;
                            while (ex is AggregateException && ex.InnerException != null)
                                ex = ex.InnerException;
                            Log.Error("{class} {method} {message}", "QueryTraceHub", "StartAsync", "SSASEngine: " + ex.Message);
                            Clients.Caller.OnTraceError("Error starting Trace - " + ex.Message);
                        }
                        else if (x.IsCanceled)
                        {
                            Log.Warning("{class} {method} {message}", "QueryTraceHub", "StartAsync", "Trace Cancelled during startup");
                            Clients.Caller.OnTraceError("Trace cancelled during startup");
                        }
                        else
                        {
                            Log.Debug("{class} {method} {message}", "QueryTraceHub", "StartAsync", "Trace Starting");
                            Clients.Caller.OnTraceStarting();
                        }
                    }, TaskScheduler.Default);
                    return;
                }
                else
                {
                    // if neither engine has been created report an error
                    Log.Error("{class} {method} {message}", "QueryTraceHub", "StartAsync", "QueryTraceEngine not constructed");
                    Clients.Caller.OnTraceError("QueryTraceEngine not constructed");
                }
            }

            public void Stop()
            {
                Stop(true);
            }

            public void Stop(bool shouldDispose)
            {
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "enter");
                if (_xlEngine != null)
                {
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "stopping xlEngine");
                    // Anonymouse delegate stops .Net from trying to load MIcrosoft.Excel.Amo.dll when we are running inside Excel 2010
                    VoidDelegate f = delegate
                    {
                        _xlEngine.Stop(shouldDispose);
                    };
                    f();
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "stopped xlEngine");
                }
                if (_engine != null)
                {
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "stopping engine");
                    _engine.Stop(shouldDispose);
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "stopped engine");
                }
                Log.Debug("{class} {method} {message}", "QueryTraceHub", "Stop", "Trace Stopping");
                Clients.Caller.OnTraceStopped();
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Stop", "exit");
            }

            public new void Dispose()
            {
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Dispose", "enter");
                if (_xlEngine != null)
                {
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Dispose", "disposing xlEngine");
                    // Anonymouse delegate stops .Net from trying to load MIcrosoft.Excel.Amo.dll when we are running inside Excel 2010
                    VoidDelegate f = delegate
                    {
                        _xlEngine.Dispose();
                        _xlEngine = null;
                    };
                    f();

                }
                if (_engine != null)
                {
                    Log.Debug("{class} {method} {event}", "QueryTraceHub", "Dispose", "disposing engine");

                    _engine.Dispose();
                    _engine = null;
                }
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Dispose", "exit");
            }

            public void UpdateEvents(List<DaxStudioTraceEventClass> events)
            {
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "UpdateEvents", "enter");
                if (_xlEngine != null)
                {
                    VoidDelegate f = delegate
                    {
                        _xlEngine.Events.Clear();
                        _xlEngine.Events.AddRange(events);
                    };
                    f();
                }
                else
                {
                    _engine.Events.Clear();
                    _engine.Events.AddRange(events);
                }
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "UpdateEvents", "exit");
            }

            public void Update()
            {
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Update", "enter");
                if (_xlEngine != null)
                {
                    VoidDelegate f = delegate
                    {
                        _xlEngine.Update();
                    };
                    f();
                }
                else
                {
                    _engine.Update();
                }
                Log.Debug("{class} {method} {event}", "QueryTraceHub", "Update", "exit");
            }

        }
    
}
