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
using System.Net;
using System.Reflection;
using Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ActivateDocumentEvent>
        , IHandle<AllDocumentsClosedEvent>
        , IHandle<ApplicationActivatedEvent>
        , IHandle<ConnectionPendingEvent>
        , IHandle<CancelConnectEvent>
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<FileOpenedEvent>
        , IHandle<FileSavedEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<RefreshOutputTargetsEvent>
        , IHandle<TraceChangingEvent>
        , IHandle<TraceChangedEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<UpdateGlobalOptions>
        , IHandle<UpdateHotkeys>

        //        , IViewAware
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private bool _isDocumentActivating;
        private bool _isConnecting;
        private readonly string _sqlProfilerCommand = string.Empty;

        private const string urlDaxStudioWiki = "https://daxstudio.org";
        private const string urlPowerPivotForum = "https://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlkjpowerpivotforexcel";
        private const string urlSsasForum = "https://docs.microsoft.com/en-us/answers/topics/sql-server-analysis-services";
        private const string urlGithubBugReportPrefix = @"https://github.com/DaxStudio/DaxStudio/issues/new?labels=from+app&template=bug_report.md&body=";
        private const string urlGithubBugReportSuffix = @"%23%23%20Summary%20of%20Issue%0A%0A%0A%23%23%20Steps%20to%20Reproduce%0A1.%0A2.";
        private const string urlGithubFeatureRequest = @"https://github.com/DaxStudio/DaxStudio/issues/new?assignees=&labels=from+app&template=feature_request.md&title=";
        private const string urlGithubDiscussions = @"https://github.com/DaxStudio/DaxStudio/discussions";
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
            RunStyles.Add(new RunStyle("Run Query", RunStyleIcons.RunOnly, false, false, false, "Executes the query and sends the results to the selected output"));
            RunStyles.Add(new RunStyle("Clear Cache then Run", RunStyleIcons.ClearThenRun, true,false,false, "Clears the database cache, then executes the query and sends the results to the selected output"));
#if DEBUG
//            RunStyles.Add(new RunStyle("Benchmark", RunStyleIcons.RunBenchmark, false, false, false, "Executes the query multiple times and captures the timings"));
            //RunStyles.Add(new RunStyle("Run Table Function", RunStyleIcons.RunFunction, true, true,false, "Attempts to executes the selected function by inserting 'EVALUATE' in front of it and sends the results to the selected output"));
            //RunStyles.Add(new RunStyle("Run Measure", RunStyleIcons.RunScalar, true, true, true, "Attempts to executes the selected measure or scalar function by wrapping the selection with 'EVALUATE ROW(...)' and sends the results to the selected output"));
#endif
            // set default run style
            var defaultRunStyle = RunStyleIcons.RunOnly;
            if (Options.SetClearCacheAsDefaultRunStyle) defaultRunStyle = RunStyleIcons.ClearThenRun;

            SelectedRunStyle = RunStyles.FirstOrDefault(rs => rs.Icon == defaultRunStyle);
        }

        public ObservableCollection<RunStyle> RunStyles { get; } = new ObservableCollection<RunStyle>();
        private RunStyle _selectedRunStyle;
        public RunStyle SelectedRunStyle {
            get => _selectedRunStyle;
            set { _selectedRunStyle = value;
                NotifyOfPropertyChange(() => SelectedRunStyle);
                _eventAggregator.PublishOnUIThread(new RunStyleChangedEvent(SelectedRunStyle));
                //RunQuery(); // TODO if we change run styles should we immediately run the query with the new style??
            } }
        public IGlobalOptions Options { get; private set; }
        public Visibility OutputGroupIsVisible => _host.IsExcel ? Visibility.Visible : Visibility.Collapsed;

        public bool ServerTimingsIsChecked =>
            // TODO - Check if ServerTiming Trace is checked - Update on check change
            //return _traceStatus == QueryTraceStatus.Started ? Visibility.Visible : Visibility.Collapsed; 
            true;

        public void NewQuery()
        {
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(SelectedTarget));
        }

        //public string NewQueryTitle => $"New ({Options.HotkeyNewDocument})";

        public void NewQueryWithCurrentConnection()
        {
            if (ActiveDocument == null) return;
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(SelectedTarget, ActiveDocument));
        }

        public bool CanNewQueryWithCurrentConnection => ActiveDocument != null && ActiveDocument.IsConnected;

        public bool CanCommentSelection => ActiveDocument != null;

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(true));
        }

        public string CommentSelectionTitle => $"Comment ({Options.HotkeyCommentSelection})";

        public bool CanMergeParameters => ActiveDocument != null;
        public void MergeParameters()
        {
            ActiveDocument?.MergeParameters();
        }

        public bool CanFormatQueryStandard => ActiveDocument != null && !Options.BlockExternalServices;

        public void FormatQueryStandard()
        {
            ActiveDocument?.FormatQuery( false );
        }

        public string FormatQueryTitle => $"Format Query ({Options.HotkeyFormatQueryStandard})";
        public void FormatQueryAlternate()
        {
            ActiveDocument?.FormatQuery( true );
        }

        public string FormatQueryStandardTitle => Options.DefaultDaxFormatStyle.GetDescription() + " (Default) (" + Options.HotkeyFormatQueryStandard + ")";

        public string FormatQueryAlternateTitle { get 
            {
                string title;
                if (Options.DefaultDaxFormatStyle == DaxStudio.Interfaces.Enums.DaxFormatStyle.LongLine)
                {
                    title = DaxStudio.Interfaces.Enums.DaxFormatStyle.ShortLine.GetDescription();
                }
                else
                {
                    title = DaxStudio.Interfaces.Enums.DaxFormatStyle.LongLine.GetDescription();
                }
                return title + " (" + Options.HotkeyFormatQueryAlternate + ")";
            } 
        }

        public string FormatQueryDisabledReason
        {
            get
            {
                if (Options.BlockExternalServices) return "Access to External Services blocked in Options privacy settings";
                if (ActiveDocument == null) return "No Active Document";
                return "Not disabled";
            }
        }

        public bool CanUndo => ActiveDocument != null;

        public void Undo()
        {
            ActiveDocument?.Undo();
        }

        public bool CanRedo => ActiveDocument != null;

        public void Redo()
        {
            ActiveDocument?.Redo();
        }
        public bool CanUncommentSelection => ActiveDocument != null;

        public void UncommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(false));
        }
        public string UncommentSelectionTitle => $"Uncomment ({Options.HotkeyUnCommentSelection})";
        public bool CanToUpper => ActiveDocument != null;

        public void ToUpper()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }
        public string ToUpperTitle => $"To Upper ({Options.HotkeyToUpper})";

        public bool CanToLower => ActiveDocument != null;

        public void ToLower()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }
        public string ToLowerTitle => $"To Lower ({Options.HotkeyToLower})";
        public void RunQuery()
        {
            
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);
            
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(SelectedTarget, SelectedRunStyle) );

        }
        public string RunQueryTitle => $"Run Query ({Options.HotkeyRunQuery})";

        public string RunQueryDisableReason
        {
            get
            {
                if (ActiveDocument == null) return "There is no active Query window";
                if ( QueryRunning) return  "A query is currently executing";
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
                if (!QueryRunning) return "A query is not currently executing";
                return "not disabled";
            }
        }

        public bool CanRunQuery =>
            !QueryRunning 
            && (ActiveDocument != null && ActiveDocument.IsConnected) 
            && !ActiveDocument.ShowMeasureExpressionEditor
            && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);

        public bool CanDisplayQueryBuilder =>
            !QueryRunning
            && (ActiveDocument != null && ActiveDocument.IsConnected)
            && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);

        public bool CanRunBenchmark =>
            !QueryRunning
            && (ActiveDocument != null && ActiveDocument.IsConnected && ActiveDocument.IsAdminConnection)
            && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);

        public void CancelQuery()
        {
            _eventAggregator.PublishOnUIThread(new CancelQueryEvent());
        }

        public bool CanCancelQuery => !CanRunQuery && (ActiveDocument != null && ActiveDocument.IsConnected);

        public bool CanClearCache => CanRunQuery && (ActiveDocument != null && ActiveDocument.IsAdminConnection);

        public string ClearCacheDisableReason
        {
            get 
            { 
                if (!ActiveDocument.IsAdminConnection) return "Only a server administrator can run the clear cache command";
                if (QueryRunning) return "A query is currently executing";
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
        public bool CanConnect =>
            ActiveDocument != null 
            && !QueryRunning 
            && !_isConnecting 
            && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);

        public ShellViewModel Shell { get; set; }

        public void Exit()
        {
            Shell.TryClose();
        }

        public void Open()
        {
            _eventAggregator.PublishOnUIThread(new OpenFileEvent());

        }

        private void RefreshConnectionDetails(IConnection connection)
        {
            var doc = ActiveDocument;
            
            if (connection == null)
            {
                Log.Debug(Common.Constants.LogMessageTemplate, "RibbonViewModel", "RefreshConnectionDetails", "connection == null");
                _isConnecting = false;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                TraceWatchers?.DisableAll();
                return;
            }

            try
            {
                Log.Debug("{Class} {Event} {ServerName}", "RibbonViewModel", "RefreshConnectionDetails", connection.ServerName);                
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
                NotifyOfPropertyChange(() => TraceLayoutGroupVisible);
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
        private bool QueryRunning => ActiveDocument?.IsQueryRunning??false;
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

        public IObservableCollection<ITraceWatcher> TraceWatchers => ActiveDocument?.TraceWatchers;

        public void Handle(ActivateDocumentEvent message)
        {
            DocumentViewModel doc = null;
            Log.Debug("{Class} {Event} {Document}", "RibbonViewModel", "Handle:ActivateDocumentEvent", message.Document.DisplayName);
            try
            {
                _isDocumentActivating = true;
                ActiveDocument = message.Document;
                doc = ActiveDocument;
                SelectedTarget = ActiveDocument.SelectedTarget;

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
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "IHandle<ActivateDocumentEvent>", ex.Message);
                doc?.OutputError($"Error Activating Document: {ex.Message}");
            }
   
            try
            {
                RefreshConnectionDetails(ActiveDocument.Connection);
                // TODO - do we still need to check trace watchers if we are not connected??
                UpdateTraceWatchers();
            }
            catch (AdomdConnectionException ex)
            {
                Log.Error("{class} {method} {Exception}", "RibbonViewModel", "Handle(ActivateDocumentEvent)", ex);
                ActiveDocument?.OutputError(ex.Message);
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
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanCancelQuery));
            NotifyOfPropertyChange(nameof(CanClearCache));
            NotifyOfPropertyChange(nameof(CanRefreshMetadata));
            NotifyOfPropertyChange(nameof(CanFormatQueryStandard));
            NotifyOfPropertyChange(nameof(CanCommentSelection));
            NotifyOfPropertyChange(nameof(CanUncommentSelection));
            NotifyOfPropertyChange(nameof(CanToUpper));
            NotifyOfPropertyChange(nameof(CanToLower));
            NotifyOfPropertyChange(nameof(CanSwapDelimiters));
            NotifyOfPropertyChange(nameof(CanMergeParameters));
            NotifyOfPropertyChange(nameof(CanUndo));
            NotifyOfPropertyChange(nameof(CanRedo));
            NotifyOfPropertyChange(nameof(CanConnect));
            NotifyOfPropertyChange(nameof(CanLaunchSqlProfiler));
            NotifyOfPropertyChange(nameof(CanLaunchExcel));
            NotifyOfPropertyChange(nameof(CanExportAllData));
            NotifyOfPropertyChange(nameof(CanExportAnalysisData));
            NotifyOfPropertyChange(nameof(CanViewAnalysisData));
            NotifyOfPropertyChange(nameof(CanLoadPowerBIPerformanceData));
            NotifyOfPropertyChange(nameof(CanDisplayQueryBuilder));
            NotifyOfPropertyChange(nameof(CanRunBenchmark));
            NotifyOfPropertyChange(nameof(CanNewQueryWithCurrentConnection));
            NotifyOfPropertyChange(nameof(TraceWatchers));
            UpdateTraceWatchers();
        }

        private void UpdateTraceWatchers()
        {
            if (TraceWatchers == null) return;

            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
            foreach (var tw in TraceWatchers)
            {
                tw.CheckEnabled(ActiveDocument.Connection, activeTrace);
            }
            NotifyOfPropertyChange(() => TraceLayoutGroupVisible);
        }

        private DocumentViewModel _activeDocument;
         
        public DocumentViewModel ActiveDocument
        {
            get => _activeDocument;
            set {
                if(_activeDocument != null) _activeDocument.PropertyChanged -= ActiveDocumentPropertyChanged;
                _activeDocument = value;
                NotifyOfPropertyChange(() => CanSave);
                NotifyOfPropertyChange(() => CanSaveAs);
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                NotifyOfPropertyChange(() => CanShowViewAsDialog);
                NotifyOfPropertyChange(() => IsActiveDocumentConnected);
                NotifyOfPropertyChange(() => IsActiveDocumentVertipaqAnalyzerRunning);
                NotifyOfPropertyChange(() => CanImportAnalysisData);
                NotifyOfPropertyChange(() => CanDisplayQueryBuilder);
                NotifyOfPropertyChange(() => DisplayQueryBuilder);
                NotifyOfPropertyChange(() => FormatQueryDisabledReason);
                NotifyOfPropertyChange(() => TraceLayoutGroupVisible);
                if (_activeDocument != null) _activeDocument.PropertyChanged += ActiveDocumentPropertyChanged;
            }
        }

        private void ActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ActiveDocument.IsQueryRunning):
                    NotifyOfPropertyChange(() => CanRunQuery);
                    NotifyOfPropertyChange(() => CanRunBenchmark);
                    NotifyOfPropertyChange(() => CanCancelQuery);
                    NotifyOfPropertyChange(() => CanClearCache);
                    NotifyOfPropertyChange(() => CanRefreshMetadata);
                    NotifyOfPropertyChange(() => CanConnect);
                    NotifyOfPropertyChange(() => CanViewAnalysisData);
                    NotifyOfPropertyChange(() => CanShowViewAsDialog);
                    break;
                case nameof(ActiveDocument.IsVertipaqAnalyzerRunning):
                    NotifyOfPropertyChange(() => CanViewAnalysisData);
                    NotifyOfPropertyChange(() => CanExportAnalysisData);
                    NotifyOfPropertyChange(() => CanRunBenchmark);
                    NotifyOfPropertyChange(() => CanShowViewAsDialog);
                    break;
                case nameof(ActiveDocument.IsConnected):
                    NotifyOfPropertyChange(() => CanRunQuery);
                    NotifyOfPropertyChange(() => CanCancelQuery);
                    NotifyOfPropertyChange(() => CanClearCache);
                    NotifyOfPropertyChange(() => CanRefreshMetadata);
                    NotifyOfPropertyChange(() => CanConnect);
                    NotifyOfPropertyChange(() => CanShowViewAsDialog);
                    NotifyOfPropertyChange(() => CanViewAnalysisData);
                    NotifyOfPropertyChange(() => CanExportAnalysisData);
                    NotifyOfPropertyChange(() => CanExportAllData);
                    NotifyOfPropertyChange(() => IsActiveDocumentConnected);
                    break;
                case nameof(ActiveDocument.ShowQueryBuilder):
                    NotifyOfPropertyChange(() => DisplayQueryBuilder);
                    break;
            }
        }

        public void Handle(QueryFinishedEvent message)
        {
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);
            NotifyOfPropertyChange(() => CanShowViewAsDialog);
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

        public void LinkToGithubBugReport()
        {
            var url = urlGithubBugReportPrefix + GetVersionInfoUrlEncoded() + urlGithubBugReportSuffix;
            OpenUrl(url, "LinkToGithubBugReport");
        }

        private string GetVersionInfoUrlEncoded()
        {
            string encodedString = string.Empty;
            try
            {
                var connectionDetail = "not connected";
                var activeConnection = ActiveDocument?.Connection;
                var isConnected = activeConnection?.IsConnected ?? false;

                if (isConnected)
                {
                    connectionDetail = $"{activeConnection.ServerType} - {activeConnection.ServerVersion}";
                }

                var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
                encodedString = WebUtility.UrlEncode($"DAX Studio v{version}\nConnection: {connectionDetail}\n\n");
            }
            catch (Exception ex)
            {
                // any errors should be swallowed
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), nameof(GetVersionInfoUrlEncoded), "Error encoding bug report body");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error,
                    $"Error encoding bug report body: {ex.Message}"));
            }

            return encodedString;
        }

        public void LinkToGithubFeatureRequest()
        {
            OpenUrl(urlGithubFeatureRequest, "LinkToGithubFeatureRequest");
        }

        public void LinkToGithubDiscussions()
        {
            OpenUrl(urlGithubDiscussions, "LinkToGithubDiscussions");
        }

        internal void OpenUrl(string url, string name)
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
        public async void Handle(ApplicationActivatedEvent message)
        {
            Log.Debug("{Class} {Event} {Message}", "RibbonViewModel", "Handle:ApplicationActivatedEvent", "Start");
            if (ActiveDocument != null)
            {
                await ActiveDocument.CheckForMetadataUpdatesAsync();
                RefreshConnectionDetails(ActiveDocument.Connection);
            }
            
            Log.Debug("{Class} {Event} {Messsage}", "RibbonViewModel", "Handle:ApplicationActivatedEvent", "End");
        }

        
        public void Handle(TraceChangingEvent message)
        {
            if (ActiveDocument != null)
                _traceMessage = new StatusBarMessage(ActiveDocument, "Waiting for trace to update");
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanConnect);
            NotifyOfPropertyChange(() => TraceLayoutGroupVisible);
        }

        public void Handle(TraceChangedEvent message)
        {
            if(_traceMessage != null) _traceMessage.Dispose();
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanConnect);
            NotifyOfPropertyChange(() => TraceLayoutGroupVisible);
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            RefreshConnectionDetails(message.Connection);
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

        public bool CanRefreshMetadata => ActiveDocument != null && ActiveDocument.IsConnected;

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

        public bool ServerTimingsChecked => ActiveDocument?.ServerTimingsChecked??false;

        public ServerTimingDetailsViewModel ServerTimingDetails => ActiveDocument?.ServerTimingDetails;

        public void Handle(TraceWatcherToggleEvent message)
        {
            if (message.TraceWatcher is ServerTimesViewModel)
            {
                NotifyOfPropertyChange(() => ServerTimingsChecked);
            }
        }

        public ObservableCollection<IDaxFile> RecentFiles { get; set; }

        internal void OnClose()
        {
            //SettingProvider.SaveFileMRUList(null, this.RecentFiles);
        }

        private void AddToRecentFiles(string fileName)
        {
            SettingProvider.SaveFileMRUList(new DaxFile(fileName,false), RecentFiles);

            //DaxFile df = (from DaxFile f in RecentFiles
            //              where f.FullPath.Equals(fileName, StringComparison.CurrentCultureIgnoreCase)
            //              select f).FirstOrDefault<DaxFile>();
            //if (df == null)
            //{
            //    RecentFiles.Insert(0, new DaxFile(fileName));
            //}
            //else
            //{
            //    // move the file to the first position in the list     
            //    RecentFiles.Move(RecentFiles.IndexOf(df), 0);
            //    //RecentFiles.Remove(df);
            //    //RecentFiles.Insert(0, df);
            //}

            //int MAX_RECENT_FILES = 25;
            //while (RecentFiles.Count() > MAX_RECENT_FILES) { RecentFiles.RemoveAt(RecentFiles.Count() - 1); }
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

            // Check if the file exists before attempting to open it
            if (RecentFileNoLongerExists(file)) return;

            // otherwise clost the backstage menu and open the file
            backstage.IsOpen = false;
            MoveFileToTopOfRecentList(file);
            _eventAggregator.PublishOnUIThread(new OpenDaxFileEvent(file.FullPath));
        }

        private bool RecentFileNoLongerExists(DaxFile file)
        {
            if (System.IO.File.Exists(file.FullPath)) return false;

            // File has been moved or deleted
            if (MessageBoxEx.Show($"The file '{file.FullPath}'\nhas been deleted, moved or renamed.\n\nWould you like to remove it from the recent files list?", "File not found", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.Yes)
            {
                RecentFiles.Remove(file);
            }
            Log.Warning("{class} {method} {message}", "RibbonViewModel", "RecentFileNoLongerExists", $"The following entry in the recent file list no longer exists '{file.FullPath}'");
            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"The following entry in the recent file list no longer exists '{file.FullPath}'"));
            return true;
        }

        private void MoveFileToTopOfRecentList(DaxFile file)
        {
            //// remove the file from it's current position
            //RecentFiles.Remove(file);
            //// insert it at the top of the list
            //RecentFiles.Insert(0, file);

            SettingProvider.SaveFileMRUList(file, this.RecentFiles);
        }

        public bool CanSwapDelimiters => ActiveDocument != null;
        public void SwapDelimiters()
        {
            ActiveDocument?.SwapDelimiters();
        }

        public void MoveCommasToDebugMode()
        {
            ActiveDocument?.MoveCommasToDebugMode();
        }

        public bool ShowSwapDelimiters
        {
            get
            {
                return !Options.ShowDebugCommas;
            }
        }

        public bool ShowDebugCommas
        {
            get
            {
                return Options.ShowDebugCommas;
            }
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


        #region "Preview Features"



        #endregion

        private bool _ResultAutoFormat;
        public bool ResultAutoFormat {
            get => _ResultAutoFormat;
            private set {
                _ResultAutoFormat = value;
                NotifyOfPropertyChange(() => ResultAutoFormat);
            }
        }

        public bool CanImportAnalysisData => ActiveDocument != null && !ActiveDocument.IsVertipaqAnalyzerRunning; // we don't need a valid connection to import a .vpax file
        public void ImportAnalysisData()
        {
            ActiveDocument?.ImportAnalysisData();
        }

        public bool CanExportAnalysisData => IsActiveDocumentConnected && !IsActiveDocumentVertipaqAnalyzerRunning;

        public async void ExportAnalysisData()
        {
            await ActiveDocument?.ExportAnalysisDataAsync();
        }

        public bool CanViewAnalysisData => IsActiveDocumentConnected && !QueryRunning;

        public async void ViewAnalysisData()
        {
            await ActiveDocument?.ViewAnalysisDataAsync();
        }

        public bool CanExportAllData => IsActiveDocumentConnected;

        public void ExportAllData()
        {
            if (ActiveDocument == null) return;
            try
            {
                //using (var dialog = new ExportDataDialogViewModel(_eventAggregator, ActiveDocument))
                using (var dialog = new ExportDataWizardViewModel(_eventAggregator, ActiveDocument, Options))
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
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(RibbonViewModel), nameof(ExportAllData), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Exporting All Data: {ex.Message}"));
            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            UpdateGlobalOptions();
        }

        private void UpdateGlobalOptions()
        {
            ResultAutoFormat = Options.ResultAutoFormat;
            NotifyOfPropertyChange(nameof(FormatQueryAlternateTitle));
            NotifyOfPropertyChange(nameof(FormatQueryStandardTitle));
            NotifyOfPropertyChange(nameof(FormatQueryDisabledReason));
            NotifyOfPropertyChange(nameof(CanFormatQueryStandard));
            NotifyOfPropertyChange(nameof(ShowDebugCommas));
            NotifyOfPropertyChange(nameof(ShowSwapDelimiters));
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

        public bool CanLaunchSqlProfiler => IsActiveDocumentConnected && !string.IsNullOrEmpty(_sqlProfilerCommand);

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

        public bool CanLaunchExcel { 
            get { 
                if (!IsActiveDocumentConnected) return false;
                if (ActiveDocument.Connection.IsPowerPivot) return false;
                return true;
            } 
        }

        private bool IsActiveDocumentConnected
        {
            get {
                if (ActiveDocument == null) return false;
                if (!ActiveDocument.Connection.IsConnected) return false;

                return true;
            }
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
            get => _theme;
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

        public bool IsActiveDocumentVertipaqAnalyzerRunning { get; private set; }

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

        public bool CanLoadPowerBIPerformanceData => ActiveDocument != null;

        public void LoadPowerBIPerformanceData()
        {
            if (this.ActiveDocument == null)
            {
                MessageBoxEx.Show("You cannot load Power BI Performance data when you do not have a document window open", "Load Power BI Performance Data");
                return;
            }

            // Configure open file dialog box
            using (var dlg = new System.Windows.Forms.OpenFileDialog()
            {
                Title = "Open Power BI Performance Data",
                FileName = "PowerBIPerformanceData.json",
                DefaultExt = ".json",
                Filter = "Power BI Performance Data (*.json)|*.json"
            })
            {
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
                    else
                    {
                        // make sure the window is not hidden
                        perfDataWindow.IsVisible = true;
                    }

                    // load the perfomance data
                    perfDataWindow.FileName = fileName;

                    // set the performance window as the active tab
                    perfDataWindow.Activate();
                }
            }
        }


        public bool DisplayQueryBuilder {
            get => ActiveDocument?.ShowQueryBuilder??false;
            set {
                ActiveDocument.ShowQueryBuilder = value;
                NotifyOfPropertyChange(nameof(DisplayQueryBuilder));
            }
        }


        public void RunBenchmark()
        {
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(this.SelectedTarget, this.SelectedRunStyle, true));
        }

        public bool TraceLayoutGroupVisible { get
            {
                if (ActiveDocument == null) return false;
                if (TraceWatchers == null) return false;
                return TraceWatchers.Any(tw => tw.IsChecked && tw is ServerTimesViewModel);
            } 
        }

        public void Handle(UpdateHotkeys message)
        {
            // TODO - should we create an attribute for these properties so that we don't
            //        have to maintain a hard coded list?
            NotifyOfPropertyChange(nameof(CommentSelectionTitle));
            NotifyOfPropertyChange(nameof(UncommentSelectionTitle));
            NotifyOfPropertyChange(nameof(ToLowerTitle));
            NotifyOfPropertyChange(nameof(ToUpperTitle));
            NotifyOfPropertyChange(nameof(FormatQueryTitle));
            NotifyOfPropertyChange(nameof(RunQueryTitle));
        }

        public void CrashTest()
        {
            throw new Exception("This is a fake exception to test the crash reporting");
        }

        public void OpenConnection()
        {
            ActiveDocument?.OpenConnection();
        }

        public void CloseConnection()
        {
            ActiveDocument?.CloseConnection();
        }

        public bool CanShowViewAsDialog
        {
            get =>
                (ActiveDocument?.IsConnected ?? false)
                  && !(ActiveDocument?.IsViewAsActive ??false)
                  && !(ActiveDocument?.IsQueryRunning ?? false)
                  && !(ActiveDocument?.IsVertipaqAnalyzerRunning ?? false);
            
        }
        public void ShowViewAsDialog()
        {
            ActiveDocument?.ShowViewAsDialog();
        }

    }
}
