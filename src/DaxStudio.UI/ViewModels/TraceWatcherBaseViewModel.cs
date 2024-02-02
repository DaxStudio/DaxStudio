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
using System;
using System.Linq;
using System.Windows.Media;
using DaxStudio.UI.Extensions;
using System.Threading;
using System.Threading.Tasks;
using Timer = System.Timers.Timer;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.Common.Enums;
using DaxStudio.Common;
using AsyncAwaitBestPractices;
using System.Collections.Concurrent;
using System.Windows;
using Windows.UI.Core;
using Fluent;

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
        //, IHandle<QueryTraceCompletedEvent>
    {
        private ConcurrentQueue<DaxStudioTraceEventArgs> _events;
        protected readonly IEventAggregator _eventAggregator;
        private IQueryHistoryEvent _queryHistoryEvent;
        protected readonly IGlobalOptions _globalOptions;
        protected readonly IWindowManager _windowManager;
        private IQueryTrace _tracer;

        protected IGlobalOptions GlobalOptions { get => _globalOptions; }

        [ImportingConstructor]
        protected TraceWatcherBaseViewModel(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager)
        {
            _eventAggregator = eventAggregator;
            _globalOptions = globalOptions;
            _windowManager = windowManager;
            HideCommand = new DelegateCommand(HideTrace, CanHideTrace);
            //_eventAggregator.Subscribe(this); 
        }

        private bool CanHideTrace(object obj)
        {
            return CanHide;
        }

        public  void HideTrace(object obj)
        {
            _eventAggregator.PublishOnUIThreadAsync(new CloseTraceWindowEvent(this));
        }
        

        public DelegateCommand HideCommand { get; set; }

        // this is a list of the events captured by this trace watcher
        public ConcurrentQueue<DaxStudioTraceEventArgs> Events
        {
            get { return _events ?? (_events = new ConcurrentQueue<DaxStudioTraceEventArgs>()); }
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
            Log.Verbose("{class} {method} {message}", GetSubclassName(), nameof(ProcessAllEvents), "starting ProcessResults");
            if (!IsPaused) ProcessResults();
            IsBusy = false;
            _eventAggregator.PublishOnUIThreadAsync(new QueryTraceCompletedEvent(this));
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
                // TODO - remove this 
                //if (IsEnabled && !IsChecked) return $"Trace is not currently active, click on the {Title} button in the ribbon to resume tracing";
                //if (!IsEnabled) return DisableReason;
                //TODO - should we show this for paused states too??
                //if (IsPaused) return $"Trace is paused, click on the start button in the toolbar below to re-start tracing";
                return string.Empty; } }

        public abstract string ToolTipText { get; }

        public virtual string DefaultDockingPane
        {
            get { return "DockBottom"; }
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
        public abstract string ContentId { get; }
        public abstract int SortOrder { get; }
        private bool _isEnabled ;
        public bool IsEnabled { get { return _isEnabled; }
            set { _isEnabled = value;
            NotifyOfPropertyChange(()=> IsEnabled);} 
        }

        //public bool IsActive { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                // if we are paused just turn off the pause
                if (IsPaused)
                {
                    IsPaused = false;
                    return;
                }

                // otherwise change the state
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

                    // do not try to start the trace a second time if it is started or starting
                    if (value && _tracer != null && (_tracer.Status == QueryTraceStatus.Started || _tracer.Status == QueryTraceStatus.Starting)) return;

                    if (value)
                    {
                        _eventAggregator.SubscribeOnPublishedThread(this);
                        if (!ShouldStartTrace()) {
                            _isChecked = false;
                            NotifyOfPropertyChange();
                            return; 
                        }
                        StartTraceAsync().SafeFireAndForget(onException: ex =>
                        {
                            Log.Error(ex, Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(IsChecked), "error setting IsChecked");
                            _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error starting trace: {ex.Message}"));
                        });
                    }
                    else
                    {

                        // TODO - need a synchronous way to stop traces when shutting down or closing documents
                        IsBusy = false;
                        _eventAggregator.Unsubscribe(this);
                        StopTrace();
                        //StopTraceAsync().SafeFireAndForget(onException: ex =>
                        //{
                        //    Log.Error(ex, Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(IsChecked), "error setting IsChecked");
                        //    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error stopping trace: {ex.Message}"));
                        //}); ;
                    }

                    _eventAggregator.PublishOnUIThreadAsync(new TraceWatcherToggleEvent(this, value));
                    Log.Verbose("{Class} {Event} IsChecked:{IsChecked}", GetSubclassName(), nameof(IsChecked), value);
                }
            }
        }

        public bool IsRecording
        {
            get => IsChecked;
            set
            {
                IsChecked = value;
                NotifyOfPropertyChange(nameof(IsChecked));
                NotifyOfPropertyChange(nameof(IsPaused));
                NotifyOfPropertyChange(nameof(CanPause));
                NotifyOfPropertyChange(nameof(IsStopped));
                NotifyOfPropertyChange(nameof(CanStop));
                NotifyOfPropertyChange(nameof(IsRecording));
            }
        }

        public bool IsStopped
        {
            get => !IsChecked;
            set
            {
                if (IsPaused)
                {
                    _isPaused = false;
                    IsChecked = false;
                }
                else
                {
                    IsChecked = !value;
                }
                NotifyOfPropertyChange(nameof(IsChecked));
                NotifyOfPropertyChange(nameof(IsPaused));
                NotifyOfPropertyChange(nameof(IsStopped));
                NotifyOfPropertyChange(nameof(IsTraceRunning));
                NotifyOfPropertyChange(nameof(IsRecording));
            }
        }

        private string GetSubclassName()
        {
            return this.GetType().Name;
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
            //if (active != null)
            //    IsEnabled = (connection.IsAdminConnection && connection.IsConnected && FilterForCurrentSession == active.FilterForCurrentSession);
            //else
                IsEnabled = (connection.IsAdminConnection && connection.IsConnected);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get { return _isBusy; }
            set { _isBusy = value;
            NotifyOfPropertyChange(() => IsBusy);
            }
        }

        private string _busyMessage;
        public string BusyMessage { get { return _busyMessage; }
            set { 
                _busyMessage = value;
                NotifyOfPropertyChange(() => BusyMessage);
            }
        }

        private string _errorMessage;
        public virtual string ErrorMessage
        {
            get => _errorMessage;
            set { 
                _errorMessage = value;
                NotifyOfPropertyChange(() => ErrorMessage);
            }
        }

        public Task HandleAsync(QueryStartedEvent message, CancellationToken cancellation)
        {
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "Handle<QueryStartedEvent>", "Query Started");
            if (!IsPaused && IsChecked)
            {
                BusyMessage = "Query Running...";
                ErrorMessage = string.Empty;
                IsBusy = true;
                Reset();
            }
            return Task.CompletedTask;
        }

        #region EventAggregator Handlers
        public Task HandleAsync(DocumentConnectionUpdateEvent message, CancellationToken cancellation)
        {
            CheckEnabled(message.Connection, message.ActiveTrace);
            return Task.CompletedTask;
        }

        public Task HandleAsync(CancelQueryEvent message, CancellationToken cancellationToken)
        {
            Log.Verbose("{class} {method} {message}", GetSubclassName(), "Handle<QueryCancelEvent>", "Query Cancelled");
            if (!IsPaused && !IsChecked)
            {
                IsBusy = false;
                Reset();
            }
            return Task.CompletedTask;
        }

        #endregion

        Timer _timeout;

        public IQueryHistoryEvent QueryHistoryEvent { get { return _queryHistoryEvent; } }


#region Title Bar Button methods and properties
        private bool _isPaused;
        public void Pause()
        {
            IsPaused = true;
        }

        private async Task StartTraceAsync()
        {
            await Task.Run(async () =>
            {
                await _eventAggregator.PublishOnBackgroundThreadAsync(new TraceChangingEvent(this,QueryTraceStatus.Starting));
                try
                {
                    BusyMessage = "Waiting for Trace to start";
                    IsBusy = true;
                    CreateTracer();
                    if (_tracer == null)
                    {
                        // the creation of the trace was cancelled
                        await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Stopped));
                        IsChecked = false;
                        return;
                    }
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceWatcherToggleEvent(this, true));
                    Log.Verbose(Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(StartTraceAsync),
                        $"Starting Tracer - {this.TraceSuffix}");
                    await _tracer.StartAsync(_globalOptions.TraceStartupTimeout);

                }
                catch (Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, GetSubclassName(), nameof(StartTraceAsync), "Error Starting Trace");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
                    IsBusy = false;
                }

            }).ConfigureAwait(false);
        }

        public bool CanStop { get { return IsChecked; } }
        public void Stop()
        {
            IsBusy = false;
            IsChecked = false;
            IsPaused = false;
        }

        public bool IsPaused
        {
            get { return _isPaused; }
            set
            {
                _isPaused = value;
                NotifyOfPropertyChange(() => IsRecording);
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
        public abstract void CopyEventContent();

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

        public bool ShowFilters { 
            get { return _showFilters; } 
            set { if (value != _showFilters) { _showFilters = value;  NotifyOfPropertyChange(() => ShowFilters); } } 
        }

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
                OnScaleChanged(this, null);
            }
        }

        public abstract string ImageResource { get; }

        public virtual RibbonControlSizeDefinition SizeDefinition { get; } = new RibbonControlSizeDefinition() { Large = RibbonControlSize.Large, Middle = RibbonControlSize.Large, Small = RibbonControlSize.Middle};

        public void QueryCompleted(bool isCancelled, IQueryHistoryEvent queryHistoryEvent, string errorMessage)
        {
            Log.Verbose("{class} {method} {message}", GetSubclassName(), nameof(QueryCompleted), isCancelled);
            _queryHistoryEvent = queryHistoryEvent;
            ErrorMessage = errorMessage;
            if (isCancelled) {
                IsBusy = false;
                return; 
            }
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

        private void QueryEndEventTimeout(object sender, ElapsedEventArgs e)
        {
            // Check that the QueryEnd event is not in the collection of events, if not we only have
            // a partial set of events and they cannot be relied upon so we clear them
            if(!Events.Any(ev => ev.EventClass == DaxStudioTraceEventClass.QueryEnd)) {
                Reset();
                var msg = "Trace Stopped: QueryEnd event not received - End Event timeout exceeded. You could try increasing this timeout in the Options";
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,
                    msg));
                Log.Warning(Constants.LogMessageTemplate,GetSubclassName(), nameof(QueryEndEventTimeout), msg + $" ({Events.Count} events in collection)");
                _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
            }
        }

        #region Tracer Section
        public QueryTraceStatus TraceStatus => _tracer?.Status ?? QueryTraceStatus.Stopped;
        public IDaxDocument Document { get; set; }
        public IConnectionManager Connection => Document.Connection;

        public bool CapturingStarted { get; private set; }

        public virtual bool IsPreview => false;

        // allows a subclass to perform an action before updating the monitored events
        protected virtual bool UpdatedMonitoredEvents() { return true; }

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

                // exit here if the updating of the event list has been cancelled
                if (!UpdatedMonitoredEvents()) return;

                var monitoredEvents = GetMonitoredEvents();
                var validEventsForConnection = monitoredEvents.Where(e => supportedEvents.Contains(e)).ToList();

                if (_tracer == null) // && _connection.Type != AdomdType.Excel)
                {
                    if (Connection.IsPowerPivot)
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", GetSubclassName(), nameof(CreateTracer), "about to create RemoteQueryTrace", Connection.ConnectionString, Connection.Type.ToString(), Document.Host.Proxy.Port);
                        _tracer = QueryTraceEngineFactory.CreateRemote(Connection, validEventsForConnection, Document.Host.Proxy.Port, _globalOptions, FilterForCurrentSession, TraceSuffix);
                    }
                    else
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", GetSubclassName(), nameof(CreateTracer), "about to create LocalQueryTrace", Connection.ConnectionString, Connection.Type.ToString());
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
                    Log.Error("{class} {method} {message} {stackTrace}", GetSubclassName(), nameof(CreateTracer), innerEx.Message, innerEx.StackTrace);
                }
                _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, ex.GetAllMessages()));
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stackTrace}", GetSubclassName(), nameof(CreateTracer), ex.Message, ex.StackTrace);
                _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, ex.Message));
            }
        }

        protected virtual void OnCreateTracer()
        {
            // allows for trace watchers to perform actions before a tracer gets created
        }

        private void TracerOnTraceEvent(object sender, DaxStudioTraceEventArgs e)
        {
            if (!GetMonitoredEvents().Contains(e.EventClass)) return;
            if (ShouldStartCapturing(e)) CapturingStarted = true;
            if (!CapturingStarted) return;
            if (IsPaused) return;

            Events.Enqueue(e);
            ProcessSingleEvent(e);

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
            Log.Debug(Constants.LogMessageTemplate, GetSubclassName(), nameof(TracerOnTraceCompleted), "Trace Completed");
            
        }

        private void TracerOnTraceStarted(object sender, EventArgs e)
        {
            Log.Debug("{Class} {Event} {@TraceStartedEventArgs}", GetSubclassName(), nameof(TracerOnTraceStarted), e);

            Execute.OnUIThread(() => {
                Document.OutputMessage($"{Title} Trace Started");
                this.IsEnabled = true;
                _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Started));
            });
        }

        private void TracerOnTraceError(object sender, string e)
        {
            Document.OutputError(e);
            _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
            Log.Error(Constants.LogMessageTemplate, GetSubclassName(), nameof(TracerOnTraceError), e);
            // stop the trace if there was an error
            IsRecording = false;
        }

        public void StopTrace()
        {
            _eventAggregator.Unsubscribe(this);
            if (_tracer == null) return;
            try
            {
                Log.Verbose(Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTrace), $"Stopping Trace - {TraceSuffix}");
                _tracer.TraceCompleted -= TracerOnTraceCompleted;
                _tracer.TraceError -= TracerOnTraceError;
                _tracer.TraceStarted -= TracerOnTraceStarted;
                _tracer.TraceWarning -= TracerOnTraceWarning;
                _tracer.TraceEvent -= TracerOnTraceEvent;
                _tracer?.Stop();
                _tracer?.Dispose();
                _tracer = null;
                _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Stopped));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTrace), ex.Message);
            }
        }

        public async Task StopTraceAsync()
        {
            await Task.Run(async () =>
            {
                await _eventAggregator.PublishOnUIThreadAsync(new TraceChangingEvent(this, QueryTraceStatus.Stopping));
                try
                {
                    StopTrace();
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceWatcherToggleEvent(this, false));
                    Log.Verbose(Common.Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTraceAsync),
                        "Stopping Tracer");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Stopped));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, GetSubclassName(), nameof(StopTraceAsync), "Error Stopping Trace");
                    await _eventAggregator.PublishOnUIThreadAsync(new TraceChangedEvent(this, QueryTraceStatus.Error));
                }
            }).ConfigureAwait(false);
        }

        private void TracerOnTraceWarning(object sender, string e)
        {
            Document.OutputWarning(e);
            Log.Warning(Constants.LogMessageTemplate, GetSubclassName(), nameof(TracerOnTraceWarning), e);
        }

        protected virtual void OnUpdateGlobalOptions(UpdateGlobalOptions message) { }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            OnUpdateGlobalOptions(message);
            return Task.CompletedTask;
        }

        public Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            if(message == null) return Task.CompletedTask;
            if (message.Sender != this) return Task.CompletedTask;
            IsBusy = false;
            OnTraceChanged(message);
            return Task.CompletedTask;
        }

        protected virtual void OnTraceChanged(TraceChangedEvent message)
        {
            
        }

        public Task HandleAsync(TraceChangingEvent message, CancellationToken cancellationToken)
        {
            if (message == null) return Task.CompletedTask;
            if (message.Sender != this) return Task.CompletedTask;
            OnTraceChanging(message);
            return Task.CompletedTask;
        }

        protected virtual void OnTraceChanging(TraceChangingEvent message)
        {
            
        }

        public abstract string KeyTip { get; }
        #endregion
        protected IWindowManager WindowManager => _windowManager;

        //public bool HasEvents => (Events?.Count ?? 0) > 0;

        /// <summary>
        /// This method gives sub classes the opportunity to do work
        /// and possible cancel the startup of a trace watcher
        /// </summary>
        public virtual bool ShouldStartTrace() {   return true;  }


    }
}
