extern alias ExcelAmo;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xlAmo = ExcelAmo.Microsoft.AnalysisServices;
using System.Xml;
using System.Timers;
using DaxStudio.QueryTrace.Interfaces;
using Serilog;
using Caliburn.Micro;
using System.Globalization;
using System.Diagnostics.Contracts;
using ADOTabular.Enums;
using System.Linq;
using System.IO;

namespace DaxStudio.QueryTrace
{
    public class QueryTraceEngineExcel : IQueryTrace, IDisposable
    {
#region public IQueryTrace interface
        public async Task StartAsync(int startTimeoutSec)
        {
            Log.Debug("{class} {method} {message}", "QueryTraceEngineExcel", "StartAsync", "entered");
            TraceStartTimeoutSecs = startTimeoutSec;
            await Task.Run(() => Start()).ConfigureAwait(false);
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
                        Dispose();
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
        public QueryTraceStatus Status { get; private set; }

        public List<DaxStudioTraceEventClass> Events { get; }
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
        public delegate void DaxStudioTraceEventHandler(object sender, DaxStudioTraceEventArgs e);
#pragma warning restore CA1711 // Identifiers should not have incorrect suffix

        public event DaxStudioTraceEventHandler TraceEvent;
        public event EventHandler<IList<DaxStudioTraceEventArgs>> TraceCompleted;
        public event EventHandler TraceStarted;
        public event EventHandler<string> TraceError;
        public event EventHandler<string> TraceWarning;
        #endregion

        #region Internal implementation
        private xlAmo.Server _server;
        private xlAmo.Trace _trace;
        private DateTime _utcPingStart;
        private string _connectionString;
        private readonly string _originalConnectionString;

        private ADOTabular.ADOTabularConnection _connection;

        private AdomdType _connectionType;
        private readonly string _sessionId;
        
        private Timer _startingTimer;
        private List<DaxStudioTraceEventArgs> _capturedEvents = new List<DaxStudioTraceEventArgs>();
        private string _friendlyServerName = "";

        
        public QueryTraceEngineExcel(string connectionString, AdomdType connectionType, string sessionId, string applicationName, List<DaxStudioTraceEventClass> events, bool filterForCurrentSession)
        {
            Contract.Requires(connectionString != null, "connectionString must not be null");

            Status = QueryTraceStatus.Stopped;
            _originalConnectionString = connectionString;
            _sessionId = sessionId;
            FilterForCurrentSession = filterForCurrentSession;
            ConfigureTrace(connectionString, connectionType, applicationName);
            Events = events;
        }

        public bool FilterForCurrentSession { get; }

        private void ConfigureTrace(string connectionString, AdomdType connectionType, string applicationName) //, List<DaxStudioTraceEventClass> events)
        {
            //_connectionString = string.Format("{0};SessionId={1}",connectionString,sessionId);
            _connectionString = connectionString;
            _connectionString = _connectionString.Replace("MDX Compatibility=1;", ""); // remove MDX Compatibility setting
            _connectionString = _connectionString.Replace("Cell Error Mode=TextValue;", ""); // remove MDX Compatibility setting
            _connectionType = connectionType;
            _applicationName = applicationName;
        }
        private static void SetupTrace(xlAmo.Trace trace, List<DaxStudioTraceEventClass> events)
        {
            Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "SetupTrace", "enter");
            trace.Events.Clear();
            // Add CommandBegin & DiscoverBegin so we can catch the heartbeat events
            trace.Events.Add(TraceEventFactoryExcel.CreateTrace(xlAmo.TraceEventClass.DiscoverBegin)); 
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

#pragma warning disable IDE0051 // Remove unused private members
        private static  XmlNode GetSpidFilter(int spid)
#pragma warning restore IDE0051 // Remove unused private members
        {
            var filterXml = string.Format(CultureInfo.InvariantCulture
                , "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)xlAmo.TraceColumn.Spid
                , spid );
            var doc = new XmlDocument()
            { 
                XmlResolver = null
            };

            var stringReader = new StringReader(filterXml);
            using (var reader = XmlReader.Create(stringReader, new XmlReaderSettings() { XmlResolver = null }))
            {
                doc.Load(reader);
                return doc;
            }
            
        }

        private static XmlNode GetSessionIdFilter(string sessionId)
        {
            string filterTemplate = "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\">" +
                            "<ColumnID>{0}</ColumnID><Value>{1}</Value>" +
                            "</Equal>";
            var filterXml = string.Format(CultureInfo.InvariantCulture
                , filterTemplate
                , (int)xlAmo.TraceColumn.SessionID
                , sessionId);
            var doc = new XmlDocument() { XmlResolver = null };

            var stringReader = new StringReader(filterXml);
            using (XmlReader reader = XmlReader.Create(stringReader, new XmlReaderSettings() { XmlResolver = null }))
            {
                doc.Load(reader);
                return doc;
            }
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
                        return;  // exit here if trace is already started trace is already running exit here
                    }

	            if (Status != QueryTraceStatus.Started)  Status = QueryTraceStatus.Starting;
				Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "Connecting to: " + _connectionString);
                //HACK - wait 1.5 seconds to allow trace to start 
                //       using the ping method throws a connection was lost error when starting a second trace
                _connection = new ADOTabular.ADOTabularConnection($"{_originalConnectionString};SessionId={_sessionId}", _connectionType);
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
	            _utcPingStart = DateTime.UtcNow;
                // Wait for Trace to become active
                Log.Debug("{class} {method} {event}", "QueryTraceEngine", "Start", "exit");
            }
			catch (Exception ex)
			{
                OutputError($"Error starting trace: {ex.Message}" );
				Log.Error("{class} {method} {message} {stacktrace}","QueryTraceEngine" , "Start", ex.Message, ex.StackTrace);
			}
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "OnTimerElapsed", "Ping");
            //HACK - wait 1.5 seconds to allow trace to start 
            //       using the ping method thows a connection was lost error when starting a second trace
            Execute.OnUIThread(() => _connection.PingTrace());
            //Execute.OnUIThread(() => ServerPing());
            // if past timeout then exit and display error
            if ((_utcPingStart - DateTime.UtcNow).Seconds > TraceStartTimeoutSecs)
            {
                _startingTimer.Stop();
                _trace.Drop();
                OutputError("Timeout exceeded attempting to start Trace. You could try increasing this timeout in the Options");
                Log.Warning("{class} {method} {event}", "QueryTraceEngineExcel", "OnTimerElapsed", "Timeout exceeded attempting to start Trace");
            }

        }

        private xlAmo.Trace GetTrace()
        {

            if (_trace == null)
            {
                Log.Debug("{class} {method} {event}", "QueryTraceEngineExcel", "GetTrace", "about to create new trace");
                _server = new xlAmo.Server();
                _server.Connect(_connectionString);
                _trace = _server.Traces.Add( $"DaxStudio_Trace_SPID_{_sessionId}");
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

        public void OutputError( string message)
        {
            TraceError?.Invoke(this, message);
        }

        public void OutputWarning(string message)
        {
            TraceWarning?.Invoke(this, message);
        }

        private bool _traceStarted;
#pragma warning disable IDE0052 // Remove unread private members
        private string _applicationName;
#pragma warning restore IDE0052 // Remove unread private members
        
        private void OnTraceEventInternal(object sender, xlAmo.TraceEventArgs e)
        {
            // we are using CommandBegin as a "heartbeat" to check if the trace
            // has started capturing events
            if (!_traceStarted)
            {
                Log.Debug("{class} {method} Pending TraceEvent: {eventClass}","QueryTraceEngineExcel","OnTraceEventInternal", e.EventClass.ToString());
                StopTimer();
                _traceStarted = true;
                _connection.Close(false);
                _connection.Dispose();
                _connection = null;
                Status = QueryTraceStatus.Started;
                TraceStarted?.Invoke(this, null);

                _friendlyServerName = GetShortFileName(_trace.Parent.Name);
            }
            else
            {
                // exit early if this is a DiscoverBegin event (used for the trace heartbeat)
                if (e.EventClass == xlAmo.TraceEventClass.DiscoverBegin ) return;

                Log.Debug("{class} {method} TraceEvent: {eventClass}", "QueryTraceEngineExcel", "OnTraceEventInternal", e.EventClass.ToString());
                //OnTraceEvent(e);
                _capturedEvents.Add( CreateTraceEventArg(e, _friendlyServerName));
                if (e.EventClass == xlAmo.TraceEventClass.QueryEnd || e.EventClass == xlAmo.TraceEventClass.Error)
                {
                    // Raise an event with the captured events
                    TraceCompleted?.Invoke(this, _capturedEvents);
                    // reset the captured events collection
                    _capturedEvents = new List<DaxStudioTraceEventArgs>();
                }
            }
        }

        private static string GetShortFileName(string filename)
        {
          
            if (filename.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || filename.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return filename.Split('/').Last();
            }
            else
            {
                var fi = new FileInfo(filename);
                return fi.Name;
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
        private static DaxStudioTraceEventArgs CreateTraceEventArg(xlAmo.TraceEventArgs traceEvent, string xlsxFile)
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

        public void Update(string databaseName, string sessionId)
        {
            // Note: Excel Query Trace does not use the database name or sessions parameters
            Update();
        }

        public void Update()
        {
            Stop(false);
            Start();
        }


        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                    if (_trace != null)
                    {
                        _trace.OnEvent -= OnTraceEventInternal;
                        _trace?.Drop();
                        _trace?.Dispose();
                        _trace = null;
                    }
                    
                    _connection?.Dispose();
                    _connection = null;
                    
                    _server?.Disconnect();
                    _server?.Dispose();
                    _server = null;
                    
                    if (_startingTimer != null)
                    {
                        _startingTimer.Stop();
                        _startingTimer.Elapsed -= OnTimerElapsed;
                        _startingTimer.Dispose();
                        _startingTimer = null;
                    }
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
