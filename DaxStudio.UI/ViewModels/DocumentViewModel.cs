using System;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Views;
using GongSolutions.Wpf.DragDrop;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.AnalysisServices;
using Microsoft.Win32;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof (Screen))]
    [Export(typeof (DocumentViewModel))]
    public class DocumentViewModel : Screen
                                     , IHandle<RunQueryEvent>
                                     , IHandle<SaveDocumentEvent>
                                     , IHandle<SendTextToEditor>
                                     , IHandle<UpdateConnectionEvent> // ,IDropTarget
                                     , IHandle<TraceWatcherToggleEvent>
                                     , IHandle<LoadFileEvent>
                                     , IHandle<CancelQueryEvent>
                                     , IHandle<OutputInformationMessageEvent>
                                     , IHandle<ApplicationActivatedEvent>
                                     , IHandle<ConnectEvent>
                                     , IHandle<CancelConnectEvent>
                                     , IHandle<SelectionChangeCaseEvent>
                                     , IQueryRunner
                                     , IHaveShutdownTask

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

        }

        public void Init(IWindowManager windowManager, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;

            // Initialize default Tool Windows
            MetadataPane = new MetadataPaneViewModel(_connection, _eventAggregator);
            FunctionPane = new FunctionPaneViewModel(_connection, _eventAggregator);
            DmvPane = new DmvPaneViewModel(_connection, _eventAggregator);
            OutputPane = new OutputPaneViewModel();
            QueryResultsPane = new QueryResultsPaneViewModel();
            Document = new TextDocument();
//            StatusBar = new StatusBarViewModel(_eventAggregator);

            _logger = LogManager.GetLog(typeof (DocumentViewModel));

            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;
            NotifyOfPropertyChange(() => SelectedWorksheet);
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            var e = GetEditor();
            if (e != null)
            {
                e.TextArea.Caret.PositionChanged += OnPositionChanged;
                e.TextChanged += OnDocumentChanged;
            }
        }

        private void OnDocumentChanged(object sender, EventArgs e)
        {
            _logger.Info("In OnDocumentChanged");
            IsDirty = true;
            NotifyOfPropertyChange(() => IsDirty);
            NotifyOfPropertyChange(() => DisplayName);
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            var caret = sender as Caret;
            if (caret != null)
                _eventAggregator.Publish(new EditorPositionChangedMessage(caret.Column, caret.Line));
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
                    _tracer = new QueryTrace(_connection);
                    _tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                }
                return _tracer;
            }
        }

        private void TracerOnTraceCompleted(object sender, EventArgs e)
        {
            _tracer.Stop();
        }

        private void TracerOnTraceStarted(object sender, TraceStartedEventArgs e)
        {
            Execute.OnUIThread(() => e.ResultsTarget.OutputResults(this));
        }

        private void TracerOnTraceEvent(object sender, TraceEventArgs traceEventArgs)
        {
            foreach (var tw in Tracer.CheckedTraceWatchers)
            {
                tw.ProcessEvent(traceEventArgs);
            }
        }

        [ImportMany(typeof (ITraceWatcher))]
        public BindableCollection<ITraceWatcher> TraceWatchers
        {
            get { return _traceWatchers ?? (_traceWatchers = new BindableCollection<ITraceWatcher>()); }
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
            _queryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
        }

        public IDaxStudioHost Host { get { return _host; } }
        public string SelectedWorksheet { get; set; }
        public string ConnectionString { get { return _connection.ConnectionString; } }

        public MetadataPaneViewModel MetadataPane
        {
            get { return _metadataPane; }
            set { _metadataPane = value; }
        }

        public FunctionPaneViewModel FunctionPane { get; private set; }


        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
        }

        protected override void OnActivate()
        {
            _logger.Info("In OnActivate");
            base.OnActivate();
            _eventAggregator.Subscribe(this);
            var loc = Document.GetLocation(0);
            _eventAggregator.Publish(new EditorPositionChangedMessage(loc.Column, loc.Line));
            _eventAggregator.Publish(new ActivateDocumentEvent(this));
        }

        public override void CanClose(Action<bool> callback)
        {
            DoCloseCheck(callback);
            /*
            //base.CanClose(callback);
            if (IsDirty)
            {
                var result = MessageBox.Show("You have unsaved changes. Do you wish to close without saving", 
                                             "Unsaved Changes",
                                             MessageBoxButton.YesNo, 
                                             MessageBoxImage.Question,
                                             MessageBoxResult.No);
                callback(result == MessageBoxResult.Yes);
            }
            else
            {
                callback(true);
            }
             */ 
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
            set
            {
                if (_connection == value)
                    return;
                var m = new StatusBarMessage("Connecting...");
                
                UpdateConnections(value,"");
                _eventAggregator.Publish(new ConnectionChangedEvent(_connection)); 
                
                /*
                Execute.BeginOnUIThread(() => { 
                UpdateConnectionsAsync(value, "").ContinueWith((antecedant) =>
                    { _eventAggregator.Publish(new ConnectionChangedEvent(_connection)); });
                  
                } );
            */
        } 
        }
        
        
        private void UpdateConnections(ADOTabularConnection value,string selectedDatabase)
        {
            _logger.Info("In UpdateConnections");
            using (new StatusBarMessage("Connecting..."))
            {
                if (value == null) return;
                if (_connection != null)
                {
                    if (value.Database.Name == _connection.Database.Name
                        && (selectedDatabase == "" || value.Database.Name != selectedDatabase)
                        && value.ServerName == _connection.ServerName) return;
                }

                _connection = value;

                foreach (var traceWatcher in TraceWatchers)
                {
                    traceWatcher.IsEnabled = (_connection.Type == AdomdType.AnalysisServices);
                }
                MetadataPane.Connection = _connection;
                FunctionPane.Connection = _connection;
                DmvPane.Connection = _connection;
                //StatusBar.Connection = _connection;
            }
        }

        private Task UpdateConnectionsAsync(ADOTabularConnection value, string selectedDatabase)
        {
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
            var connStr = Connection == null ? string.Empty : Connection.ConnectionString;
            var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator);
            _windowManager.ShowDialog(connDialog);
            /*
            try
            {
                Connection = connDialog.Connection;
                ConnectionError = "";
            }
            catch (Exception ex)
            {
                ConnectionError = ex.Message;
            }
             */ 
        }
             

        public string ConnectionError { get; set; }

        public bool IsConnected
        {
            get { return Connection != null; }
        }

        public DmvPaneViewModel DmvPane { get; private set; }

        public OutputPaneViewModel OutputPane { get; set; }

        public QueryResultsPaneViewModel QueryResultsPane { get; set; }

        public string QueryText
        {
            get
            {
                if (!Dispatcher.CurrentDispatcher.CheckAccess())
                {
                    Dispatcher.CurrentDispatcher.Invoke(new Func<string>(() =>
                        { return GetQueryTextFromEditor(); }));
                }

                return GetQueryTextFromEditor();

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

        private string GetQueryTextFromEditorInternal(DAXEditor.DAXEditor editor)
        {
            var queryText = editor.SelectedText;
            if (editor.SelectionLength == 0)
            {
                queryText = editor.Text;
            }
            return queryText;
        }

        public DataTable ExecuteQuery(string daxQuery)
        {
            try
            {
                var c = Connection;
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
                _eventAggregator.Publish(new QueryFinishedEvent());
            }

        }

        private void CancelQuery()
        {
            try
            {
                using (new StatusBarMessage("Cancelling Query..."))
                {
                    var c = Connection;
                    c.Cancel();
                    _eventAggregator.Publish(new QueryFinishedEvent());
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
            // if there are any trace listners we need to make sure that the trace is started
            // and that the appropriate events are registered
            
            _queryRunning = true;
            SelectedWorksheet = message.SelectedWorksheet;
            NotifyOfPropertyChange(()=>CanRunQuery);
            RegisterTraceWatchers();
            if (Tracer != null && Tracer.CheckedTraceWatchers.Count > 0)
            {
                using (var msg1 = new StatusBarMessage("Waiting for Trace to start..."))
                {
                    // only run the query after the trace starts
                    Tracer.Start(message.ResultsTarget);
                }
                
            }
            else
            {
                var m1 = new StatusBarMessage("Running Query...");

                message.ResultsTarget.OutputResultsAsync(this).ContinueWith((antecendant) =>
                    {
                        _queryRunning = false;
                        NotifyOfPropertyChange(() => CanRunQuery);
                        m1 = new StatusBarMessage("Ready");
                    });

            }
            

        }

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
            get { return !_queryRunning; }
        }

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

        public void Handle(SaveDocumentEvent message)
        {
            // todo - savedocument
            throw new NotImplementedException();
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (dropInfo.Data is ADOTabularTable || dropInfo.Data is ADOTabularColumn)
            {
                dropInfo.Effects = DragDropEffects.Move;
            }
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
            InsertTextAtSelection(message.TextToSend);
        }

        public void Handle(UpdateConnectionEvent message)
        {
            _logger.Info("In Handle<UpdateConnectionEvent>");
            var m = new StatusBarMessage("Refreshing Metadata...");
            
            UpdateConnections(message.Connection,message.DatabaseName);
            var m2 = new StatusBarMessage("Ready");
            
            /*
            Execute.BeginOnUIThread(() =>
            {
                UpdateConnectionsAsync(message.Connection, message.DatabaseName).ContinueWith((antecedant) =>
                {
                    var m2 = new StatusBarMessage("Ready");
                });
            });
            */
            
             
        }

        public void Handle(TraceWatcherToggleEvent message)
        {
            if (message.IsActive)
            {
                ToolWindows.Add(message.TraceWatcher);
            }
            else
            {
                ToolWindows.Remove(message.TraceWatcher);
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
            OpenFileAsync(); //.ContinueWith((antecendant)=> ChangeConnection());
        }

        private Task OpenFileAsync()
        {
            return Task.Factory.StartNew(ShowOpenFileDialog);
        }

        private void ShowOpenFileDialog()
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".dax",
                Filter = "DAX documents (.dax)|*.dax"
            };

            // Show open file dialog box
            var result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                var fileName = dlg.FileName;
                Execute.OnUIThread(() => { 
                    LoadFile(fileName);
                    ChangeConnection(); 
                });

            }
            else
            {
                this.Close();
            }
            
        }


        public void LoadFile(string fileName)
        {
            FileName = fileName;
            _displayName = Path.GetFileName(fileName);
            IsDiskFileName = true;
            using (TextReader tr = new StreamReader(FileName, true))
            {
                // put contents in edit window
                GetEditor().Text = tr.ReadToEnd();
                tr.Close();
            }
            IsDirty = false;
        }
        
        /*
        protected  void OnContentRendered()
        {
            if (!IsDiskFileName) return;

            LoadFile(FileName);
            if (Connection==null)
                ChangeConnection();
        }
        */
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

        public void Handle(OutputInformationMessageEvent message)
        {
            OutputMessage(message.Text);
            ActivateOutput();
        }

        public void Handle(ApplicationActivatedEvent message)
        {
            if (_host.IsExcel)
            {
                //TODO - refresh workbooks and powerpivot conn if the host is excel
            }
        }

        public void Handle(ConnectEvent message)
        {
            _logger.Info("In Handle<ConnectEvent>");
            var m = new StatusBarMessage("Connecting...");
            Task.Factory.StartNew(() =>
                {

                    var cnn = message.PowerPivotModeSelected
                                     ? Host.GetPowerPivotConnection()
                                     : new ADOTabularConnection(message.ConnectionString, AdomdType.AnalysisServices);
                    if (Dispatcher.CurrentDispatcher.CheckAccess())
                    {
                        Dispatcher.CurrentDispatcher.Invoke(new System.Action(() => Connection = cnn ));
                    }
                    else
                    {
                        Connection = cnn;    
                    }
                    
                }).ContinueWith((antecendant) =>
                    {
                        _eventAggregator.Publish(new UpdateConnectionEvent(Connection));
                    });
            
        }

        public void Handle(CancelConnectEvent message)
        {
            // refresh the other views with the existing connection details
            _eventAggregator.Publish(new UpdateConnectionEvent(Connection));
        }

        public IResult GetShutdownTask()
        {
            return IsDirty ? new ApplicationCloseCheck(this, DoCloseCheck) : null;
        }

        protected virtual void DoCloseCheck( Action<bool> callback)
        {
            
            var res = MessageBoxEx.Show(Application.Current.MainWindow,
                string.Format("\"{0}\" has unsaved data. Are you sure you want to close this document? All changes will be lost.",_displayName),
                "Unsaved Data", MessageBoxButton.YesNo
                
                );
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
    }


}
