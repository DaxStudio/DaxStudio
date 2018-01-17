using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DAXEditor;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Views;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using UnitComboLib.Unit;
using UnitComboLib.Unit.Screen;
using UnitComboLib.ViewModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using DaxStudio.UI.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Utils.DelimiterTranslator;
using DaxStudio.UI.Extensions;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO.Compression;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof (Screen))]
    [Export(typeof (DocumentViewModel))]
    public class DocumentViewModel : Screen
        , IHandle<CancelQueryEvent>
        , IHandle<CancelConnectEvent>
        , IHandle<CommentEvent>
        , IHandle<ConnectEvent>
        , IHandle<LoadFileEvent>
        , IHandle<RunQueryEvent>
        , IHandle<SelectionChangeCaseEvent>
        , IHandle<SendTextToEditor>
        , IHandle<DefineMeasureOnEditor>
        , IHandle<SetSelectedWorksheetEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<UpdateConnectionEvent> 
        , IHandle<NavigateToLocationEvent>
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<OutputMessage>
        , IHandle<ExportDaxFunctionsEvent>
        , IHandle<CloseTraceWindowEvent>
        , IHandle<ShowTraceWindowEvent>
        , IQueryRunner
        , IHaveShutdownTask
        , IConnection
        , ISaveable
    {
        private ADOTabularConnection _connection;
        private IWindowManager _windowManager;
        private IEventAggregator _eventAggregator;
        private MetadataPaneViewModel _metadataPane;
        private IObservableCollection<object> _toolWindows;
        private BindableCollection<ITraceWatcher> _traceWatchers;
        private bool _queryRunning;
        private readonly IDaxStudioHost _host;
        private string _displayName = "";
        private ILog _logger;
        private RibbonViewModel _ribbon;
        private Regex _rexQueryError;
        private Guid _uniqueId;
        private IGlobalOptions _options;
        private IQueryHistoryEvent currentQueryDetails;

        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDaxStudioHost host, RibbonViewModel ribbon, ServerTimingDetailsViewModel serverTimingDetails , IGlobalOptions options)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            _ribbon = ribbon;
            ServerTimingDetails = serverTimingDetails;
            _rexQueryError = new Regex(@"^(?:Query \()(?<line>\d+)(?:\s*,\s*)(?<col>\d+)(?:\s*\))(?<err>.*)$|Line\s+(?<line>\d+),\s+Offset\s+(?<col>\d+),(?<err>.*)$", RegexOptions.Compiled);
            _uniqueId = Guid.NewGuid();
            _options = options;
            Init(_ribbon);
        }

        public void Init(RibbonViewModel ribbon)
        {
            
            State = DocumentState.New;        
            var items = new ObservableCollection<UnitComboLib.ViewModel.ListItem>( GenerateScreenUnitList());
            
            SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(), 0);
            
            // Initialize default Tool Windows
            // HACK: could not figure out a good way of passing '_connection' and 'this' using IoC (MEF)
            MetadataPane =  new MetadataPaneViewModel(_connection, _eventAggregator, this, _options);
            FunctionPane = new FunctionPaneViewModel(_connection, _eventAggregator, this);
            DmvPane = new DmvPaneViewModel(_connection, _eventAggregator, this);
            OutputPane = IoC.Get<OutputPaneViewModel>();// (_eventAggregator);
            QueryResultsPane = IoC.Get<QueryResultsPaneViewModel>();//(_eventAggregator,_host);

            var globalHistory = IoC.Get<GlobalQueryHistory>();
            //var qryHistFactory = IoC.Get<Func<GlobalQueryHistory, IEventAggregator, DocumentViewModel, QueryHistoryPaneViewModel>>();
            QueryHistoryPane = new QueryHistoryPaneViewModel(globalHistory, _eventAggregator, this, _options);
            //QueryHistoryPane = IoC.Get<QueryHistoryPaneViewModel>();
            
            Document = new TextDocument();
            FindReplaceDialog = new FindReplaceDialogViewModel(this.GetEditor());
            _logger = LogManager.GetLog(typeof (DocumentViewModel));
            _selectedTarget = ribbon.SelectedTarget;
            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;

            var t = DaxFormatterProxy.PrimeConnectionAsync(_options, _eventAggregator);

        }

        /// <summary>
        /// Initialize Scale View with useful units in percent and font point size
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<ListItem> GenerateScreenUnitList()
        {
            List<ListItem> unitList = new List<ListItem>();

            var percentDefaults = new ObservableCollection<string>() { "25", "50", "75", "100", "125", "150", "175", "200", "300", "400", "500" };
            var pointsDefaults = new ObservableCollection<string>() { "3", "6", "8", "9", "10", "12", "14", "16", "18", "20", "24", "26", "32", "48", "60" };

            unitList.Add(new ListItem(Itemkey.ScreenPercent, "percent", "%", percentDefaults));
            unitList.Add(new ListItem(Itemkey.ScreenFontPoints, "font size", "pt", pointsDefaults));

            return unitList;
        }


        public override void TryClose(bool? dialogResult = null)
        {
            base.TryClose(dialogResult);
        }
        

        private DAXEditor.DAXEditor _editor;
        /// <summary>
        /// Initialization that requires a reference to the editor control needs to happen here
        /// </summary>
        /// <param name="view"></param>
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            _editor = GetEditor();

            IntellisenseProvider = new DaxIntellisenseProvider(this, _editor, _eventAggregator);
            UpdateSettings();
            if (_editor != null)
            {
                FindReplaceDialog.Editor = _editor;
                SetDefaultHighlightFunction(); 
                _editor.TextArea.Caret.PositionChanged += OnPositionChanged;
                _editor.TextChanged += OnDocumentChanged;
                _editor.PreviewDrop += OnDrop;
                
            }
            if (this.State == DocumentState.LoadPending)
            {
                OpenFile();
            }
        }


        private void OnDrop(object sender, DragEventArgs e)
        {
            if (_editor.SelectionLength == 0)
            {
                e.Handled = true;
                var data = (string)e.Data.GetData(typeof(string));
                InsertTextAtCaret(data);
            }
        }

        void OnEditorLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.CanCopy = false;
            this.CanPaste = false;
            this.CanCut = false;
        }

        void OnEditorGotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            this.CanCopy = true;
            this.CanPaste = true;
            this.CanCut = true;
        }

        void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.Equals(string.Empty))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            //Log.Debug("{Class} {Event} {@EventArgs}", "DocumentViewModel", "OnDocumentChanged", e);          
            _logger.Info("In OnDocumentChanged");
            IsDirty = true;
            NotifyOfPropertyChange(() => IsDirty);
            NotifyOfPropertyChange(() => DisplayName);
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            var caret = sender as Caret;
            if (caret != null)
                _eventAggregator.PublishOnUIThread(new EditorPositionChangedMessage(caret.Column, caret.Line));
        }

        private bool _isDirty;

        public bool IsDirty
        {
            get { return _isDirty; }

            set
            {
                _isDirty = value;
                NotifyOfPropertyChange(()=>IsDirty);
                NotifyOfPropertyChange(()=>DisplayName);
            }
        }
    

        private IQueryTrace _tracer;

        public IQueryTrace Tracer
        {
            get
            {
                return _tracer;
            }
        }

        public void CreateTracer()
        {
            try
            {
                if (_connection == null) return;
                if (_tracer == null) // && _connection.Type != AdomdType.Excel)
                {
                    if (_connection.IsPowerPivot)
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionstring} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create RemoteQueryTrace", _connection.ConnectionString, _connection.Type.ToString(), Host.Proxy.Port);
                        _tracer = QueryTraceEngineFactory.CreateRemote(_connection, GetTraceEvents(TraceWatchers), Host.Proxy.Port, _options, ShouldFilterForCurrentSession(TraceWatchers));
                    }
                    else
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionstring} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create LocalQueryTrace", _connection.ConnectionString, _connection.Type.ToString());
                        _tracer = QueryTraceEngineFactory.CreateLocal(_connection, GetTraceEvents(TraceWatchers), _options, ShouldFilterForCurrentSession(TraceWatchers));
                    }
                    //_tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                    _tracer.TraceError += TracerOnTraceError;
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

        private bool ShouldFilterForCurrentSession(BindableCollection<ITraceWatcher> traceWatchers)
        {
            var w = traceWatchers.FirstOrDefault(tw => tw.IsChecked && tw.FilterForCurrentSession);
            if (w != null) return true;
            return false;
        }

        private void UpdateTraceEvents()
        {
            _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Starting));
            OutputMessage("Reconfiguring Trace");
            var events = GetTraceEvents(TraceWatchers);
            var newEventCnt = 0;
            _tracer.Events.Clear();
            _tracer.Events.Add(DaxStudioTraceEventClass.CommandBegin);
            _tracer.Events.Add(DaxStudioTraceEventClass.QueryEnd);
            foreach (var e in events)
            {
                if (!_tracer.Events.Contains(e))
                {
                    _tracer.Events.Add(e);
                    newEventCnt++;
                } 
            }
            _tracer.Update();
            Log.Debug("Trace Updated with {count} new events", newEventCnt);
        }

        private void TracerOnTraceError(object sender, string e)
        {
            OutputError(e);
            _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Error));
        }

        private List<DaxStudioTraceEventClass> GetTraceEvents(BindableCollection<ITraceWatcher> traceWatchers)
        {
            var events = new List<DaxStudioTraceEventClass>();
            foreach (var tw in traceWatchers.Where(t => t.IsChecked == true))
            {
                foreach (var e in tw.MonitoredEvents)
                {
                    // Don't add DirectQueryEvent if we are not interested in tracing it
                    if (e == DaxStudioTraceEventClass.DirectQueryEnd && !_options.TraceDirectQuery) continue;
                    // Add the even to the collection if we don't already have it
                    if (!events.Contains(e) )
                    {
                        events.Add(e);
                    }
                }
            }
            return events;
        }

        private void TracerOnTraceCompleted(object sender, IList<DaxStudioTraceEventArgs> capturedEvents)
        {
            var checkedTraceWatchers = from tw in TraceWatchers
                                       where tw.IsChecked == true
                                       select tw;

            foreach (var tw in checkedTraceWatchers)
            {
                tw.ProcessAllEvents(capturedEvents);
            }
            _eventAggregator.PublishOnUIThread(new QueryTraceCompletedEvent());
        }

        private void TracerOnTraceStarted(object sender, EventArgs e)
        {
            Log.Debug("{Class} {Event} {@TraceStartedEventArgs}", "DocumentViewModel", "TracerOnTraceStarted", e);
            Execute.OnUIThread(() => { 
                OutputMessage("Query Trace Started");
                TraceWatchers.EnableAll();
                _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Started));
            }); 
        }

        //private void TracerOnTraceEvent(object sender, TraceEventArgs traceEventArgs)
        //{
        //    var checkedTraceWatchers = from tw in TraceWatchers 
        //                               where tw.IsChecked == true
        //                               select tw;
                
        //    foreach (var tw in checkedTraceWatchers)
        //    {
        //        tw.ProcessEvent(traceEventArgs);
        //    }
        //}

        // Use MEF to give us a collection of TraceWatcher factory objects
        // used to create unique instances of each TraceWatcher type per document
        [ImportMany(typeof(ITraceWatcher))]
        public List<ExportFactory<ITraceWatcher>> TraceWatcherFactories { get; set; }
        
        public BindableCollection<ITraceWatcher> TraceWatchers
        {
            // we use the factory to make sure that each DocumentViewModel has it's
            // own set of TraceWatchers so that they can be enabled/disabled per
            // document
            get
            {
                if (_traceWatchers == null)
                {
                    _traceWatchers = new BindableCollection<ITraceWatcher>();
                    foreach( var fac in TraceWatcherFactories)
                    {
                        var tw = fac.CreateExport().Value;
                        _traceWatchers.Add(tw);
                    }
                }
                return _traceWatchers;
            }
            
        }

        
        /// <summary>
        /// Properties added to this collection populate the available tool windows inside the document pane
        /// </summary>
        public IObservableCollection<object> ToolWindows
        {
            get
            {
                return _toolWindows ?? (_toolWindows = new BindableCollection<object>
                    {
                        MetadataPane,
                        FunctionPane,
                        DmvPane,
                        OutputPane,
                        QueryResultsPane,
                        QueryHistoryPane
                    });
            }
        }

        private DAXEditor.DAXEditor GetEditor()
        {
            DocumentView v = (DocumentView)GetView();
            return v != null ? v.daxEditor : null;
        }

        public TextDocument Document { get; set; }

        public void ActivateResults()
        {
            if (!TraceWatchers.Any(tw => tw.IsChecked))
            {
                // only activate if no trace watchers are active
                // otherwise we assume that the user will want to keep the
                QueryResultsPane.Activate();
            }
        }

        public void ActivateOutput()
        {
            OutputPane.Activate();
        }

        public void QueryCompleted()
        {
            QueryCompleted(false);
        }

        public void QueryCompleted(bool isCancelled)
        {
            _queryStopWatch.Stop();
            IsQueryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            QueryResultsPane.IsBusy = false;  // TODO - this should be some sort of collection of objects with a specific interface, not a hard coded object reference
            currentQueryDetails.ClientDurationMs = _queryStopWatch.ElapsedMilliseconds;
            currentQueryDetails.RowCount = ResultsDataSet.RowCounts();

            bool svrTimingsEnabled = false;
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsChecked) tw.QueryCompleted(isCancelled, currentQueryDetails);
                var svrTimings = tw as ServerTimesViewModel;
                if (svrTimings != null) { svrTimingsEnabled = true; }

            }
            if (!svrTimingsEnabled)
            {
                _eventAggregator.BeginPublishOnUIThread(currentQueryDetails);
            }
        }

        public IDaxStudioHost Host { get { return _host; } }

        private string _selectedWorksheet = "";
        public string SelectedWorksheet { get { return _selectedWorksheet; } 
            set { _selectedWorksheet = value; 
                NotifyOfPropertyChange(() => SelectedWorksheet); 
            } 
        }
        public string SelectedDatabase { get {
            if (_selectedDatabase == null && IsConnected)
            {
                _selectedDatabase = Connection.Database.Name;
            }
            return _selectedDatabase; }
            set
            {
                if (value != _selectedDatabase)
                {
                    _selectedDatabase = value;
                    Connection.ChangeDatabase(value);
                    var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                    _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this, Databases,activeTrace));
                    NotifyOfPropertyChange(() => SelectedDatabase);
                    
                }
            }
        }

        //public string ConnectionString { get { return _connection.ConnectionString; } }

        public string ConnectionStringWithInitialCatalog {
            get {
                //var cubeEquals = this.Connection.IsMultiDimensional ? $";Cube={this.Sele}: "";
                //return string.Format("{0};Initial Catalog={1}", _connection.ConnectionString , SelectedDatabase );
                return Connection.ConnectionStringWithInitialCatalog;
            }
        }

        public MetadataPaneViewModel MetadataPane
        {
            get { return _metadataPane; }
            set { _metadataPane = value; }
        }

        public FunctionPaneViewModel FunctionPane { get; private set; }


        protected override void OnDeactivate(bool close)
        {
            Log.Debug("{Class} {Event} Close:{Value} Doc:{Document}", "DocumentViewModel", "OnDeactivated (close)", close, this.DisplayName);          
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
            _eventAggregator.Unsubscribe(QueryResultsPane);
            foreach (var tw in this.TraceWatchers)
            {
                _eventAggregator.Unsubscribe(tw);
            }
        }

        protected override void OnActivate()
        {
            Log.Debug("{Class} {Event} {Document}", "DocumentViewModel", "OnActivate", this.DisplayName);
            _logger.Info("In OnActivate");
            base.OnActivate();
            _eventAggregator.Subscribe(this);
            _eventAggregator.Subscribe(QueryResultsPane);
            foreach (var tw in this.TraceWatchers)
            {
                _eventAggregator.Subscribe(tw);
            }
            _ribbon.SelectedTarget = _selectedTarget;
            var loc = Document.GetLocation(0);
            //SelectedWorksheet = QueryResultsPane.SelectedWorksheet;
            try
            {
                if (HasDatabaseSchemaChanged())
                {
                    RefreshMetadata();
                    OutputMessage("Model schema change detected - Metadata refreshed");
                }
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {Method} {Exception}", "DocumentViewModel", "OnActivate [Updating Metadata]", ex);
                OutputError(string.Format("Error Refreshing Metadata - {0}", ex.Message));
            }

            try
            {
                _eventAggregator.PublishOnUIThread(new EditorPositionChangedMessage(loc.Column, loc.Line));
                _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {Method} {Exception}", "DocumentViewModel", "OnActivate", ex);
            }

        }

        public override void CanClose(Action<bool> callback)
        {
            DoCloseCheck(callback);
        }

        internal void SwapDelimiters()
        {
            if (_editor.SelectionLength > 0)
            {
                _editor.SelectedText = SwapDelimiters(_editor.SelectedText);
            }
            else
            {
                _editor.Text = SwapDelimiters(_editor.Text);
            }
        }

        private string SwapDelimiters(string selectedText)
        {
            var dsm = new DelimiterStateMachine();
            return dsm.ProcessString(selectedText);
        }

        public bool Close()
        {
            // Close the document's connection 
            if (Connection != null)
            {
                if (Connection.State != ConnectionState.Closed && Connection.State != ConnectionState.Broken)
                {
                    Connection.Close();
                }
            }

            var docTab = Parent as DocumentTabViewModel;
            docTab.CloseItem(this);
            if (docTab != null) docTab.Items.Remove(this);
            return true;
        }

        internal ADOTabularConnection Connection
        {
            get { return _connection; }
            set
            {
                if (_connection == value)
                    return;
                
                UpdateConnections(value,"");
                Log.Debug("{Class} {Event} {Connection}", "DocumentViewModel", "Publishing ConnectionChangedEvent", _connection==null? "<null>": _connection.ConnectionString);
                NotifyOfPropertyChange(() => IsConnected);
                NotifyOfPropertyChange(() => IsAdminConnection);
                _eventAggregator.PublishOnUIThread(new ConnectionChangedEvent(_connection, this)); 
            } 
        }

        
        private void UpdateConnections(ADOTabularConnection value,string selectedDatabase)
        {
            _logger.Info("In UpdateConnections");
            OutputPane.AddInformation("Establishing Connection");
            Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "DocumentViewModel", "UpdateConnections"
                , value == null ? "<null>" : value.ConnectionString
                , selectedDatabase);          
            if (value != null && value.State != ConnectionState.Open)
            {
                OutputPane.AddWarning(string.Format("Connection for server {0} is not open",value.ServerName));
                return;
            }
            using (NewStatusBarMessage("Refreshing Metadata..."))
            {
                if (value == null) return;
                _connection = value;
                NotifyOfPropertyChange(() => IsAdminConnection);
                var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                // enable/disable traces depending on the current connection
                foreach (var traceWatcher in TraceWatchers)
                {
                    // on change of connection we need to disable traces as the will
                    // be pointing to the old connection
                    traceWatcher.IsChecked = false;
                    // then we need to check if the new connection can be traced
                    traceWatcher.CheckEnabled(this,activeTrace);   
                }
                MetadataPane.Connection = _connection;
                FunctionPane.Connection = _connection;
                DmvPane.Connection = _connection;
                Execute.OnUIThread(() =>
               {
                   try
                   {
                       if (_editor == null) _editor = GetEditor();
                   //    _editor.UpdateKeywordHighlighting(_connection.Keywords);
                       _editor.UpdateFunctionHighlighting(_connection.AllFunctions);
                       Log.Information("{class} {method} {message}", "DocumentViewModel", "UpdateConnections", "SyntaxHighlighting updated");
                   }
                   catch (Exception ex)
                   {
                       Log.Error(ex, "{class} {method} {message}", "DocumentViewModel","UpdateConnections", "Error Updating SyntaxHighlighting: " + ex.Message);
                   }
               });
            }
        }

        private Task UpdateConnectionsAsync(ADOTabularConnection value, string selectedDatabase)
        {
            Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "DocumentViewModel", "UpdateConnectionsAsync", value.ConnectionString,selectedDatabase);          
            return Task.Run(() =>
                {
                    UpdateConnections(value,selectedDatabase);
                });
        }

        public void ContentRendered()
        {
            if (Connection == null)
            {
                ChangeConnection();
            }
        }


        public string SelectedText { get; set; }
        public string Text { get; set; }
        public string FileName { get; set; }

        public void ChangeConnection()
        {
            _eventAggregator.PublishOnUIThread(new ConnectionPendingEvent(this));
            Log.Debug("{class} {method} {event}", "DocumentViewModel", "ChangeConnection", "start");          
            var connStr = Connection == null ? string.Empty : Connection.ConnectionString;
            var msg = NewStatusBarMessage("Checking for PowerPivot model...");
            Log.Debug("{class} {method} {Event} ", "DocumentViewModel", "ChangeConnection", "starting async call to Excel");          
                
            Task.Run(() => Host.Proxy.HasPowerPivotModel).ContinueWith((x) =>
            {
                // todo - should we be checking for exceptions in this continuation
                try
                {
                    Log.Debug("{class} {method} {Event} ", "DocumentViewModel", "ChangeConnection", "recieved async result from Excel");
                    bool hasPpvtModel = x.Result;
                            
                    Log.Debug("{class} {method} Has PowerPivotModel: {hasPpvtModel} ", "DocumentViewModel", "ChangeConnection", hasPpvtModel);
                    msg.Dispose();

                    Execute.OnUIThread(() =>
                    {
                        var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator, hasPpvtModel, this);

                        _windowManager.ShowDialogBox(connDialog, settings: new Dictionary<string, object>
                                        {
                                            {"Top", 40},
                                            { "WindowStyle", WindowStyle.None},
                                            { "ShowInTaskbar", false},
                                            { "ResizeMode", ResizeMode.NoResize},
                                            { "Background", System.Windows.Media.Brushes.Transparent},
                                            { "AllowsTransparency",true}
                                        });
                    });
                    
                }
                catch (Exception ex)
                {
                    // if the task throws an exception the "real" exception is usually in the innerException
                    var innerMsg = ex.Message;
                    if (ex.InnerException != null) innerMsg = ex.InnerException.Message;
                    Log.Error("{class} {method} {message}", "DocumentViewModel", "ChangeConnection", innerMsg);
                    OutputError(innerMsg);
                }
                finally
                {
                    if (!msg.IsDisposed)
                    {
                        msg.Dispose(); // turn off the status bar message
                    }
                }
            }, TaskScheduler.Default);
            
        }

        public async Task<bool> HasPowerPivotModelAsync()
        {
           return await Task.Run(() => Host.Proxy.HasPowerPivotModel );
        }

        public string ConnectionError { get; set; }

        public bool IsConnected
        {
            get { 
                if (Connection == null) return false;
                return Connection.State != ConnectionState.Closed && Connection.State != ConnectionState.Broken;
            }
        }

        public bool IsQueryRunning 
        {
            get { return _queryRunning; }
            set {
                _queryRunning = value;
                NotifyOfPropertyChange(() => IsQueryRunning);
                NotifyOfPropertyChange(() => CanRunQuery);
            }
        }

        public DmvPaneViewModel DmvPane { get; private set; }

        public OutputPaneViewModel OutputPane { get; set; }

        public QueryResultsPaneViewModel QueryResultsPane { get; set; }

        public string QueryText
        {
            get
            {
                string qry;
                if (!Dispatcher.CurrentDispatcher.CheckAccess())
                {
                    Dispatcher.CurrentDispatcher.Invoke(new Func<string>(() =>
                        { qry = GetQueryTextFromEditor();
                        qry = DaxHelper.PreProcessQuery(qry);
                        return qry;
                        }));
                }
                qry = GetQueryTextFromEditor();
                // merge in any parameters
                qry = DaxHelper.PreProcessQuery(qry);
                // swap delimiters if not using default style
                if (_options.DefaultSeparator != DaxStudio.Interfaces.Enums.DelimiterType.Comma)
                {
                    qry = SwapDelimiters(qry);
                }
                return qry;
            }
        }

        #region Text Formatting Functions
        public void MergeParameters()
        {
            var editor = this.GetEditor();
            var txt = GetQueryTextFromEditor();
            txt = DaxHelper.PreProcessQuery(txt);
            if (editor.Dispatcher.CheckAccess())
            {
                if (editor.SelectionLength == 0)
                { editor.Text = txt; }
                else
                { editor.SelectedText = txt; }
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() => 
                {
                    if (editor.SelectionLength == 0)
                    { editor.Text = txt; }
                    else
                    { editor.SelectedText = txt; }
                }));
            }
        }

        public void Undo()
        {
            var editor = this.GetEditor();
            
            if (editor.Dispatcher.CheckAccess())
            {
                if (editor.CanUndo) editor.Undo();
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() =>
                {
                    if (editor.CanUndo) editor.Undo();
                }));
            }
        }

        public void Redo()
        {
            var editor = this.GetEditor();

            if (editor.Dispatcher.CheckAccess())
            {
                if (editor.CanRedo) editor.Redo();
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() =>
                {
                    if (editor.CanRedo) editor.Redo();
                }));
            }
        }
        private string GetQueryTextFromEditor()
        {
            var editor = GetEditor();
            string txt = "";
            if (editor.Dispatcher.CheckAccess())
            {
                txt = GetQueryTextFromEditorInternal(editor);
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(()=> { txt = GetQueryTextFromEditorInternal(editor); }));
            }
            return txt;
        }

        private void SelectedTextToUpperInternal(DAXEditor.DAXEditor editor)
        {
            if (editor.SelectionLength == 0) return;
            editor.SelectedText = editor.SelectedText.ToUpper();   
        }

        private void SelectionToUpper()
        {
            var editor = GetEditor();
            if (editor.Dispatcher.CheckAccess())
            {
                SelectedTextToUpperInternal(editor);
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() => SelectedTextToUpperInternal(editor)));
            }
        }

        private void SelectedTextToLowerInternal(DAXEditor.DAXEditor editor)
        {
            if (editor.SelectionLength == 0) return;
            editor.SelectedText = editor.SelectedText.ToLower();
        }

        private void SelectionToLower()
        {
            var editor = GetEditor();
            if (editor.Dispatcher.CheckAccess())
            {
                SelectedTextToLowerInternal(editor);
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() => SelectedTextToLowerInternal(editor)));
            }
        }

        public void CommentSelection()
        {
            var editor = GetEditor();
            if (editor.Dispatcher.CheckAccess())
            {
                editor.CommentSelectedLines();
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() => editor.CommentSelectedLines()));
            }
        }

        public void UnCommentSelection()
        {
            var editor = GetEditor();
            if (editor.Dispatcher.CheckAccess())
            {
                editor.UncommentSelectedLines();
            }
            else
            {
                editor.Dispatcher.Invoke(new System.Action(() => editor.UncommentSelectedLines()));
            }
        }

        private string GetQueryTextFromEditorInternal(DAXEditor.DAXEditor editor)
        {
            var queryText = editor.SelectedText;
            if (editor.SelectionLength == 0)
            {
                queryText = editor.Text;
            }
            return queryText;
        }
        #endregion

        #region Execute Query
        private Timer _timer;
        private Stopwatch _queryStopWatch;
        public DataTable ExecuteDataTableQuery(string daxQuery)
        {
            int row = 0;
            int col = 0;
            this._editor.Dispatcher.Invoke(() =>
            {
                if (_editor.SelectionLength > 0) { 
                    var loc = this._editor.Document.GetLocation(this._editor.SelectionStart);
                    row = loc.Line;
                    col = loc.Column;
                }
            });
            try
            {
                var c = Connection;
                foreach (var tw in TraceWatchers)
                {
                    if (tw.IsChecked && !tw.IsPaused)
                    {
                        tw.IsBusy = true;
                    }
                }
                if (_options.DefaultSeparator != DaxStudio.Interfaces.Enums.DelimiterType.Comma) {
                    var dsm = new DelimiterStateMachine(DaxStudio.Interfaces.Enums.DelimiterType.Comma);
                    daxQuery = dsm.ProcessString(daxQuery);
                } 
                _timer = new Timer(300);
                _timer.Elapsed += _timer_Elapsed;
                _timer.Start();
                _queryStopWatch = new Stopwatch();
                _queryStopWatch.Start();
                var dt = c.ExecuteDaxQueryDataTable(daxQuery);
                dt.FixColumnNaming(daxQuery);
                return dt;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                OutputError(e.Message,row,col);
                ActivateOutput();
                return null;
            }
            finally
            {
                _timer.Stop();
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Dispose();
                NotifyOfPropertyChange(() => ElapsedQueryTime);
                _eventAggregator.PublishOnUIThread(new UpdateTimerTextEvent(ElapsedQueryTime));
                QueryCompleted();

            }

        }

        public AdomdDataReader ExecuteDataReaderQuery(string daxQuery)
        {
            int row = 0;
            int col = 0;
            this._editor.Dispatcher.Invoke(() =>
            {
                if (_editor.SelectionLength > 0)
                {
                    var loc = this._editor.Document.GetLocation(this._editor.SelectionStart);
                    row = loc.Line;
                    col = loc.Column;
                }
            });
            try
            {
                var c = Connection;
                foreach (var tw in TraceWatchers)
                {
                    if (tw.IsChecked && !tw.IsPaused)
                    {
                        tw.IsBusy = true;
                    }
                }
                _timer = new Timer(300);
                _timer.Elapsed += _timer_Elapsed;
                _timer.Start();
                _queryStopWatch = new Stopwatch();
                _queryStopWatch.Start();
                var dr = c.ExecuteReader(daxQuery);

                return dr;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                OutputError(e.Message, row, col);
                ActivateOutput();
                return null;
            }
            finally
            {
                _queryStopWatch.Stop();
                _timer.Stop();
                _timer.Elapsed -= _timer_Elapsed;
                _timer.Dispose();
                NotifyOfPropertyChange(() => ElapsedQueryTime);
                _eventAggregator.PublishOnUIThread(new UpdateTimerTextEvent(ElapsedQueryTime));
                // Can't call query completed here as for a DataReader we still need to stream the results back
                // we can't marke the query as complete until we've finished processing the DataReader
                //QueryCompleted();

            }

        }

        public string ElapsedQueryTime
        {
            get { return _queryStopWatch == null ? "" : _queryStopWatch.Elapsed.ToString("mm\\:ss\\.f"); }
            
        }

        void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            NotifyOfPropertyChange(() => ElapsedQueryTime);
            _eventAggregator.PublishOnUIThread(new UpdateTimerTextEvent(ElapsedQueryTime));
        }

        private void CancelQuery()
        {
            try
            {
                using (NewStatusBarMessage("Cancelling Query..."))
                {
                    var c = Connection;
                    c.Cancel();
                    QueryCompleted(true);
                    OutputWarning("Cancel of running query requested");
                }
            }
            catch (Exception e)
            {
                OutputError(e.Message);
                ActivateOutput();
            }
        }

        public Task CancelQueryAsync()
        {
            return Task.Run(()=>CancelQuery());
        }

        public Task<DataTable> ExecuteQueryAsync(string daxQuery)
        {
            return Task.Run(() => ExecuteDataTableQuery(daxQuery));
        }

        public async void Handle(RunQueryEvent message)
        {
            // if we can't run the query then do nothing 
            // (ribbon button will be disabled, but the following check blocks hotkey requests)
            if (!CanRunQuery) return;

            IsQueryRunning = true;
            
            NotifyOfPropertyChange(()=>CanRunQuery);
            if (message.ClearCache) await ClearDatabaseCacheAsync();
            RunQueryInternal(message);
        }

        private void RunQueryInternal(RunQueryEvent message)
        {
            var msg = NewStatusBarMessage("Running Query...");
            _eventAggregator.PublishOnUIThread(new QueryStartedEvent());
            currentQueryDetails = new QueryHistoryEvent(this.QueryText, DateTime.Now, this.ServerName, this.SelectedDatabase, this.FileName);
            currentQueryDetails.Status = QueryStatus.Running;
            message.ResultsTarget.OutputResultsAsync(this).ContinueWith((antecendant) =>
            {
                // todo - should we be checking for exceptions in this continuation
                IsQueryRunning = false;
                NotifyOfPropertyChange(() => CanRunQuery);

                // if the server times trace watcher is not active then just record client timings
                if (!TraceWatchers.OfType<ServerTimesViewModel>().First().IsChecked)
                {
                    currentQueryDetails.ClientDurationMs = _queryStopWatch.ElapsedMilliseconds;
                    currentQueryDetails.RowCount = ResultsDataSet.RowCounts();
                    _eventAggregator.PublishOnUIThreadAsync(currentQueryDetails);
                }

                _eventAggregator.PublishOnUIThread(new QueryFinishedEvent());
                msg.Dispose();               
            },TaskScheduler.Default);
        }
        #endregion

        public StatusBarViewModel StatusBar { get; set; }
        

        public DataTable ResultsTable
        {
            get { return QueryResultsPane.ResultsDataTable; }
            set { QueryResultsPane.ResultsDataTable = value; }
        }

        public DataSet ResultsDataSet
        {
            get { return QueryResultsPane.ResultsDataSet; }
            set { QueryResultsPane.ResultsDataSet = value; }
        }

        public bool CanRunQuery
        {
            // todo - do we need to track query traces changing?
            get { return !IsQueryRunning && IsConnected; }
        }

        #region Output Messages
        public void OutputMessage(string message)
        {
            OutputPane.AddInformation(message);
        }

        public void OutputMessage(string message, double duration)
        {
            OutputPane.AddInformation(message, duration);
        }

        public void OutputWarning(string warning)
        {
            OutputPane.AddWarning(warning);
        }

        public void OutputError(string error)
        {

            //"Query ( , )"
            var m = _rexQueryError.Match(error);
            if (m.Success)
            {
                int line = 0;
                int.TryParse(m.Groups["line"].Value, out line);
                int col = 0;
                int.TryParse(m.Groups["col"].Value, out col);
                OutputError(error , line, col);
            }
            else
            {
                OutputPane.AddError(error);
            }
        }

        public void OutputError(string error, int row, int column)
        {
            int msgRow = 0;
            int msgCol = 0;
            //"Query ( , )"
            var m = _rexQueryError.Match(error);
            if (m.Success)
            {
                int.TryParse(m.Groups["line"].Value, out msgRow);   
                int.TryParse(m.Groups["col"].Value, out msgCol);
                msgCol += column > 0 ? column -1 : 0;
                msgRow += row > 0 ? row -1 : 0;
            }
            _editor.Dispatcher.Invoke(() =>
            {
                _editor.DisplayErrorMarkings(msgRow, msgCol, 1, error);
            });
                
            OutputPane.AddError(error,msgRow,msgCol);
        }

        #endregion

        private void InsertTextAtCaret(string text)
        {
            var editor = GetEditor();
            editor.Document.Insert(editor.CaretOffset, text);
            editor.Focus();
        }

        private void InsertTextAtSelection(string text)
        {
            
            var editor = GetEditor();
            if (editor.SelectionLength == 0)
            {
                editor.Document.Insert(editor.SelectionStart, text);
            }
            else
            {
                editor.SelectedText = text;
            }
            editor.Focus();
              
        }

        /*
        private int mTextEditorCaretOffset = 0;
        private int mTextEditorSelectionStart = 0;
        private int mTextEditorSelectionLength = 0;
        
        /// <summary>
        /// Get/set editor carret position
        /// for CTRL-TAB Support http://avalondock.codeplex.com/workitem/15079
        /// </summary>
        public int CaretOffset
        {
            get
            {
                return this.mTextEditorCaretOffset;
            }

            set
            {
                if (this.mTextEditorCaretOffset != value)
                {
                    this.mTextEditorCaretOffset = value;
                    this.NotifyOfPropertyChange(() => CaretOffset);
                }
            }
        }

        /// <summary>
        /// Get/set editor start of selection
        /// for CTRL-TAB Support http://avalondock.codeplex.com/workitem/15079
        /// </summary>
        public int TextEditorSelectionStart
        {
            get
            {
                return this.mTextEditorSelectionStart;
            }

            set
            {
                if (this.mTextEditorSelectionStart != value)
                {
                    this.mTextEditorSelectionStart = value;
                    this.NotifyOfPropertyChange(() => SelectionStart);
                }
            }
        }

        /// <summary>
        /// Get/set editor length of selection
        /// for CTRL-TAB Support http://avalondock.codeplex.com/workitem/15079
        /// </summary>
        public int TextEditorSelectionLength
        {
            get
            {
                return this.mTextEditorSelectionLength;
            }

            set
            {
                if (this.mTextEditorSelectionLength != value)
                {
                    this.mTextEditorSelectionLength = value;
                    this.NotifyOfPropertyChange(() => SelectionLength);
                }
            }
        }

        public int SelectionStart
        {
            get
            {
                int start = 0, length = 0;
                bool IsRectSelect = false;

                if (this.TxtControl != null)
                    this.TxtControl.CurrentSelection(out start, out length, out IsRectSelect);

                return start;
            }
        }

        public int SelectionLength
        {
            get
            {
                int start = 0, length = 0;
                bool IsRectSelect = false;

                if (this.TxtControl != null)
                    this.TxtControl.CurrentSelection(out start, out length, out IsRectSelect);

                return length;
            }
        }
        */

        public void Handle(SendTextToEditor message)
        {
            if (!string.IsNullOrEmpty(message.DatabaseName))
            {
                if (Databases.Contains(message.DatabaseName))
                    if (SelectedDatabase != message.DatabaseName)
                    {
                        SelectedDatabase = message.DatabaseName;
                        OutputMessage($"Current Database changed to '{message.DatabaseName}'");
                    }
                    else
                        OutputWarning($"Could not switch to the '{message.DatabaseName}' database");
            }
            InsertTextAtSelection(message.TextToSend);
        }

        public void Handle(DefineMeasureOnEditor message)
        {
            DefineMeasureOnEditor(message.MeasureName, message.MeasureExpression);
        }

        //RRomano: Should this be on DaxEditor?

        private Regex defineMeasureRegex = new Regex(@"(?<=DEFINE)((.|\n)*?)(?=EVALUATE|\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void DefineMeasureOnEditor(string measureName, string measureExpression)
        {
            var editor = GetEditor();

            // Try to find the DEFINE statements

            var currentText = editor.Text;                      
            
            var measureDeclaration = string.Format("MEASURE {0} = {1}", measureName, measureExpression);
            // TODO - expand measure expression and generate other measures here!!


            // If found then add the measure inside the DEFINE statement, if not then just paste the measure expression
            if (defineMeasureRegex.IsMatch(currentText))
            {                
                currentText = defineMeasureRegex.Replace(currentText, (m) =>
                {
                    var measuresText = new StringBuilder(m.Groups[1].Value);

                    measuresText.AppendLine(measureDeclaration);

                    return measuresText.ToString();

                });

                editor.Text = currentText;

                editor.Focus();
            }
            else
            {
                measureDeclaration = string.Format("DEFINE {1}{0}{1}", measureDeclaration, System.Environment.NewLine);

                InsertTextAtSelection(measureDeclaration);
            }                        
        }

        public void Handle(UpdateConnectionEvent message)
        {
            _logger.Info("In Handle<UpdateConnectionEvent>");
            Log.Debug("{Class} {Event} {ConnectionString} DB: {Database}", "DocumentViewModel", "Handle:UpdateConnectionEvent", message.Connection == null? "<null>":message.Connection.ConnectionString, message.DatabaseName);
            
            UpdateConnections(message.Connection, message.DatabaseName);
            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
            _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this,Databases,activeTrace));     
        }


        public void Handle(TraceWatcherToggleEvent message)
        {
            Log.Verbose("{Class} {Event} TraceWatcher:{TraceWatcher} IsActive:{IsActive}", "DocumentViewModel", "Handle(TraceWatcherToggleEvent", message.TraceWatcher.ToString(), message.IsActive);
            TraceWatchers.DisableAll();

            if (message.IsActive)
            {
                EnableTrace(message.TraceWatcher);
            }
            else
            {
                DisableTrace(message.TraceWatcher);
            }

        }

        private void DisableTrace(ITraceWatcher watcher)
        {
            var otherTracesRunning = false;
            //ToolWindows.Remove(watcher);
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsChecked) otherTracesRunning = true;
            }
            if (otherTracesRunning)
            {
                UpdateTraceEvents();
                return;
            }

            // If we got here no traces are running so shut everything down
            _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Stopping));
            OutputMessage("Stopping Trace");
            // spin down trace as no tracewatchers are active
            Tracer.Stop();
            ResetTracer();
            OutputMessage("Trace Stopped");
            _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Stopped));
            TraceWatchers.EnableAll();
        }

        private void EnableTrace(ITraceWatcher watcher)
        {
            if (!ToolWindows.Contains(watcher))
                ToolWindows.Add(watcher);

            // synch the ribbon buttons and the server timings pane
            if (watcher is ServerTimesViewModel && watcher.IsChecked)
            {
                ((ServerTimesViewModel)watcher).ServerTimingDetails = ServerTimingDetails;
            }

            if (Tracer == null) CreateTracer();
            else UpdateTraceEvents();

            // spin up trace if one is not already running
            if (Tracer.Status != QueryTraceStatus.Started
                && Tracer.Status != QueryTraceStatus.Starting)
            {
                _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Starting));
                OutputMessage("Waiting for Trace to start");

                var t = Tracer;
                t.StartAsync().ContinueWith((p) =>
                {
                    if (p.Exception != null)
                    {
                        p.Exception.Handle((x) =>
                        {
                            Log.Error("{class} {method} {message} {stacktrace}", "DocumentViewModel", "Handle<TraceWatcherToggleEvent>", x.Message, x.StackTrace);
                            OutputError("Error Starting Trace: " + x.Message);
                            return false;
                        });
                    };
                }, TaskScheduler.Default);
            }
            // Disable other tracewatchers with different filter for current session values
            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
            foreach (var tw in TraceWatchers)
            {
                tw.CheckEnabled(this, activeTrace);
            }
        }

        private void ResetTracer()
        {
            if (Tracer != null)
            {
                Tracer.Stop();
                Tracer.Dispose();
                _tracer = null;
            }
        }

        public void Save()
        {
            if (!IsDiskFileName)
                SaveAs();
            else
            {
                using (TextWriter tw = new StreamWriter(FileName, false, Encoding.Unicode))
                {
                    tw.Write(GetEditor().Text);
                    tw.Close();
                }
                // Save all visible TraceWatchers
                foreach (var tw in ToolWindows)
                {
                    var saver = tw as ISaveState;
                    if (saver != null)
                    {
                        saver.Save(FileName);
                    }
                }
                _eventAggregator.PublishOnUIThread(new FileSavedEvent(FileName));
                IsDirty = false;
                NotifyOfPropertyChange(() => DisplayName);
            }
        }

        public async void PublishDaxFunctions() {
            if (!IsConnected)
            {
                MessageBoxEx.Show("The active query window is not connected to a data source. You need to be connected to a data source in order to use the publish functions option", "Publish DAX Functions", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            Stopwatch publishStopWatch = new Stopwatch();
            publishStopWatch.Start();

            // Ping server to see whether the version is already there
            string ssasVersion = DaxMetadataInfo.Version.SSAS_VERSION;
            string metadataFilename = Path.GetTempFileName(); 
            try {
                _options.CanPublishDaxFunctions = false;
                using (var client = GetHttpClient()) {
                    client.Timeout = new TimeSpan(0, 0, 60); // set 30 second timeout
                    Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", string.Format("Ping version {0} to DaxVersioning ", ssasVersion));
                    HttpResponseMessage response = await client.PostAsJsonAsync("api/v1/pingversion", new VersionRequest { SsasVersion = ssasVersion });  // responseTask.Result;
                    if (!response.IsSuccessStatusCode) {
                        publishStopWatch.Stop();
                        string pingResult = string.Format("Error from ping version: ", response.StatusCode.ToString());
                        Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", pingResult);
                        OutputMessage(pingResult, publishStopWatch.ElapsedMilliseconds);
                        return;
                    }
                    response.EnsureSuccessStatusCode(); // probably redundant
                    string productFound = response.Content.ReadAsStringAsync().Result;
                    if (!(string.IsNullOrEmpty(productFound) || productFound == "null")) {
                        publishStopWatch.Stop();
                        string pingResult = string.Format("Result from ping version {0} : {1}", ssasVersion, productFound);
                        Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", pingResult);
                        OutputMessage(pingResult, publishStopWatch.ElapsedMilliseconds);
                        return;
                    }
                    Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", "No products from ping version - preparing metadata file");

                    // Always compress content
                    ExportDaxFunctions(metadataFilename, true);

                    var requestContent = new MultipartFormDataContent();
                    var fileContent = File.ReadAllBytes(metadataFilename);
                    var metadataContent = new ByteArrayContent(fileContent);

                    string uploadingMessage = string.Format("file {0} ({1} bytes)", metadataFilename, fileContent.Count());
                    Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", string.Format("Uploading {0}", uploadingMessage));

                    metadataContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("fileUpload") {
                        FileName = string.Format("DAX Functions {0}.zip", ssasVersion)
                    };
                    requestContent.Add(metadataContent);
                    await client.PostAsync("api/v1/uploadversion", requestContent);

                    Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", "Upload completed");
                    publishStopWatch.Stop();
                    OutputMessage(string.Format("Uploaded DAX metadata v.{0}: {1}", ssasVersion, uploadingMessage), publishStopWatch.ElapsedMilliseconds);
                }
            }
            finally {
                // Remove temporary filename
                if (File.Exists(metadataFilename)) {
                    File.Delete(metadataFilename);
                }
                _options.CanPublishDaxFunctions = true;
            }
        }
        public void ExportDaxFunctions() {
            if (!IsConnected)
            {
                MessageBoxEx.Show("The active query window is not connected to a data source. You need to be connected to a data source in order to use the export functions option", "Export DAX Functions", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Configure save file dialog box
            var dlg = new Microsoft.Win32.SaveFileDialog {
                FileName = "DAX Functions " + DaxMetadataInfo.Version.SSAS_VERSION,
                DefaultExt = ".zip",
                Filter = "DAX metadata (ZIP)|*.zip|DAX metadata|*.json"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true) {
                // Save document 
                ExportDaxFunctions(dlg.FileName);
            }
        }
        public void ExportDaxFunctions(string path) {
            string extension = Path.GetExtension(path).ToLower();
            bool compression = (extension == ".zip");
            ExportDaxFunctions(path, compression);
        }

        public void ExportDaxFunctions(string path, bool compression) {
            var info = DaxMetadataInfo;
            if (compression) {
                string pathJson = string.Format( @".\{0}.json", Path.GetFileNameWithoutExtension(path) );
                Uri uri = PackUriHelper.CreatePartUri(new Uri(pathJson, UriKind.Relative));
                using (Package package = Package.Open(path, FileMode.Create)) {
                    using (TextWriter tw = new StreamWriter(package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream(),Encoding.Unicode)) {
                        tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                        tw.Close();
                    }
                    package.Close();
                }
            }
            else {
                using (TextWriter tw = new StreamWriter(path, false, Encoding.Unicode)) {
                    tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                    tw.Close();
                }
            }
        }

        // TODO: move Versionrequest definition elsewhere?
        public class VersionRequest {
            public string SsasVersion { get; set; }
        }
        internal HttpClient GetHttpClient() {
            var client = new HttpClient();
            
            //Uri _baseUri = new Uri(string.Format("http://localhost:1941/"));
            Uri _baseUri = new Uri(string.Format("http://daxversioningservice.azurewebsites.net/"));
            client.BaseAddress = _baseUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        //
        public void SaveAs()
        {
            // Configure save file dialog box
            var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = this.FileName?? _displayName ,
                    DefaultExt = ".dax",
                    Filter = "DAX documents|*.dax"
                };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Save document 
                FileName = dlg.FileName;
                IsDiskFileName = true;
                _displayName = Path.GetFileName(FileName); 
                Save();
            }
            
        }

        public bool IsDiskFileName { get; set; }

        public void OpenFile()
        {    
            Execute.OnUIThread(() =>
            {
                Task.Run(() =>
                {
                    Execute.OnUIThread(() => { LoadFile(); });
                }).ContinueWith((previousOutput) =>
                {
                    // todo - should we be checking for exceptions in this continuation
                    Execute.OnUIThread(() => { ChangeConnection(); });
                },TaskScheduler.Default).ContinueWith((previousOutput) =>
                {
                    // todo - should we be checking for exceptions in this continuation
                    Execute.OnUIThread(() => { IsDirty = false; });
                },TaskScheduler.Default);
            }) ;
            
        }

        private void LoadState()
        {
            if (!_isLoadingFile) return;
            // we can only load trace watchers if we are connected to a server
            //if (!this.IsConnected) return;

            foreach (var tw in TraceWatchers)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;
                loader.Load(FileName);
            }
            _isLoadingFile = false;
        }

        
        public void LoadFile()
        {
            _isLoadingFile = true;
            _displayName = Path.GetFileName(FileName);
            IsDiskFileName = true;
            if (File.Exists(FileName))
            {
                using (TextReader tr = new StreamReader(FileName, true))
                {
                    // put contents in edit window
                    GetEditor().Text = tr.ReadToEnd();
                    tr.Close();
                }
            }
            else
            {
                OutputError(string.Format("The file '{0}' was not found",FileName));
            }

            LoadState();

            IsDirty = false;
            State = DocumentState.Loaded;
        }
        
        public new string DisplayName
        {
            get { return _displayName + (IsDirty?"*":"") ; }
            set { _displayName = value; }
        }
        
        public void Handle(LoadFileEvent message)
        {
            FileName = message.FileName;
            IsDiskFileName = true;
        }

        public void Handle(CancelQueryEvent message)
        {
            CancelQuery();
        }
        

        public void Handle(ConnectEvent message)
        {
            _logger.Info("In Handle<ConnectEvent>");
            var msg = NewStatusBarMessage("Connecting...");
            
            Task.Run(() =>
                {
                    
                    var cnn = message.PowerPivotModeSelected
                                     ? Host.Proxy.GetPowerPivotConnection(message.ConnectionType,string.Format("Location={0};Extended Properties=\"Location={0}\";Workstation ID={0}", message.WorkbookName))
                                     : new ADOTabularConnection(message.ConnectionString, AdomdType.AnalysisServices);
                    cnn.IsPowerPivot = message.PowerPivotModeSelected;
                    if (message.PowerBIFileName.Length > 0)
                    {
                        cnn.PowerBIFileName = message.PowerBIFileName;
                    }
                    if (Dispatcher.CurrentDispatcher.CheckAccess())
                    {
                        Dispatcher.CurrentDispatcher.Invoke(new System.Action(() => {
                            SetupConnection(message, cnn);
                        }));
                    }
                    else
                    {
                        SetupConnection(message, cnn);
                    }
                    
                }).ContinueWith((antecendant) =>
                    {
                        // todo - should we be checking for exceptions in this continuation
                        var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                        _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this,Databases,activeTrace));//,IsPowerPivotConnection));
                        _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
                        //LoadState();
                        msg.Dispose(); //reset the status message
                    }, TaskScheduler.Default);
            
        }

        private void SetupConnection(ConnectEvent message, ADOTabularConnection cnn)
        {
            if (Connection != null && Connection.State == ConnectionState.Open)
            {
                Connection.Close();
                Connection.Dispose();
            }

            if (cnn != null && cnn.State != ConnectionState.Open) cnn.Open();

            Connection = cnn;
            this.IsPowerPivot = message.PowerPivotModeSelected;
            this.Spid = cnn.SPID;
            this.SelectedDatabase = cnn.Database.Name;
            CurrentWorkbookName = message.WorkbookName;
            Databases = cnn.Databases.ToBindableCollection();

            if (Connection == null)
            { ServerName = "<Not Connected>"; }
            else
            {

                if (Connection.State == ConnectionState.Broken || Connection.State == ConnectionState.Closed)
                {
                    ServerName = "<Not Connected>";
                    Connection = null;
                }
                else
                {
                    if (Connection.IsPowerPivot)
                    {
                        ServerName = "<PowerPivot>";
                        ServerVersion = Connection.ServerVersion;
                    }
                    else
                    {
                        ServerName = Connection.ServerName;
                        ServerVersion = Connection.ServerVersion;
                    }
                }
            }
        }
        

        public BindableCollection<string> Databases { get; private set; }
        public async Task ClearDatabaseCacheAsync()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                currentQueryDetails = new QueryHistoryEvent("", DateTime.Now, this.ServerName, this.SelectedDatabase, this.FileName);
                currentQueryDetails.Status = QueryStatus.Running;
                Connection.Database.ClearCache();
                OutputMessage(string.Format("Evaluating Calculation Script for Database: {0}", SelectedDatabase));
                await ExecuteQueryAsync("EVALUATE ROW(\"BLANK\",0)").ContinueWith((ascendant) =>
                {
                    // todo - should we be checking for exceptions in this continuation
                    sw.Stop();
                    var duration = sw.ElapsedMilliseconds;
                    OutputMessage(string.Format("Cache Cleared for Database: {0}", SelectedDatabase), duration);
                },TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                OutputError(ex.Message);
            }
        }
        public void Handle(CancelConnectEvent message)
        {
            if (Connection == null) return;
            // refresh the other views with the existing connection details
            if (Connection.State == ConnectionState.Open) _eventAggregator.PublishOnUIThread(new UpdateConnectionEvent(Connection));//,IsPowerPivotConnection));
        }

        public IResult GetShutdownTask()
        {
            //if (!IsDirty)
            //{
                ShutDownTraces();
            //}
            return IsDirty ? new ApplicationCloseCheck(this, DoCloseCheck) : null;
        }

        private void ShutDownTraces()
        {
            ResetTracer();
            //foreach (var tw in TraceWatchers)
            //{
            //    if (tw.IsChecked) { tw.IsChecked = false; }
            //}
        }

        protected virtual void DoCloseCheck( Action<bool> callback)
        {
            
        //    var res = MessageBoxEx.Show(Application.Current.MainWindow,
        //        string.Format("\"{0}\" has unsaved changes.\nAre you sure you want to close this document without saving?.",_displayName),
        //        "Unsaved Changes", MessageBoxButton.YesNo
        //        );
            // don't close if the file has unsaved changes
            callback(!IsDirty);
        }

        public void Handle(SelectionChangeCaseEvent message)
        {
            switch (message.ChangeType)
            {
                case ChangeCase.ToUpper: SelectionToUpper();
                    break;
                case ChangeCase.ToLower: SelectionToLower();
                    break;
            }
        }

        public UnitViewModel SizeUnitLabel { get; set; }

        public void Handle(CommentEvent message)
        {
            if (message.CommentSelection)
            {
                CommentSelection();
            }
            else
            {
                UnCommentSelection();
            }
        }

        public DocumentState State { get; set; }

        public string CurrentWorkbookName { get; set; }

        public bool ConnectedToPowerPivot
        {
            get { return Connection.IsPowerPivot; } 
        }

        private string _statusBarMessage = "Ready";
        private string _selectedDatabase;
        public string StatusBarMessage
        {
            get
            { 
                return _statusBarMessage; 
            }
        }
        public string ServerName
        { get; private set;  }

        public IStatusBarMessage NewStatusBarMessage(string message)
        {
            return new StatusBarMessage(this, message);
        }

        internal void SetStatusBarMessage(string message)
        {
            _statusBarMessage = message;
            NotifyOfPropertyChange(() => StatusBarMessage);
        }

        public int Spid { get; private set; }
        public bool IsAdminConnection { get { return Spid != -1 || Connection.ConnectionString.Contains("Roles=") ; } }
        public bool IsPowerPivot {get; private set; }

        private bool _canCopy = true;
        public bool CanCopy { 
            get { return _canCopy; } 
            set { _canCopy = value;
                NotifyOfPropertyChange(() => CanCopy);
            } 
        }
        public void Copy() { this.GetEditor().Copy(); }
        private bool _canCut = true;
        public bool CanCut {get { return _canCut; } 
            set { _canCut = value;
                NotifyOfPropertyChange(() => CanCut);
            } }
        public void Cut() { this.GetEditor().Cut(); }
        private bool _canPaste = true;
        private bool _isLoadingFile;
        public bool CanPaste
        {
            get { return _canPaste; }
            set
            {
                _canPaste = value;
                NotifyOfPropertyChange(() => CanPaste);
            }
        }
        public void Paste() { this.GetEditor().Paste(); }

        public void SetResultsMessage(string message, OutputTargets icon)
        {
            QueryResultsPane.ResultsMessage = message;
            QueryResultsPane.ResultsIcon = icon;
        }

        public FindReplaceDialogViewModel FindReplaceDialog { get; set; }

        #region Highlighting

        //private HighlightDelegate _defaultHighlightFunction;

        private List<HighlightPosition> InternalDefaultHighlightFunction(string text, int startOffset, int endOffset)
        {
            if (string.IsNullOrWhiteSpace(TextToHighlight)) return null; ;
            var list = new List<HighlightPosition>();
            var start = 0;
            var selStart = _editor.SelectionStart;
            var lineSelStart = -1;
            if (selStart >= startOffset && selStart <= endOffset)
            {
                lineSelStart = selStart - startOffset;
            }
            while (true)
            {
                var idx = text.IndexOf(TextToHighlight,start,StringComparison.InvariantCultureIgnoreCase);
                if (idx == -1) break;
                start = idx + 1;
                if (idx == lineSelStart) continue; // skip the currently selected text
                list.Add(new HighlightPosition() { Index = idx, Length = TextToHighlight.Length });
            }
            return list;
        }

        private void SetDefaultHighlightFunction()
        {
            SetHighlightFunction(InternalDefaultHighlightFunction );
        }

        private void SetHighlightFunction(HighlightDelegate highlightFunction)
        {
            _editor.HighlightFunction = highlightFunction;
        }
        #endregion

        public string TextToHighlight { get { return _editor.SelectedText; } }

        public void Find()
        {
            if (!string.IsNullOrWhiteSpace(SelectedText))
            {
                FindReplaceDialog.TextToFind = SelectedText;
            }
            FindReplaceDialog.ShowReplace = false;
            FindReplaceDialog.IsVisible = true;
        }
        public void Replace()
        {
            if (!string.IsNullOrWhiteSpace(SelectedText))
            {
                FindReplaceDialog.TextToFind = SelectedText;
            }
            FindReplaceDialog.ShowReplace = true;
            FindReplaceDialog.IsVisible = true;
        }

        public void Handle(OutputMessage message)
        {
            switch (message.MessageType)
            {
                case MessageType.Error:
                        OutputError(message.Text);
                        break;
                case MessageType.Warning:
                        OutputWarning(message.Text);
                        break;
                case MessageType.Information:
                        OutputMessage(message.Text);
                        break;
            }
        }

        public void Handle(NavigateToLocationEvent message)
        {
            var lineOffset = 0;
            var colOffset = 0;
            var editor = GetEditor();

            if (editor.SelectionLength > 0)
            {
                // clear the selection
                editor.SelectionStart = 0;
                editor.SelectionLength = 0;
            }
            var caret = editor.TextArea.Caret;
            caret.Location = new TextLocation(message.Row + lineOffset, message.Column + colOffset);
            caret.BringCaretToView();

            
            editor.Dispatcher.BeginInvoke( new System.Threading.ThreadStart( () =>
            {
                editor.Focus();
                editor.TextArea.Focus();
                editor.TextArea.TextView.Focus();
                Keyboard.Focus(editor);
            }), DispatcherPriority.Input);
            
        }


        public void FormatQuery()
        {
            var msg = new StatusBarMessage(this, "Formatting Query...");

            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Start");
            int colOffset = 1;
            int rowOffset = 1;
            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Getting Query Text");
            // todo - do I want to disable the editor control while formatting is in progress???
            string qry;
            // if there is a selection send that to DocumentViewModel.com otherwise send all the text
            qry = _editor.SelectionLength == 0 ? _editor.Text : _editor.SelectedText;
            if (_editor.SelectionLength > 0)
            {
                var loc = _editor.Document.GetLocation(_editor.SelectionStart);
                colOffset = loc.Column;
                rowOffset = loc.Line;
            }
            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "About to Call daxformatter.com");

            DaxFormatterProxy.FormatDaxAsync(qry, _options, _eventAggregator).ContinueWith((res) => {
                // todo - should we be checking for exceptions in this continuation
                Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "daxformatter.com call complete");

                try
                {
                    if ((res.Result.errors == null) || (res.Result.errors.Count == 0))
                    {

                        _editor.Dispatcher.Invoke(()=>{
                            _editor.IsReadOnly = true;
                            if (_editor.SelectionLength == 0)
                            {
                                _editor.IsEnabled = false;
                                _editor.Document.BeginUpdate();
                                _editor.Document.Text = res.Result.FormattedDax.TrimEnd();
                                _editor.Document.EndUpdate();
                                _editor.IsEnabled = true;
                            }
                            else
                            {
                    
                                _editor.SelectedText = res.Result.FormattedDax.TrimEnd();
                            }
                            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Query Text updated");
                            OutputMessage("Query Formatted via daxformatter.com");
                        });
                    }
                    else
                    {

                        foreach (var err in res.Result.errors)
                        {
                            // write error 
                            // note: daxformatter.com returns 0 based coordinates so we add 1 to them
                            int errLine = err.line + 1;
                            int errCol = err.column + 1;

                            _editor.Dispatcher.Invoke(() => { 
                                // if the error is at the end of text then we need to move in 1 character
                                var errOffset = _editor.Document.GetOffset(errLine, errCol);
                                if (errOffset == _editor.Document.TextLength && !_editor.Text.EndsWith(" "))
                                {
                                    _editor.Document.Insert(errOffset, " ");
                                }

                                // TODO - need to figure out if more than 1 character should be highlighted
                            
                                OutputError(string.Format("Query ({0}, {1}) {2} ", errLine, errCol, err.message), rowOffset , colOffset);
                                ActivateOutput();
                            });
                            
                            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Error markings set");
                        }
                        
                    }
                }
                catch (Exception ex)
                {
                    var exMsg = ex.Message;
                    if (ex is AggregateException)
                    {
                        exMsg = ex.InnerException.Message;
                    }
                    Log.Error("{Class} {Event} {Exception}", "DocumentViewModel", "FormatQuery", ex.Message);
                    Dispatcher.CurrentDispatcher.Invoke(() => { 
                        OutputError(string.Format("DaxFormatter.com Error: {0}", exMsg)); 
                    });
                }
                finally
                {
                    _editor.Dispatcher.Invoke(() => { 
                        _editor.IsReadOnly = false;
                    });
                    msg.Dispose();
                    Log.Verbose("{class} {method} {end}", "DocumentViewModel", "FormatDax:End");
                }
            },TaskScheduler.Default);
        
        }


        public bool HasDatabaseSchemaChanged()
        {
            try
            {
                if (IsQueryRunning) return false; // if query is running schema cannot have changed (and this connection will be busy with the query)
                if (Connection == null) return false;
                if (!IsConnected && !string.IsNullOrWhiteSpace(ServerName ))
                {
                    Log.Error("{class} {method} {message} ", "DocumentViewModel", "HasDatabaseSchemaChanged", "Connection is not open");
                    OutputError(string.Format("Error Connecting to server: {0}", ServerName));
                    ServerName = string.Empty; // clear the server name so that we don't throw this error again
                    ActivateOutput();
                    // need to reset ribbon buttons if there is an error on the connection
                    NotifyOfPropertyChange(() => IsConnected);
                    NotifyOfPropertyChange(() => CanRunQuery);
                    NotifyOfPropertyChange(() => IsQueryRunning);

                    _eventAggregator.PublishOnUIThread(new ConnectionClosedEvent());
                    return false;
                }
                if (!IsConnected) return false;
                if (Connection.Database == null) return false;
                return Connection.Database.HasSchemaChanged();
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "DocumentViewModel", "HasDatabaseSchemaChanged", ex.Message, ex.StackTrace);
                OutputError(string.Format("Error Connecting to server: {0}", ex.Message));
                ServerName = string.Empty; // clear the server name so that we don't throw this error again
                ActivateOutput();
                // need to reset ribbon buttons if there is an error on the connection
                NotifyOfPropertyChange(() => IsConnected);
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => IsQueryRunning);

                _eventAggregator.PublishOnUIThread(new ConnectionClosedEvent());
                return false;
            }
        }

        internal void RefreshMetadata()
        {
            this.Connection.Refresh();
            this.MetadataPane.RefreshDatabases();// = CopyDatabaseList(this.Connection);
            this.Databases = MetadataPane.Databases;
            this.MetadataPane.ModelList = this.Connection.Database.Models;
            this.MetadataPane.RefreshMetadata();
            //NotifyOfPropertyChange(() => MetadataPane.SelectedModel);
            OutputMessage("Metadata Refreshed");
        }
        private bool _isFocused;
        public bool IsFocused { get { return _isFocused; } set { _isFocused = value;  NotifyOfPropertyChange(()=>IsFocused); } }

        public void Handle(SetSelectedWorksheetEvent message)
        {
            SelectedWorksheet = message.Worksheet;
        }

        private IResultsTarget _selectedTarget;
        public IResultsTarget SelectedTarget
        {
            get { return _selectedTarget; }
        }
        public void Handle(QueryResultsPaneMessageEvent message)
        {
            _selectedTarget = message.Target;
        }

        public bool ServerTimingsChecked
        {
            get { 
                foreach (var tw in _traceWatchers)
                {
                    if (tw is ServerTimesViewModel)
                    {
                        return tw.IsChecked;
                    }
                }
                return false;
                //return _traceWatchers.Select(tw => tw.IsChecked && tw is ServerTimesViewModel).Count() > 0; 
            }
        }

        private ServerTimingDetailsViewModel _serverTimingDetails;
        public ServerTimingDetailsViewModel ServerTimingDetails { 
            get { return _serverTimingDetails; } 
            set { _serverTimingDetails = value;
            NotifyOfPropertyChange(() => ServerTimingDetails);
                } 
        }

        private DaxIntellisenseProvider _intellisenseProvider;
        
        public DaxIntellisenseProvider IntellisenseProvider
        {
            get { return _intellisenseProvider; }
            set
            {
                _intellisenseProvider = value;
            }
        }

        public object UniqueID { get { return _uniqueId; } }

        private int _rowCount = -1;
        private  string _serverVersion = "";
        public int RowCount { 
            get {return _rowCount;} 
            set {_rowCount = value;  NotifyOfPropertyChange(()=>RowCount);}

        }

        public void UpdateSettings()
        {
            var editor = GetEditor();
            if (editor.ShowLineNumbers != _options.EditorShowLineNumbers)
            {
                editor.ShowLineNumbers = _options.EditorShowLineNumbers;
            }
            if (editor.FontFamily.Source != _options.EditorFontFamily)
            {
                editor.FontFamily = new System.Windows.Media.FontFamily( _options.EditorFontFamily);
            }
            if (editor.FontSize != _options.EditorFontSize)
            {
                editor.FontSize = _options.EditorFontSize;
                this.SizeUnitLabel.SetOneHundredPercentFontSize(_options.EditorFontSize);
                this.SizeUnitLabel.StringValue = "100";
            }
            if (_options.EditorEnableIntellisense)
            {
                _editor.EnableIntellisense(IntellisenseProvider);
            }
            else
            {
                _editor.DisableIntellisense();
            }
        }

        public QueryHistoryPaneViewModel QueryHistoryPane { get; set; }

        public string ServerVersion
        {
            get { return _serverVersion; }
            set
            {
                _serverVersion = value;
                NotifyOfPropertyChange(() => ServerVersion);
            }
        }

        public ADOTabular.MetadataInfo.DaxMetadata DaxMetadataInfo
        {
            get {
                return _connection.DaxMetadataInfo;
            }
        }

        public void Handle(ExportDaxFunctionsEvent exportFunctions)
        {
            if (exportFunctions.AutoDelete) PublishDaxFunctions();
            else ExportDaxFunctions();
        }

        public void Handle(CloseTraceWindowEvent message)
        {
            message.TraceWatcher.IsChecked = false;
            ToolWindows.Remove(message.TraceWatcher);
        }

        public void Handle(ShowTraceWindowEvent message)
        {
            ToolWindows.Add(message.TraceWatcher);
        }

        #region ISaveable 
        public FileIcons Icon { get { 
            
            return  !IsDiskFileName || Path.GetExtension(FileName).ToLower() == ".dax" ? FileIcons.Dax : FileIcons.Other; } }
        public string FileAndExtension { get { 
            if (IsDiskFileName)
                return Path.GetFileName(FileName); 
            else
                return DisplayName.TrimEnd('*');
            } 
        }
        public string Folder { get { return IsDiskFileName ? Path.GetDirectoryName(FileName) : ""; } }
        private bool _shouldSave = true;
        public bool ShouldSave
        {
            get { return _shouldSave; }
            set { _shouldSave = value; NotifyOfPropertyChange(() => ShouldSave); }
        }
        public string ExtensionLabel { 
            get {
                var ext = Path.GetExtension(DisplayName).TrimStart('.').TrimEnd('*').ToUpper();
                return ext == "DAX" ? "" : ext;
            } 
        }
        #endregion

        #region Export Analysis Data

        public void ExportAnalysisData()
        {
            if (!IsConnected)
            {
                MessageBoxEx.Show("The active query window is not connected to a data source. You need to be connected to a data source in order to use the export functions option", "Export DAX Functions", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Configure save file dialog box
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = "DaxStudioModelAnalyzer_" + this.SelectedDatabase,
                DefaultExt = ".zip",
                Filter = "Analyzer Data (ZIP)|*.zip|Analyzer Data|*.json"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Save document 
                ExportAnalysisData(dlg.FileName);
            }
        }
        public void ExportAnalysisData(string path)
        {
            string extension = Path.GetExtension(path).ToLower();
            bool compression = (extension == ".zip");
            ExportAnalysisData(path, compression);
        }

        public void ExportAnalysisData(string path, bool compression)
        {
            var info = ModelAnalyzer.Create(_connection);
            if (compression)
            {
                string pathJson = string.Format(@".\{0}.json", Path.GetFileNameWithoutExtension(path));
                Uri uri = PackUriHelper.CreatePartUri(new Uri(pathJson, UriKind.Relative));
                using (Package package = Package.Open(path, FileMode.Create))
                {
                    using (TextWriter tw = new StreamWriter(package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream(), Encoding.Unicode))
                    {
                        tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                        tw.Close();
                    }
                    package.Close();
                }

                // create gz file
                var gzfile = Path.Combine( Path.GetDirectoryName(path), string.Format(@".\{0}.json.gz", Path.GetFileNameWithoutExtension(path)));

                using (FileStream fs = new FileStream(gzfile, FileMode.Create))
                using (GZipStream zipStream = new GZipStream(fs, CompressionMode.Compress, false))
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(info, Formatting.Indented) ?? "")))
                {
                    ms.Position = 0;
                    ms.CopyTo(zipStream);
                }

            }
            else
            {
                using (TextWriter tw = new StreamWriter(path, false, Encoding.Unicode))
                {
                    tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                    tw.Close();
                }
            }
        }
        #endregion
    }
}
