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
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using ADOTabular;
using DaxStudio.Interfaces.Enums;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ActivateDocumentEvent>
        , IHandle<AllDocumentsClosedEvent>
        , IHandle<ApplicationActivatedEvent>
        , IHandle<ConnectFailedEvent>
        , IHandle<ConnectionPendingEvent>
        , IHandle<CancelConnectEvent>
        , IHandle<ChangeThemeEvent>
        , IHandle<DatabaseChangedEvent>
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
        , IHandle<SetRunStyleEvent>

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
        private const string urlSsasForum = "https://docs.microsoft.com/en-us/answers/topics/sql-server-analysis-services.html";
        private const string urlPbiDesktopForum = "https://community.powerbi.com/t5/Desktop/bd-p/power-bi-designer";
        private const string urlDaxForum = "https://community.powerbi.com/t5/DAX-Commands-and-Tips/bd-p/DAXCommands";
        private const string urlGithubBugReportPrefix = @"https://github.com/DaxStudio/DaxStudio/issues/new?labels=from+app&template=bug_report.md&body=";
        private const string urlGithubBugReportSuffix = @"%23%23%20Summary%20of%20Issue%0A%0A%0A%23%23%20Steps%20to%20Reproduce%0A1.%0A2.";
        private const string urlGithubFeatureRequest = @"https://github.com/DaxStudio/DaxStudio/issues/new?assignees=&labels=from+app&template=feature_request.md&title=";

        internal void ToggleTheme()
        {
            switch( this.Theme)
            {
                case UITheme.Light: 
                    Theme = UITheme.Dark;
                    break;
                case UITheme.Dark:
                    Theme = UITheme.Auto;
                    break;
                default: 
                    Theme = UITheme.Light;
                    break;
            }
        }

        public string ThemeImageResource => Theme == UITheme.Auto? "file_auto_themeDrawingImage":"file_themeDrawingImage";



        private const string urlGithubDiscussions = @"https://github.com/DaxStudio/DaxStudio/discussions";
        private const string urlSponsors = @"https://daxstudio.org/sponsors/";
        private ISettingProvider SettingProvider;
        [ImportingConstructor]
        public RibbonViewModel(IDaxStudioHost host, IEventAggregator eventAggregator, IWindowManager windowManager, IGlobalOptions options, ISettingProvider settingProvider)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnPublishedThread(this);
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
            ClearCacheAuto = Options.SetClearCacheAsDefaultRunStyle;
        }



        private void InitRunStyles()
        {
            // populate run styles
            RunStyles.Add(new RunStyle("Run Query", RunStyleIcons.RunOnly,  "Executes the text in the Editor and sends the results to the selected output"));
            RunStyles.Add(new RunStyle("Run Query Builder", RunStyleIcons.RunBuilder,"Executes the Query Builder and sends the results to the selected output"));
            //RunStyles.Add(new RunStyle("Clear Cache then Run", RunStyleIcons.ClearThenRun, true,false,false, "Clears the database cache, then executes the query and sends the results to the selected output"));
#if DEBUG
            //            RunStyles.Add(new RunStyle("Benchmark", RunStyleIcons.RunBenchmark, false, false, false, "Executes the query multiple times and captures the timings"));
            //RunStyles.Add(new RunStyle("Run Table Function", RunStyleIcons.RunFunction, true, true,false, "Attempts to executes the selected function by inserting 'EVALUATE' in front of it and sends the results to the selected output"));
            //RunStyles.Add(new RunStyle("Run Measure", RunStyleIcons.RunScalar, true, true, true, "Attempts to executes the selected measure or scalar function by wrapping the selection with 'EVALUATE ROW(...)' and sends the results to the selected output"));
#endif
            // set default run style
            var defaultRunStyle = RunStyleIcons.RunOnly;

            SelectedRunStyle = RunStyles.FirstOrDefault(rs => rs.Icon == defaultRunStyle);
        }

        public ObservableCollection<RunStyle> RunStyles { get; } = new ObservableCollection<RunStyle>();
        private RunStyle _selectedRunStyle;
        public RunStyle SelectedRunStyle {
            get => _selectedRunStyle;
            set { _selectedRunStyle = value;
                NotifyOfPropertyChange(() => SelectedRunStyle);
                _eventAggregator?.PublishOnUIThreadAsync(new RunStyleChangedEvent(SelectedRunStyle));
                //RunQuery(); // TODO if we change run styles should we immediately run the query with the new style??
            } }
        public IGlobalOptions Options { get; private set; }
        public Visibility OutputGroupIsVisible => _host.IsExcel ? Visibility.Visible : Visibility.Collapsed;

        public async Task NewQuery()
        {
            await _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(SelectedTarget));
        }

        //public string NewQueryTitle => $"New ({Options.HotkeyNewDocument})";

        public void NewQueryWithCurrentConnection(bool copyContent = false)
        {
            if (ActiveDocument == null) return;
            _eventAggregator.PublishOnUIThreadAsync(new NewDocumentEvent(SelectedTarget, ActiveDocument, copyContent)).ContinueWith((precedent) => { 
                if (precedent.IsFaulted)
                {
                    var msg = "Error opening new document with current connection";
                    Log.Error(precedent.Exception, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), nameof(NewQueryWithCurrentConnection), msg);
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"{msg}\n{precedent.Exception.Message}"));
                }
            });
        }

        public bool CanNewQueryWithCurrentConnection => ActiveDocument != null && ActiveDocument.IsConnected;

        public bool CanCommentSelection => ActiveDocument != null;

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CommentEvent(true));
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
            _eventAggregator.PublishOnUIThreadAsync(new CommentEvent(false));
        }
        public string UncommentSelectionTitle => $"Uncomment ({Options.HotkeyUnCommentSelection})";
        public bool CanToUpper => ActiveDocument != null;

        public void ToUpper()
        {
            _eventAggregator.PublishOnUIThreadAsync(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }
        public string ToUpperTitle => $"To Upper ({Options.HotkeyToUpper})";

        public bool CanToLower => ActiveDocument != null;

        public void ToLower()
        {
            _eventAggregator.PublishOnUIThreadAsync(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }
        public string ToLowerTitle => $"To Lower ({Options.HotkeyToLower})";
        public void RunQuery()
        {
            
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);

            var runStyle = SelectedRunStyle;
            runStyle.ClearCache = ClearCacheAuto;
            _eventAggregator.PublishOnUIThreadAsync(new RunQueryEvent(SelectedTarget, runStyle) );

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

        private bool IsTraceChanging() { return _traceStatus == QueryTraceStatus.Starting || _traceStatus == QueryTraceStatus.Stopping || _traceStatus == QueryTraceStatus.Unknown; }

        public bool CanRunQuery =>
            !QueryRunning 
            && (ActiveDocument != null && ActiveDocument.IsConnected) 
            && !ActiveDocument.ShowMeasureExpressionEditor
            && !IsTraceChanging();

        public bool CanDisplayQueryBuilder =>
            !QueryRunning
            && (ActiveDocument != null && ActiveDocument.IsConnected)
            && !IsTraceChanging();

        public bool CanRunBenchmark =>
            !QueryRunning
            && (ActiveDocument != null && ActiveDocument.IsConnected && ActiveDocument.IsAdminConnection)
            && !IsTraceChanging();


        public bool CanRunServerFEBenchmark =>
            !QueryRunning
            && (ActiveDocument != null && ActiveDocument.IsConnected && ActiveDocument.IsAdminConnection)
            && !IsTraceChanging();

        public void CancelQuery()
        {
            _eventAggregator.PublishOnUIThreadAsync(new CancelQueryEvent());
        }

        public bool CanCancelQuery => !CanRunQuery && (ActiveDocument != null && ActiveDocument.IsConnected);

        public bool CanClearCache => CanRunQuery && (ActiveDocument != null && ActiveDocument.IsAdminConnection);

        public string ClearCacheDisableReason
        {
            get 
            { 
                if (ActiveDocument == null) return "Query window not connected to a model";
                if (!ActiveDocument.IsAdminConnection) return "Only a server administrator can run the clear cache command";
                if (QueryRunning) return "A query is currently executing";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                return "Cannot clear the cache while a query is currently running";
            }
        }

        public bool CanClearCacheAuto => IsActiveDocumentConnected;

        public bool _clearCacheAuto = false;
        public bool ClearCacheAuto { get => _clearCacheAuto;
            set { 
                _clearCacheAuto = value;
                NotifyOfPropertyChange();
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

        public void SaveAsDaxx()
        {
            ActiveDocument?.SaveAs(Enums.SaveAsExtension.daxx);
        }

        public async void Connect()
        {
            if (ActiveDocument == null) await NewQuery();
            else await ActiveDocument.ChangeConnectionAsync();
        }

        //private bool _canConnect;
        public bool CanConnect =>
            ActiveDocument != null 
            && !QueryRunning 
            && !_isConnecting 
            && !IsTraceChanging();

        public ShellViewModel Shell { get; set; }

        public void Exit(FrameworkElement view)
        {
            Fluent.Backstage backstage = GetBackStageParent(view) as Fluent.Backstage;
            backstage.IsOpen = false;
            Application.Current.Shutdown();
        }

        public async void Open(FrameworkElement view)
        {
            Fluent.Backstage backstage = GetBackStageParent(view) as Fluent.Backstage;
            await _eventAggregator.PublishOnUIThreadAsync(new OpenFileEvent());
            backstage.IsOpen = false;
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
                NotifyOfPropertyChange(() => CanClearCacheAuto);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                TraceWatchers?.DisableAll();
                return;
            }

            try
            {
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanClearCacheAuto);
                NotifyOfPropertyChange(() => CanRefreshMetadata);
                NotifyOfPropertyChange(() => CanConnect);
                UpdateTraceWatchers();
                Log.Debug("{Class} {Event} {ServerName}", "RibbonViewModel", "RefreshConnectionDetails", connection.ServerName);                
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), nameof(RefreshConnectionDetails), "Error refreshing connection");
                doc.OutputError(ex.Message);
            }
            finally
            {
                _isConnecting = false;
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
                Log.Verbose("{class} {property} {value}", "RibbonViewModel", "SelectedTarget:Set", value?.Name??"<null>");
                if (_selectedTarget is IActivateResults)
                    ActiveDocument?.ActivateResults();
                NotifyOfPropertyChange(()=>SelectedTarget);
                if (!_isDocumentActivating)
                {
                    _eventAggregator.PublishOnUIThreadAsync(new QueryResultsPaneMessageEvent(_selectedTarget));
                }
                if (_selectedTarget is IActivateResults) { ActiveDocument?.ActivateResults(); }
                
            }
        }

        public IObservableCollection<ITraceWatcher> TraceWatchers => ActiveDocument?.TraceWatchers;

        public Task HandleAsync(ActivateDocumentEvent message, CancellationToken cancellationToken)
        {
            DocumentViewModel doc = null;
            try
            {
                Log.Debug("{Class} {Event} {Document}", "RibbonViewModel", "Handle:ActivateDocumentEvent", message.Document.DisplayName);
                _isDocumentActivating = true;
                ActiveDocument = message.Document;
                doc = ActiveDocument;
                SelectedTarget = ActiveDocument.SelectedTarget??SelectedTarget;

                _traceStatus = GetTraceStatus();

                RefreshRibbonButtonEnabledStatus();

                if (!ActiveDocument.IsConnected)
                {
                    UpdateTraceWatchers();
                    NotifyOfPropertyChange(() => TraceWatchers);
                    NotifyOfPropertyChange(() => ServerTimingsChecked);
                    NotifyOfPropertyChange(() => ServerTimingDetails);
                    return Task.CompletedTask;
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

            _eventAggregator.PublishOnUIThreadAsync(new DocumentActivatedEvent(message.Document));
            return Task.CompletedTask;
        }

        private QueryTraceStatus GetTraceStatus()
        {
            var watchers = ActiveDocument.TraceWatchers;
            var status = QueryTraceStatus.Stopped; // assume no traces are running
            foreach (var tw in watchers)
            {
                switch (tw.TraceStatus)
                {
                    // if any of the trace watchers are in a changing or error state
                    // return the state of the first one we find
                    case QueryTraceStatus.Starting:
                    case QueryTraceStatus.Stopping:
                    case QueryTraceStatus.Error:
                        return tw.TraceStatus;
                    // if one or more traces are started and none are in a changing or 
                    // error state we will return "Started"
                    case QueryTraceStatus.Started:
                        status = tw.TraceStatus;
                        break;
                    case QueryTraceStatus.Stopped:
                    case QueryTraceStatus.Unknown:
                        // do nothing in these cases
                        break;

                }

            }
            return status;
        }

        private void RefreshRibbonButtonEnabledStatus()
        {
            NotifyOfPropertyChange(nameof(CanRunQuery));
            NotifyOfPropertyChange(nameof(CanCancelQuery));
            NotifyOfPropertyChange(nameof(CanClearCache));
            NotifyOfPropertyChange(nameof( CanClearCacheAuto));
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
            NotifyOfPropertyChange(nameof(CanRunServerFEBenchmark));
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
                tw.CheckEnabled(ActiveDocument?.Connection, activeTrace);
            }
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
                    NotifyOfPropertyChange(() => CanRunServerFEBenchmark);
                    NotifyOfPropertyChange(() => CanCancelQuery);
                    NotifyOfPropertyChange(() => CanClearCache);
                    NotifyOfPropertyChange(nameof(CanClearCacheAuto));
                    NotifyOfPropertyChange(() => CanRefreshMetadata);
                    NotifyOfPropertyChange(() => CanConnect);
                    NotifyOfPropertyChange(() => CanViewAnalysisData);
                    NotifyOfPropertyChange(() => CanShowViewAsDialog);
                    break;
                case nameof(ActiveDocument.IsVertipaqAnalyzerRunning):
                    NotifyOfPropertyChange(() => CanViewAnalysisData);
                    NotifyOfPropertyChange(() => CanExportAnalysisData);
                    NotifyOfPropertyChange(() => CanRunBenchmark);
                    NotifyOfPropertyChange(() => CanRunServerFEBenchmark);
                    NotifyOfPropertyChange(() => CanShowViewAsDialog);
                    break;
                case nameof(ActiveDocument.IsConnected):
                    NotifyOfPropertyChange(() => CanRunQuery);
                    NotifyOfPropertyChange(() => CanCancelQuery);
                    NotifyOfPropertyChange(() => CanClearCache);
                    NotifyOfPropertyChange(nameof(CanClearCacheAuto));
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

        public Task HandleAsync(QueryFinishedEvent message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanRefreshMetadata);
            NotifyOfPropertyChange(() => CanConnect);
            NotifyOfPropertyChange(() => CanShowViewAsDialog);
            return Task.CompletedTask;
        }

        public void LinkToDaxStudioWiki()
        {
            OpenUrl(urlDaxStudioWiki, "LinkToDaxStudioWiki");        
        }



        public void LinkToPowerPivotForum()
        {
            OpenUrl(urlPowerPivotForum, nameof(LinkToPowerPivotForum));
        }

        public void LinkToSsasForum()
        {
            OpenUrl(urlSsasForum, nameof(LinkToSsasForum));
        }

        public void LinkToPbiDesktopForum()
        {
            OpenUrl(urlPbiDesktopForum, nameof(LinkToPbiDesktopForum));
        }

        public void LinkToDaxForum()
        {
            OpenUrl(urlDaxForum, nameof(LinkToDaxForum));
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
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error,
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
        public void LinkToSponsors()
        {
            OpenUrl(urlSponsors, "LinkToSponsors");
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
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, string.Format("The following error occurred while trying to open the {1}: {0}", ex.Message, name)));
            }
        }

        public Task HandleAsync(ConnectionPendingEvent message, CancellationToken cancellationToken)
        {
            _isConnecting = true;
            return Task.CompletedTask;
        }
        public async Task HandleAsync(ApplicationActivatedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                Log.Debug("{Class} {Event} {Message}", "RibbonViewModel", "Handle:ApplicationActivatedEvent", "Start");

                if (ActiveDocument != null)
                {
                    await ActiveDocument?.CheckForMetadataUpdatesAsync();
                    RefreshConnectionDetails(ActiveDocument.Connection);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "Handle<ApplicationActivatedEvent>", "Error Activating Application");
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Activating Application:\n{ex.Message}"));
            }
            finally
            {
                Log.Debug("{Class} {Event} {Messsage}", "RibbonViewModel", "Handle:ApplicationActivatedEvent", "End");
            }
            
        }

        
        public Task HandleAsync(TraceChangingEvent message, CancellationToken cancellationToken)
        {
            try
            {
                if (ActiveDocument != null)
                    _traceMessage = new StatusBarMessage(ActiveDocument, "Waiting for trace to update");
                _traceStatus = message.TraceStatus;
                RefreshRibbonButtonEnabledStatus();
            }
            catch(Exception ex)
            {
                var msg = $"Error updating trace status\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel),"Handle<TraceChangingEvent>" , msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
            return Task.CompletedTask;
        }

        public Task HandleAsync(TraceChangedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                if (_traceMessage != null) _traceMessage.Dispose();
                _traceStatus = message.TraceStatus;
                RefreshRibbonButtonEnabledStatus();
            }
            catch (Exception ex)
            {
                var msg = $"Error updating trace status:\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel),"Handle<TraceChangedEvent>", msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(DocumentConnectionUpdateEvent message, CancellationToken cancellationToken)
        {
            try
            {
                RefreshConnectionDetails(message.Connection);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "Handle<DocumentConnectionUpdateEvent>", "Error updating the current connection");
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error updating the current connection\n{ex.Message}"));
            }
           
        }
        public async Task HandleAsync(ConnectFailedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                RefreshConnectionDetails(null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "Handle<ConnectFailedEvent>", "Error updating the current connection");
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error handling the failed connection attempt\n{ex.Message}"));
            }

        }
        public bool CanCut { get; set; }
        
        public bool CanCopy { get;set; }
        
        public bool CanPaste { get; set; }
        
        [Import]
        HelpAboutViewModel aboutDialog { get; set; }

        public async void ShowHelpAbout()
        {
            await _windowManager.ShowDialogBoxAsync(aboutDialog , 
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
            if (QueryRunning) {
                ActiveDocument.OutputWarning("Metadata cannot be refreshed while a query is running");
                return; 
            }
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

        public Task HandleAsync(TraceWatcherToggleEvent message, CancellationToken cancellationToken)
        {
            try
            {
                if (message.TraceWatcher is ServerTimesViewModel)
                {
                    NotifyOfPropertyChange(() => ServerTimingsChecked);
                }
                RefreshRibbonButtonEnabledStatus();
            }
            catch (Exception ex)
            {
                var msg = $"Error enabling trace:\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel),"Handle<TraceWatcherToggleEvent>", msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
            return Task.CompletedTask;
        }

        public ObservableCollection<IDaxFile> RecentFiles { get; set; }

        internal void OnClose()
        {
            //SettingProvider.SaveFileMRUList(null, this.RecentFiles);
        }

        private void AddToRecentFiles(string fileName)
        {
            SettingProvider.SaveFileMRUList(new DaxFile(fileName,false));

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

        public Task HandleAsync(FileOpenedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                AddToRecentFiles(message.FileName);
                RefreshRibbonButtonEnabledStatus();
            }
            catch (Exception ex)
            {
                var msg = $"Error opening file\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "Handle<FileOpenedEvent>", msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
            return Task.CompletedTask;
        }

        public Task HandleAsync(FileSavedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                AddToRecentFiles(message.FileName);
            }
            catch(Exception ex)
            {
                var msg = $"Error saving file:\n{ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel),"Handle<FileSavedEvent>", msg);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
            }
            return Task.CompletedTask;
        }

        public void OpenRecentFile(DaxFile file, FrameworkElement backstage)
        {
            
            Fluent.Backstage item = GetBackStageParent(backstage as FrameworkElement) as Fluent.Backstage;
            OpenRecentFile(file, item);

        }

        public FrameworkElement GetBackStageParent(FrameworkElement element)
        {
            if (element == null) return null;
            if (element.Parent == null) return null;
            if (null != (element.Parent as Fluent.Backstage)) return element.Parent as FrameworkElement;
            return GetBackStageParent(element.Parent as FrameworkElement);
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
            _eventAggregator.PublishOnUIThreadAsync(new OpenDaxFileEvent(file.FullPath));
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
            _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"The following entry in the recent file list no longer exists '{file.FullPath}'"));
            return true;
        }

        private void MoveFileToTopOfRecentList(DaxFile file)
        {
            SettingProvider.SaveFileMRUList(file);
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

        public string DebugCommasTitle => "Debug Commas (" + Options.HotkeyDebugCommas + ")";
        public string SwapDelimitersTitle => "Swap Delimiters (" + Options.HotkeySwapDelimiters + ")";
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

        public bool CanExportAnalysisData => IsActiveDocumentConnected && !IsActiveDocumentVertipaqAnalyzerRunning 
                                            || (ActiveDocument?.ToolWindows.Any(tw => tw is VertiPaqAnalyzerViewModel) ?? false);

        public async void ExportAnalysisData()
        {
            try
            {
                await ActiveDocument?.ExportAnalysisDataAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), nameof(ExportAnalysisData), "Error while exporting metrics");
                ActiveDocument?.OutputError(ex.Message);
            }
        }

        public bool CanViewAnalysisData => IsActiveDocumentConnected && !QueryRunning;

        public async void ViewAnalysisData()
        {
            await ActiveDocument?.ViewAnalysisDataAsync();
        }

        public bool CanExportAllData => IsActiveDocumentConnected;

        public async void ExportAllData()
        {
            if (ActiveDocument == null) return;
            try
            {
                //using (var dialog = new ExportDataDialogViewModel(_eventAggregator, ActiveDocument))
                using (var dialog = new ExportDataWizardViewModel(_eventAggregator, ActiveDocument, Options))
                {

                    await _windowManager.ShowDialogBoxAsync(dialog, settings: new Dictionary<string, object>
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
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error Exporting All Data: {ex.Message}"));
            }
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            UpdateGlobalOptions();
            return Task.CompletedTask;
        }

        private void UpdateGlobalOptions()
        {
            ResultAutoFormat = Options.ResultAutoFormat;
            ClearCacheAuto = Options.SetClearCacheAsDefaultRunStyle;
            NotifyOfPropertyChange(nameof(FormatQueryAlternateTitle));
            NotifyOfPropertyChange(nameof(FormatQueryStandardTitle));
            NotifyOfPropertyChange(nameof(FormatQueryDisabledReason));
            NotifyOfPropertyChange(nameof(CanFormatQueryStandard));
            NotifyOfPropertyChange(nameof(ShowDebugCommas));
            NotifyOfPropertyChange(nameof(ShowSwapDelimiters));
            NotifyOfPropertyChange(nameof(DebugCommasTitle));
            NotifyOfPropertyChange(nameof(SwapDelimitersTitle));
            NotifyOfPropertyChange(nameof(ShowFEBenchmark));
        }

        public async void LaunchSqlProfiler()
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
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, "Error Launching SQL Profiler: " + ex.Message));
            }
        }

        public bool CanLaunchSqlProfiler => IsActiveDocumentConnected && !string.IsNullOrEmpty(_sqlProfilerCommand);

        public async void LaunchExcel()
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
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, "Error Launching Excel: " + ex.Message));

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
            _eventAggregator.PublishOnUIThreadAsync(new DockManagerSaveLayout());
        }

        public void LoadLayout()
        {
            _eventAggregator.PublishOnUIThreadAsync(new DockManagerLoadLayout(false));
        }

        public void ResetLayout()
        {
            _eventAggregator.PublishOnUIThreadAsync(new DockManagerLoadLayout(true));
        }

        private UITheme _theme = UITheme.Auto; // default to auto theme
        public UITheme Theme
        {
            get => _theme;
            set { if (value != _theme)
                {
                    _theme = value;
                    Options.Theme = _theme;
                    _eventAggregator.PublishOnUIThreadAsync(new ChangeThemeEvent(_theme));
                    NotifyOfPropertyChange(() => Theme);
                    NotifyOfPropertyChange(() => ThemeImageResource);
                }
            }
        }

        public bool IsActiveDocumentVertipaqAnalyzerRunning { get; private set; }

        public Task HandleAsync(AllDocumentsClosedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                this.ActiveDocument = null;
                RefreshRibbonButtonEnabledStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "IHandle<AllDocumentsClosedEvent>", "Error updating ribbon for all documents closed event");
            }
            return Task.CompletedTask;
        }

        public Task HandleAsync(RefreshOutputTargetsEvent message, CancellationToken cancellationToken)
        {
            // This message tell fluent ribbon that the ResultsTargets collection has changed
            // and should be re-evaluated
            NotifyOfPropertyChange(() => ResultsTargets);
            return Task.CompletedTask;
        }

        public Task HandleAsync(CancelConnectEvent message, CancellationToken cancellationToken)
        {
            _isConnecting = false;
            return Task.CompletedTask;
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
                        perfDataWindow = IoC.Get<PowerBIPerformanceDataViewModel>(); // new PowerBIPerformanceDataViewModel(_eventAggregator, Options);
                        _eventAggregator.SubscribeOnPublishedThread(perfDataWindow);
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
                NotifyOfPropertyChange();
            }
        }


        public async void RunBenchmark()
        {
            await _eventAggregator.PublishOnUIThreadAsync(new RunQueryEvent(this.SelectedTarget, this.SelectedRunStyle, RunQueryEvent.BenchmarkTypes.QueryBenchmark));
        }
        public async void RunServerFEBenchmark()
        {
            await _eventAggregator.PublishOnUIThreadAsync(new RunQueryEvent(this.SelectedTarget, this.SelectedRunStyle, RunQueryEvent.BenchmarkTypes.ServerFEBenchmark));
        }

        public async void CaptureDiagnostics()
        {
            if (ActiveDocument != null)
            {

            }
            var capdiagDialog = new CaptureDiagnosticsViewModel(this, Options, _eventAggregator);
            _eventAggregator.SubscribeOnPublishedThread(capdiagDialog);
            await _windowManager.ShowDialogBoxAsync(capdiagDialog);
            _eventAggregator.Unsubscribe(capdiagDialog);
        }

        public Task HandleAsync(UpdateHotkeys message, CancellationToken cancellationToken)
        {
            // TODO - should we create an attribute for these properties so that we don't
            //        have to maintain a hard coded list?
            NotifyOfPropertyChange(nameof(CommentSelectionTitle));
            NotifyOfPropertyChange(nameof(UncommentSelectionTitle));
            NotifyOfPropertyChange(nameof(ToLowerTitle));
            NotifyOfPropertyChange(nameof(ToUpperTitle));
            NotifyOfPropertyChange(nameof(FormatQueryTitle));
            NotifyOfPropertyChange(nameof(RunQueryTitle));
            return Task.CompletedTask;
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

        public Task HandleAsync(SetRunStyleEvent message, CancellationToken cancellationToken)
        {
            SelectedRunStyle = RunStyles.FirstOrDefault(rs => rs.Icon == message.Icon);
            return Task.CompletedTask;
        }

        public void RunStyleClicked(RunStyle runStyle)
        {
            if (runStyle == null)
            {
                // todo - log an error
                return;
            }
            System.Diagnostics.Debug.WriteLine("RunStyle Clicked");
            SelectedRunStyle = runStyle;
            RunQuery();
        }

        public Task HandleAsync(ChangeThemeEvent message, CancellationToken cancellationToken)
        {
            Theme = message.Theme;
            NotifyOfPropertyChange(nameof(ThemeImageResource));
            return Task.CompletedTask;
        }

        public Task HandleAsync(DatabaseChangedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                RefreshConnectionDetails(ActiveDocument?.Connection);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(RibbonViewModel), "IHandle<DatabaseChangedEvent>", "error handling event");
                ActiveDocument?.OutputError(ex.Message);
            }
            return Task.CompletedTask;
        }

        public bool ShowFEBenchmark => Options.ShowFEBenchmark;
    }
}
