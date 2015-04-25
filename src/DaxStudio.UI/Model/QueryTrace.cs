using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using ADOTabular;
using Caliburn.Micro;
using Microsoft.AnalysisServices;
using DaxStudio.UI.ViewModels;
using DaxStudio.UI.Interfaces;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio.UI.Model
{
    /*
    public class TraceStartedEventArgs:EventArgs 
    {
        public TraceStartedEventArgs(IResultsTarget target)
        {
            ResultsTarget = target;
        }
        public IResultsTarget ResultsTarget { get; private set; }

    }
    */
    public class TraceStartedEventArgs : EventArgs
    {
        public TraceStartedEventArgs()
        {
           
        }
        //public IResultsTarget ResultsTarget { get; private set; }

    }
    /*
    public enum QueryTraceStatus
    {
        Stopped,
        Stopping,
        Started,
        Starting
    }
    */
    public class QueryTrace
    {
        public delegate void TraceStartedHandler(object sender, TraceStartedEventArgs eventArgs);

        public event TraceStartedHandler TraceStarted;
    
        private readonly Server _server;
        private Trace _trace;
        private WeakReference _currentDocumentReference;
        private DateTime utcPingStart;

        // todo - implement operations queue
        //private Queue<TraceOperation> operationQueue;

        //private readonly Dictionary<TraceEventClass, TraceEvent> _traceEvents;

        public QueryTrace(ADOTabularConnection connection, DocumentViewModel document)
        {
            _server = new Server();
            _server.Connect(connection.ConnectionString);
            _connection = connection;
            Status = QueryTraceStatus.Stopped;
            // new Dictionary<TraceEventClass, TraceEvent>();
            _currentDocumentReference = new WeakReference(document);
        }

        public DocumentViewModel CurrentDocument
        {
            get { return _currentDocumentReference.Target as DocumentViewModel; }
        }

        public QueryTraceStatus Status { get; set; }
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

            //foreach (var watcher in CheckedTraceWatchers)
            foreach (var watcher in AvailableTraceWatchers)
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

        public BindableCollection<ITraceWatcher> AvailableTraceWatchers
        {
            get { return CurrentDocument.TraceWatchers; }
        }

        private List<ITraceWatcher> _registeredWatchers;
        public List<ITraceWatcher> RegisteredTraceWatchers { get { return _registeredWatchers ?? (_registeredWatchers = new List<ITraceWatcher>()); } }   

        public IList<ITraceWatcher> CheckedTraceWatchers
        {
            get {
                return new List<ITraceWatcher>(RegisteredTraceWatchers.Where(rw => rw.IsChecked));
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

        private XmlNode GetSessionIdFilter()
        {
            var filterXml = string.Format(
                "<Equal xmlns=\"http://schemas.microsoft.com/analysisservices/2003/engine\"><ColumnID>{0}</ColumnID><Value>{1}</Value></Equal>"
                , (int)TraceColumn.SessionID
                , _connection.SessionId);
            var doc = new XmlDocument();
            doc.LoadXml(filterXml);
            return doc;
        }

        public void Clear()
        {
            _trace.Events.Clear();
        }

        //public Task StartAsync(IResultsTarget resultsTarget)
        //{
        //    return Task.Factory.StartNew(() => Start(resultsTarget));
        //}

        public Task  StartAsync()
        {
            return Task.Factory.StartNew(() => Start());
        }

        private Timer _startingTimer;
        //private IResultsTarget _resultsTarget;
        public void Start()
        {
            
            if (_trace != null)
                if (_trace.IsStarted)
                    throw new InvalidOperationException("Cannot start a new trace as one is already running");

            if (Status != QueryTraceStatus.Started)
                Status = QueryTraceStatus.Starting;

            _trace = GetTrace();
            SetupTrace(_trace);
            _trace.Start();
            
            // create timer to "ping" the server with DISCOVER_SESSION requests
            // until the trace events start to fire.
            if (_startingTimer == null)
                _startingTimer = new Timer();
            _startingTimer.Interval = 300;  //TODO - make time interval shorter
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
                CurrentDocument.OutputError("Timeout exceeded attempting to start Trace");
            }
        }

        private Trace GetTrace()
        {
          
              if (_trace == null)
              {
                  _trace = _server.Traces.Add( string.Format("DaxStudio_Trace_SPID_{0}", _connection.SPID));
                  //TODO - filter on session id
                  // _trace.Filter = GetSpidFilter();
                  _trace.Filter = GetSessionIdFilter();
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
                Status = QueryTraceStatus.Started;
                if (TraceStarted != null)
                    TraceStarted(this, new TraceStartedEventArgs());
            }
            else
            {
                OnTraceEvent(e);
                if (e.EventClass == TraceEventClass.QueryEnd)
                {
                    //Stop();
                    if (TraceCompleted != null)
                        TraceCompleted(this, null);
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
            Status = QueryTraceStatus.Stopping;
            
            if (_trace != null)
            {
                _trace.OnEvent -= OnTraceEventInternal;
                try
                {
                    //    _trace.Stop();
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
    }
    
}
