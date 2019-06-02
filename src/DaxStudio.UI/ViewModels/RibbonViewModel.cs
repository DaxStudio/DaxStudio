using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.ComponentModel;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ConnectionPendingEvent>
        , IHandle<CancelConnectEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<ApplicationActivatedEvent>
        , IHandle<FileOpenedEvent>
        , IHandle<FileSavedEvent>
        , IHandle<TraceChangingEvent>
        , IHandle<TraceChangedEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<UpdateGlobalOptions>
        , IHandle<AllDocumentsClosedEvent>
        , IHandle<RefreshOutputTargetsEvent>
    //        , IViewAware
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private bool _isDocumentActivating = false;
        private bool _isConnecting = false;
        private readonly string _sqlProfilerCommand = "";

        private const string urlDaxStudioWiki = "http://daxstudio.org";
        private const string urlPowerPivotForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlkjpowerpivotforexcel";
        private const string urlSsasForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlanalysisservices";
        private ISettingProvider SettingProvider;
        [ImportingConstructor]
        public RibbonViewModel(IDaxStudioHost host, IEventAggregator eventAggregator, IWindowManager windowManager, IGlobalOptions options, ISettingProvider settingProvider)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _host = host;
            _windowManager = windowManager;
            SettingProvider = settingProvider;
            Options = options;
            _theme = Options.Theme;
            UpdateGlobalOptions();
            CanCut = true;
            CanCopy = true;
            CanPaste = true;
            _sqlProfilerCommand = SqlProfilerHelper.GetSqlProfilerLaunchCommand();
            RecentFiles = SettingProvider.GetFileMRUList();
            InitRunStyles();
        }



        private void InitRunStyles()
        {
            // populate run styles
            RunStyles = new List<RunStyle>();
            RunStyles.Add(new RunStyle("Run Query", RunStyleIcons.RunOnly, false, false, false, "Executes the query and sends the results to the selected output"));
            RunStyles.Add(new RunStyle("Clear Cache then Run", RunStyleIcons.ClearThenRun, true,false,false, "Clears the database cache, then executes the query and sends the results to the selected output"));
#if DEBUG
            RunStyles.Add(new RunStyle("Run Table Function", RunStyleIcons.RunFunction, true, true,false, "Attempts to executes the selected function by inserting 'EVALUATE' in front of it and sends the results to the selected output"));
            RunStyles.Add(new RunStyle("Run Measure", RunStyleIcons.RunScalar, true, true, true, "Attempts to executes the selected measure or scalar function by wrapping the selection with 'EVALUATE ROW(...)' and sends the results to the selected output"));
#endif
            // set default run style
            var defaultRunStyle = RunStyleIcons.RunOnly;
            if (Options.SetClearCacheAsDefaultRunStyle) defaultRunStyle = RunStyleIcons.ClearThenRun;

            SelectedRunStyle = RunStyles.FirstOrDefault(rs => rs.Icon == defaultRunStyle);
        }

        public List<RunStyle> RunStyles { get; set; }
        private RunStyle _selectedRunStyle;
        public RunStyle SelectedRunStyle {
            get { return _selectedRunStyle; }
            set { _selectedRunStyle = value;
                NotifyOfPropertyChange(() => SelectedRunStyle);
                //RunQuery(); // TODO if we change run styles should we immediately run the query with the new style??
            } }
        public IGlobalOptions Options { get; private set; }
        public Visibility OutputGroupIsVisible
        {
            get { return _host.IsExcel ? Visibility.Visible : Visibility.Collapsed; }
        }

        public bool ServerTimingsIsChecked
        {
            get
            {
                // TODO - Check if ServerTiming Trace is checked - Update on check change
                //return _traceStatus == QueryTraceStatus.Started ? Visibility.Visible : Visibility.Collapsed; 
                return true; 
            }
        }

        public void NewQuery()
        {
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(SelectedTarget));
        }

        public void NewQueryWithCurrentConnection()
        {
            if (ActiveDocument == null) return;

            var connectionString = "";
            if (ActiveDocument.IsConnected)
                connectionString = ActiveDocument.ConnectionStringWithInitialCatalog;
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(SelectedTarget, ActiveDocument));
        }

        public bool CanCommentSelection { get => ActiveDocument != null; }
        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(true));
        }

        public void MergeParameters()
        {
            ActiveDocument?.MergeParameters();
        }

        public bool CanFormatQueryStandard { get => ActiveDocument != null; }

        public void FormatQueryStandard()
        {
            ActiveDocument?.FormatQuery( false );
        }
        public void FormatQueryAlternate()
        {
            ActiveDocument?.FormatQuery( true );
        }

        public bool CanUndo { get => ActiveDocument != null; }
        public void Undo()
        {
            ActiveDocument?.Undo();
        }

        public bool CanRedo { get => ActiveDocument != null; }
        public void Redo()
        {
            ActiveDocument?.Redo();
        }
        public bool CanUncommentSelection { get => ActiveDocument != null; }
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
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(SelectedTarget, SelectedRunStyle) );

        }

        public string RunQueryDisableReason
        {
            get
            {
                if (ActiveDocument == null) return "There is no active Query window";
                if ( _queryRunning) return  "A query is currently executing";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                return "not disabled";
            }
        }

        public string CancelQueryDisableReason
        {
            get
            {
                if (ActiveDocument == null) return "There is no active Query window";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                if (!_queryRunning) return "A query is not currently executing";
                return "not disabled";
            }
        }

        public bool CanRunQuery
        {
            get
            {
                return !_queryRunning 
                    && (ActiveDocument != null && ActiveDocument.IsConnected) 
                    && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);
            }
        }

        public void CancelQuery()
        {
            _eventAggregator.PublishOnUIThread(new CancelQueryEvent());
        }

        public bool CanCancelQuery
        {
            get { return !CanRunQuery && (ActiveDocument != null && ActiveDocument.IsConnected); }
        }

        public bool CanClearCache
        {
            get { return CanRunQuery && (ActiveDocument != null && ActiveDocument.IsAdminConnection); }
        }

        public string ClearCacheDisableReason
        {
            get 
            { 
                if (!ActiveDocument.IsAdminConnection) return "Only a server administrator can run the clear cache command";
                if (_queryRunning) return "A query is currently executing";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                return "Cannot clear the cache while a query is currently running";
            }
        }

        public void ClearCache()
        {
            ActiveDocument?.ClearDatabaseCacheAsync().FireAndForget();
        }

        public bool CanSave => ActiveDocument != null;

        public bool CanSaveAs => ActiveDocument != null;

        public void Save()
        {
            ActiveDocument?.Save();
        }
        public void SaveAs()
        {
            ActiveDocument?.SaveAs();
        }
        

        public void Connect()
        {
            if (ActiveDocument == null) NewQuery();
            else ActiveDocument.ChangeConnection();
        }

        //private bool _canConnect;
        public bool CanConnect
        {
            get {
                return ActiveDocument != null 
                    && !_queryRunning 
                    && !_isConnecting 
                    && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);
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

        private void RefreshConnectionDetails(IConnection connection, string databaseName)
        {
            var doc = ActiveDocument;
            
            if (connection == null)
            {
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", "<null>", "<null>");
                _isConnecting = false;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                TraceWatchers.DisableAll();
                return;
            }

            try
            {
                Log.Debug("{Class} {Event} {ServerName} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", connection.ServerName, databaseName);                
            }
            catch (Exception ex)
            {
                //_eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                doc.OutputError(ex.Message);
            }
            finally
            {
                _isConnecting = false;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
            }
        }
        
        [ImportMany]
        public IEnumerable<IResultsTarget> AvailableResultsTargets {get; set; }

        public IEnumerable<IResultsTarget> ResultsTargets { get {
            //return  AvailableResultsTargets.OrderBy<IEnumerable<IResultsTarget>,int>(AvailableResultsTargets, x => x.DisplayOrder).Where(x=> x.IsEnabled.ToList<IResultsTarget>();
            return (from t in AvailableResultsTargets
                    where t.IsAvailable
                    select t).OrderBy(x => x.DisplayOrder).AsEnumerable<IResultsTarget>();
        } }

        private IResultsTarget _selectedTarget;
        private bool _queryRunning;
        private QueryTraceStatus _traceStatus = QueryTraceStatus.Stopped;
        private StatusBarMessage _traceMessage;
        // default to first target if none currently selected
        public IResultsTarget SelectedTarget {
            get { return _selectedTarget ?? AvailableResultsTargets.Where(x => x.IsDefault).First<IResultsTarget>(); }
            set { _selectedTarget = value;
                Log.Verbose("{class} {property} {value}", "RibbonViewModel", "SelectedTarget:Set", value.Name);
                if (_selectedTarget is IActivateResults)
                    ActiveDocument?.ActivateResults();
                NotifyOfPropertyChange(()=>SelectedTarget);
                if (!_isDocumentActivating)
                {
                    _eventAggregator.BeginPublishOnUIThread(new QueryResultsPaneMessageEvent(_selectedTarget));
                }
                if (_selectedTarget is IActivateResults) { ActiveDocument?.ActivateResults(); }
                
            }
        }

        public IObservableCollection<ITraceWatcher> TraceWatchers { get { return ActiveDocument == null ? null : ActiveDocument.TraceWatchers; } }
         
        public void Handle(ActivateDocumentEvent message)
        {
            Log.Debug("{Class} {Event} {Document}", "RibbonViewModel", "Handle:ActivateDocumentEvent", message.Document.DisplayName);
            _isDocumentActivating = true;
            ActiveDocument = message.Document;
            var doc = ActiveDocument;
            SelectedTarget = ActiveDocument.SelectedTarget;

            _queryRunning = ActiveDocument.IsQueryRunning;
            if (ActiveDocument.Tracer == null)
                _traceStatus = QueryTraceStatus.Stopped;
            else
                _traceStatus = ActiveDocument.Tracer.Status;

            RefreshRibbonButtonEnabledStatus();

            if (!ActiveDocument.IsConnected)
            {
                UpdateTraceWatchers();
                NotifyOfPropertyChange(() => TraceWatchers);
                NotifyOfPropertyChange(() => ServerTimingsChecked);
                NotifyOfPropertyChange(() => ServerTimingDetails);
                return;
            }
            try
            {
                RefreshConnectionDetails(ActiveDocument, ActiveDocument.SelectedDatabase);
                // TODO - do we still need to check trace watchers if we are not connected??
                UpdateTraceWatchers();
            }
            catch (AdomdConnectionException ex)
            {
                Log.Error("{class} {method} {Exception}", "RibbonViewModel", "Handle(ActivateDocumentEvent)", ex);
                doc.OutputError(ex.Message);
            }
            finally
            {
                _isDocumentActivating = false;
            }
            NotifyOfPropertyChange(() => TraceWatchers);
            NotifyOfPropertyChange(() => ServerTimingsChecked);
            NotifyOfPropertyChange(() => ServerTimingDetails);
        }

        private void RefreshRibbonButtonEnabledStatus()
        {
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanFormatQueryStandard);
            NotifyOfPropertyChange(() => CanCommentSelection);
            NotifyOfPropertyChange(() => CanUncommentSelection);
            NotifyOfPropertyChange(() => CanUndo);
            NotifyOfPropertyChange(() => CanRedo);
            NotifyOfPropertyChange(() => CanConnect);
            NotifyOfPropertyChange(() => CanLaunchSqlProfiler);
            NotifyOfPropertyChange(() => CanLaunchExcel);
            NotifyOfPropertyChange(() => CanExportAllData);
            NotifyOfPropertyChange(() => CanExportAnalysisData);
            UpdateTraceWatchers();
        }

        private void UpdateTraceWatchers()
        {
            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
            foreach (var tw in TraceWatchers)
            {
                tw.CheckEnabled(ActiveDocument, activeTrace);
            }
        }

        private DocumentViewModel _activeDocument;
         
        public DocumentViewModel ActiveDocument
        {
            get { return _activeDocument; }
            set {
                if(_activeDocument != null) _activeDocument.PropertyChanged -= ActiveDocumentPropertyChanged;
                _activeDocument = value;
                NotifyOfPropertyChange(() => CanSave);
                NotifyOfPropertyChange(() => CanSaveAs);
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                if (_activeDocument != null) _activeDocument.PropertyChanged += ActiveDocumentPropertyChanged;
            }
        }

        private void ActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "IsQueryRunning":
                    _queryRunning = ActiveDocument.IsQueryRunning;
                    NotifyOfPropertyChange(() => CanRunQuery);
                    NotifyOfPropertyChange(() => CanCancelQuery);
                    NotifyOfPropertyChange(() => CanClearCache);
                    NotifyOfPropertyChange(() => CanRefreshMetadata);
                    NotifyOfPropertyChange(() => CanConnect);
                    break;
            }
        }

        public void Handle(QueryFinishedEvent message)
        {
            _queryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);
        }

        public void LinkToDaxStudioWiki()
        {
            OpenUrl(urlDaxStudioWiki, "LinkToDaxStudioWiki");
                
        }

        public void LinkToPowerPivotForum()
        {
            OpenUrl(urlPowerPivotForum, "LinkToPowerPivotForum");
        }

        public void LinkToSsasForum()
        {
            OpenUrl(urlSsasForum, "LinkToSsasForum");
        }

        internal void OpenUrl(string name, string url)
        {
            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error Launching {method}", "RibbonViewModel", "LinkToDaxStudioWiki");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("The following error occurred while trying to open the {1}: {0}", ex.Message, name)));
            }
        }

        public void Handle(ConnectionPendingEvent message)
        {
            _isConnecting = true;
        }
        public void Handle(ApplicationActivatedEvent message)
        {
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "RibbonViewModel", "Handle:ApplicationActivatedEvent:Start", message);
            if (ActiveDocument != null)
            {
                if (ActiveDocument.ShouldAutoRefreshMetadata())
                {
                    
                    ActiveDocument.RefreshMetadata();
                    ActiveDocument.OutputMessage("Model schema change detected - Metadata refreshed");
                    
                }
                RefreshConnectionDetails(ActiveDocument, ActiveDocument.SelectedDatabase);
            }
           
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "RibbonViewModel", "Handle:ApplicationActivatedEvent:End", message);
        }

        
        public void Handle(TraceChangingEvent message)
        {
            if (ActiveDocument != null)
                _traceMessage = new StatusBarMessage(ActiveDocument, "Waiting for trace to update");
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanConnect);
        }

        public void Handle(TraceChangedEvent message)
        {
            if(_traceMessage != null) _traceMessage.Dispose();
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanConnect);
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            RefreshConnectionDetails(message.Connection, message.Connection.SelectedDatabase);
        }
        
        public bool CanCut { get; set; }
        
        public bool CanCopy { get;set; }
        
        public bool CanPaste { get; set; }
        
        [Import]
        HelpAboutViewModel aboutDialog { get; set; }

        public void ShowHelpAbout()
        {
            _windowManager.ShowDialogBox(aboutDialog , 
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
            _activeDocument?.Find();
        }

        public void GotoLine()
        {
            _activeDocument?.GotoLine();
        }

        public void Replace()
        {
            _activeDocument?.Replace();
        }

        public void RefreshMetadata()
        {
            ActiveDocument?.RefreshMetadata();
        }

        public bool CanRefreshMetadata
        {
            get { return ActiveDocument != null && ActiveDocument.IsConnected; }
        }

        internal void FindNow()
        {
            if (ActiveDocument == null) return;
            ActiveDocument.FindReplaceDialog.SearchUp = false;
            ActiveDocument.FindReplaceDialog.FindText();
        }
        internal void FindPrevNow()
        {
            if (ActiveDocument == null) return;
            ActiveDocument.FindReplaceDialog.SearchUp = true;
            ActiveDocument.FindReplaceDialog.FindText();
        }

        public bool ServerTimingsChecked { get { return ActiveDocument?.ServerTimingsChecked??false; } }

        public ServerTimingDetailsViewModel ServerTimingDetails { get { return ActiveDocument?.ServerTimingDetails; } }

        public void Handle(TraceWatcherToggleEvent message)
        {
            if (message.TraceWatcher is ServerTimesViewModel)
            {
                NotifyOfPropertyChange(() => ServerTimingsChecked);
            }
        }

        public ObservableCollection<DaxFile> RecentFiles { get; set; }

        internal void OnClose()
        {
            SettingProvider.SaveFileMRUList(this.RecentFiles);
        }

        private void AddToRecentFiles(string fileName)
        {
            DaxFile df = (from DaxFile f in RecentFiles
                          where f.FullPath.Equals(fileName, StringComparison.CurrentCultureIgnoreCase)
                          select f).FirstOrDefault<DaxFile>();
            if (df == null)
            {
                RecentFiles.Insert(0, new DaxFile(fileName));
            }
            else
            {
                // move the file to the first position in the list                
                RecentFiles.Remove(df);
                RecentFiles.Insert(0, df);
            }
        }

        public void Handle(FileOpenedEvent message)
        {
            AddToRecentFiles(message.FileName);
        }

        public void Handle(FileSavedEvent message)
        {
            AddToRecentFiles(message.FileName);
        }

        public void OpenRecentFile(DaxFile file, Fluent.Backstage backstage)
        {
            // if a user clicks on the edge of the recent files list
            // it's possible to hit this method with no selected file
            // if this happens we need to exit here and ignore the click
            if (file == null) return; 

            // otherwise clost the backstage menu and open the file
            backstage.IsOpen = false;
            MoveFileToTopOfRecentList(file);
            _eventAggregator.PublishOnUIThread(new OpenRecentFileEvent(file.FullPath));
        }

        private void MoveFileToTopOfRecentList(DaxFile file)
        {
            // remove the file from it's current position
            RecentFiles.Remove(file);
            // insert it at the top of the list
            RecentFiles.Insert(0, file);

            SettingProvider.SaveFileMRUList(this.RecentFiles);
        }

        public void SwapDelimiters()
        {
            ActiveDocument?.SwapDelimiters();
        }

        public bool IsDebugBuild
        {
            get {
#if DEBUG 
                return true;
#else
                return false;
#endif

            }
        }

        public bool IsPreviewBuild
        {
            get
            {
#if PREVIEW || DEBUG
                return true;
#else
                return false;
#endif

            }
        }

        public bool ShowAdvancedTab
        {
            get
            {
                return ShowExportMetrics | ShowExternalTools | ShowExportAllData | ResultAutoFormat;
            }
        }

        public bool ShowExportGroup => ShowExportAllData | ShowExportMetrics;

        private bool _showExternalTools;
        public bool ShowExternalTools
        {
            get { return _showExternalTools; }
            private set
            {
                _showExternalTools = value;
                NotifyOfPropertyChange(() => ShowExternalTools);
                NotifyOfPropertyChange(() => ShowAdvancedTab);
            }
        }


        private bool _showExportAllData;
        public bool ShowExportAllData
        {
            get { return _showExportAllData; }
            private set
            {
                _showExportAllData = value;
                NotifyOfPropertyChange(() => ShowExportAllData);
                NotifyOfPropertyChange(() => ShowExportGroup);
                NotifyOfPropertyChange(() => ShowAdvancedTab);
            }
        }

        private bool _showExportMetrics;
        public bool ShowExportMetrics {
            get { return _showExportMetrics; }
            private set {
                _showExportMetrics = value;
                NotifyOfPropertyChange(() => ShowExportMetrics);
                NotifyOfPropertyChange(() => ShowExportGroup);
                NotifyOfPropertyChange(() => ShowAdvancedTab);
            }
        }

        private bool _ResultAutoFormat;
        public bool ResultAutoFormat {
            get { return _ResultAutoFormat; }
            private set {
                _ResultAutoFormat = value;
                NotifyOfPropertyChange(() => ResultAutoFormat);
                NotifyOfPropertyChange(() => ShowAdvancedTab);
            }
        }

        public bool CanExportAnalysisData => IsActiveDocumentConnected();

        public void ExportAnalysisData()
        {
            ActiveDocument?.ExportAnalysisData();
        }

        public bool CanExportAllData => IsActiveDocumentConnected();

        public void ExportAllData()
        {
            if (ActiveDocument == null) return;

            using (var dialog = new ExportDataDialogViewModel(_eventAggregator, ActiveDocument))
            {

                _windowManager.ShowDialogBox(dialog, settings: new Dictionary<string, object>
                {
                    { "WindowStyle", WindowStyle.None},
                    { "ShowInTaskbar", false},
                    { "ResizeMode", ResizeMode.NoResize},
                    { "Background", System.Windows.Media.Brushes.Transparent},
                    { "AllowsTransparency",true}

                });
            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            UpdateGlobalOptions();
        }

        private void UpdateGlobalOptions()
        {
            ShowExportMetrics = Options.ShowExportMetrics;
            ShowExternalTools = Options.ShowExternalTools;
            ShowExportAllData = Options.ShowExportAllData;
            ResultAutoFormat = Options.ResultAutoFormat;
        }

        public void LaunchSqlProfiler()
        {
            if (ActiveDocument == null) return;
            try
            {
                var serverName = ActiveDocument.Connection.ServerName;
                System.Diagnostics.Process.Start(_sqlProfilerCommand, $" /A {serverName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "RibbonViewModel", "LaunchSqlProfiler", "Error launching SQL Profiler");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error Launching SQL Profiler: " + ex.Message));
            }
        }

        public bool CanLaunchSqlProfiler => IsActiveDocumentConnected() && !string.IsNullOrEmpty(_sqlProfilerCommand);

        public void LaunchExcel()
        {
            if (ActiveDocument == null) return;
            try
            {
                var conn = ActiveDocument.Connection;
                var datasource = conn.ServerName;
                var database = conn.Database.Name;
                var cube = ActiveDocument.MetadataPane.SelectedModelName;
                OdcHelper.CreateOdcFile(datasource, database, cube);
                var fileName = OdcHelper.OdcFilePath();
                System.Diagnostics.Process.Start(fileName);
            } catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "RibbonViewModel", "LaunchExcel", ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error Launching Excel: " + ex.Message));

            }
        }

        public bool CanLaunchExcel => IsActiveDocumentConnected();

        private bool IsActiveDocumentConnected()
        {
            if (ActiveDocument == null) return false;
            if (ActiveDocument.Connection == null) return false;
            if (ActiveDocument.Connection.State != System.Data.ConnectionState.Open) return false;

            return true;
        }

        public void SaveLayout()
        {
            _eventAggregator.BeginPublishOnUIThread(new DockManagerSaveLayout());
        }

        public void LoadLayout()
        {
            _eventAggregator.BeginPublishOnUIThread(new DockManagerLoadLayout(false));
        }

        public void ResetLayout()
        {
            _eventAggregator.BeginPublishOnUIThread(new DockManagerLoadLayout(true));
        }

        private string _theme = "Light"; // default to light theme
        public string Theme
        {
            get { return _theme; }
            set { if (value != _theme)
                {
                    _theme = value;
                    Options.Theme = _theme;
                    //SetRibbonTheme(_theme);
                    _eventAggregator.PublishOnUIThread(new ChangeThemeEvent(_theme));
                    NotifyOfPropertyChange(() => Theme);
                }
            }
        }

        private void SetRibbonTheme(string theme)
        {
            Application.Current.ChangeRibbonTheme(theme);
        }
        public void Handle(AllDocumentsClosedEvent message)
        {
            this.ActiveDocument = null;
            RefreshRibbonButtonEnabledStatus();
        }

        public void Handle(RefreshOutputTargetsEvent message)
        {
            // This message tell fluent ribbon that the ResultsTargets collection has changed
            // and should be re-evaluated
            NotifyOfPropertyChange(() => ResultsTargets);
        }

        public void Handle(CancelConnectEvent message)
        {
            _isConnecting = false;
        }

        public void LoadPowerBIPerformanceData()
        {

            // Configure open file dialog box
            var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Title = "Open Power BI Performance Data",
                FileName = "PowerBIPerformanceData.json",
                DefaultExt = ".json",
                Filter = "Power BI Performance Data (*.json)|*.json"
            };

            // Show open file dialog box
            System.Windows.Forms.DialogResult result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Open document 
                var fileName = dlg.FileName;
                // check if PerfData Window is already open and use that
                var perfDataWindow = this.ActiveDocument.ToolWindows.FirstOrDefault(win => (win as PowerBIPerformanceDataViewModel) != null) as PowerBIPerformanceDataViewModel;
                
                if (perfDataWindow == null)
                {
                    // todo - get viewmodel from IoC container
                    perfDataWindow = new PowerBIPerformanceDataViewModel(_eventAggregator, Options);
                    this.ActiveDocument.ToolWindows.Add(perfDataWindow);
                }

                // load the perfomance data
                perfDataWindow.FileName = fileName;

                // set the performance window as the active tab
                perfDataWindow.Activate();
            }
        }
    }
}
