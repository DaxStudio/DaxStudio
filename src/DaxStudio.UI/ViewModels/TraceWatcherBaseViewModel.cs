using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Microsoft.AnalysisServices;
using Serilog;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using System.Timers;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    [InheritedExport(typeof(ITraceWatcher)), PartCreationPolicy(CreationPolicy.NonShared)]
    public abstract class TraceWatcherBaseViewModel 
        : PropertyChangedBase
        , IToolWindow
        , ITraceWatcher
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<QueryStartedEvent>
        , IHandle<CancelQueryEvent>
        //, IHandle<QueryTraceCompletedEvent>
    {
        private List<DaxStudioTraceEventArgs> _events;
        protected readonly IEventAggregator _eventAggregator;
        private IQueryHistoryEvent _queryHistoryEvent;
        private IGlobalOptions _globalOptions;
        

        [ImportingConstructor]
        protected TraceWatcherBaseViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions)
        {
            _eventAggregator = eventAggregator;
            
            _globalOptions = globalOptions;
            WaitForEvent = TraceEventClass.QueryEnd;
            Init();
            
            //_eventAggregator.Subscribe(this); 
        }

        private void Init()
        {
            MonitoredEvents = GetMonitoredEvents();
        }

        public List<DaxStudioTraceEventClass> MonitoredEvents { get; private set; }
        public TraceEventClass WaitForEvent { get; set; }

        // this is a list of the events captured by this trace watcher
        public List<DaxStudioTraceEventArgs> Events
        {
            get { return _events ?? (_events = new List<DaxStudioTraceEventArgs>()); }
        }

        protected abstract List<DaxStudioTraceEventClass> GetMonitoredEvents();

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected abstract void ProcessResults();

        public void ProcessAllEvents(IList<DaxStudioTraceEventArgs> capturedEvents)
        {
            if (_timeout != null)
            {
                _timeout.Stop();
                _timeout.Elapsed -= QueryEndEventTimeout;
                _timeout.Dispose();
                _timeout = null;
            }

            foreach (var e in capturedEvents)
            {
                if (MonitoredEvents.Contains(e.EventClass))
                {
                    Events.Add(e);
                }
            }
            Log.Verbose("{class} {method} {message}", "TraceWatcherBaseViewModel", "ProcessAllEvents", "starting ProcessResults");
            ProcessResults();
            IsBusy = false;
        }

        // This method is called before a trace starts which gives you a chance to 
        // reset any stored state
        public void Reset()
        {
            Events.Clear();
            OnReset();
        }

        public abstract void OnReset();
       
        // IToolWindow interface
        public abstract string Title { get; set; }

        public abstract string ToolTipText { get; set; }

        public virtual string DefaultDockingPane
        {
            get { return "DockBottom"; }
            set { }
        }

        public bool CanClose
        {
            get { return false; }
            set { }
        }
        public bool CanHide
        {
            get { return false; }
            set { }
        }
        public int AutoHideMinHeight { get; set; }
        public bool IsSelected { get; set; }

        private bool _isEnabled ;
        public bool IsEnabled { get { return _isEnabled; }
            set { _isEnabled = value;
            NotifyOfPropertyChange(()=> IsEnabled);} 
        }

        public bool IsActive { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    NotifyOfPropertyChange(() => IsChecked);

                    if (!_isChecked) Reset();
                    if (value)
                    {
                        _eventAggregator.Subscribe(this);
                    }
                    else
                    {
                        _eventAggregator.Unsubscribe(this);
                    }
                    
                    _eventAggregator.PublishOnUIThread(new TraceWatcherToggleEvent(this, value));
                    Log.Verbose("{Class} {Event} IsChecked:{IsChecked}", "TraceWatcherBaseViewModel", "IsChecked", value);
                }
            }
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            CheckEnabled(message.Connection, message.ActiveTrace);
        }

        public void CheckEnabled(IConnection connection, ITraceWatcher active)
        {
            if (connection == null) {
                IsEnabled = false;
                IsChecked = false;
                return; 
            }
            if (!connection.IsConnected)
            {
                // if connection has been closed or broken then uncheck and disable
                IsEnabled = false;
                IsChecked = false;
                return;
            }
            IsAdminConnection = connection.IsAdminConnection;
            //IsEnabled = (!_connection.IsPowerPivot && _connection.IsAdminConnection && _connection.IsConnected);
            if (active != null)
                IsEnabled = (connection.IsAdminConnection && connection.IsConnected && FilterForCurrentSession == active.FilterForCurrentSession);
            else
                IsEnabled = (connection.IsAdminConnection && connection.IsConnected);
        }

        private bool _isBusy = false;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value;
            BusyMessage = "Query Running...";
            NotifyOfPropertyChange(() => IsBusy);
            }
        }

        private string _busyMessage;
        public string BusyMessage { get { return _busyMessage; }
            set { _busyMessage = value;
            NotifyOfPropertyChange(() => BusyMessage);
            }
        }

        public void Handle(QueryStartedEvent message)
        {
            Log.Verbose("{class} {method} {message}", "TraceWatcherBaseViewModel", "Handle<QueryStartedEvent>", "Query Started");
            IsBusy = true;
            Reset();
        }

        public void Handle(CancelQueryEvent message)
        {
            Log.Verbose("{class} {method} {message}", "TraceWatcherBaseViewModel", "Handle<QueryCancelEvent>", "Query Cancelled");
            IsBusy = false;
            Reset();
        }

        Timer _timeout;

        public IQueryHistoryEvent QueryHistoryEvent { get { return _queryHistoryEvent; } }

        public abstract bool FilterForCurrentSession { get; }
        public bool IsAdminConnection { get; private set; }
        public string DisableReason { get {
                if (!IsAdminConnection) return "You must have Admin rights on the server to enable traces";
                return "You cannot have both session traces and all queries traces enabled at the same time";
            }
        }

        public void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent)
        {
            Log.Verbose("{class} {method} {message}", "TraceWatcherBaseViewModel", "QueryCompleted", isCancelled);
            _queryHistoryEvent = queryHistoryEvent;
            if (isCancelled) return;
            if (queryHistoryEvent.QueryText.Length == 0) return; // query text should only be empty for clear cache queries

            // start timer, if timer elapses then print warning and set IsBusy = false
            _timeout = new Timer(_globalOptions.QueryEndEventTimeout.SecondsToMilliseconds());
            _timeout.AutoReset = false;
            _timeout.Elapsed += QueryEndEventTimeout;
            _timeout.Start();
            BusyMessage = "Waiting for Query End event...";
        }

        private void QueryEndEventTimeout(object sender, ElapsedEventArgs e)
        {
            Reset();
            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Trace Stopped: QueryEnd event not received - Tracing timeout exceeded"));
        }

    }
}
