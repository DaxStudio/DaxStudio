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
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Views;
using GongSolutions.Wpf.DragDrop;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.AnalysisServices;
using Microsoft.Win32;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(Screen))]
    [Export(typeof(DocumentViewModel))]
    public class DocumentViewModel : Screen
        ,IHandle<RunQueryEvent>
        ,IHandle<SaveDocumentEvent>
        ,IHandle<SendTextToEditor>
        ,IHandle<UpdateConnectionEvent> // ,IDropTarget
        ,IHandle<TraceWatcherToggleEvent>
        ,IHandle<LoadFileEvent>
        ,IQueryRunner
        
    {
        private ADOTabularConnection _connection;
        private IWindowManager _windowManager;
        private IEventAggregator _eventAggregator;
        private MetadataPaneViewModel _metadataPane;
        private IObservableCollection<object> _toolWindows;
        private BindableCollection<ITraceWatcher> _traceWatchers;
        private bool _queryRunning;

        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager, IEventAggregator eventAggregator )
        {
            Init(windowManager,eventAggregator);
        }

        public void Init( IWindowManager windowManager, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _windowManager = windowManager;
        
            // Initialize default Tool Windows
            MetadataPane = new MetadataPaneViewModel(_connection,_eventAggregator);
            FunctionPane = new FunctionPaneViewModel(_connection, _eventAggregator);
            DmvPane = new DmvPaneViewModel(_connection,_eventAggregator);
            OutputPane = new OutputPaneViewModel();
            QueryResultsPane = new QueryResultsPaneViewModel();
            Document = new TextDocument();
        }

        private QueryTrace _tracer;
        public QueryTrace Tracer
        {
            get
            {
                if (_tracer == null)
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
            Execute.OnUIThread(() =>  e.ResultsTarget.OutputResults(this));
        }

        private void TracerOnTraceEvent(object sender, TraceEventArgs traceEventArgs)
        {
            foreach (var tw in Tracer.EnabledTraceWatchers)
            {
                tw.ProcessEvent(traceEventArgs);
            }
        }

        [ImportMany(typeof(ITraceWatcher))]
        public BindableCollection<ITraceWatcher> TraceWatchers {
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
            return (DocumentView)GetView();
        }

        
        private DAXEditor.DAXEditor GetEditor()
        {
            DocumentView v = GetDocumentView();
            return v.daxEditor;
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
            NotifyOfPropertyChange(()=> CanRunQuery);
        }

        public MetadataPaneViewModel MetadataPane {
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
            base.OnActivate();
            _eventAggregator.Subscribe(this);
            _eventAggregator.Publish(new ActivateDocumentEvent(this));
        }

        public override void CanClose(Action<bool> callback)
        {
            base.CanClose(callback);
            
            MessageBox.Show("in Can Close", "debug");
        }

        public bool Close()
        {
            // todo - need to make sure "dirty" files are prompted for saving
            return true;
        }

        public override void TryClose()
        {
            base.TryClose();
            MessageBox.Show("In Try Close", "debug");
        }

        public ADOTabularConnection Connection
        {
            get { return _connection; }
            set
            {
                if (_connection == value)
                    return;
                UpdateConnections(value);
                _eventAggregator.Publish(new ConnectionChangedEvent(_connection));
            }
        }

        private void UpdateConnections(ADOTabularConnection value)
        {
            _connection = value;
            MetadataPane.Connection = _connection;
            FunctionPane.Connection = _connection;
            DmvPane.Connection = _connection; 
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
            var connDialog = new ConnectionDialogViewModel(Connection);
            _windowManager.ShowDialog(connDialog);
            try
            {
                Connection = connDialog.Connection;
                ConnectionError = "";
            }
            catch (Exception ex)
            {
                ConnectionError = ex.Message;
            }    
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
                Debug.WriteLine("Query Executed");
                return dt;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return null;
            }
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
            NotifyOfPropertyChange(()=>CanRunQuery);
            RegisterTraceWatchers();
            if (Tracer.EnabledTraceWatchers.Count > 0)
            {
                
                // only run the query after the trace starts
                Tracer.Start(message.ResultsTarget);
            }
            else
            {
                message.ResultsTarget.OutputResults(this);
            }

        }

        
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
            UpdateConnections(message.Connection);
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
                    FileName = this.FileName==""?DisplayName:FileName ,
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
            DisplayName = Path.GetFileName(fileName);
            IsDiskFileName = true;
            using (TextReader tr = new StreamReader(FileName, true))
            {
                // put contents in edit window
                GetEditor().Text = tr.ReadToEnd();
                tr.Close();
            }
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

        
        public void Handle(LoadFileEvent message)
        {
            FileName = message.FileName;
            IsDiskFileName = true;
        }
        
    }
}
