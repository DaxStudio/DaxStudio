extern alias ExcelAmo;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using xlAmo = ExcelAmo.Microsoft.AnalysisServices;
using System.Xml;
using System.Timers;
using Caliburn.Micro;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio.QueryTrace
{
    public class QueryTraceEngineExcel: IQueryTrace
    {
#region public IQueryTrace interface
        public Task StartAsync()
        {
            return Task.Factory.StartNew(() => Start());
        }

        public void Stop()
        {
            Status = QueryTraceStatus.Stopping;
            if (_trace != null)
            {
                _trace.OnEvent -= OnTraceEventInternal;
                try
                {
                    _trace.Drop();
                    _trace = null;
                    Status = QueryTraceStatus.Stopped;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine(e);
                }
            }
            _traceStarted = false;
        }


        public QueryTraceStatus Status
        {
	        get { return _status;  }
            private set {_status = value;}
        }

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
        private ADOTabular.ADOTabularConnection _connection;
        private AdomdType _connectionType;
        private string _sessionId;
        private List<DaxStudioTraceEventClass> _eventsToCapture;
        private Timer _startingTimer;
        private List<DaxStudioTraceEventArgs> _capturedEvents = new List<DaxStudioTraceEventArgs>();

        //public delegate void TraceStartedHandler(object sender);//, TraceStartedEventArgs eventArgs);

        
        public QueryTraceEngineExcel(string connectionString, AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> events)
        {
            Status = QueryTraceStatus.Stopped;
            ConfigureTrace(connectionString, connectionType, sessionId, events);
        }

        public void ConfigureTrace(string connectionString, AdomdType connectionType, string sessionId, List<DaxStudioTraceEventClass> events)
        {
            _connectionString = string.Format("{0};SessionId={1}",connectionString,sessionId);
            _connectionString = _connectionString.Replace("MDX Compatibility=1;", ""); // remove MDX Compatibility setting
            _connectionType = connectionType;
            _sessionId = sessionId;
            _eventsToCapture = events;
        }
        private void SetupTrace(xlAmo.Trace trace, List<DaxStudioTraceEventClass> events)
        {
            // Add CommandBegin so we can catch the heartbeat events
            if (trace.Events.Find(xlAmo.TraceEventClass.CommandBegin) == null)
                trace.Events.Add(TraceEventFactory.CreateExcelTrace(xlAmo.TraceEventClass.CommandBegin));
            // Add QueryEnd so we know when to stop the trace
            if (trace.Events.Find(xlAmo.TraceEventClass.QueryEnd)==null)
                trace.Events.Add(TraceEventFactory.CreateExcelTrace(xlAmo.TraceEventClass.QueryEnd));

            //reset the watcher so it can clear any cached events 
            ///watcher.Reset();
            
            // catch the events in the ITraceWatcher
            foreach (DaxStudioTraceEventClass eventClass in events)
            {
                var amoEventClass = (ExcelAmo.Microsoft.AnalysisServices.TraceEventClass)eventClass;
                if (trace.Events.Find(amoEventClass) != null)
                    continue;

                xlAmo.TraceEvent trcEvent = TraceEventFactory.CreateExcelTrace(amoEventClass);
                trace.Events.Add(trcEvent);
            }
            trace.Update();   
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
            var filterXml = string.Format(
                "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)xlAmo.TraceColumn.SessionID
                , sessionId);
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        //public void Clear()
        //{
        //    _trace.Events.Clear();
        //}


        public void Start()
        {
            if (_trace != null)
                if (_trace.IsStarted)
                    throw new InvalidOperationException("Cannot start a new trace as one is already running");

            if (Status != QueryTraceStatus.Started)
                Status = QueryTraceStatus.Starting;
            _connection = new ADOTabular.ADOTabularConnection(_connectionString, _connectionType);
            _connection.Open();
            _trace = GetTrace();
            SetupTrace(_trace, _eventsToCapture);
            _trace.Start();
            
            // create timer to "ping" the server with DISCOVER_SESSION requests
            // until the trace events start to fire.
            if (_startingTimer == null)
                _startingTimer = new Timer();
            _startingTimer.Interval = 300;  //TODO - make time interval shorter?
            _startingTimer.Elapsed += OnTimerElapsed;
            _startingTimer.Enabled = true;
            _startingTimer.Start();
            utcPingStart = DateTime.UtcNow;
            // Wait for Trace to become active
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            
            Execute.OnUIThread(()=> _connection.Ping());
            // if past timeout then exit and display error
            if ( ( utcPingStart - DateTime.UtcNow ).Seconds > 30)
            {
                _startingTimer.Stop();
                _trace.Drop();
                RaiseError("Timeout exceeded attempting to start Trace");
            }
        }

        private xlAmo.Trace GetTrace()
        {
              if (_trace == null)
              {

                  _server = new xlAmo.Server();
                  _server.Connect(_connectionString);
                  _trace = _server.Traces.Add( string.Format("DaxStudio_Trace_SPID_{0}", _sessionId));
                  _trace.Filter = GetSessionIdFilter(_sessionId);
                  _trace.OnEvent += OnTraceEventInternal;
              }
              return _trace;
        }

        public void OnTraceEvent( DaxStudioTraceEventArgs e)
        {
            if (TraceEvent != null)
                TraceEvent(this, e);
        }

        public void RaiseError( string message)
        {
            if (TraceError != null) TraceError(this, message);
        }

        private bool _traceStarted;
        private ADOTabular.ADOTabularConnection connection;
        private List<DaxStudioTraceEventClass> eventsToCapture;

        private void OnTraceEventInternal(object sender, xlAmo.TraceEventArgs e)
        {
            // we are using CommandBegin as a "heartbeat" to check if the trace
            // has started capturing events
            if (!_traceStarted)
            {
                StopTimer();
                _traceStarted = true;
                Status = QueryTraceStatus.Started;
                if (TraceStarted != null)
                    TraceStarted(this,  null);
            }
            else
            {
                //OnTraceEvent(e);
                _capturedEvents.Add( CreateTraceEventArg(e));
                if (e.EventClass == xlAmo.TraceEventClass.QueryEnd)
                {
                    // Raise an event with the captured events
                    if (TraceCompleted != null)
                        TraceCompleted(this, _capturedEvents);
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
        private DaxStudioTraceEventArgs CreateTraceEventArg(xlAmo.TraceEventArgs traceEvent)
        {
            long cpuTime;
            long duration;

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

            var dsEvent = new DaxStudioTraceEventArgs(  
                traceEvent.EventClass.ToString(),
                traceEvent.EventSubclass.ToString(),
                duration, 
                cpuTime, 
                traceEvent.TextData);   
            return dsEvent;
        }

    }
}
