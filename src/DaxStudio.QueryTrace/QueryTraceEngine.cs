using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using Microsoft.AnalysisServices;
using System.Xml;
using System.Timers;
using Caliburn.Micro;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;

namespace DaxStudio.QueryTrace
{
    public class QueryTraceEngine: IQueryTrace
    {
#region public IQueryTrace interface
        public Task StartAsync()
        {
            return Task.Run(() => Start());
        }

        public void Stop()
        {
            Stop(true);
        }

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
                                _connection.Close(false);
                            }
                        }

                        if (shouldDispose)
                        {
                            Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Stop", "Disposing underlying trace");
                            //_trace.OnEvent -= OnTraceEventInternal;
                            //_trace.Drop();
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
                Execute.OnUIThread( () => RaiseError("Stop:" + ex.Message ));
            }
        }


        public QueryTraceStatus Status
        {
	        get { return _status;  }
            private set {_status = value;}
        }

        public List<DaxStudioTraceEventClass> Events { get; } 

        public event TraceEventHandler TraceEvent;
        public event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;
        public event EventHandler TraceStarted;
        public event EventHandler<string> TraceError;

#endregion

#region Internal implementation
        private Server _server;
        private Trace _trace;
        private DateTime utcPingStart;
        private QueryTraceStatus _status;
        private string _connectionString;
        private ADOTabular.ADOTabularConnection _connection;
        private AdomdType _connectionType;
        private string _sessionId;
        //private List<DaxStudioTraceEventClass> _eventsToCapture;
        private Timer _startingTimer;
        private List<DaxStudioTraceEventArgs> _capturedEvents = new List<DaxStudioTraceEventArgs>();
        private IGlobalOptions _globalOptions;
        private object connectionLockObj = new object();
        private bool _filterForCurrentSession = true;
        public QueryTraceEngine(string connectionString, AdomdType connectionType, string sessionId, string applicationName, List<DaxStudioTraceEventClass> events, IGlobalOptions globalOptions, bool filterForCurrentSession)
        {
            _globalOptions = globalOptions;
            Log.Verbose("{class} {method} {event} connstr: {connnectionString} sessionId: {sessionId}", "QueryTraceEngine", "<Constructor>", "Start",connectionString,sessionId);
            Status = QueryTraceStatus.Stopped;
            ConfigureTrace(connectionString, connectionType, sessionId, applicationName, filterForCurrentSession);
            Events = events;
            _filterForCurrentSession = filterForCurrentSession;
            Log.Verbose("{class} {method} {event}", "QueryTraceEngine", "<Constructor>", "End - event count" + events.Count);
        }

        private void ConfigureTrace(string connectionString, AdomdType connectionType, string sessionId, string applicationName, bool filterForCurrentSession)
        {
            Log.Verbose("{class} {method} {event} ConnStr: {connectionString} SessionId: {sessionId}", "QueryTraceEngine", "ConfigureTrace", "Start",connectionString, sessionId);
            _connectionString = string.Format("{0};SessionId={1}", connectionString, sessionId);
            _connectionString = _connectionString.Replace("MDX Compatibility=1;", ""); // remove MDX Compatibility setting
            _connectionString = _connectionString.Replace("Cell Error Mode=TextValue;", ""); // remove MDX Compatibility setting
            _connectionType = connectionType;
            _sessionId = sessionId;
            _applicationName = applicationName;
            Log.Verbose("{class} {method} {event} ", "QueryTraceEngine", "ConfigureTrace", "End");
        }
        private void SetupTrace(Trace trace, List<DaxStudioTraceEventClass> events)
        {
            Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "SetupTrace", "entering");
            trace.Events.Clear();
            // Add CommandBegin so we can catch the heartbeat events
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
            trace.Update();
            Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "SetupTrace", "exiting");
        }

        private XmlNode GetSpidFilter(int spid)
        {
            var filterXml = string.Format(
                "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)TraceColumn.Spid
                , spid );
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

        public void Start()
        {
            try
            {
                Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Start", "entering");
                if (_trace != null)
                    if (_trace.IsStarted || Status == QueryTraceStatus.Starting || Status == QueryTraceStatus.Started)
                        return; // exit here if trace is already started

                if (Status != QueryTraceStatus.Started)  Status = QueryTraceStatus.Starting;
                Log.Verbose("{class} {method} {event}", "QueryTraceEngine", "Start", "Connecting to: " + _connectionString);
                if (_connection != null) _connection.Dispose();
                _connection = new ADOTabular.ADOTabularConnection(_connectionString, _connectionType);
                try
                {
                    _connection.Open();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", "QueryTraceEngine", "Start (connection.open catch)", ex.Message);
                    // try again?
                    _connection.Open();
                }
                _trace = GetTrace();
                SetupTrace(_trace, Events);
               
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
                utcPingStart = DateTime.UtcNow;
                // Wait for Trace to become active
                Log.Verbose("{class} {method} {message}", "QueryTraceEngine", "Start", "exiting");
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
                Execute.OnUIThread(() => {
                    // lock to prevent multiple threads attempting to open the connection
                    lock (connectionLockObj)
                    {
                        if (_connection.State != System.Data.ConnectionState.Open) _connection.Open();
                        _connection.Ping();
                    }
                    
                });
                
            }
            catch (Exception ex)
            {
                Execute.OnUIThread(()=> RaiseError(ex));
                //TODO stop trace and send message to reset UI
            }
            finally
            {
                // if past timeout then exit and display error
                if ((DateTime.UtcNow - utcPingStart).Seconds > 30)
                {
                    _startingTimer.Stop();
                    DisposeTrace();
                    RaiseError("Timeout exceeded attempting to start Trace");
                }
            }
        }
        
        private Trace GetTrace()
        {
            if (_trace == null)
            {
                _server = new Server();
                _server.Connect(_connectionString);
            
                _trace = _server.Traces.Add( string.Format("DaxStudio_Session_{0}", _sessionId));

                // Enable automatic filter only if DirectQuery is not enabled - otherwise, it will filter events in the trace event (slower, use DirectQuery with care!)
                if (!_globalOptions.TraceDirectQuery && _filterForCurrentSession) {
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
            if (TraceEvent != null)
                TraceEvent(this, e);
        }

        public void RaiseError( string message)
        {
            TraceError?.Invoke(this, message);
            Log.Error("{class} {method} {message}", "QueryTraceEngine", "RaiseError", message);
        }

        public void RaiseError(Exception ex)
        {
            TraceError?.Invoke(this, ex.Message);
            Log.Error("{class} {method} {message}/n{stacktrace}", "QueryTraceEngine", "RaiseError", ex.Message, ex.StackTrace);
        }

        private bool _traceStarted;
        private string _applicationName;
        private string _activityId;

        // private variables
        private void OnTraceEventInternal(object sender, TraceEventArgs e)
        {
            try
            {
                if (_globalOptions.TraceDirectQuery)
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
                    _connection.Close(false);
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
                    System.Diagnostics.Debug.Print("TraceEvent: {0}", e.EventClass.ToString());
                    Log.Verbose("TraceEvent: {EventClass} - {EventSubClass} - {ActivityId}", e.EventClass.ToString(), e.EventSubclass.ToString(), e[TraceColumn.ActivityID]);
                    if (e.EventClass == TraceEventClass.QueryBegin)
                    {
                        // Save activityId and skip event handling
                        _activityId = e[TraceColumn.ActivityID];
                        Log.Verbose("Started ActivityId: {EventClass} - {ActivityId}", e.EventClass.ToString(), e[TraceColumn.ActivityID]);
                        return;
                    }

                    OnTraceEvent(e);
                    _capturedEvents.Add(new DaxStudioTraceEventArgs(e));
                    if (e.EventClass == TraceEventClass.QueryEnd)
                    {
                        // Raise an event with the captured events
                        if (TraceCompleted != null)
                            TraceCompleted(this, _capturedEvents);
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
            _trace.Drop();
            _trace.Dispose();
            _trace = null;
        }

        public void Dispose()
        {
            DisposeTrace();
            ClearEventSubscribers();
        }
    }
}
