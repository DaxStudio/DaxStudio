using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Xml;
using ADOTabular;
using Caliburn.Micro;
using Microsoft.AnalysisServices;

namespace DaxStudio.UI.Model
{

    public class TraceStartedEventArgs:EventArgs 
    {
        public TraceStartedEventArgs(IResultsTarget target)
        {
            ResultsTarget = target;
        }
        public IResultsTarget ResultsTarget { get; private set; }

    }

    

    public class QueryTrace
    {
        public delegate void TraceStartedHandler(object sender, TraceStartedEventArgs eventArgs);

        public event TraceStartedHandler TraceStarted;
    
        private readonly Server _server;
        private Trace _trace;
        //private readonly Dictionary<TraceEventClass, TraceEvent> _traceEvents;

        public QueryTrace(ADOTabularConnection connection)
        {
            _server = new Server();
            _server.Connect(connection.ConnectionString);
            _connection = connection;
            
            //    _traceEvents = new Dictionary<TraceEventClass, TraceEvent>();
        }

        public void UnRegisterTraceWatcher(ITraceWatcher watcher)
        {
            RegisteredTraceWatchers.Remove(watcher);
        }

        public void RegisterTraceWatcher(ITraceWatcher watcher)
        {
            if (!RegisteredTraceWatchers.Contains(watcher))
                RegisteredTraceWatchers.Add(watcher);
        }

        private void SetupTrace(Trace trace)
        {
            
            // Add CommandBegin so we can catch the heartbeat events
            if (trace.Events.Find(TraceEventClass.CommandBegin) == null)
                trace.Events.Add(TraceEventFactory.Create(TraceEventClass.CommandBegin));
            // Add QueryEnd so we know when to stop the trace
            if (trace.Events.Find(TraceEventClass.QueryEnd)==null)
                trace.Events.Add(TraceEventFactory.Create(TraceEventClass.QueryEnd));

            foreach (var watcher in EnabledTraceWatchers)
            {
                //reset the watcher so it can clear any cached events 
                watcher.Reset();
                // catch the events in the ITraceWatcher
                foreach (TraceEventClass eventClass in watcher.MonitoredEvents)
                {
                    if (trace.Events.Find(eventClass) != null)
                        continue;

                    var trcEvent = TraceEventFactory.Create(eventClass);
                    trace.Events.Add(trcEvent);
                }
            }
            trace.Update();
            
        }

        private List<ITraceWatcher> _registeredWatchers;
        public List<ITraceWatcher> RegisteredTraceWatchers { get { return _registeredWatchers ?? (_registeredWatchers = new List<ITraceWatcher>()); } }   

        public IList<ITraceWatcher> EnabledTraceWatchers
        {
            get {
                return new List<ITraceWatcher>(RegisteredTraceWatchers.Where(rw => rw.IsEnabled));
            }
        }

        private XmlNode GetSpidFilter()
        {
            var filterXml = string.Format(
                "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)TraceColumn.Spid
                , _connection.SPID );
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        public void Clear()
        {
            _trace.Events.Clear();
        }

        private Timer _startingTimer;
        private IResultsTarget _resultsTarget;
        public void Start(IResultsTarget resultsTarget)
        {
            if (_trace != null)
                if (_trace.IsStarted)
                    throw new InvalidOperationException("Cannot start a new trace as one is already running");

            _trace = GetTrace();
            SetupTrace(_trace);
            _resultsTarget = resultsTarget;
            _trace.Start();
            
            // create timer to "ping" the server with DISCOVER_SESSION requests
            // until the trace events start to fire.
            if (_startingTimer == null)
                _startingTimer = new Timer();
            _startingTimer.Interval = 300;  //TODO - make time interval shorter
            _startingTimer.Elapsed += OnTimerElapsed;
            _startingTimer.Enabled = true;
            _startingTimer.Start();
            
            // Wait for Trace to become active
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
             Execute.OnUIThread(()=> _connection.Ping());
        }

        private Trace GetTrace()
        {
          
              if (_trace == null)
              {
                  _trace = _server.Traces.Add( string.Format("DaxStudio_Trace_SPID_{0}", _connection.SPID));
                  //TODO - filter on session id
                  // _trace.Filter = GetSpidFilter();
                  _trace.OnEvent += OnTraceEventInternal;
              }
              return _trace;
          
        }

        public event TraceEventHandler TraceEvent;
        public event EventHandler TraceCompleted;

        public void OnTraceEvent( TraceEventArgs e)
        {
            if (TraceEvent != null)
                TraceEvent(this, e);
            //TraceEvent.Raise(this, e);           
        }

        private bool _traceStarted;
        private readonly ADOTabularConnection _connection;

        private void OnTraceEventInternal(object sender, TraceEventArgs e)
        {
            // we are using CommandBegin as a "heartbeat" to check if the trace
            // has started capturing events

            if (!_traceStarted)
            {
                StopTimer();
                _traceStarted = true;
                if (TraceStarted != null)
                    TraceStarted(this, new TraceStartedEventArgs(_resultsTarget));
                //TraceStarted.Raise(this, new TraceStartedEventArgs(_resultsTarget)); 
            }
            else
            {
                OnTraceEvent(e);
                if (e.EventClass == TraceEventClass.QueryEnd)
                {
                    //Stop();
                    if (TraceCompleted != null)
                        TraceCompleted(this, null);
                    //TraceCompleted.Raise(this, null);
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

        public void Stop()
        {
            //Execute.OnUIThread(() =>
            //    {
                    _trace.OnEvent -= OnTraceEventInternal;
                    try
                    {
                    //    _trace.Stop();
                        _trace.Drop();
                        _trace = null;
                    }
                    catch (Exception e)
                    {
                        System.Diagnostics.Debug.WriteLine(e);
                    }
             //   });
            _traceStarted = false;
        }
    }
    
}
