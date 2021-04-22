using System.Collections.Generic;
using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Microsoft.AnalysisServices;
using Serilog;
using DaxStudio.Common.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using System.Timers;
using DaxStudio.UI.Utils;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media;
using AsyncAwaitBestPractices;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Model;
using Exception = System.Exception;

namespace DaxStudio.UI.ViewModels
{
    [InheritedExport(typeof(ITraceWatcher)), PartCreationPolicy(CreationPolicy.NonShared)]
    public abstract class TraceWatcherBaseViewModel 
        : Screen
        , IToolWindow
        , ITraceWatcher
        , IZoomable
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<QueryStartedEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<TraceChangedEvent>
        , IHandle<ReconnectEvent>
        , IHandle<SelectedModelChangedEvent>
        //, IHandle<QueryTraceCompletedEvent>
    {
        private List<DaxStudioTraceEventArgs> _events;
        protected readonly IEventAggregator _eventAggregator;
        private IQueryHistoryEvent _queryHistoryEvent;
        private readonly IGlobalOptions _globalOptions;
        private IQueryTrace _tracer;

        [ImportingConstructor]
        protected TraceWatcherBaseViewModel( IEventAggregator eventAggregator, IGlobalOptions globalOptions)
        {
            _eventAggregator = eventAggregator;
            _globalOptions = globalOptions;
            WaitForEvent = TraceEventClass.QueryEnd;
            HideCommand = new DelegateCommand(HideTrace, CanHideTrace);
            Init();

            //_eventAggregator.Subscribe(this); 
        }

        public IDaxDocument Document { get; set; }

        private bool CanHideTrace(object obj)
        {
            return CanHide;
        }

        public  void HideTrace(object obj)
        {
            _eventAggregator.PublishOnUIThread(new CloseTraceWindowEvent(this));
        }
        

        private void Init()
        {
            MonitoredEvents = GetMonitoredEvents();
            
        }

        public DelegateCommand HideCommand { get; set; }
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

        protected virtual void ProcessSingleEvent(DaxStudioTraceEventArgs singleEvent)
        {
            // by default do nothing with individual events
        }
        
        public void ProcessAllEvents()
        {
            if (_timeout != null)
            {
                _timeout.Stop();
                _timeout.Elapsed -= QueryEndEventTimeout;
                _timeout.Dispose();
                _timeout = null;
            }

            //foreach (var e in capturedEvents)
            //{
            //    if (MonitoredEvents.Contains(e.EventClass))
            //    {
            //        Events.Add(e);
            //    }
            //}
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "ProcessAllEvents", "starting ProcessResults");
            if (!IsPaused) ProcessResults();
            IsBusy = false;
        }

        // This method is called before a trace starts which gives you a chance to 
        // reset any stored state
        public void Reset()
        {
            IsPaused = false;
            Events.Clear();
            OnReset();
        }

        public abstract void OnReset();
       
        // IToolWindow interface
        public abstract string Title { get; }
        
        public abstract string TraceSuffix { get; }

        public virtual string TraceStatusText {
            get {
                if (IsEnabled && !IsChecked) return $"Trace is not currently active, click on the {Title} button in the ribbon to resume tracing";
                if (!IsEnabled) return DisableReason;
                //TODO - should we show this for paused states too??
                //if (IsPaused) return $"Trace is paused, click on the start button in the toolbar below to re-start tracing";
                return string.Empty; } }

        public abstract string ToolTipText { get; }

        public virtual string DefaultDockingPane
        {
            get => "DockBottom";
            set { }
        }

        public virtual bool CanCloseWindow
        {
            get => true;
            set { }
        }
        public virtual bool CanHide
        {
            get => true;
            set { }
        }
        public int AutoHideMinHeight { get; set; }
        public bool IsSelected { get; set; }
        
        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                //OnVisibilityChanged(EventArgs.Empty);
                NotifyOfPropertyChange(nameof(IsVisible));
            }
        }
        
        public abstract string ContentId { get; }
        public abstract ImageSource IconSource { get; }

        private bool _isEnabled ;
        public bool IsEnabled { 
            get => _isEnabled; 
            set { _isEnabled = value;
            NotifyOfPropertyChange(()=> IsEnabled);} 
        }

        //public bool IsActive { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    if (!_isChecked) IsPaused = false; // make sure pause is set to false if we are not checked
                    NotifyOfPropertyChange(() => CanPause);
                    NotifyOfPropertyChange(() => CanStart);
                    NotifyOfPropertyChange(() => CanStop);
                    NotifyOfPropertyChange(() => IsTraceRunning);
                    NotifyOfPropertyChange(() => IsChecked);
                    NotifyOfPropertyChange(() => TraceStatusText);
                    //if (!_isChecked) Reset();
                    if (value)
                    {
                        _eventAggregator.Subscribe(this);
                        StartTraceAsync().SafeFireAndForget(onException: ex =>
                        {
                            Log.Error(ex, Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(IsChecked),"error setting IsChecked");
                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error starting trace: {ex.Message}"));                            
                        });
                    }
                    else
                    {
                        
                        // TODO - need a synchronous way to stop traces when shutting down or closing documents
                        
                        _eventAggregator.Unsubscribe(this);
                        StopTrace();
                        //StopTraceAsync().SafeFireAndForget(onException: ex =>
                        //{
                        //    Log.Error(ex, Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(IsChecked), "error setting IsChecked");
                        //    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error stopping trace: {ex.Message}"));
                        //}); ;
                    }
                    
                    //_eventAggregator.PublishOnUIThread(new TraceWatcherToggleEvent(this, value));
                    //Log.Verbose("{Class} {Event} IsChecked:{IsChecked}", GetSubclassName(), "IsChecked", value);
                }
            }
        }

        private string GetSubclassName()
        {
            return this.GetType().Name;
        }

        public async Task StopTraceAsync()
        {
            await Task.Run(async () =>
            {
                await _eventAggregator.PublishOnUIThreadAsync(new TraceChangingEvent(QueryTraceStatus.Stopping));
                try
                {
                    StopTrace();
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceWatcherToggleEvent(this, false));
                    Log.Verbose(Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTraceAsync),
                        "Stopping Tracer");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(QueryTraceStatus.Stopped));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTraceAsync), "Error Stopping Trace");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(QueryTraceStatus.Error));
                }
            }).ConfigureAwait(false);
        }

        private async Task StartTraceAsync()
        {
            await Task.Run(async () =>
            {
                await _eventAggregator.PublishOnUIThreadAsync(new TraceChangingEvent(QueryTraceStatus.Starting));
                try
                {
                    CreateTracer();
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceWatcherToggleEvent(this, true));
                    Log.Verbose(Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(StartTraceAsync),
                        "Starting Tracer");
                    await _tracer.StartAsync(_globalOptions.TraceStartupTimeout);

                }
                catch (Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTraceAsync), "Error Stopping Trace");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(QueryTraceStatus.Error));
                }
            }).ConfigureAwait(false);
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
            
#if PREVIEW
            //HACK: Temporary hack to test Power BI XMLA Endpoint
            IsAdminConnection = true;
#else
            IsAdminConnection = connection.IsAdminConnection;
#endif
            //IsEnabled = (!_connection.IsPowerPivot && _connection.IsAdminConnection && _connection.IsConnected);
            if (active != null)
                IsEnabled = (connection.IsAdminConnection && connection.IsConnected && FilterForCurrentSession == active.FilterForCurrentSession);
            else
                IsEnabled = (connection.IsAdminConnection && connection.IsConnected);
        }

        private bool _isBusy;
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
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "Handle<QueryStartedEvent>", "Query Started");
            if (!IsPaused && IsChecked)
            {
                IsBusy = true;
                Reset();
            }
        }

        public void Handle(CancelQueryEvent message)
        {
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "Handle<QueryCancelEvent>", "Query Cancelled");
            if (!IsPaused && !IsChecked)
            {
                IsBusy = false;
                Reset();
            }
        }

        Timer _timeout;

        public IQueryHistoryEvent QueryHistoryEvent { get { return _queryHistoryEvent; } }


#region Title Bar Button methods and properties
        private bool _isPaused;
        public void Pause()
        {
            IsPaused = true;
        }

        public void Start()
        {
            IsChecked = true;
            IsPaused = false;
        }

        public bool CanStop { get { return IsChecked; } }
        public void Stop()
        {
            IsBusy = false;
            IsPaused = false;
            IsChecked = false;
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                _isPaused = value;
                NotifyOfPropertyChange(() => IsTraceRunning);
                NotifyOfPropertyChange(() => IsPaused);
                NotifyOfPropertyChange(() => CanPause);
                NotifyOfPropertyChange(() => CanStart);
            }
        }

        public bool IsTraceRunning { get { return IsChecked && !IsPaused; } }
        public bool CanPause { get { return IsEnabled && (IsChecked && !IsPaused); } }
        public bool CanStart { get { return IsEnabled && (IsPaused || !IsChecked); } }
        

        public abstract void ClearAll();
        public virtual bool IsCopyAllVisible { get { return false; } }
        public abstract void CopyAll();

        public virtual bool CanCopyResults => false;
        public abstract void CopyResults();
        public virtual bool IsCopyResultsVisible => false;
        
        public virtual bool CanExport { get { return true; }  }  // TODO - should this be conditional on whether we have data?

        public void Export() {
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "JSON file (*.json)|*.json";
            dialog.Title = "Export Trace Details";

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) ExportTraceDetails(dialog.FileName);
        }

        public abstract void ExportTraceDetails(string filePath);

        public virtual bool IsFilterVisible { get { return false; } }
        public virtual void ClearFilters() { }

        private bool _showFilters;

        public bool ShowFilters { get { return _showFilters; } set { if (value != _showFilters) { _showFilters = value;  NotifyOfPropertyChange(() => ShowFilters); } } }

#endregion

        public abstract bool FilterForCurrentSession { get; }
        public bool IsAdminConnection { get; private set; }
        public string DisableReason { get {
                if (!IsAdminConnection) return "You must have Admin rights on the server to enable traces";
                if (IsChecked && IsEnabled) return "Trace is already running";
                return "You cannot have both session traces and all queries traces enabled at the same time";
            }
        }

        public event EventHandler OnScaleChanged;
        private double _scale = 1;


        public double Scale
        {
            get => _scale;
            set
            {
                _scale = value;
                NotifyOfPropertyChange();
                OnScaleChanged?.Invoke(this, null);
            }
        }

        public void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent)
        {
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "QueryCompleted", isCancelled);
            _queryHistoryEvent = queryHistoryEvent;
            if (isCancelled) return;
            if (queryHistoryEvent.QueryText.Length == 0) return; // query text should only be empty for clear cache queries

            // Check if the Events collection does not already contain a QueryEnd event
            // if it doesn't we start the timeout timer
            if (!Events.Any(ev => ev.EventClass == DaxStudioTraceEventClass.QueryEnd))
            {
                // start timer, if timer elapses then print warning and set IsBusy = false
                _timeout = new Timer(_globalOptions.QueryEndEventTimeout.SecondsToMilliseconds());
                _timeout.AutoReset = false;
                _timeout.Elapsed += QueryEndEventTimeout;
                _timeout.Start();
                BusyMessage = "Waiting for Query End event...";
            }
        }

        public QueryTraceStatus TraceStatus => _tracer?.Status ?? QueryTraceStatus.Stopped;

        private void QueryEndEventTimeout(object sender, ElapsedEventArgs e)
        {
            // Check that the QueryEnd event is not in the collection of events, if not we only have
            // a partial set of events and they cannot be relied upon so we clear them
            if(!Events.Any(ev => ev.EventClass == DaxStudioTraceEventClass.QueryEnd)) {
                Reset();
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning,
                    "Trace Stopped: QueryEnd event not received - Server Timing End Event timeout exceeded. You could try increasing this timeout in the Options"));
            }
        }

        public IConnectionManager Connection => Document.Connection;

        public bool CapturingStarted { get; private set; }

        public void CreateTracer()
        {
            try
            {
                if (!Connection.IsConnected)
                {
                    var msg = "Cannot start trace, the current window is not connected";
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                    Log.Error(Common.Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(CreateTracer), msg);
                    return;
                }

                OnCreateTracer();
                
                var supportedEvents = Connection.SupportedTraceEventClasses;
                var validEventsForConnection = MonitoredEvents.Where(e => supportedEvents.Contains(e)).ToList();
                
                if (_tracer == null) // && _connection.Type != AdomdType.Excel)
                {
                    if (Connection.IsPowerPivot)
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create RemoteQueryTrace", Connection.ConnectionString, Connection.Type.ToString(), Document.Host.Proxy.Port);
                        _tracer = QueryTraceEngineFactory.CreateRemote(Connection, validEventsForConnection, Document.Host.Proxy.Port, _globalOptions, FilterForCurrentSession, TraceSuffix);
                    }
                    else
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create LocalQueryTrace", Connection.ConnectionString, Connection.Type.ToString());
                        _tracer = QueryTraceEngineFactory.CreateLocal(Connection, validEventsForConnection, _globalOptions, FilterForCurrentSession, TraceSuffix);
                    }
                    //_tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                    _tracer.TraceError += TracerOnTraceError;
                    _tracer.TraceWarning += TracerOnTraceWarning;
                    _tracer.TraceEvent += TracerOnTraceEvent;
                }
            }
            catch (AggregateException ex)
            {
                foreach (var innerEx in ex.InnerExceptions)
                {
                    Log.Error("{class} {method} {message} {stackTrace}", "DocumentViewModel", "CreateTrace", innerEx.Message, innerEx.StackTrace);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stackTrace}", "DocumentViewModel", "CreateTrace", ex.Message, ex.StackTrace);
            }
        }

        protected virtual void OnCreateTracer()
        {
            // allows for trace watchers to perform actions before a tracer gets created
        }

        private void TracerOnTraceEvent(object sender, DaxStudioTraceEventArgs e)
        {
            if (!MonitoredEvents.Contains(e.EventClass)) return;
            if (ShouldStartCapturing(e)) CapturingStarted = true;
            if (!CapturingStarted) return;

            ProcessSingleEvent(e);
            Events.Add(e);

            if (IsFinalEvent(e)) ProcessAllEvents();
        }

        protected abstract bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent);

        protected virtual bool ShouldStartCapturing(DaxStudioTraceEventArgs traceEvent)
        {
            return true;
        }
        
        private void TracerOnTraceCompleted(object sender, EventArgs e)
        {
            if (IsChecked && !IsPaused) ProcessAllEvents();
            _eventAggregator.PublishOnUIThread(new QueryTraceCompletedEvent());
        }

        private void TracerOnTraceStarted(object sender, EventArgs e)
        {
            Log.Debug("{Class} {Event} {@TraceStartedEventArgs}", GetSubclassName(), nameof(TracerOnTraceStarted), e);

            Execute.OnUIThread(() => {
                Document.OutputMessage("Query Trace Started");
                this.IsEnabled = true;
                _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Started));
            });
        }

        private void TracerOnTraceError(object sender, string e)
        {
            Document.OutputError(e);
            _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Error));
            StopTrace();
        }

        public void StopTrace()
        {
            if (_tracer == null) return;
            
            _tracer.TraceCompleted -= TracerOnTraceCompleted;
            _tracer.TraceError -= TracerOnTraceError;
            _tracer.TraceStarted -= TracerOnTraceStarted;
            _tracer.TraceWarning -= TracerOnTraceWarning;
            _tracer.TraceEvent -= TracerOnTraceEvent;
            _tracer?.Stop();
            _tracer?.Dispose();
            _tracer = null;
        }

        private void TracerOnTraceWarning(object sender, string e)
        {
            Document.OutputWarning(e);
        }


        public void Handle(TraceChangedEvent message)
        {
            //throw new NotImplementedException();
        }

        public void Handle(ReconnectEvent message)
        {
            // TODO - handle reconnect event
        }

        public void Handle(SelectedModelChangedEvent message)
        {
            // TODO - handle model changed event
        }
        
        
    }
}
