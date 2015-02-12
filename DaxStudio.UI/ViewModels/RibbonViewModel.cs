using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Properties;
using System.Linq;
using ADOTabular.AdomdClientWrappers;
using Microsoft.Win32;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ConnectionPendingEvent>
        //, IHandle<UpdateConnectionEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<ApplicationActivatedEvent>
        , IHandle<TraceChangingEvent>
        , IHandle<TraceChangedEvent>
        , IHandle<DocumentConnectionUpdateEvent>
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private bool _databaseComboChanging = false;

        private const string urlDaxStudioWiki = "http://daxstudio.codeplex.com/documentation";
        private const string urlPowerPivotForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlkjpowerpivotforexcel";
        private const string urlSsasForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlanalysisservices";

        [ImportingConstructor]
        public RibbonViewModel(IDaxStudioHost host, IEventAggregator eventAggregator, IWindowManager windowManager )
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _host = host;
            _windowManager = windowManager;
            CanCut = true;
            CanCopy = true;
            CanPaste = true;
        }

        public Visibility OutputGroupIsVisible
        {
            get { return _host.IsExcel?Visibility.Visible : Visibility.Collapsed; }
        }

        public void NewQuery()
        {
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent());
        }

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(true));
        }

        public void MergeParameters()
        {
            ActiveDocument.MergeParameters();
        }

        public void FormatQuery()
        {
            ActiveDocument.FormatQuery();
        }

        public void Undo()
        {
            ActiveDocument.Undo();
        }

        public void Redo()
        {
            ActiveDocument.Redo();
        }

        public void UncommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(false));
        }

        public void ToUpper()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void ToLower()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }

        public void RunQuery()
        {
            _queryRunning = true;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(SelectedTarget,SelectedWorksheet) );

        }

        public string RunQueryDisableReason
        {
            get
            {
                if ( _queryRunning) return  "A query is currently executing";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                return "not disabled";
            }
        }

        public bool CanSelectDatabase
        {
            get
            {
                return ActiveDocument.IsConnected && !ActiveDocument.IsPowerPivot ;
            }
        }

        public bool CanRunQuery
        {
            get
            {
                return !_queryRunning && ActiveDocument.IsConnected && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);
            }
        }

        public void CancelQuery()
        {
            _eventAggregator.PublishOnUIThread(new CancelQueryEvent());
        }

        public bool CanCancelQuery
        {
            get { return !CanRunQuery && ActiveDocument.IsConnected; }
        }

        public bool CanClearCache
        {
            get { return CanRunQuery && ActiveDocument.IsAdminConnection; }
        }

        public string ClearCacheDisableReason
        {
            get 
            { 
                if (!ActiveDocument.IsAdminConnection) return "Only a server administrator can run the clear cache command";
                return "Cannot clear the cache while a query is currently running";
            }
        }

        public void ClearCache()
        {
            ActiveDocument.ClearDatabaseCache();
        }

        public void Save()
        {
            ActiveDocument.Save();
        }
        public void SaveAs()
        {
            ActiveDocument.SaveAs();
        }

        public void Connect()
        {
            ActiveDocument.ChangeConnection();
        }

        private bool _canConnect;
        public bool CanConnect
        {
            get { return _canConnect; }
            set { 
                _canConnect = value;
                NotifyOfPropertyChange(()=> CanConnect);
            }
        }

        public ShellViewModel Shell { get; set; }

        public void Exit()
        {
            Shell.TryClose();
        }

        public void Open()
        {
            _eventAggregator.PublishOnUIThread(new OpenFileEvent());
        }

//        public void Handle(UpdateConnectionEvent message)
//        {
//            RefreshConnectionDetails(message .Connection, message.DatabaseName);
//        }

        private void RefreshConnectionDetails(IConnection connection, string databaseName)
        {
            var doc = ActiveDocument;
            
            if (connection == null)
            {
                Databases = null;
                SelectedDatabase = null;
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", "<null>", "<null>");
                CanConnect = true;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanSelectDatabase);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                return;
            }
            try
            {
                Log.Debug("{Class} {Event} {ServerName} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", connection.ServerName, databaseName);
                
                _databaseComboChanging = true;
                Databases = doc.Databases;
                _databaseComboChanging = false;

                SelectedDatabase = doc.SelectedDatabase;
                NotifyOfPropertyChange(()=>SelectedDatabase);
            }
            catch (Exception ex)
            {
                //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                doc.OutputError(ex.Message);
            }
            finally
            {
                CanConnect = true;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanSelectDatabase);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
            }
        }

        private string _selectedDatabase; 
        public string SelectedDatabase {
            get { return _selectedDatabase; }
            set
            {
                // TODO - can't change database if query is running

                var doc = ActiveDocument;
                //Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set", value);
                if (_databaseComboChanging) return;
                if (value == _selectedDatabase )
                { 
                    NotifyOfPropertyChange(()=> SelectedDatabase);
                    return; 
                }

                _databaseComboChanging = true;
                
                _selectedDatabase = value;

                //var msg = doc.NewStatusBarMessage("Loading Metadata...");
                //_eventAggregator.PublishOnUIThread(new StatusBarMessage("Loading Metadata..."));
                

                //Task.Factory.StartNew(() =>  {
                //Execute.OnUIThreadAsync(() => {
                    if (doc.IsConnected)
                    {
                        if (_selectedDatabase == null || !doc.SelectedDatabase.Equals(_selectedDatabase))
                        {
                            Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set (changing)", value);
                            doc.SelectedDatabase = _selectedDatabase;
                            //doc.Connection.ChangeDatabase(_selectedDatabase);
                            //_eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(doc));//, ActiveDocument.IsPowerPivotConnection));
                        }
                    }

                    NotifyOfPropertyChange(() => SelectedDatabase);

                //}).ContinueWith((x) =>
                //{
                    //msg.Dispose();
                    _databaseComboChanging = false;
                //}); 
            }
        }

        [ImportMany]
        public IEnumerable<IResultsTarget> AvailableResultsTargets {get; set; }

        public IEnumerable<IResultsTarget> ResultsTargets { get {
            //return  AvailableResultsTargets.OrderBy<IEnumerable<IResultsTarget>,int>(AvailableResultsTargets, x => x.DisplayOrder).Where(x=> x.IsEnabled.ToList<IResultsTarget>();
            return (from t in AvailableResultsTargets
                    where t.IsEnabled
                    select t).OrderBy(x => x.DisplayOrder).AsEnumerable<IResultsTarget>();
        } }

        private IResultsTarget _selectedTarget;
        private bool _queryRunning;
        private QueryTraceStatus _traceStatus;
        // default to first target if none currently selected
        public IResultsTarget SelectedTarget {
            get { return _selectedTarget ?? AvailableResultsTargets.Where(x => x.IsDefault).First<IResultsTarget>(); }
            set { _selectedTarget = value;
            Log.Verbose("{class} {property} {value}", "RibbonViewModel", "SelectedTarget:Set", value.Name);
            NotifyOfPropertyChange(()=>SelectedTarget);}
        }

        public IObservableCollection<ITraceWatcher> TraceWatchers { get { return ActiveDocument == null ? null : ActiveDocument.TraceWatchers; } }

        private SortedSet<string> _databases = new SortedSet<string>();
        public SortedSet<string> Databases
        {
            get { return _databases; }
            set
            {
                _databases = value;
                NotifyOfPropertyChange(() => Databases);
            }
        }
        public void Handle(ActivateDocumentEvent message)
        {
            Log.Debug("{Class} {Event} {Document}", "RibbonViewModel", "Handle:ActivateDocumentEvent", message.Document.DisplayName);
            ActiveDocument = message.Document;
            var doc = ActiveDocument;
            //ActiveDocument.PropertyChanged += ActiveDocumentOnPropertyChanged;   
            
        //if (ActiveDocument.IsQueryRunning != _queryRunning)
        //{
            _queryRunning = ActiveDocument.IsQueryRunning;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanSelectDatabase);
        //}


            if (!ActiveDocument.IsConnected)
            {
                _selectedDatabase = null;
                NotifyOfPropertyChange(() => SelectedDatabase);
                UpdateTraceWatchers();
                NotifyOfPropertyChange(() => TraceWatchers);
                return;
            }
            try
            {
                //if (ActiveDocument.Connection.State == System.Data.ConnectionState.Open)
                {
                    Databases = null;
                    _selectedDatabase = null;
                    RefreshConnectionDetails(ActiveDocument, ActiveDocument.SelectedDatabase);
                    // TODO - do we still need to check trace watchers if we are not connected??
                    UpdateTraceWatchers();
                }
            }
            catch (AdomdConnectionException ex)
            {
                //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                Log.Error("{Exception}", ex);
                doc.OutputError(ex.Message);
            }
            NotifyOfPropertyChange(() => TraceWatchers);
        }

        private void UpdateTraceWatchers()
        {
            foreach (var tw in TraceWatchers)
            {
                // TODO - can we enable traces for PowerPivot?
                //    tw.IsEnabled = (ActiveDocument.Connection.Type == AdomdType.AnalysisServices);
                tw.CheckEnabled(ActiveDocument);
            }
        }
        private DocumentViewModel _activeDocument;
        protected DocumentViewModel ActiveDocument
        {
            get { return _activeDocument; }
            set { _activeDocument = value;
            //_activeDocument.PropertyChanged += OnActiveDocumentPropertyChanged;
            }
        }
        /*
        void OnActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch ( e.PropertyName)
            {
                case "CanPaste":
                    this.CanPaste = ActiveDocument.CanPaste;
                    NotifyOfPropertyChange(() => CanPaste);
                    break;
                case "CanCopy":
                    this.CanCopy = ActiveDocument.CanCopy;
                    NotifyOfPropertyChange(() => CanCopy);
                    break;
                case "CanCut":
                    this.CanCut = ActiveDocument.CanCut;
                    NotifyOfPropertyChange(() => CanCut);
                    break;
            }

        }
         */ 
        // TODO - should this be an observable collection??
        public IEnumerable<string> Worksheets
        {
            get { return _host.Proxy.Worksheets; }
        }

        public string SelectedWorksheet {
            get { return ActiveDocument.SelectedWorksheet; } 
            set { ActiveDocument.SelectedWorksheet = value; } }

        // TODO - configure MRU list
        public System.Collections.Specialized.StringCollection RecentDocuments
        {
            get { return Settings.Default.RecentDocuments; }
            
        }

        public void Handle(QueryFinishedEvent message)
        {
            _queryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
        }

        public void LinkToDaxStudioWiki()
        {
            System.Diagnostics.Process.Start(urlDaxStudioWiki);
        }

        public void LinkToPowerPivotForum()
        {
            System.Diagnostics.Process.Start(urlPowerPivotForum);
        }

        public void LinkToSsasForum()
        {
            System.Diagnostics.Process.Start(urlSsasForum);
        }

        public void Handle(ConnectionPendingEvent message)
        {
            CanConnect = false;
        }
        public void Handle(ApplicationActivatedEvent message)
        {
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "RibbonViewModel", "Handle:ApplicationActivatedEvent:Start", message);
            if (ActiveDocument != null)
            {
                if (ActiveDocument.HasDatabaseSchemaChanged())
                {
                    ActiveDocument.RefreshMetadata();
                    ActiveDocument.OutputMessage("Model schema change detected - Metadata refreshed");
                }
            }
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "RibbonViewModel", "Handle:ApplicationActivatedEvent:MetadataChecked", message);
            if (_host.IsExcel)
            {
                //TODO - refresh workbooks and powerpivot conn if the host is excel
                NotifyOfPropertyChange(() => Worksheets);
            }
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "RibbonViewModel", "Handle:ApplicationActivatedEvent:End", message);
        }

        public void Handle(NewDocumentEvent message)
        {
            Databases = null;
            SelectedDatabase = null;
        }

        public void Handle(TraceChangingEvent message)
        {
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
        }

        public void Handle(TraceChangedEvent message)
        {
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            RefreshConnectionDetails(message.Connection, message.Connection.SelectedDatabase);
        }
        
        public bool CanCut { get; set; }
        //public void Cut() { ActiveDocument.Cut(); }
        public bool CanCopy { get;set; }
        //public void Copy() { ActiveDocument.Copy(); }
        public bool CanPaste { get; set; }

        //public void Paste() { ActiveDocument.Paste(); }
        
        [Import]
        HelpAboutViewModel aboutDialog { get; set; }

        public void ShowHelpAbout()
        {
            //var about = new HelpAboutViewModel(_eventAggregator);
            _windowManager.ShowDialog(aboutDialog , 
                settings: new Dictionary<string, object>
                {
                    { "WindowStyle", WindowStyle.None},
                    { "ShowInTaskbar", false},
                    { "ResizeMode", ResizeMode.NoResize},
                    { "Background", System.Windows.Media.Brushes.Transparent},
                    { "AllowsTransparency",true}
                
                });
        }

        public void Find()
        {
            _activeDocument.Find();
        }

        public void Replace()
        {
            _activeDocument.Replace();
        }

        public void RefreshMetadata()
        {
            _activeDocument.RefreshMetadata();
        }

        public bool CanRefreshMetadata
        {
            get { return ActiveDocument.IsConnected; }
        }

        internal void FindNow()
        {
            _activeDocument.FindReplaceDialog.SearchUp = false;
            _activeDocument.FindReplaceDialog.FindText();
        }
        internal void FindPrevNow()
        {
            _activeDocument.FindReplaceDialog.SearchUp = true;
            _activeDocument.FindReplaceDialog.FindText();
        }
    }
}
