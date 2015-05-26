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
using Microsoft.AnalysisServices;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.IO;
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
        , IHandle<SetSelectedWorksheetEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<UpdateConnectionEvent> 
        , IHandle<NavigateToLocationEvent>
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<OutputMessage>
        , IQueryRunner
        , IHaveShutdownTask
        , IConnection
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

        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDaxStudioHost host, RibbonViewModel ribbon, ServerTimingDetailsViewModel serverTimingDetails)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            _ribbon = ribbon;
            Init(_ribbon);
            ServerTimingDetails = serverTimingDetails;
            _rexQueryError = new Regex(@"^(?:Query \()(?<line>\d+)(?:\s*,\s*)(?<col>\d+)(?:\s*\))(?<err>.*)$",RegexOptions.Compiled);
            _uniqueId = Guid.NewGuid();
        }

        public void Init(RibbonViewModel ribbon)
        {
            
            State = DocumentState.New;        
            var items = new ObservableCollection<UnitComboLib.ViewModel.ListItem>( GenerateScreenUnitList());
            this.SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(), 0);

            // Initialize default Tool Windows
            MetadataPane = new MetadataPaneViewModel(_connection, _eventAggregator, this);
            FunctionPane = new FunctionPaneViewModel(_connection, _eventAggregator);
            DmvPane = new DmvPaneViewModel(_connection, _eventAggregator);
            OutputPane = new OutputPaneViewModel(_eventAggregator);
            QueryResultsPane = new QueryResultsPaneViewModel(_eventAggregator,_host);

            Document = new TextDocument();
            FindReplaceDialog = new FindReplaceDialogViewModel(this.GetEditor());
            _logger = LogManager.GetLog(typeof (DocumentViewModel));
            _selectedTarget = ribbon.SelectedTarget;
            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;

            var t = DaxFormatterProxy.PrimeConnectionAsync();
                        
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
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            _editor = GetEditor();
            if (_editor != null)
            {
                FindReplaceDialog.Editor = _editor;
                SetDefaultHighlightFunction(); 
                _editor.TextArea.Caret.PositionChanged += OnPositionChanged;
                _editor.TextChanged += OnDocumentChanged;
                _editor.PreviewDrop += OnDrop;
                IntellisenseProvider = new DaxIntellisenseProvider(this, _editor);
                _editor.IntellisenseProvider = IntellisenseProvider;
            }
            if (this.State == DocumentState.LoadPending)
            {
                OpenFile();
            }
        }


        private void OnDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            var data = (string)e.Data.GetData(typeof(string));
            InsertTextAtSelection(data);
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
            Log.Debug("{Class} {Event} {@EventArgs}", "DocumentViewModel", "OnDocumentChanged", e);          
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
                if (_tracer == null) // && _connection.Type != AdomdType.Excel)
                {
                    if (_connection.IsPowerPivot)
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionstring} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create RemoteQueryTrace", _connection.ConnectionString, _connection.Type.ToString(), Host.Proxy.Port);
                        _tracer = QueryTraceEngineFactory.CreateRemote(_connection, GetTraceEvents(TraceWatchers),Host.Proxy.Port);
                    }
                    else
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionstring} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create LocalQueryTrace", _connection.ConnectionString, _connection.Type.ToString());
                        _tracer = QueryTraceEngineFactory.CreateLocal(_connection, GetTraceEvents(TraceWatchers));
                    }
                    //_tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                    _tracer.TraceError += TracerOnTraceError;
                }
                return _tracer;
            }
        }

        private void TracerOnTraceError(object sender, string e)
        {
            OutputError(e);
        }

        private List<TraceEventClass> GetTraceEvents(BindableCollection<ITraceWatcher> traceWatchers)
        {
            var events = new List<TraceEventClass>();
            foreach (var tw in traceWatchers)
            {
                foreach (var e in tw.MonitoredEvents)
                {
                    if (!events.Contains(e))
                    {
                        events.Add(e);
                    }
                }
            }
            return events;
        }
        // TODO - remove this method
        //private void TracerOnTraceCompleted(object sender, DaxStudioTraceEventArgs[] capturedEvents)
        //{
        //    TracerOnTraceCompleted(sender, capturedEvents.ToList<DaxStudioTraceEventArgs>());
        //}

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
                        QueryResultsPane
                    });
            }
        }

        private DocumentView GetDocumentView()
        {
            return (DocumentView) GetView();
        }

        private DAXEditor.DAXEditor GetEditor()
        {
            DocumentView v = GetDocumentView();
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
            IsQueryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            QueryResultsPane.IsBusy = false;
            foreach(var tw in TraceWatchers)
            {
                if (tw.IsChecked) tw.QueryCompleted(isCancelled);
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
                    _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this, Databases));
                    NotifyOfPropertyChange(() => SelectedDatabase);
                }
            }
        }
        public string ConnectionString { get { return _connection.ConnectionString; } }

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
        }

        protected override void OnActivate()
        {
            Log.Debug("{Class} {Event} {Document}", "DocumentViewModel", "OnActivate", this.DisplayName);
            _logger.Info("In OnActivate");
            base.OnActivate();
            _eventAggregator.Subscribe(this);
            _eventAggregator.Subscribe(QueryResultsPane);
            _ribbon.SelectedTarget = _selectedTarget;
            var loc = Document.GetLocation(0);
            //SelectedWorksheet = QueryResultsPane.SelectedWorksheet;
            if (HasDatabaseSchemaChanged())
            {
                RefreshMetadata();
                OutputMessage("Model schema change detected - Metadata refreshed");
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
/*            if (Host.Proxy.WorkbookName != this.CurrentWorkbookName)
            {
                // TODO - active workbook has changed need to 
                MessageBox.Show("active workbook has changed");
            }
 */ 
        }

        public override void CanClose(Action<bool> callback)
        {
            DoCloseCheck(callback);
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
                _eventAggregator.PublishOnUIThread(new ConnectionChangedEvent(_connection)); 
            } 
        }

        
        private void UpdateConnections(ADOTabularConnection value,string selectedDatabase)
        {
            _logger.Info("In UpdateConnections");

            Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "DocumentViewModel", "UpdateConnections"
                , value == null ? "<null>" : value.ConnectionString
                , selectedDatabase);          
            using (NewStatusBarMessage("Refreshing Metadata..."))
            {
                if (value == null) return;
                _connection = value;

                // enable/disable traces depending on the current connection
                foreach (var traceWatcher in TraceWatchers)
                {
                    traceWatcher.CheckEnabled(this);   
                }
                MetadataPane.Connection = _connection;
                FunctionPane.Connection = _connection;
                DmvPane.Connection = _connection;
            }
        }

        private Task UpdateConnectionsAsync(ADOTabularConnection value, string selectedDatabase)
        {
            Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "DocumentViewModel", "UpdateConnectionsAsync", value.ConnectionString,selectedDatabase);          
            return Task.Factory.StartNew(() =>
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
            _eventAggregator.PublishOnUIThread(new ConnectionPendingEvent());
            Log.Debug("{class} {method} {event}", "DocumentViewModel", "ChangeConnection", "start");          
            var connStr = Connection == null ? string.Empty : Connection.ConnectionString;
            var msg = NewStatusBarMessage("Checking for PowerPivot model...");
            Log.Debug("{class} {method} {Event} ", "DocumentViewModel", "ChangeConnection", "starting async call to Excel");          
                
            Task.Factory.StartNew(() => Host.Proxy.HasPowerPivotModel).ContinueWith((x) =>
            {
                try
                {
                    Log.Debug("{class} {method} {Event} ", "DocumentViewModel", "ChangeConnection", "recieved async result from Excel");
                    bool hasPpvtModel = x.Result;
                            
                    Log.Debug("{class} {method} Has PowerPivotModel: {hasPpvtModel} ", "DocumentViewModel", "ChangeConnection", hasPpvtModel);
                    msg.Dispose();
                    Execute.BeginOnUIThread(() =>
                    {
                        var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator, hasPpvtModel, this);

                        _windowManager.ShowDialog(connDialog, settings: new Dictionary<string, object>
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
            });
            
        }

        public async Task<bool> HasPowerPivotModelAsync()
        {
           return await Task.Factory.StartNew(() => Host.Proxy.HasPowerPivotModel );
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
                qry = DaxHelper.PreProcessQuery(qry);
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
        public DataTable ExecuteQuery(string daxQuery)
        {
            try
            {
                var c = Connection;
                foreach (var tw in TraceWatchers)
                {
                    if (tw.IsChecked)
                    {
                        tw.IsBusy = true;
                    }
                }
                _timer = new Timer(300);
                _timer.Elapsed += _timer_Elapsed;
                _timer.Start();
                _queryStopWatch = new Stopwatch();
                _queryStopWatch.Start();
                var dt = c.ExecuteDaxQueryDataTable(daxQuery);
                
                return dt;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                OutputError(e.Message);
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
                QueryCompleted();

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
                    OutputWarning("Query Cancelled");
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
            return Task.Factory.StartNew(CancelQuery);
        }

        public Task<DataTable> ExecuteQueryAsync(string daxQuery)
        {
            return Task.Factory.StartNew(() => ExecuteQuery(daxQuery));
        }

        public void Handle(RunQueryEvent message)
        {
            // if we can't run the query then do nothing 
            // (ribbon button will be disabled, but the following check blocks hotkey requests)
            if (!CanRunQuery) return;

            IsQueryRunning = true;
            //SelectedWorksheet = message.SelectedWorksheet;
            NotifyOfPropertyChange(()=>CanRunQuery);
            //RegisterTraceWatchers();  // CHECK - is this required now that we are starting the trace as soon as one watcher is enabled...?
            RunQueryInternal(message);
        }

        private void RunQueryInternal(RunQueryEvent message)
        {
            var msg = NewStatusBarMessage("Running Query...");
            _eventAggregator.PublishOnUIThread(new QueryStartedEvent());
            message.ResultsTarget.OutputResultsAsync(this).ContinueWith((antecendant) =>
            {
                IsQueryRunning = false;
                NotifyOfPropertyChange(() => CanRunQuery);
                
                _eventAggregator.PublishOnUIThread(new QueryFinishedEvent());
                msg.Dispose();               
            });
        }
        #endregion

        public StatusBarViewModel StatusBar { get; set; }
        
        //public void RegisterTraceWatchers()
        //{
        //    if (TraceWatchers == null)
        //        return;
        //    foreach (var tw in TraceWatchers)
        //    {
        //        if (tw.IsEnabled)
        //        {
        //            Tracer.RegisterTraceWatcher(tw);
        //        }
        //    }
        //}


        public DataTable ResultsTable
        {
            get { return QueryResultsPane.ResultsDataTable; }
            set { QueryResultsPane.ResultsDataTable = value; }
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
            _editor.Dispatcher.Invoke(() =>
            {
                _editor.DisplayErrorMarkings(row, column, 1, error);
            });
                
            OutputPane.AddError(error,row,column);
        }

        #endregion

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
            InsertTextAtSelection(message.TextToSend);
        }

        public void Handle(UpdateConnectionEvent message)
        {
            _logger.Info("In Handle<UpdateConnectionEvent>");
            Log.Debug("{Class} {Event} {ConnectionString} DB: {Database}", "DocumentViewModel", "Handle:UpdateConnectionEvent", message.Connection == null? "<null>":message.Connection.ConnectionString, message.DatabaseName);
            
            UpdateConnections(message.Connection, message.DatabaseName);
            
            _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this,Databases));     
        }


        public void Handle(TraceWatcherToggleEvent message)
        {
            Log.Verbose("{Class} {Event} TraceWatcher:{TraceWatcher} IsActive:{IsActive}", "DocumentViewModel", "Handle(TraceWatcherToggleEvent", message.TraceWatcher.ToString(), message.IsActive);
            if (message.IsActive)
            {
                ToolWindows.Add(message.TraceWatcher);

                // synch the ribbon buttons and the server timings pane
                if (message.TraceWatcher is ServerTimesViewModel && message.TraceWatcher.IsChecked)
                {
                    ((ServerTimesViewModel)message.TraceWatcher).ServerTimingDetails = ServerTimingDetails;
                }

                // spin up trace if one is not already running
                if (Tracer.Status != QueryTraceStatus.Started
                    && Tracer.Status != QueryTraceStatus.Starting)
                {
                    _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Starting));
                    OutputMessage("Waiting for Trace to start");
                    Tracer.StartAsync();
                }
            }
            else
            {
                ToolWindows.Remove(message.TraceWatcher);
                foreach (var tw in TraceWatchers)
                {
                    if (tw.IsChecked) return;
                }
                _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Starting));
                OutputMessage("Stopping Trace");
                // spin down trace is no tracewatchers are active
                Tracer.Stop();
                ResetTracer();
                OutputMessage("Trace Stopped");
                _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Stopped));
            }
        }

        private void ResetTracer()
        {
            Tracer.Dispose();
            _tracer = null;
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
                foreach (var tw in ToolWindows)
                {
                    var saver = tw as ISaveState;
                    if (saver != null)
                    {
                        saver.Save(FileName);
                    }
                }

                IsDirty = false;
                NotifyOfPropertyChange(() => DisplayName);
            }
        }

        public void SaveAs()
        {
            // Configure save file dialog box
            var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = this.FileName=="" ? _displayName:FileName ,
                    DefaultExt = ".dax",
                    Filter = "DAX documents (.dax)|*.dax"
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
                    Execute.OnUIThread(() => { ChangeConnection(); });
                }).ContinueWith((previousOutput) =>
                {
                    Execute.OnUIThread(() => { IsDirty = false; });

                });
            }) ;
            
        }

        private void LoadState()
        {
            if (!_isLoadingFile) return;
            // we can only load trace watchers if we are connected to a server
            if (!this.IsConnected) return;

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
            using (TextReader tr = new StreamReader(FileName, true))
            {
                // put contents in edit window
                GetEditor().Text = tr.ReadToEnd();
                tr.Close();
            }
            
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
            
            Task.Factory.StartNew(() =>
                {
                    
                    var cnn = message.PowerPivotModeSelected
                                     ? Host.Proxy.GetPowerPivotConnection(message.ConnectionType)
                                     : new ADOTabularConnection(message.ConnectionString, AdomdType.AnalysisServices);
                    if (Dispatcher.CurrentDispatcher.CheckAccess())
                    {
                        Dispatcher.CurrentDispatcher.Invoke(new System.Action(() => { 
                            Connection = cnn;
                            Connection.IsPowerPivot = message.PowerPivotModeSelected;
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
                                { ServerName = "<Not Connected>"; }
                                else
                                {
                                    if (Connection.IsPowerPivot)
                                    {
                                        ServerName = string.Format("<PowerPivot>  {0}", Connection.ServerVersion);
                                    }
                                    else
                                    {
                                    ServerName = string.Format("{0}  {1}",Connection.ServerName , Connection.ServerVersion) ;
                                    }
                                }
                            }

                        }));
                    }
                    else
                    {
                        Connection = cnn;
                        Connection.IsPowerPivot = message.PowerPivotModeSelected;
                        this.IsPowerPivot = message.PowerPivotModeSelected;
                        this.Spid = cnn.SPID;
                        this.SelectedDatabase = cnn.Database.Name;
                        CurrentWorkbookName = message.WorkbookName;
                        Databases = cnn.Databases.ToBindableCollection();
                    }
                    
                }).ContinueWith((antecendant) =>
                    {
                        _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this,Databases));//,IsPowerPivotConnection));
                        _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
                        LoadState();
                        msg.Dispose(); //reset the status message
                    });
            
        }
        

        public BindableCollection<string> Databases { get; private set; }
        public void ClearDatabaseCache()
        {
            try
            {
                var sw = Stopwatch.StartNew();

                Connection.Database.ClearCache();
                OutputMessage(string.Format("Evaluating Calculation Script for Database: {0}", SelectedDatabase));
                ExecuteQueryAsync("EVALUATE ROW(\"BLANK\",0)").ContinueWith((ascendant) =>
                {
                    sw.Stop();
                    var duration = sw.ElapsedMilliseconds;
                    OutputMessage(string.Format("Cache Cleared for Database: {0}", SelectedDatabase), duration);
                });
            }
            catch (Exception ex)
            {
                OutputError(ex.Message);
            }
        }
        public void Handle(CancelConnectEvent message)
        {
            // refresh the other views with the existing connection details
            _eventAggregator.PublishOnUIThread(new UpdateConnectionEvent(Connection));//,IsPowerPivotConnection));
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
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsChecked) { tw.IsChecked = false; }
            }
        }

        protected virtual void DoCloseCheck( Action<bool> callback)
        {
            
            var res = MessageBoxEx.Show(Application.Current.MainWindow,
                string.Format("\"{0}\" has unsaved changes.\nAre you sure you want to close this document without saving?.",_displayName),
                "Unsaved Changes", MessageBoxButton.YesNo
                );
            //if (res== MessageBoxResult.Yes)
            //{
            //    ShutDownTraces();
            //}
            callback(res == MessageBoxResult.Yes);
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
        public bool IsAdminConnection { get { return Spid != -1; } }
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
                // need to position this in relation to the current selection...
                var selstart = _editor.Document.GetLocation(_editor.SelectionStart);
                lineOffset = selstart.Line;
                colOffset = selstart.Column;
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
            

            //InsertTextAtSelection("><");
        }
        /*
        public async void FormatQuery_()
        {
            var msg = new StatusBarMessage(this,"Formatting Query...");

            await Model.DaxFormatterProxy.FormatQuery(this, _editor).ContinueWith((data) =>
                {
                    msg.Dispose();
                });
            
        }
        */

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

            Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "About to Call daxformatter.com");

            DaxFormatterProxy.FormatDaxAsync(qry).ContinueWith((res) => {
                Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "daxformatter.com call complete");

                try
                {
                    if (res.Result.errors == null)
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
                                var loc = _editor.Document.GetLocation(_editor.SelectionStart);
                                colOffset = loc.Column;
                                rowOffset = loc.Line;
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
                            int errLine = err.line + rowOffset;
                            int errCol = err.column + colOffset;

                            _editor.Dispatcher.Invoke(() => { 
                                // if the error is at the end of text then we need to move in 1 character
                                var errOffset = _editor.Document.GetOffset(errLine, errCol);
                                if (errOffset == _editor.Document.TextLength && !_editor.Text.EndsWith(" "))
                                {
                                    _editor.Document.Insert(errOffset, " ");
                                }

                                // TODO - need to figure out if more than 1 character should be highlighted
                            
                                OutputError(string.Format("(Ln {0}, Col {1}) {2} ", errLine, errCol, err.message), err.line + rowOffset, err.column + colOffset);
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
            });
        
        }


        public bool HasDatabaseSchemaChanged()
        {
            try
            {
                if (IsQueryRunning) return false; // if query is running schema cannot have changed (and this connection will be busy with the query)
                if (Connection == null) return false;
                if (!IsConnected && !string.IsNullOrWhiteSpace(ServerName ))
                {
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
            this.MetadataPane.Refresh();
            OutputMessage("Metadata Refreshed");
        }
        private bool _isFocused;
        public bool IsFocused { get { return _isFocused; } set { _isFocused = value; NotifyOfPropertyChange(()=>IsFocused); } }

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
    }
}
