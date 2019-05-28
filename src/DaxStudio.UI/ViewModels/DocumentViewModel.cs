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
using DaxStudio.Common;
using GongSolutions.Wpf.DragDrop;
using System.ComponentModel;
using Xceed.Wpf.AvalonDock;
using CsvHelper;

namespace DaxStudio.UI.ViewModels
{



    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof (Screen))]
    [Export(typeof (DocumentViewModel))]
    public class DocumentViewModel : Screen
        , IDaxDocument
        , IHandle<CancelConnectEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<CommentEvent>
        , IHandle<ConnectEvent>
        , IHandle<CloseTraceWindowEvent>
        , IHandle<CopyConnectionEvent>
        , IHandle<DefineMeasureOnEditor>
        , IHandle<ExportDaxFunctionsEvent>
        , IHandle<LoadFileEvent>
        , IHandle<NavigateToLocationEvent>
        , IHandle<OutputMessage>
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<RunQueryEvent>
        , IHandle<SelectionChangeCaseEvent>
        , IHandle<SendTextToEditor>
        , IHandle<SelectedModelChangedEvent>
        , IHandle<SetSelectedWorksheetEvent>
        , IHandle<ShowTraceWindowEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<UpdateConnectionEvent>
        , IHandle<DockManagerLoadLayout>
        , IHandle<DockManagerSaveLayout>
        , IHandle<UpdateGlobalOptions>
        , IDropTarget
        , IQueryRunner
        , IHaveShutdownTask
        , IConnection
        , ISaveable
    {
        // Changed from the original Unicode - if required we could make this an optional setting in future
        // but UTF8 seems to be the most sensible default going forward
        private readonly Encoding DefaultFileEncoding = Encoding.UTF8; 

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
        private Guid _autoSaveId =  Guid.NewGuid();
        private DocumentViewModel _sourceDocument;
        private ISettingProvider SettingProvider { get; }
        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDaxStudioHost host, RibbonViewModel ribbon, ServerTimingDetailsViewModel serverTimingDetails , IGlobalOptions options, ISettingProvider settingProvider)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            _ribbon = ribbon;
            SettingProvider = settingProvider;
            ServerTimingDetails = serverTimingDetails;
            _rexQueryError = new Regex(@"^(?:Query \()(?<line>\d+)(?:\s*,\s*)(?<col>\d+)(?:\s*\))(?<err>.*)$|Line\s+(?<line>\d+),\s+Offset\s+(?<col>\d+),(?<err>.*)$", RegexOptions.Compiled | RegexOptions.Multiline);
            _uniqueId = Guid.NewGuid();
            _options = options;
            Init(_ribbon);
        }

        public void Init(RibbonViewModel ribbon)
        {
            
            State = DocumentState.New;        
            var items = new ObservableCollection<UnitComboLib.ViewModel.ListItem>( ScreenUnitsHelper.GenerateScreenUnitList());
            
            SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(_options.EditorFontSizePx), 0);
            SizeUnitLabel.PropertyChanged += SizeUnitLabelChanged;
            
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
            FindReplaceDialog = new FindReplaceDialogViewModel(_eventAggregator);
            _logger = LogManager.GetLog(typeof (DocumentViewModel));
            _selectedTarget = ribbon.SelectedTarget;
            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;

            var t = DaxFormatterProxy.PrimeConnectionAsync(_options, _eventAggregator);

        }

        private void SizeUnitLabelChanged(object sender, PropertyChangedEventArgs e)
        {
            _eventAggregator.PublishOnUIThreadAsync(new SizeUnitsUpdatedEvent((UnitViewModel)sender));
        }

        internal void LoadAutoSaveFile(Guid autoSaveId)
        {
            _isLoadingFile = true;
            var text = AutoSaver.GetAutoSaveText(autoSaveId);
            // put contents in edit window
            var editor = GetEditor();
            
            editor.Dispatcher.Invoke(() => {
                editor.Text = text;
            });
                
            LoadState();

            State = DocumentState.Loaded;

            _eventAggregator.PublishOnUIThread(new RecoverNextAutoSaveFileEvent());
        }




        public override void TryClose(bool? dialogResult = null)
        {
            base.TryClose(dialogResult);
        }
        
        public Guid AutoSaveId { get { return _autoSaveId; } set { _autoSaveId = value; } }

        private DAXEditor.DAXEditor _editor;
        /// <summary>
        /// Initialization that requires a reference to the editor control needs to happen here
        /// </summary>
        /// <param name="view"></param>
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            _editor = GetEditor();

            // TODO - if theme is dark increase brightness of syntax highlights
            //_editor.ChangeColorBrightness(1.25);
            _editor.SetSyntaxHighlightColorTheme(_options.Theme);

            IntellisenseProvider = new DaxIntellisenseProvider(this, _editor, _eventAggregator, _options);
            UpdateSettings();
            if (_editor != null)
            {
                FindReplaceDialog.Editor = _editor;
                SetDefaultHighlightFunction(); 
                _editor.TextArea.Caret.PositionChanged += OnPositionChanged;
                _editor.TextChanged += OnDocumentChanged;
                _editor.PreviewDrop += OnDrop;
                _editor.PreviewDragEnter += OnDragEnter;

                _editor.OnPasting += OnPasting;
                
            }
            switch (this.State)
            {
                case DocumentState.LoadPending:
                    OpenFile();
                    break;
                case DocumentState.RecoveryPending:
                    LoadAutoSaveFile(AutoSaveId);
                    break;
            }

            if (_sourceDocument != null)
            {
                var cnn = _sourceDocument.Connection;
                _eventAggregator.PublishOnUIThread(new ConnectEvent(
                    cnn.ConnectionStringWithInitialCatalog,
                    cnn.IsPowerPivot,
                    cnn.IsPowerPivot ? _sourceDocument.FileName : "",
                    "",
                    cnn.IsPowerPivot ? "" : cnn.FileName,
                    cnn.ServerType)
                    { DatabaseName = cnn.Database.Name});

                _sourceDocument = null;
            }
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            
            try
            {
                string content = e.DataObject.GetData("UnicodeText", true) as string;
                if (_editor.SelectionLength > 0)
                {
                    // if we have a selection - delete the currently selected text
                    _editor.SelectedText = "";
                    _editor.SelectionLength = 0;
                }
                // strip out unicode "non-breaking" space characters \u00A0 and replace with standard spaces
                // the SSAS engine does not understand "non-breaking" spaces and throws a syntax error    
                _editor.Document.Insert(_editor.CaretOffset, content.Replace('\u00A0', ' '));

                // tell the paste event that it has been handled
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while Pasting: {message}", ex.Message);
                OutputError($"Error while Pasting: {ex.Message}");
            }
            finally
            {
                e.CancelCommand();
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
            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, "OnDragOver Fired"));
            IntellisenseProvider?.CloseCompletionWindow();
            if (e.Data.Equals(string.Empty))
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            //Log.Debug("{Class} {Event} {@EventArgs}", "DocumentViewModel", "OnDocumentChanged", e);          
            _logger.Info("In OnDocumentChanged");
            IsDirty = this._editor.Text.Length > 0;
            LastModifiedUtcTime = DateTime.UtcNow;
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
                    // Don't add DirectQueryEvent if the server does not support direct query session filters 
                    // and the options has not been enabled in the options screen
                    if (e == DaxStudioTraceEventClass.DirectQueryEnd && !_options.TraceDirectQuery && !_connection.ServerVersion.SupportsDirectQueryFilters())  continue;

                    // if the server version does not support Aggregate Table Events do not add them
                    if (e == DaxStudioTraceEventClass.AggregateTableRewriteQuery && !_connection.ServerVersion.SupportsAggregateTables()) continue;

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
            return v?.daxEditor;
        }

        private DockingManager GetDockManager()
        {
            DocumentView v = (DocumentView)GetView();
            return v?.Document;
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
            if (currentQueryDetails != null)
            {
                currentQueryDetails.ClientDurationMs = _queryStopWatch.ElapsedMilliseconds;
                currentQueryDetails.RowCount = ResultsDataSet.RowCounts();
            }
            bool svrTimingsEnabled = false;
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsChecked) tw.QueryCompleted(isCancelled, currentQueryDetails);
                var svrTimings = tw as ServerTimesViewModel;
                if (svrTimings != null) { svrTimingsEnabled = true; }

            }
            if (!svrTimingsEnabled && currentQueryDetails != null)
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
                return Connection?.Database?.Name;
                //if (_selectedDatabase == null && IsConnected)
                //{
                //    _selectedDatabase = Connection.Database.Name;
                //}
                //return _selectedDatabase;
            }
            //set
            //{
            //    if (value != _selectedDatabase)
            //    {
            //        _selectedDatabase = value;
            //        Connection.ChangeDatabase(value);
            //        var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
            //        _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this, Databases,activeTrace));
            //        NotifyOfPropertyChange(() => SelectedDatabase);

            //        // set metadata pane SelectedDatabase
            //        MetadataPane.SelectedDatabase = MetadataPane.DatabasesView.Where(db => db.Name == _selectedDatabase).FirstOrDefault();

            //    }
            //}
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

        internal void CloseIntellisenseWindows()
        {
            IntellisenseProvider?.CloseCompletionWindow();
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

            // exit here if we are not in a state to run a query
            // means something is using the connection like
            // either a query is running or a trace is starting
            if (CanRunQuery)
            { 
                try
                {
                    if (ShouldAutoRefreshMetadata())
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

        public ADOTabularConnection Connection
        {
            get { return _connection; }
            internal set
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


        private void UpdateConnections(ADOTabularConnection value, string selectedDatabase)
        {
            _logger.Info("In UpdateConnections");
            OutputPane.AddInformation("Establishing Connection");
            Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "DocumentViewModel", "UpdateConnections"
                , value == null ? "<null>" : value.ConnectionString
                , selectedDatabase);
            if (value != null && value.State != ConnectionState.Open)
            {
                OutputPane.AddWarning(string.Format("Connection for server {0} is not open", value.ServerName));
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
                    traceWatcher.CheckEnabled(this, activeTrace);
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
                       Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "UpdateConnections", "Error Updating SyntaxHighlighting: " + ex.Message);
                   }
               });
            }
            if (Connection.Databases.Count == 0) {
                var msg = $"No Databases were found in the when connecting to {Connection.ServerName} ({Connection.ServerType})"
                + (Connection.ServerType=="PBI Desktop"?"\nIf your Power BI File is using a Live Connection please connect directly to the source model instead.": "");
                OutputWarning(msg);
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

        public string SelectedText { get {
                var editor = GetEditor();
                if (editor == null) return "";
                return editor.SelectedText;
            }
        }
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
                        var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator, hasPpvtModel, this, SettingProvider);

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
                return Connection.State == ConnectionState.Open;
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

        public bool IsTraceChanging
        {
            get { return _traceChanging; }
            set
            {
                _traceChanging = value;
                NotifyOfPropertyChange(() => IsTraceChanging);
                NotifyOfPropertyChange(() => CanRunQuery);
            }
        }

        public DmvPaneViewModel DmvPane { get; private set; }

        public OutputPaneViewModel OutputPane { get; set; }

        public QueryResultsPaneViewModel QueryResultsPane { get; set; }

        public QueryInfo QueryInfo { get; set; }

        private DialogResult PreProcessQuery(bool injectEvaluate, bool injectRowFunction)
        {
            // merge in any parameters
            QueryInfo = new QueryInfo(EditorText, injectEvaluate, injectRowFunction, _eventAggregator);
            DialogResult paramDialogResult = DialogResult.Skip;
            if (QueryInfo.NeedsParameterValues)
            {
                var paramDialog = new QueryParametersDialogViewModel(this, QueryInfo);


                _windowManager.ShowDialogBox(paramDialog, settings: new Dictionary<string, object>
                        {
                            { "WindowStyle", WindowStyle.None},
                            { "ShowInTaskbar", false},
                            { "ResizeMode", ResizeMode.NoResize},
                            { "Background", System.Windows.Media.Brushes.Transparent},
                            { "AllowsTransparency",true}

                        });
                paramDialogResult = paramDialog.DialogResult;
            }
                        
            return paramDialogResult;
        }

        public string QueryText
        {
            get {
                return QueryInfo.ProcessedQuery;
            }
        }

        public string EditorText
        {
            get
            {
                string qry = string.Empty;
                if (!Dispatcher.CurrentDispatcher.CheckAccess())
                {
                    Dispatcher.CurrentDispatcher.Invoke(new Func<string>(() =>
                        { qry = GetQueryTextFromEditor();
                            
                            return qry;
                        }));
                } else
                    qry = GetQueryTextFromEditor();

                
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
            var queryProcessor = new QueryInfo(txt, false, false, _eventAggregator);
            txt = queryProcessor.ProcessedQuery;
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
            
            if (editor == null)
            {
                Log.Error("{class} {method} Unable to get a reference to the editor control", "DocumentViewModel", "Undo");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Undo: Unable to get a reference to the editor control"));
                return;
            }

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

            if (editor == null)
            {
                Log.Error("{class} {method} Unable to get a reference to the editor control", "DocumentViewModel", "Redo");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Redo: Unable to get a reference to the editor control"));
                return;
            }

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

        public Stopwatch QueryStopWatch
        {
            get
            {
                if (_queryStopWatch == null) _queryStopWatch = new Stopwatch();
                return _queryStopWatch;
            }
        }

        public void RefreshElapsedTime()
        {
            NotifyOfPropertyChange(() => ElapsedQueryTime);
        }

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
            get { return _queryStopWatch == null ? "" : _queryStopWatch.Elapsed.ToString(Constants.StatusBarTimerFormat); }
            
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
            if (message.RunStyle.ClearCache) await ClearDatabaseCacheAsync();
            RunQueryInternal(message);
            
        }

        private void RunQueryInternal(RunQueryEvent message)
        {
            var msg = NewStatusBarMessage("Running Query...");

            // somehow people are getting into this method while the connection is not open
            // even though the CanRun state should be false so this is a double check
            if (Connection.State != ConnectionState.Open)
            {
                Log.Error("{class} {method} Attempting run a query on a connection which is not open", "DocumentViewMode", "RunQueryInternal");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "You cannot run a query on a connection which is not open"));
                _eventAggregator.PublishOnUIThread(new ConnectionChangedEvent(Connection, this));
                return;
            }



            if (PreProcessQuery(message.RunStyle.InjectEvaluate,message.RunStyle.InjectRowFunction) == DialogResult.Cancel)
            {
                IsQueryRunning = false;
            }
            else
            {
                _eventAggregator.PublishOnUIThread(new QueryStartedEvent());
                
                currentQueryDetails = CreateQueryHistoryEvent(QueryText);

                message.ResultsTarget.OutputResultsAsync(this).ContinueWith((antecendant) =>
                {
                // todo - should we be checking for exceptions in this continuation
                IsQueryRunning = false;
                    NotifyOfPropertyChange(() => CanRunQuery);

                // if the server times trace watcher is not active then just record client timings
                if (!TraceWatchers.OfType<ServerTimesViewModel>().First().IsChecked && currentQueryDetails != null)
                    {
                        currentQueryDetails.ClientDurationMs = _queryStopWatch.ElapsedMilliseconds;
                        currentQueryDetails.RowCount = ResultsDataSet.RowCounts();
                        _eventAggregator.PublishOnUIThreadAsync(currentQueryDetails);
                    }

                    _eventAggregator.PublishOnUIThread(new QueryFinishedEvent());
                    msg.Dispose();
                }, TaskScheduler.Default);
            } 
        }

        private IQueryHistoryEvent CreateQueryHistoryEvent(string queryText)
        {
            QueryHistoryEvent qhe = null;
            try
            {
                
                //var queryText = includeQueryText ? this.QueryText : "";
                qhe = new QueryHistoryEvent(queryText, DateTime.Now, this.ServerName, this.SelectedDatabase, this.FileName);
                qhe.Status = QueryStatus.Running;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating QueryHistory details");
                OutputWarning("Error saving query details to history pane");
            }
            return qhe;
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
            get { return !IsQueryRunning && !IsTraceChanging && IsConnected; }
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
            OutputError(error, double.NaN);
        }

        public void OutputError(string error, double durationMs)
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
                OutputPane.AddError(error, durationMs);
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

        private void InsertTextAtSelection(string text, bool selectInsertedText)
        {
            
            var editor = GetEditor();
            var startOffset = editor.CaretOffset;

            if (editor.SelectionLength == 0)
            {
                editor.Document.Insert(editor.SelectionStart, text);
            }
            else
            {
                editor.SelectedText = text;
                startOffset = editor.SelectionStart;
            }

            editor.Focus();

            if (selectInsertedText)
            {
                editor.Select(startOffset, text.Length);
            }
            
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
                        MetadataPane.ChangeDatabase( message.DatabaseName);
                        OutputMessage($"Current Database changed to '{message.DatabaseName}'");
                    }
                    else
                        OutputWarning($"Could not switch to the '{message.DatabaseName}' database");
            }
            InsertTextAtSelection(message.TextToSend, message.RunQuery);

            if (!message.RunQuery) return;  // exit here if we don't want to run the selected text
            
            //run the query
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(SelectedTarget));
            
            // un-select text
            _editor.SelectionLength = 0;
            _editor.SelectionStart = _editor.Text.Length;
            
        }

        public void Handle(DefineMeasureOnEditor message)
        {
            DefineMeasureOnEditor(message.MeasureName, message.MeasureExpression);
        }

        //RRomano: Should this be on DaxEditor?

        const string MODELMEASURES_BEGIN = "---- MODEL MEASURES BEGIN ----";
        const string MODELMEASURES_END = "---- MODEL MEASURES END ----";
        // private Regex defineMeasureRegex = new Regex(@"(?<=DEFINE)((.|\n)*?)(?=EVALUATE|\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex defineMeasureRegex_ModelMeasures = new Regex(@"(?<=DEFINE)((.|\n)*?)(?=" + MODELMEASURES_END + @")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private Regex defineMeasureRegex_DefineOnly = new Regex(@"(?<=DEFINE([\s\t])*?)(\w(.|\n)*?)(?=\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void DefineMeasureOnEditor(string measureName, string measureExpression)
        {
            var editor = GetEditor();

            // TODO (Marco 2018-08-04)
            //
            // We include a section ---- MODEL MEASURES ----
            // where we include all the measures
            // the section ends with ---- END MODEL MEASURES ----
            // so we append the measures at the end of that section

            // Try to find the DEFINE statements

            var currentText = editor.Text;                      
            
            var measureDeclaration = string.Format("MEASURE {0} = {1}", measureName, measureExpression);
            // TODO - expand measure expression and generate other measures here!!


            // If found then add the measure inside the DEFINE statement, if not then just paste the measure expression
            if (defineMeasureRegex_ModelMeasures.IsMatch(currentText))
            {                
                currentText = defineMeasureRegex_ModelMeasures.Replace(currentText, (m) =>
                {
                    var measuresText = new StringBuilder(m.Groups[1].Value);

                    measuresText.AppendLine(measureDeclaration);

                    return measuresText.ToString();


                });

                editor.Text = currentText;

                editor.Focus();
            }
            else if (defineMeasureRegex_DefineOnly.IsMatch(currentText))
            {
                currentText = defineMeasureRegex_DefineOnly.Replace(currentText, (m) => 
                {

                    var newSection = new StringBuilder();
                    newSection.AppendLine();
                    newSection.AppendLine(MODELMEASURES_BEGIN);
                    newSection.AppendLine(measureDeclaration);
                    newSection.AppendLine(MODELMEASURES_END);
                    newSection.AppendLine();
                    newSection.Append(m.Groups[0].Value);
                    
                    return newSection.ToString();

                });

                editor.Text = currentText;

                editor.Focus();
            }
            else
            {
                measureDeclaration = string.Format("DEFINE {1}{0}{1}", measureDeclaration, System.Environment.NewLine);

                InsertTextAtSelection(measureDeclaration,false);
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
            IsTraceChanging = true;
            var otherTracesRunning = false;
           
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
            ResetTracer();
            OutputMessage("Trace Stopped");
            _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Stopped));
            TraceWatchers.EnableAll();
            IsTraceChanging = false;
        }

        private void EnableTrace(ITraceWatcher watcher)
        {
            try
            {
                IsTraceChanging = true;
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
                    t.StartAsync(_options.TraceStartupTimeout).ContinueWith((p) =>
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
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "EnableTrace", "Error while enabling trace");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error enabling Trace: " + ex.Message));
            }
            finally
            {
                IsTraceChanging = false;
            }
        }

        private void ResetTracer()
        {
            if (Tracer != null)
            {
                Tracer?.Stop();
                Tracer?.Dispose();
                _tracer = null;
            }
        }

        internal string AutoSaveFileName {
            get { return Path.Combine(Environment.ExpandEnvironmentVariables(Constants.AutoSaveFolder), $"{AutoSaveId.ToString()}.dax"); }
        }

        // writes the file out to a temp folder in case of crashes or unplanned restarts
        internal async Task AutoSave()
        {

            var fileName = AutoSaveFileName;
            AutoSaver.EnsureDirectoryExists(fileName);
            var editor = GetEditor();
            // if the editor is null that means that the view has not been loaded fully yet,
            // which means that this is a recovered autosave file and we don't need to re-save
            // it unless the user activates this document and makes a change
            if (editor == null) return;

            using (TextWriter tw = new StreamWriter(fileName, false, DefaultFileEncoding))
            {
                
                var text = string.Empty;
                editor.Dispatcher.Invoke(() =>
                {
                    text = editor.Text;
                });
                await tw.WriteAsync(text);
                tw.Close();
            }
            LastAutoSaveUtcTime = DateTime.UtcNow;
        }

        internal void DeleteAutoSave()
        {
            try
            {
                File.Delete(AutoSaveFileName);
                // trigger an autosave of the workspace to remove this file from the index
                _eventAggregator.PublishOnUIThreadAsync(new AutoSaveEvent());
            }
            catch { }
        }
        // this may be overkill, but we track UTC save and modified times just in case a user changes timezones
        public DateTime LastAutoSaveUtcTime { get; private set; }
        public DateTime LastModifiedUtcTime { get; private set; }

        public void Save()
        {
            if (!IsDiskFileName)
                SaveAs();
            else
            {
                try
                {
                    using (TextWriter tw = new StreamWriter(FileName, false, DefaultFileEncoding))
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
                    DeleteAutoSave();
                }
                catch (Exception ex)
                {
                    // catch and report any errors while trying to save
                    Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "Save", ex.Message);
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error saving: {ex.Message}"));
                }
                
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
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions",ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error Publishing DAX Functions: " + ex.Message));
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
                try
                {
                    ExportDaxFunctions(dlg.FileName);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "ExportDaxFunctions", ex.Message);
                    OutputError($"Error Exporting Functions: {ex.Message}");
                }
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
                    Execute.OnUIThread(() => { LoadFile(FileName); });
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

        
        public void LoadFile(string fileName)
        {
            FileName = fileName;
            _isLoadingFile = true;
            _displayName = Path.GetFileName(FileName);
            IsDiskFileName = true;
            if (File.Exists(FileName))
            {
                try
                {
                    using (TextReader tr = new StreamReader(FileName, true))
                    {
                        // put contents in edit window
                        GetEditor().Text = tr.ReadToEnd();
                        tr.Close();
                    }
                }
                catch (Exception ex)
                {
                    // todo - need to test what happens if we enter this catch block
                    //        
                    _isLoadingFile = false;
                    Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "LoadFile", ex.Message);
                    OutputError($"Error opening file: {ex.Message}");
                }
            }
            else
            {
                Log.Warning("{class} {method} {message}", "DocumentViewModel", "LoadFile", $"File not found {FileName}");
                OutputError(string.Format("The file '{0}' was not found",FileName));
            }

            LoadState();

            IsDirty = false;
            State = DocumentState.Loaded;
        }
        
        public new string DisplayName
        {
            get { return _displayName;  }
            set { _displayName = value;
                NotifyOfPropertyChange(() => DisplayName);
            }
        }
        
        public void Handle(LoadFileEvent message)
        {
            FileName = message.FileName;
            IsDiskFileName = true;
            LoadFile(message.FileName);
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
                                     ? Host.Proxy.GetPowerPivotConnection(message.ConnectionType,string.Format("Location=\"{0}\";Workstation ID=\"{0}\";", message.WorkbookName))
                                     : new ADOTabularConnection(message.ConnectionString, AdomdType.AnalysisServices);
                    cnn.IsPowerPivot = message.PowerPivotModeSelected;
                    cnn.ServerType = message.ServerType;

                    if (message.PowerBIFileName.Length > 0)
                    {
                        cnn.FileName = message.PowerBIFileName;
                    }
                    if (message.WorkbookName.Length > 0)
                    {
                        cnn.FileName = message.WorkbookName;
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
                    
                }).ContinueWith((taskResult) =>
                    {
                        if (taskResult.IsFaulted)
                        {
                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Connecting: {taskResult?.Exception?.InnerException?.Message}"));
                        }
                        else
                        {
                            // todo - should we be checking for exceptions in this continuation
                            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                            _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this, Databases, activeTrace));//,IsPowerPivotConnection));
                            _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
                            //LoadState();
                        }
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
            //this.SelectedDatabase = cnn.Database.Name;
            CurrentWorkbookName = message.WorkbookName;

            //SelectedDatabase = message.DatabaseName;

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

                    if (!string.IsNullOrEmpty(message.DatabaseName))
                    {
                        MetadataPane.ChangeDatabase(message.DatabaseName);
                    }
                    else
                    {
                        // Check that the selected database is set to the current database for the connection
                        // this prevents issues when changing the connection on an existing window where
                        // the database did not match the table list in the metadata pane
                        if (MetadataPane.SelectedDatabase.Caption != Connection.Database.Name)
                        {
                            MetadataPane.ChangeDatabase(Connection.Database.Name);
                        }
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
                currentQueryDetails = CreateQueryHistoryEvent(string.Empty);
                
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
            // make sure any other view models know that this document is the active one
            _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));

            if (Connection == null) return;
            // refresh the other views with the existing connection details
            if (Connection.State == ConnectionState.Open) _eventAggregator.PublishOnUIThread(new UpdateConnectionEvent(Connection));
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

        private DocumentState _documentState;
        public DocumentState State { get { return _documentState; }
            set { _documentState = value;
                // if we are recovering an autosave file then we
                // know that it's already been autosaved before
                // so we can set the last saved time to UtcNow
                if (_documentState == DocumentState.RecoveryPending)
                    LastAutoSaveUtcTime = DateTime.UtcNow;
            }
        }

        public string CurrentWorkbookName { get; set; }

        public bool ConnectedToPowerPivot
        {
            get { return Connection.IsPowerPivot; } 
        }

        private string _statusBarMessage = "Ready";
        //private string _selectedDatabase;
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
        public bool IsAdminConnection => Spid != -1 || Connection.Properties.ContainsKey("roles") || Connection.Properties.ContainsKey("EffectiveUserName") || Connection.IsPowerBIXmla;
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
        public GotoLineDialogViewModel GotoLineDialog { get; set; }

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

        public void GotoLine()
        {

            Log.Debug("{class} {method} {event}", "DocumentViewModel", "GotoLine", "start");
            
            try
            {
                // create a gotoLineDialog view model                     
                var gotoLineDialog = new GotoLineDialogViewModel(this._editor);

                // show the dialog
                _windowManager.ShowDialogBox(gotoLineDialog, settings: new Dictionary<string, object>
                                {
                                    {"Top", 40},
                                    { "WindowStyle", WindowStyle.None},
                                    { "ShowInTaskbar", false},
                                    { "ResizeMode", ResizeMode.NoResize},
                                    { "Background", System.Windows.Media.Brushes.Transparent},
                                    { "AllowsTransparency",true}
                                });
            }
            catch (Exception ex)
            {
                // if the task throws an exception the "real" exception is usually in the innerException
                var innerMsg = ex.Message;
                if (ex.InnerException != null) innerMsg = ex.InnerException.Message;
                Log.Error("{class} {method} {message}", "DocumentViewModel", "GotoLine", innerMsg);
                OutputError(innerMsg);
            }
        }

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
                    if (message.DurationMs > 0) OutputMessage(message.Text,message.DurationMs);
                    else OutputMessage(message.Text);
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


        public void FormatQuery( bool formatAlternateStyle ) 
        {
            using (var msg = new StatusBarMessage(this, "Formatting Query..."))
            {

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


                ServerDatabaseInfo info = new Model.ServerDatabaseInfo();
                if (_connection != null && _connection.State == ConnectionState.Open)
                {
                    var serverName = _connection.IsPowerPivot | _connection.IsPowerBIorSSDT ? _connection.FileName : _connection.ServerName;
                    var databaseName = _connection.IsPowerPivot | _connection.IsPowerBIorSSDT ? _connection.FileName : _connection.Database?.Name;
                    try
                    {
                        info.ServerName = serverName ?? "";
                        info.ServerEdition = _connection.ServerEdition ?? "";
                        info.ServerType = _connection.ServerType ?? "";
                        info.ServerMode = _connection.ServerMode ?? "";
                        info.ServerLocation = _connection.ServerLocation ?? "";
                        info.ServerVersion = _connection.ServerVersion ?? "";
                        info.DatabaseName = databaseName ?? "";
                        info.DatabaseCompatibilityLevel = _connection.Database?.CompatibilityLevel ?? "";
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "{class} {method} Unable to get server details for daxformatter call", "DocumentViewModel", "FormatQuery");
                    }
                }

                if (qry.Trim().Length == 0) return; // no query text to format so exit here

                DaxFormatterProxy.FormatDaxAsync(qry, info, _options, _eventAggregator, formatAlternateStyle ).ContinueWith((res) =>
                {
                    // todo - should we be checking for exceptions in this continuation
                    Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "daxformatter.com call complete");

                    try
                    {
                        if ((res.Result.errors == null) || (res.Result.errors.Count == 0))
                        {

                            _editor.Dispatcher.Invoke(() =>
                            {
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

                                _editor.Dispatcher.Invoke(() =>
                                {
                                    // if the error is past the last line of the document
                                    // move back to the last character of the last line
                                    if (errLine > _editor.LineCount)
                                    {
                                        errLine = _editor.LineCount;
                                        errCol = _editor.Document.Lines[errLine - 1].TotalLength + 1;
                                    }
                                    // if the error is at the end of text then we need to move in 1 character
                                    var errOffset = _editor.Document.GetOffset(errLine, errCol);
                                    if (errOffset == _editor.Document.TextLength && !_editor.Text.EndsWith(" "))
                                    {
                                        _editor.Document.Insert(errOffset, " ");
                                    }

                                    // TODO - need to figure out if more than 1 character should be highlighted

                                    OutputError(string.Format("Query ({0}, {1}) {2} ", errLine, errCol, err.message), rowOffset, colOffset);
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
                        Dispatcher.CurrentDispatcher.Invoke(() =>
                        {
                            OutputError(string.Format("DaxFormatter.com Error: {0}", exMsg));
                        });
                    }
                    finally
                    {
                        _editor.Dispatcher.Invoke(() =>
                        {
                            _editor.IsReadOnly = false;
                        });
                        msg.Dispose();
                        Log.Verbose("{class} {method} {end}", "DocumentViewModel", "FormatDax:End");
                    }
                }, TaskScheduler.Default);
            }
        }


        public bool ShouldAutoRefreshMetadata()
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
                if (!Connection.ShouldAutoRefreshMetadata(_options)) return false;

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
            try
            {
                this.Connection.Refresh();
                this.MetadataPane.RefreshDatabases();// = CopyDatabaseList(this.Connection);
                this.Databases = MetadataPane.Databases;
                this.MetadataPane.ModelList = this.Connection.Database.Models;
                this.MetadataPane.RefreshMetadata();
                //NotifyOfPropertyChange(() => MetadataPane.SelectedModel);
                OutputMessage("Metadata Refreshed");
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    msg = ex.Message;
                }
                OutputError("Error Refreshing Metadata: " + ex.Message);
            }
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
            
            if (editor == null) return;

            if (editor.ShowLineNumbers != _options.EditorShowLineNumbers)
            {
                editor.ShowLineNumbers = _options.EditorShowLineNumbers;
            }
            if (editor.FontFamily.Source != _options.EditorFontFamily)
            {
                editor.FontFamily = new System.Windows.Media.FontFamily( _options.EditorFontFamily);
            }
            if (editor.FontSize != _options.EditorFontSizePx)
            {
                editor.FontSize = _options.EditorFontSizePx;
                this.SizeUnitLabel.SetOneHundredPercentFontSize(_options.EditorFontSizePx);
                this.SizeUnitLabel.StringValue = "100";
            }
            
            /*
                * MARCO 2018-07-17 - How to set the font family and size of the result grid?
            var result = QueryResultsPane;
            if (result.FontFamily.Source != _options.ResultFontFamily)
            {
                result.FontFamily = new System.Windows.Media.FontFamily(_options.ResultFontFamily);
            }
            if (result.FontSize != _options.ResultFontSize)
            {
                result.FontSize = _options.ResultFontSize;
                this.SizeUnitLabel.SetOneHundredPercentFontSize(_options.ResultFontSize);
                this.SizeUnitLabel.StringValue = "100";
            }
            */
            if (_options.EditorEnableIntellisense)
            {
                editor.EnableIntellisense(IntellisenseProvider);
            }
            else
            {
                editor.DisableIntellisense();
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

        public void Handle(DockManagerLoadLayout message)
        {
            try
            {
                var dm = this.GetDockManager();
                if (message.RestoreDefault)
                {
                    dm.RestoreDefaultLayout();
                    OutputMessage("Default Window Layout Restored");
                }
                else
                {
                    dm.LoadLayout();
                    OutputMessage("Window Layout Loaded");
                }
            }
            catch (Exception ex)
            {
                OutputError($"Error Loading Window Layout: {ex.Message}");
            }
        }

        public void Handle(DockManagerSaveLayout message)
        {
            try { 
                var dm = this.GetDockManager();
                dm.SaveLayout();
                OutputMessage("Window Layout Saved.");
            }
            catch (Exception ex)
            {
                OutputError($"Error Saving Window Layout: {ex.Message}");
            }
        }

        public void PreviewMouseLeftButtonUp()
        {
            // if it's open close the completion window when 
            // the left mouse button is clicked
            _editor.DisposeCompletionWindow();
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
        private bool _traceChanging;

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

        //private Guid _autoSaveRecoveryId = Guid.Empty;
        //public Guid AutoSaveRecoveryId { get { return _autoSaveRecoveryId; } 
        //    set {
        //        _autoSaveRecoveryId = value;
        //        // we are recovering an autosave file, so it's already been autosaved
        //        LastAutoSaveUtcTime = DateTime.UtcNow;
        //    }
        //}
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
                FileName = "DaxStudioModelMetrics_" + this.SelectedDatabase,
                DefaultExt = ".vpax",
                Filter = "Analyzer Data (vpax)|*.vpax|Analyzer Data (vpa)|*.vpa"
                //Filter = "Vertipaq Analyzer Data File (vpa)|*.vpa"
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
            using (var msg = new StatusBarMessage(this, "Exporting Model Metrics"))
            {
                string extension = Path.GetExtension(path).ToLower();
                
                try
                {
                    ExportAnalysisData(path, extension);
                    OutputMessage("Model Metrics exported successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} Error Exporting Metrics", "DocumentViewModel", "ExportAnalysisData");
                    var exMsg = ex.GetAllMessages();
                    OutputError("Error exporting metrics: " + exMsg);
                }
            }
        }

        public void ExportAnalysisData(string path, string extension)
        {
            // Generate the data required for Vertipaq Analyzer
            var info = ModelAnalyzer.Create(_connection);

            //if (extension == ".vpa")
            //{
            //    // create gz compressed file
            //    //var gzfile = Path.Combine(Path.GetDirectoryName(path), string.Format(@".\{0}.json.gz", Path.GetFileNameWithoutExtension(path)));
            //    var gzfile = path;
            //    using (FileStream fs = new FileStream(gzfile, FileMode.Create))
            //    using (GZipStream zipStream = new GZipStream(fs, CompressionMode.Compress, false))
            //    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(info, Formatting.Indented) ?? "")))
            //    {
            //        ms.Position = 0;
            //        ms.CopyTo(zipStream);
            //    }
            //}
            //else
            //{
                // create zipped .vpax file
                //Uri uri = PackUriHelper.CreatePartUri(new Uri("DaxStudioModelMetrics.json", UriKind.Relative));
                //using (Package package = Package.Open(path, FileMode.Create))
                //{
                //    using (TextWriter tw = new StreamWriter(package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream(), DefaultFileEncoding))
                //    {
                //        tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                //        tw.Close();
                //    }
                //    package.Close();
                //}

                // create zipped .vpax2 file with individual csv files

                // setup CSVHelper to create tab delimited files
                CsvHelper.Configuration.Configuration config = new CsvHelper.Configuration.Configuration
                {
                    Delimiter = "\t"
                };

                using (Package package = Package.Open(path, FileMode.Create))
                {
                    foreach (DataTable dt in info.Tables)
                    {
                        Uri uri2 = PackUriHelper.CreatePartUri(new Uri($"{dt.TableName}.txt", UriKind.Relative));
                        using (StreamWriter sw = new StreamWriter(package.CreatePart(uri2, "application/text", CompressionOption.Maximum).GetStream(), DefaultFileEncoding))
                        using (CsvWriter csv = new CsvWriter(sw,config))
                        {

                            // Write columns
                            foreach (DataColumn column in dt.Columns)
                            {
                                csv.WriteField(column.ColumnName);
                            }
                            csv.NextRecord();

                            // Write row values
                            foreach (DataRow row in dt.Rows)
                            {
                                for (var i = 0; i < dt.Columns.Count; i++)
                                {
                                    csv.WriteField(row[i]);
                                }
                                csv.NextRecord();
                            }

                        }
                    }
                    package.Close();
                }

            //}  

            // write uncompressed json data
            //using (TextWriter tw = new StreamWriter(path, false, Encoding.Unicode))
            //{
            //    tw.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
            //    tw.Close();
            //}
            
        }

        internal void AppendText(string paramXml)
        {
            var editor = this.GetEditor();

            if (editor.Dispatcher.CheckAccess())
            {
                editor.AppendText(paramXml);
            }
            else
            {
                editor.Dispatcher.Invoke(() => {
                    editor.AppendText(paramXml);
                });

            }
            
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, "OnDragOver Fired"));
            IntellisenseProvider?.CloseCompletionWindow();
            if (dropInfo.Data.Equals(string.Empty))
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            return;
        }

        public void OnDragEnter(object sender, System.Windows.DragEventArgs e)
        {
            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, "OnDragEnter Fired"));
            IntellisenseProvider?.CloseCompletionWindow();
        }

        public void Handle(CopyConnectionEvent message)
        {
            _sourceDocument = message.SourceDocument;
        }

        public void Handle(UpdateGlobalOptions message)
        {
            UpdateTheme();
        }


        public void UpdateTheme()
        {
            NotifyOfPropertyChange(() => AvalonDockTheme);
            _editor?.SetSyntaxHighlightColorTheme(_options.Theme);
        }
        public void Handle(SelectedModelChangedEvent message)
        {
            // if there is not a running trace exit here
            if (_tracer == null) return;
            if (_tracer.Status != QueryTraceStatus.Started ) return;

            // reconnect any running traces to pick up the initial catalog property
            try
            {
                IsTraceChanging = true;
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Reconfiguring trace for database: {message.Document.SelectedDatabase}"));
                _tracer.Update(message.Document.SelectedDatabase);
            }
            finally
            {
                IsTraceChanging = false;
            }
        }
        #endregion

        public Xceed.Wpf.AvalonDock.Themes.Theme AvalonDockTheme { get {

                if (_options.Theme == "Dark") return new Theme.MonotoneTheme();
                else return null; // new Xceed.Wpf.AvalonDock.Themes.GenericTheme();
            }
        }
    }
}
