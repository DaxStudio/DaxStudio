extern alias ExcelAmo;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xlAmo = ExcelAmo.Microsoft.AnalysisServices;
using System.Xml;
using System.Timers;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;
using Caliburn.Micro;

namespace DaxStudio.QueryTrace
{
    public class QueryTraceEngineExcel: IQueryTrace
    {
#region public IQueryTrace interface
        public Task StartAsync(int startTimeoutSecs)
        {
            Log.Debug("{class} {method} {message}", "QueryTraceEngineExcel", "StartAsync", "entered");
            this.TraceStartTimeoutSecs = startTimeoutSecs;
            return Task.Run(() => Start());
        }

        public void Stop()
        {
            Stop(true);
        }

        public void Stop(bool shouldDispose)
        {
            Log.Debug("{class} {method} {message}", "QueryTraceEngineExcel", "Stop", "entered");
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
                            _startingTimer.Stop();
                            _startingTimer.Elapsed -= OnTimerElapsed;
                            _connection.Close(false);
                            _connection.Dispose();
                            _connection = null;
                        }
                    }

                    if (shouldDispose)
                    {
                        //_trace.OnEvent -= OnTraceEventInternal;
                        _trace.Drop();
                        _trace.Dispose();
                        _trace = null;
                    }
                    else
                    {
                        _trace.Stop();
                    }
                    
                    Status = QueryTraceStatus.Stopped;
                }
                catch (Exception e)
                {
                    Log.Error("{class} {method} {message} {stacktrace}", "QueryTraceEngineExcel", "Stop", e.Message, e.StackTrace);
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }
            Log.Debug("{class} {method} {message}", "QueryTraceEngineExcel", "Stop", "exited");
            _traceStarted = false;
        }

        public int TraceStartTimeoutSecs { get; private set; }
        public QueryTraceStatus Status
        {
	        get { return _status;  }
            private set {_status = value;}
        }

        public List<DaxStudioTraceEventClass> Events { get; }
        public delegate void DaxStudioTraceEventHandler(System.Object sender, DaxStudioTraceEventArgs e);

        public event DaxStudioTraceEventHandler TraceEvent;
        public event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;
        public event EventHandler TraceStarted;
        public event EventHandler<string> TraceError;

#endregion

#region Internal implementation
        private xlAmo.Server _server;
        private xlAmo.Trace _trace;
        private DateTime utcPingStart;
        private QueryTraceStatus _status;
        private string _connectionString;
        private readonly string _originalConnectionString;
        private ADOTabular.ADOTabularConnection _connection;
        private AdomdType _connectionType;
        private readonly string _sessionId;
        //private List<DaxStudioTraceEventClass> _eventsToCapture;
        private Timer _startingTimer;
        private List<DaxStudioTraceEventArgs> _capturedEvents = new List<DaxStudioTraceEventArgs>();
        private string _friendlyServerName = "";
        //public delegate void TraceStartedHandler(object sender);//, TraceStartedEventArgs eventArgs);

        
        public QueryTraceEngineExcel(string connectionString, AdomdType connectionType, string sessionId, string applicationName, List<DaxStudioTraceEventClass> events, bool filterForCurrentSession)
        {
            Status = QueryTraceStatus.Stopped;
            _originalConnectionString = connectionString;
            _sessionId = sessionId;
            FilterForCurrentSession = filterForCurrentSession;
            ConfigureTrace(connectionString, connectionType, sessionId, applicationName);
            Events = events;
        }

        public bool FilterForCurrentSession { get; private set; }

        private void ConfigureTrace(string connectionString, AdomdType connectionType, string sessionId, string applicationName) //, List<DaxStudioTraceEventClass> events)
        {
            //_connectionString = string.Format("{0};SessionId={1}",connectionString,sessionId);
            _connectionString = connectionString;
            _connectionString = _connectionString.Replace("MDX Compatibility=1;", ""); // remove MDX Compatibility setting
            _connectionString = _connectionString.Replace("Cell Error Mode=TextValue;", ""); // remove MDX Compatibility setting
            _connectionType = connectionType;
            _applicationName = applicationName;
        }
        private void SetupTrace(xlAmo.Trace trace, List<DaxStudioTraceEventClass> events)
        {
            Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "SetupTrace", "enter");
            trace.Events.Clear();
            // Add CommandBegin so we can catch the heartbeat events
            trace.Events.Add(TraceEventFactoryExcel.CreateTrace(xlAmo.TraceEventClass.CommandBegin));
            // Add QueryEnd so we know when to stop the trace
            trace.Events.Add(TraceEventFactoryExcel.CreateTrace(xlAmo.TraceEventClass.QueryEnd));
            
            // catch the events in the ITraceWatcher
            foreach (DaxStudioTraceEventClass eventClass in events)
            {
                // PowerPivot in Excel does not have direct query events, so skip it if makes it this far
                if (eventClass == DaxStudioTraceEventClass.DirectQueryEnd) continue;

                var amoEventClass = (ExcelAmo.Microsoft.AnalysisServices.TraceEventClass)eventClass;
                if (trace.Events.Find(amoEventClass) != null)
                    continue;

                xlAmo.TraceEvent trcEvent = TraceEventFactoryExcel.CreateTrace(amoEventClass);
                trace.Events.Add(trcEvent);
            }
            trace.Update();
            Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "SetupTrace", "exit");
        }

        private XmlNode GetSpidFilter(int spid)
        {
            var filterXml = string.Format(
                "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)xlAmo.TraceColumn.Spid
                , spid );
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        private XmlNode GetSessionIdFilter(string sessionId)
        {
            string filterTemplate = "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">" +
                            "<ColumnID>{0}</ColumnID><Value>{1}</Value>" +
                            "</Equal>";
            var filterXml = string.Format(
                filterTemplate
                , (int)xlAmo.TraceColumn.SessionID
                , sessionId);
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }


        public void Start()
        {
			try
			{
                Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "entered");

                if (_trace != null)
                    if (_trace.IsStarted || Status == QueryTraceStatus.Starting || Status == QueryTraceStatus.Started)
                    {
                        Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "exiting method - trace already started");
                        return; // if threturn; // exit here if trace is already startede trace is already running exit here
                    }

	            if (Status != QueryTraceStatus.Started)  Status = QueryTraceStatus.Starting;
				Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "Connecting to: " + _connectionString);
                //HACK - wait 1.5 seconds to allow trace to start 
                //       using the ping method thows a connection was lost error when starting a second trace
                _connection = new ADOTabular.ADOTabularConnection(string.Format("{0};SessionId={1}", _originalConnectionString, _sessionId), _connectionType);
                _connection.Open();
                _trace = GetTrace();
	            SetupTrace(_trace, Events);

	            _trace.OnEvent += OnTraceEventInternal;
	            _trace.Start();
	            
	            // create timer to "ping" the server with DISCOVER_SESSION requests
	            // until the trace events start to fire.
	            if (_startingTimer == null)
	                _startingTimer = new Timer();
                //HACK - wait 1.5 seconds to allow trace to start 
                //       using the ping method thows a connection was lost error when starting a second trace
                _startingTimer.Interval = 300;  
	            _startingTimer.Elapsed += OnTimerElapsed;
	            _startingTimer.Enabled = true;
	            _startingTimer.Start();
	            utcPingStart = DateTime.UtcNow;
                // Wait for Trace to become active
                Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "exit");
            }
			catch (Exception ex)
			{
                RaiseError(string.Format("Error starting trace: {0}", ex.Message));
				Log.Error("{class} {method} {message} {stacktrace}","QueryTraceEngine" , "Start", ex.Message, ex.StackTrace);
			}
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "OnTimerElapsed", "Ping");
            //HACK - wait 1.5 seconds to allow trace to start 
            //       using the ping method thows a connection was lost error when starting a second trace
            Execute.OnUIThread(() => _connection.Ping());
            //Execute.OnUIThread(() => ServerPing());
            // if past timeout then exit and display error
            if ((utcPingStart - DateTime.UtcNow).Seconds > TraceStartTimeoutSecs)
            {
                _startingTimer.Stop();
                _trace.Drop();
                RaiseError("Timeout exceeded attempting to start Trace");
                Log.Warning("{class} {method} {event}", "QueryTraceEngineExcel", "OnTimerElapsed", "Timeout exceeded attempting to start Trace");
            }
            //StopTimer();
            //Status = QueryTraceStatus.Started;
            //_traceStarted = true;
            //if (TraceStarted != null)
            //    TraceStarted(this, null);
        }

        //private void ServerPing()
        //{
        //    _server.StartXmlaRequest(ExcelAmo.Microsoft.AnalysisServices.XmlaRequestType.Execute);
        //    _server.EndXmlaRequest();
        //}

        private xlAmo.Trace GetTrace()
        {

            if (_trace == null)
            {
                Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "GetTrace", "about to create new trace");
                _server = new xlAmo.Server();
                _server.Connect(_connectionString);
                _trace = _server.Traces.Add( string.Format("DaxStudio_Trace_SPID_{0}", _sessionId));
                if (FilterForCurrentSession) _trace.Filter = GetSessionIdFilter(_sessionId);
                // set default stop time in case trace gets disconnected
                //_trace.StopTime = DateTime.UtcNow.AddHours(24);
                Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "GetTrace", "created new trace");
            }
            return _trace;
        }

        public void OnTraceEvent( DaxStudioTraceEventArgs e)
        {
            TraceEvent?.Invoke(this, e);
        }

        public void RaiseError( string message)
        {
            if (TraceError != null) TraceError(this, message);
        }

        private bool _traceStarted;
        private string _applicationName;
        
        private void OnTraceEventInternal(object sender, xlAmo.TraceEventArgs e)
        {
            // we are using CommandBegin as a "heartbeat" to check if the trace
            // has started capturing events
            if (!_traceStarted)
            {
                Log.Debug("{class} {mothod} Pending TraceEvent: {eventClass}","QueryTraceEngineExcel","OnTraceEventInternal", e.EventClass.ToString());
                StopTimer();
                _traceStarted = true;
                _connection.Close(false);
                _connection.Dispose();
                _connection = null;
                Status = QueryTraceStatus.Started;
                TraceStarted?.Invoke(this, null);

                var f = new System.IO.FileInfo(_trace.Parent.Name);
                _friendlyServerName = f.Name;
            }
            else
            {
                Log.Debug("{class} {method} TraceEvent: {eventClass}", "QueryTraceEngineExcel", "OnTraceEventInternal", e.EventClass.ToString());
                //OnTraceEvent(e);
                _capturedEvents.Add( CreateTraceEventArg(e, _friendlyServerName));
                if (e.EventClass == xlAmo.TraceEventClass.QueryEnd)
                {
                    // Raise an event with the captured events
                    TraceCompleted?.Invoke(this, _capturedEvents);
                    // reset the captured events collection
                    _capturedEvents = new List<DaxStudioTraceEventArgs>();
                }
            }
        }

        private void StopTimer()
        {
            if (!_startingTimer.Enabled)
                return;
            _startingTimer.Stop();
            _startingTimer.Elapsed -= OnTimerElapsed;
        }

#endregion
        private DaxStudioTraceEventArgs CreateTraceEventArg(xlAmo.TraceEventArgs traceEvent, string xlsxFile)
        {
            long cpuTime;
            long duration;
            DateTime eventTime = DateTime.Now;
            // not all events have CpuTime
            try
            {
                cpuTime = traceEvent.CpuTime;
            }
            catch (ArgumentNullException)
            {
                cpuTime = 0;
            }
            // not all events have a duration
            try
            {
                duration = traceEvent.Duration;
            }
            catch (ArgumentNullException)
            {
                duration = 0;
            }
            try
            {
                eventTime = traceEvent.CurrentTime;
                eventTime = traceEvent.StartTime;
            }
            catch (NullReferenceException) { }
            catch (ArgumentNullException)
            {
                //do nothing - leave whatever value worked DateTime.Now / CurrentTime / StartTime
            }
                    

            var dsEvent = new DaxStudioTraceEventArgs(  
                traceEvent.EventClass.ToString(),
                traceEvent.EventSubclass.ToString(),
                duration, 
                cpuTime, 
                traceEvent.TextData,
                xlsxFile,
                eventTime); 
            return dsEvent;
        }

        public void Update(string databaseName)
        {
            // Note: Excel Query Trace does not use the database name parameter
            Update();
        }

        public void Update()
        {
            Stop(false);
            Start();
        }

        public void Dispose()
        {
            if (_trace == null) return;
			_trace.OnEvent -= OnTraceEventInternal;
            _trace.Dispose();
			_trace = null;
        }
    }
}
