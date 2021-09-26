using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using Microsoft.AnalysisServices;
using System.Xml;
using System.Timers;
using Caliburn.Micro;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;
using DaxStudio.Common;
using Polly;
using ADOTabular.Enums;
using Trace = Microsoft.AnalysisServices.Trace;

namespace DaxStudio.QueryTrace
{
    public class QueryTraceEngine : IQueryTrace
    {

        #region public IQueryTrace interface
        public async Task StartAsync(int startTimeoutSec)
        {
            TraceStartTimeoutSecs = startTimeoutSec;
            await Task.Run(Start);
        }

        public void Stop()
        {
            Stop(true);
        }

        public int TraceStartTimeoutSecs { get; private set; }

        public void Stop(bool shouldDispose)
        {
            try
            {
                Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "entering");
                Status = QueryTraceStatus.Stopping;
                if (_trace != null)
                {
                    _trace.OnEvent -= OnTraceEventInternal;
                    try
                    {
                        if (_startingTimer != null)
                        {
                            if (_startingTimer.Enabled)
                            {
                                Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "stopping ping timer");
                                _startingTimer.Stop();
                                _startingTimer.Elapsed -= OnTimerElapsed;
                            }
                        }

                        if (shouldDispose)
                        {
                            Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "Disposing underlying trace");
                            DisposeTrace();
                        }
                        else
                        {
                            Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "Stopping underlying trace");
                            _trace.Stop();
                        }
                        Status = QueryTraceStatus.Stopped;

                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                    }
                } else
                {
                    Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "skipping stop, trace is null");
                }
                _traceStarted = false;
                Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "exiting");
            }
            catch (Exception ex)
            {
                Execute.OnUIThread(() => RaiseError("QueryTraceEngine.Stop:" + ex.Message));
            }
        }


        public QueryTraceStatus Status { get; private set; }

        public List<DaxStudioTraceEventClass> Events { get; }

        public event TraceEventHandler TraceEvent;
        public event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;
        public event EventHandler TraceStarted;
        public event EventHandler<string> TraceError;
        public event EventHandler<string> TraceWarning;
        #endregion

        #region Internal implementation
        private Server _server;
        private Trace _trace;
        private DateTime _utcPingStart;
        private string _connectionString;
        private readonly IConnectionManager _connectionManager;
        private readonly AdomdType _connectionType;
        private string _sessionId;
        private Timer _startingTimer;
        private List<DaxStudioTraceEventArgs> _capturedEvents = new List<DaxStudioTraceEventArgs>();
        private readonly IGlobalOptionsBase _globalOptions;
        private readonly object _connectionLockObj = new object();
        private readonly bool _filterForCurrentSession;
        private readonly string _powerBiFileName;
        public QueryTraceEngine(IConnectionManager connectionManager, List<DaxStudioTraceEventClass> events, IGlobalOptions globalOptions, bool filterForCurrentSession, string powerBiFileName)
        {
            Log.Verbose("{class} {method} {event} connectionString: {connectionString}", "QueryTraceEngine", "<Constructor>", "Start", connectionManager.ConnectionString);
            _globalOptions = globalOptions;
            _connectionManager = connectionManager;
            Status = QueryTraceStatus.Stopped;

            // ping the connection to make sure it is connected
            _connectionManager.Ping();

            _sessionId = connectionManager.SessionId;
            _connectionType = connectionManager.Type;
            _applicationName = connectionManager.ApplicationName;
            _databaseName = connectionManager.DatabaseName;

            _connectionString = AdjustConnectionString(_connectionManager.ConnectionString);
            Events = events;
            _filterForCurrentSession = filterForCurrentSession;
            _powerBiFileName = powerBiFileName;
            Log.Verbose("{class} {method} {event}", "QueryTraceEngine", "<Constructor>", "End - event count" + events.Count);
        }

        public QueryTraceEngine(string connectionString, AdomdType connectionType, string sessionId, string applicationName, string databaseName, List<DaxStudioTraceEventClass> events, IGlobalOptionsBase globalOptions, bool filterForCurrentSession, string powerBiFileName)
        {
            Log.Verbose("{class} {method} {event} connectionString: {connectionString}", "QueryTraceEngine", "<Constructor>", "Start", connectionString);
            _globalOptions = globalOptions;
            Status = QueryTraceStatus.Stopped;

            _sessionId = sessionId;
            _connectionType = connectionType;
            _applicationName = applicationName;
            _databaseName = databaseName;

            _connectionString = AdjustConnectionString(connectionString);
            Events = events;
            _filterForCurrentSession = filterForCurrentSession;
            _powerBiFileName = powerBiFileName;
            Log.Verbose("{class} {method} {event}", "QueryTraceEngine", "<Constructor>", "End - event count" + events.Count);
        }

        private string AdjustConnectionString(string connectionString)
        {
            Log.Verbose("{class} {method} {event} ConnStr: {connectionString}", nameof(QueryTraceEngine), nameof(AdjustConnectionString), "Start", connectionString);

            var connStrBuilder = new System.Data.OleDb.OleDbConnectionStringBuilder(connectionString);
            connStrBuilder.Remove("MDX Compatibility");
            connStrBuilder.Remove("Cell Error Mode");
            connStrBuilder.Remove("Roles");
            connStrBuilder.Remove("EffectiveUsername");
            connStrBuilder.Remove("Authentication Scheme");
            connStrBuilder.Remove("Ext Auth Info");
            connStrBuilder["SessionId"] = _sessionId;
            if (_databaseName.Length > 0) connStrBuilder["Initial Catalog"] = _databaseName;
            Log.Verbose("{class} {method} {event} ", nameof(QueryTraceEngine), nameof(AdjustConnectionString), "End");

            return connStrBuilder.ToString();

        }
        private void SetupTraceEvents(Trace trace, List<DaxStudioTraceEventClass> events)
        {
            Log.Verbose(Constants.LogMessageTemplate, nameof(QueryTraceEngine), nameof(SetupTraceEvents), "entering"); 
            trace.Events.Clear();
            // Add CommandBegine & DiscoverBegin so we can catch the heartbeat events
            trace.Events.Add(TraceEventFactory.Create(TraceEventClass.DiscoverBegin)); 
            trace.Events.Add(TraceEventFactory.Create(TraceEventClass.CommandBegin));
            // Add QueryEnd so we know when to stop the trace
            trace.Events.Add(TraceEventFactory.Create(TraceEventClass.QueryEnd));
            
            // catch the events in the ITraceWatcher
            foreach (DaxStudioTraceEventClass eventClass in events)
            {
                TraceEventClass amoEventClass = (TraceEventClass)eventClass;
                if (trace.Events.Find(amoEventClass) != null)
                    continue;

                var trcEvent = TraceEventFactory.Create(amoEventClass);
                trace.Events.Add(trcEvent);
            }
            trace.Update(UpdateOptions.Default, UpdateMode.CreateOrReplace);
            Log.Verbose(Constants.LogMessageTemplate, nameof(QueryTraceEngine), nameof(SetupTraceEvents), "exiting");
        }

        private XmlNode GetSpidFilter(int spid)
        {
            var filterXml =
                $"<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{(int) TraceColumn.Spid}</ColumnID><Value>{spid}</Value></Equal>";
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        private XmlNode GetSessionIdFilter(string sessionId, string applicationName)
        {
            string filterTemplate = "<Or xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">" +
                        "<Equal><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>" +
                        "<Equal><ColumnID>{2}</ColumnID><Value>{3}</Value></Equal>" +
                        "</Or>";
            var filterXml = string.Format(
                filterTemplate
                , (int)TraceColumn.SessionID
                , sessionId
                , (int)TraceColumn.ApplicationName
                , applicationName);
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        private void Start()
        {
            try
            {
                Log.Verbose("{class} {method} {message}", nameof(QueryTraceEngine), nameof(Start), "entering");
                if (_trace != null)
                    if (_trace.IsStarted || Status == QueryTraceStatus.Starting || Status == QueryTraceStatus.Started)
                        return; // exit here if trace is already started

                if (Status != QueryTraceStatus.Started)  Status = QueryTraceStatus.Starting;
                Log.Verbose("{class} {method} {event}", "QueryTraceEngine", "Start", "Connecting to: " + _connectionString);

                if (_connectionManager != null)
                {
                    _connectionManager.Ping();
                    _sessionId = _connectionManager.SessionId;
                    _databaseName = _connectionManager.DatabaseName;
                    _connectionString = AdjustConnectionString(_connectionManager.ConnectionString);
                }

                _trace = GetTrace();
                SetupTraceEvents(_trace, Events);
               
                _trace.OnEvent += OnTraceEventInternal;
                _trace.Start();

                // create timer to "ping" the server with DISCOVER_SESSION requests
                // until the trace events start to fire.
                if (_startingTimer == null)
                    _startingTimer = new Timer();
                
                _startingTimer.Interval = 500; 
                _startingTimer.Elapsed += OnTimerElapsed;
                _startingTimer.Enabled = true;
                _startingTimer.Start();
                _utcPingStart = DateTime.UtcNow;
                // Wait for Trace to become active
                Log.Verbose("{class} {method} {message}", nameof(QueryTraceEngine), nameof(Start), "exiting");
            }
            catch (Exception ex)
            {
                RaiseError(ex);
                //Log.Error("{class} {method} {message}","QueryTraceEngine" , "Start", ex.Message);
            }
        }


        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                // lock to prevent multiple threads attempting to open the connection
                lock (_connectionLockObj)
                {
                     
                    var policy = Policy
                        .Handle<Exception>()
                        .WaitAndRetry(
                            3,
                            retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100),
                            (exception, timeSpan, context) => {
                                Log.Error(exception,"{class} {method}", "QueryTraceEngine", "OnTimerElapsed");
                                System.Diagnostics.Debug.WriteLine("Error pinging trace connection: " + exception.Message);
                                // TODO - should we raise event aggregator 
                                RaiseWarning("There was an error while pinging the trace - retrying");
                            }
                        );

                    policy.Execute(() => {
                        //if (_connection.State != System.Data.ConnectionState.Open) _connection.Open();
                        Debug.WriteLine("Connection.PingTrace()");
                        //_connection.PingTrace(); 
                        _connectionManager.PingTrace();
                        Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "OnTimerElapsed", "Pinging Connection");
                    });
                }
                    

                
            }
            catch (Exception ex)
            {
                Execute.OnUIThread(()=> RaiseError(ex));
                //TODO stop trace and send message to reset UI
            }
            finally
            {
                // if past timeout then exit and display error
                if ((DateTime.UtcNow - _utcPingStart).Seconds > this.TraceStartTimeoutSecs)
                {
                    _startingTimer.Stop();
                    DisposeTrace();
                    RaiseError("Timeout exceeded attempting to start Trace. You could try increasing this timeout in the Options");
                }
            }
        }
        
        private Trace GetTrace()
        {
            if (_trace == null)
            {
                _server = new Server();
                _server.Connect(_connectionString);
            
                _trace = _server.Traces.Add($"DaxStudio_Session_{_sessionId}");

                // Enable automatic filter only if DirectQuery is not enabled - otherwise, it will filter events in the trace event (slower, use DirectQuery with care!)
                if ((!_globalOptions.TraceDirectQuery || Version.Parse(_server.Version).Major >= 14 ) && _filterForCurrentSession) {
                    Log.Verbose("Activate filter {sessionId} - {applicationName}", _sessionId, _applicationName);
                    _trace.Filter = GetSessionIdFilter(_sessionId, _applicationName);
                }

                // set default stop time in case trace gets disconnected
                _trace.StopTime = DateTime.UtcNow.AddHours(24);
                _trace.Audit = true;
            }
            return _trace;
        }

        public void OnTraceEvent( TraceEventArgs e)
        {
            TraceEvent?.Invoke(this, e);
        }

        public void RaiseError( string message)
        {
            TraceError?.Invoke(this, message);
            Log.Error("{class} {method} {message}", "QueryTraceEngine", "RaiseError", message);
        }

        public void RaiseWarning(string message)
        {
            TraceWarning?.Invoke(this, message);
            Log.Warning("{class} {method} {message}", "QueryTraceEngine", "RaiseWarning", message);
        }

        public void RaiseError(Exception ex)
        {
            Exception e = ex;
            while (e.InnerException != null)
            {
                e = e.InnerException;
            }
            TraceError?.Invoke(this, e.Message);
            Log.Error(ex,"{class} {method} {message}", "QueryTraceEngine", "RaiseError", GetAllExceptionMessages( ex));
            if (ex.InnerException != null)
                Log.Error("{class} {method} {message}/n{stacktrace}", "QueryTraceEngine", "RaiseError (InnerException)", ex.InnerException.Message, ex.InnerException.StackTrace);
        }

        public string GetAllExceptionMessages(Exception ex)
        {
            var innerEx = ex;
            var msg = "";
            while (innerEx != null)
            {
                msg = innerEx.Message + "\n";
                innerEx = innerEx.InnerException;
            }
            return msg;
        }

        // private variables
        private bool _traceStarted;
        private readonly string _applicationName;
        private string _databaseName;
        private string _activityId;

        private void OnTraceEventInternal(object sender, TraceEventArgs e)
        {
            try
            {
                if (_globalOptions.TraceDirectQuery && _filterForCurrentSession)
                {
                    if ((e.SessionID != null) && (e.SessionID != _sessionId))
                    {
                        System.Diagnostics.Debug.Print("Skipped event by session {0} - {1}", e.EventClass.ToString(), e.SessionID);
                        Log.Verbose("Skipped event by session {EventClass} - {sessionId}", e.EventClass.ToString(), e.SessionID);
                        return;
                    }
                    // Check ActivityId only for DirectQueryEnd event (others should be already filtered by SessionID)
                    if (e.EventClass == TraceEventClass.DirectQueryEnd)
                    {
                        bool bSkipByActivity = string.IsNullOrEmpty(_activityId);
                        if (!bSkipByActivity)
                        {
                            bSkipByActivity = e[TraceColumn.ActivityID] != _activityId;
                        }
                        if (bSkipByActivity)
                        {
                            System.Diagnostics.Debug.Print("Skipped event by activity {0} - {1}", e.EventClass.ToString(), e[TraceColumn.ActivityID]);
                            Log.Verbose("Skipped event by activity {EventClass} - {sessionId}", e.EventClass.ToString(), e[TraceColumn.ActivityID]);
                            return;
                        }
                    }
                }
                // we are using CommandBegin as a "heartbeat" to check if the trace
                // has started capturing events
                if (!_traceStarted)
                {
                    System.Diagnostics.Debug.Print("Pending TraceEvent: {0}", e.EventClass.ToString());
                    Log.Verbose("Pending TraceEvent: {EventClass} - {EventSubClass}", e.EventClass.ToString(), e.EventSubclass.ToString());
                    Log.Verbose("Saving ActivityID: {ActivityID}", e[TraceColumn.ActivityID]);

                    StopTimer();
                    _traceStarted = true;

                    Status = QueryTraceStatus.Started;
                    if (TraceStarted != null)
                    {
                        Log.Debug("{class} {method} {message}", "QueryTraceEngine", "OnTraceEventInternal", "Notifying subscribers that Trace has started");
                        TraceStarted(this, null);
                    }
                    else
                    {
                        Log.Debug("{class} {method} {message}", "QueryTraceEngine", "OnTraceEventInternal", "No Trace started subscribers found");
                    }
                }
                else
                {
                    // exit early if there is no text in the query
                    if ((e.EventClass == TraceEventClass.QueryBegin ||
                         e.EventClass == TraceEventClass.QueryEnd) && e.TextData.StartsWith("/* PING */"))
                    {
                        Debug.WriteLine("Skipping Empty <Statement>");
                        return;
                    }


                    System.Diagnostics.Debug.Print("TraceEvent: {0}", e.EventClass.ToString());
                    Log.Verbose("TraceEvent: {EventClass} - {EventSubClass} - {ActivityId}", e.EventClass.ToString(), e.EventSubclass.ToString(), e[TraceColumn.ActivityID]);
                    if (e.EventClass == TraceEventClass.QueryBegin)
                    {
                        // Save activityId and skip event handling
                        _activityId = e[TraceColumn.ActivityID];
                        Log.Verbose("Started ActivityId: {EventClass} - {ActivityId}", e.EventClass.ToString(), e[TraceColumn.ActivityID]);
                        //return;
                    }
                    
                    OnTraceEvent(e);
                    _capturedEvents.Add(new DaxStudioTraceEventArgs(e, _powerBiFileName));
                    if (e.EventClass == TraceEventClass.QueryEnd || e.EventClass == TraceEventClass.Error || e.EventClass == TraceEventClass.CommandEnd)
                    {
                        // if this is not an internal DAX Studio query 
                        // like the one we issue after a ClearCache to re-establish the session
                        // then call TraceCompleted, otherwise we clear out the captured events
                        // and keep waiting
                        if (!e.TextData.Contains(Constants.InternalQueryHeader))
                        {
                            // Raise an event with the captured events
                            TraceCompleted?.Invoke(this, _capturedEvents);
                        }
                        // reset the captured events collection
                        _capturedEvents = new List<DaxStudioTraceEventArgs>();

                        // Reset activity ID
                        _activityId = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Execute.OnUIThreadAsync(() => RaiseError(ex));
            }
        }

        private void StopTimer()
        {
            if (!_startingTimer.Enabled)
                return;
            _startingTimer.Stop();
            _startingTimer.Elapsed -= OnTimerElapsed;
        }

        private void ClearEventSubscribers()
        {
            TraceStarted = (EventHandler)Delegate.RemoveAll(TraceStarted, TraceStarted);
            TraceEvent = (TraceEventHandler)Delegate.RemoveAll(TraceEvent, TraceEvent);
            TraceCompleted = (EventHandler<IList<DaxStudioTraceEventArgs>>)Delegate.RemoveAll(TraceCompleted, TraceCompleted);
            TraceError = (EventHandler<string>)Delegate.RemoveAll(TraceError, TraceError);
            TraceWarning = (EventHandler<string>)Delegate.RemoveAll(TraceWarning, TraceWarning);
        }

        public void Update(string databaseName, string sessionId)
        {
            _databaseName = databaseName;
            _sessionId = sessionId;
            Update();
        }
        public void Update()
        {
            Stop(true);
            _traceStarted = false;
            Start();
        }

        #endregion

        public void DisposeTrace()
        {
            if (_trace == null) return; // exit here if trace has already been disposed
            _trace.OnEvent -= OnTraceEventInternal;
            try
            {
                _trace.Drop();
            }
            catch (Exception ex)
            {
                // just log any error, don't rethrow as we are trying to stop the trace anyway
                Log.Error(ex, "{Class} {Method} Exception while dropping query trace {message}", "QueryTraceEngine", "DisposeTrace", ex.Message);
            }

            // TODO - do we need to call both DROP and DISPOSE ?? Sometimes causes hanging
            //        need to check if AMO is also trying to drop the trace
            //_trace.Dispose();
            _trace = null;
        }

        public void Dispose()
        {
            DisposeTrace();
            ClearEventSubscribers();
        }
    }
}
