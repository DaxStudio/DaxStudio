﻿using ADOTabular;
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
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<UpdateConnectionEvent> 
        , IHandle<NavigateToLocationEvent>
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

        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager, IEventAggregator eventAggregator, IDaxStudioHost host)
        {
            Init(windowManager, eventAggregator);
            _host = host;
            State = DocumentState.New;
              
        }

        public void Init(IWindowManager windowManager, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
            var items = new ObservableCollection<UnitComboLib.ViewModel.ListItem>( GenerateScreenUnitList());
            this.SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(), 0);

            // Initialize default Tool Windows
            MetadataPane = new MetadataPaneViewModel(_connection, _eventAggregator);
            FunctionPane = new FunctionPaneViewModel(_connection, _eventAggregator);
            DmvPane = new DmvPaneViewModel(_connection, _eventAggregator);
            OutputPane = new OutputPaneViewModel(_eventAggregator);
            QueryResultsPane = new QueryResultsPaneViewModel();
            Document = new TextDocument();
            FindReplaceDialog = new FindReplaceDialogViewModel(this.GetEditor());
            _logger = LogManager.GetLog(typeof (DocumentViewModel));

            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;
            NotifyOfPropertyChange(() => SelectedWorksheet);
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
             //   _editor.DragOver += OnDragOver;
             //   _editor.AllowDrop = true;
                //_editor.Drop += OnDrop;
                _editor.PreviewDrop += OnDrop;
             //   _editor.DragEnter += OnDragEnter;

             //   _editor.TextArea.Drop += OnDrop;
             //   _editor.TextArea.AllowDrop = true;
                
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
    

        private QueryTrace _tracer;

        public QueryTrace Tracer
        {
            get
            {
                if (_tracer == null && _connection.Type != AdomdType.Excel)
                {
                    _tracer = new QueryTrace(_connection, this);
                    _tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                }
                return _tracer;
            }
        }

        private void TracerOnTraceCompleted(object sender, EventArgs e)
        {
        //    _tracer.Stop();
        }

        private void TracerOnTraceStarted(object sender, TraceStartedEventArgs e)
        {
            Log.Debug("{Class} {Event} {@TraceStartedEventArgs}", "DocumentViewModel", "TracerOnTraceStarted", e);
            Execute.OnUIThread(() => { 
                OutputMessage("Query Trace Started");
                _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Started));
            }); 
        }

        private void TracerOnTraceEvent(object sender, TraceEventArgs traceEventArgs)
        {
            foreach (var tw in Tracer.CheckedTraceWatchers)
            {
                tw.ProcessEvent(traceEventArgs);
            }
        }

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
            QueryResultsPane.Activate();
        }

        public void ActivateOutput()
        {
            OutputPane.Activate();
        }

        public void QueryCompleted()
        {
            IsQueryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
        }

        public IDaxStudioHost Host { get { return _host; } }
        public string SelectedWorksheet { get; set; }
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
                    using (NewStatusBarMessage("Refreshing Metadata..."))
                    {
                        Connection.ChangeDatabase(value);
                        UpdateConnections(Connection, value);
                    }
                    _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this));
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
        }

        protected override void OnActivate()
        {
            Log.Debug("{Class} {Event} {Document}", "DocumentViewModel", "OnActivate", this.DisplayName);          
            _logger.Info("In OnActivate");
            base.OnActivate();
            _eventAggregator.Subscribe(this);
            var loc = Document.GetLocation(0);
            
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

        private ADOTabularConnection Connection
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
            using (NewStatusBarMessage("Connecting..."))
            {
                if (value == null) return;
                _connection = value;

                // enable/disable traces depending on the current connection
                foreach (var traceWatcher in TraceWatchers)
                {
                    //TODO - can we enable traces on PowerPivot
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
            Log.Debug("{Class} {Event}", "DocumentViewModel", "ChangeConnection");          
            var connStr = Connection == null ? string.Empty : Connection.ConnectionString;
            var msg = NewStatusBarMessage("Checking for PowerPivot model...");
            
                // todo - check for PowerPivot model
                //Execute.BeginOnUIThread(()=>
                    Task.Factory.StartNew(() => Host.Proxy.HasPowerPivotModel).ContinueWith((x) =>
                    {
                        bool hasPpvtModel = x.Result;
                        msg.Dispose();
                        Execute.BeginOnUIThread(() =>
                        {
                            var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator, hasPpvtModel,this);
                            
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
                //    )
                );
            
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
                //_eventAggregator.PublishOnUIThread(new QueryFinishedEvent());
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
                    _eventAggregator.PublishOnUIThread(new QueryFinishedEvent());
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
            SelectedWorksheet = message.SelectedWorksheet;
            NotifyOfPropertyChange(()=>CanRunQuery);
            RegisterTraceWatchers();
            if (Tracer != null && Tracer.CheckedTraceWatchers.Count > 0)
            {
                using (var msg1 = NewStatusBarMessage("Waiting for Trace to start..."))
                {
                    // TODO - only run the query after the trace starts
                    //while (Tracer.Status != QueryTraceStatus.Started)
                    //{
                    //    System.Threading.Thread.Sleep(150);
                    //}
                    RunQueryInternal(message);
                }
                
            }
            else
            {
                RunQueryInternal(message);

            }
            

        }

        private void RunQueryInternal(RunQueryEvent message)
        {
            var msg = NewStatusBarMessage("Running Query...");

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
        
        public void RegisterTraceWatchers()
        {
            if (TraceWatchers == null)
                return;
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsEnabled)
                {
                    Tracer.RegisterTraceWatcher(tw);
                }
            }
        }


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
            OutputPane.AddError(error);
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
            using (NewStatusBarMessage("Refreshing Metadata..."))
            {
                UpdateConnections(message.Connection, message.DatabaseName);
            }
            _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this));     
        }


        public void Handle(TraceWatcherToggleEvent message)
        {
            Log.Verbose("{Class} {Event} TraceWatcher:{TraceWatcher} IsActive:{IsActive}", "DocumentViewModel", "Handle(TraceWatcherToggleEvent", message.TraceWatcher.ToString(), message.IsActive);
            if (message.IsActive)
            {
                ToolWindows.Add(message.TraceWatcher);
                // todo - spin up trace if one is not already running
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
                OutputMessage("Trace Stopped");
                _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Stopped));
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
                }).ContinueWith((previous) =>
                {
                    Execute.OnUIThread(() => { ChangeConnection(); });
                }).ContinueWith((previous) =>
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
            
            /*
            foreach (var tw in ToolWindows)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;
                loader.Load(FileName);
            }
             */ 
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
                                     ? Host.Proxy.GetPowerPivotConnection()
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
                            Databases = CopyDatabaseList(cnn);

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
                        Databases = CopyDatabaseList(cnn);
                    }
                    
                }).ContinueWith((antecendant) =>
                    {
                        _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this));//,IsPowerPivotConnection));
                        _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
                        LoadState();
                        msg.Dispose(); //reset the status message
                    });
            
        }
        
        private SortedSet<string> CopyDatabaseList(ADOTabularConnection cnn)
        {
            var ss = new SortedSet<string>();
            foreach (var dbname in cnn.Databases)
            { ss.Add(dbname); }
            return ss;
        }
        public SortedSet<string> Databases { get; private set; }
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
            if (!IsDirty)
            {
                ShutDownTraces();
            }
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
            if (res== MessageBoxResult.Yes)
            {
                ShutDownTraces();
            }
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

        private string _statusBarMessage;
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
            
            if (_editor.SelectionLength > 0)
            {
                // need to position this in relation to the current selection...
                var selstart = _editor.Document.GetLocation(_editor.SelectionStart);
                lineOffset = selstart.Line;
                colOffset = selstart.Column;
            }
            var caret = _editor.TextArea.Caret;
            caret.Location = new TextLocation(message.Row + lineOffset, message.Column + colOffset);
            caret.BringCaretToView();
        }

        public async void FormatQuery()
        {
            using (var m = new StatusBarMessage(this,"Formatting Query..."))
            { 
                await Model.DaxFormatterProxy.FormatQuery(this,_editor );
            }
        }

    }

}