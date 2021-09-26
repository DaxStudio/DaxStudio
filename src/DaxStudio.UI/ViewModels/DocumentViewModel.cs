using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.Interfaces;
using ADOTabular.MetadataInfo;
using ADOTabular.Utils;
using Caliburn.Micro;
using Dax.Metadata.Extractor;
using Dax.ViewModel;
using DAXEditorControl;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using DaxStudio.Interfaces.Enums;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Utils.DelimiterTranslator;
using DaxStudio.UI.Utils.Intellisense;
using DaxStudio.UI.Views;
using GongSolutions.Wpf.DragDrop;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.AnalysisServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;
using UnitComboLib.Unit.Screen;
using UnitComboLib.ViewModel;
using AvalonDock;
using AvalonDock.Themes;
using Action = System.Action;
using Constants = DaxStudio.Common.Constants;
using Timer = System.Timers.Timer;

namespace DaxStudio.UI.ViewModels
{


    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export(typeof(Screen))]
    [Export(typeof(DocumentViewModel))]
    public class DocumentViewModel : Screen
        , IDaxDocument
        , IHandle<ApplicationActivatedEvent>
        , IHandle<CancelConnectEvent>
        , IHandle<CancelQueryEvent>
        , IHandle<CommentEvent>
        , IHandleWithTask<ConnectEvent>
        , IHandle<CloseTraceWindowEvent>
        , IHandle<CopyConnectionEvent>
        , IHandle<DefineMeasureOnEditor>
        , IHandle<ExportDaxFunctionsEvent>
        , IHandle<LoadFileEvent>
        , IHandle<LoadQueryBuilderEvent>
        , IHandle<NavigateToLocationEvent>
        , IHandle<OutputMessage>
        , IHandle<QueryResultsPaneMessageEvent>
        , IHandle<ReconnectEvent>
        , IHandle<RunQueryEvent>
        , IHandle<RunStyleChangedEvent>
        , IHandle<SelectionChangeCaseEvent>
        , IHandle<SendTextToEditor>
        , IHandle<SelectedModelChangedEvent>
        , IHandle<EditorHotkeyEvent>
        , IHandle<SetSelectedWorksheetEvent>
        , IHandle<ShowMeasureExpressionEditor>
        , IHandle<ShowTraceWindowEvent>
        , IHandle<TraceWatcherToggleEvent>
        , IHandle<DockManagerLoadLayout>
        , IHandle<DockManagerSaveLayout>
        , IHandle<UpdateGlobalOptions>
        , IHandle<SetFocusEvent>
        , IHandle<ToggleCommentEvent>
        , IDropTarget
        , IQueryRunner
        , IQueryTextProvider
        , IHaveShutdownTask
        //, IConnection
        , ISaveable

    {
        // Changed from the original Unicode - if required we could make this an optional setting in future
        // but UTF8 seems to be the most sensible default going forward
        private readonly Encoding _defaultFileEncoding = Encoding.UTF8;

        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private IObservableCollection<object> _toolWindows;
        private BindableCollection<ITraceWatcher> _traceWatchers;
        private bool _queryRunning;
        private readonly IDaxStudioHost _host;
        private string _displayName = "";
        private ILog _logger;
        private readonly RibbonViewModel _ribbon;
        private readonly Regex _rexQueryError;
        private readonly Guid _uniqueId;
        private IQueryHistoryEvent _currentQueryDetails;
        private DocumentViewModel _sourceDocument;
        private ISettingProvider SettingProvider { get; }
        private static readonly ImageSourceConverter ImgSourceConverter = new ImageSourceConverter();


        [ImportingConstructor]
        public DocumentViewModel(IWindowManager windowManager
            , IEventAggregator eventAggregator
            , IDaxStudioHost host
            , RibbonViewModel ribbon
            , ServerTimingDetailsViewModel serverTimingDetails
            , IGlobalOptions options
            , ISettingProvider settingProvider
            , IAutoSaver autoSaver
            )
        {
            try
            {
                
                _host = host;
                _eventAggregator = eventAggregator;
                _eventAggregator.Subscribe(this);
                _windowManager = windowManager;
                _ribbon = ribbon;
                SelectedRunStyle = _ribbon.SelectedRunStyle;
                SettingProvider = settingProvider;
                ServerTimingDetails = serverTimingDetails;
                _rexQueryError =
                    new Regex(
                        @"^(?:Query \()(?<line>\d+)(?:\s*,\s*)(?<col>\d+)(?:\s*\))(?<err>.*)$|Line\s+(?<line>\d+),\s+Offset\s+(?<col>\d+),(?<err>.*)$",
                        RegexOptions.Compiled | RegexOptions.Multiline);
                _uniqueId = Guid.NewGuid();
                Options = options;
                AutoSaver = autoSaver;
                IconSource =
                    ImgSourceConverter.ConvertFromInvariantString(
                        @"pack://application:,,,/DaxStudio.UI;component/images/Files/File_Dax_x16.png") as ImageSource;
                Connection = new ConnectionManager(_eventAggregator);
                IntellisenseProvider = new DaxIntellisenseProvider(this, _eventAggregator, Options);
                Init(_ribbon);
            }
            catch (Exception ex)
            {
                // log the error and re-throw it
                Log.Error(ex,Constants.LogMessageTemplate, nameof(DocumentViewModel), "ctor","Error in Constructor");
                throw;
            }
        }


        public void Init(RibbonViewModel ribbon)
        {

            State = DocumentState.New;
            var items = new ObservableCollection<ListItem>(ScreenUnitsHelper.GenerateScreenUnitList());

            SizeUnitLabel = new UnitViewModel(items, new ScreenConverter(Options.EditorFontSizePx));
            SizeUnitLabel.PropertyChanged += SizeUnitPropertyChanged;

            // Initialize default Tool Windows
            // HACK: could not figure out a good way of passing '_connection' and 'this' using IoC (MEF)
            MetadataPane = new MetadataPaneViewModel(Connection, _eventAggregator, this, Options);
            FunctionPane = new FunctionPaneViewModel(Connection, _eventAggregator, this, Options);
            DmvPane = new DmvPaneViewModel(Connection, _eventAggregator, this);
            QueryBuilder = new QueryBuilderViewModel(_eventAggregator, this, Options);
            QueryBuilder.VisibilityChanged += QueryBuilder_VisibilityChanged;

            OutputPane = IoC.Get<OutputPaneViewModel>();// (_eventAggregator);
            QueryResultsPane = IoC.Get<QueryResultsPaneViewModel>();//(_eventAggregator,_host);

            MeasureExpressionEditor = new MeasureExpressionEditorViewModel(this, _eventAggregator, Options);

            var globalHistory = IoC.Get<GlobalQueryHistory>();
            //var qryHistFactory = IoC.Get<Func<GlobalQueryHistory, IEventAggregator, DocumentViewModel, QueryHistoryPaneViewModel>>();
            QueryHistoryPane = new QueryHistoryPaneViewModel(globalHistory, _eventAggregator, this, Options);
            //QueryHistoryPane = IoC.Get<QueryHistoryPaneViewModel>();

            Document = new TextDocument();
            FindReplaceDialog = new FindReplaceDialogViewModel(_eventAggregator);
            _logger = LogManager.GetLog(typeof(DocumentViewModel));
            SelectedTarget = ribbon.SelectedTarget;
            SelectedWorksheet = Properties.Resources.DAX_Results_Sheet;

            HelpWatermark = new HelpWatermarkViewModel(Options);

            var t = DaxFormatterProxy.PrimeConnectionAsync(Options, _eventAggregator);
            t.FireAndForget();
        }

        public HelpWatermarkViewModel HelpWatermark { get; set; }

        private void QueryBuilder_VisibilityChanged(object sender, EventArgs e)
        {
            NotifyOfPropertyChange(nameof(ShowQueryBuilder));
        }

        private void SizeUnitPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ScreenPoints")
                _eventAggregator.PublishOnUIThreadAsync(new SizeUnitsUpdatedEvent((UnitViewModel)sender));
        }

        internal void LoadAutoSaveFile(Guid autoSaveId)
        {
            _isLoadingFile = true;
            var text = AutoSaver.GetAutoSaveText(autoSaveId);
            // put contents in edit window
            var editor = GetEditor();

            editor.Dispatcher.Invoke(() => {
                editor.Document.BeginUpdate();
                editor.Document.Text = text;
                editor.Document.EndUpdate();
            });

            LoadState();

            State = DocumentState.Loaded;

            _eventAggregator.PublishOnUIThread(new RecoverNextAutoSaveFileEvent());
        }

        public IQueryHistoryEvent CurrentQueryInfo => _currentQueryDetails;


        public override void TryClose(bool? dialogResult = null)
        {
            base.TryClose(dialogResult);
        }

        public Guid AutoSaveId { get; set; } = Guid.NewGuid();
        public string ContentId => "document";
        
        private DAXEditorControl.DAXEditor _editor;


        #region "Event Handlers"
        /// <summary>
        /// Initialization that requires a reference to the editor control needs to happen here
        /// </summary>
        /// <param name="view"></param>
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            _editor = GetEditor();

            try
            {
                // TODO - if theme is dark increase brightness of syntax highlights
                //_editor.ChangeColorBrightness(1.25);
                _editor.SetSyntaxHighlightColorTheme(Options.Theme);

                IntellisenseProvider.Editor = _editor;
                UpdateSettings();
                if (_editor != null)
                {
                    FindReplaceDialog.Editor = _editor;
                    SetDefaultHighlightFunction();
                    _editor.TextArea.Caret.PositionChanged += OnPositionChanged;
                    _editor.TextChanged += OnDocumentChanged;
                    _editor.PreviewDrop += OnDrop;
                    _editor.PreviewDragEnter += OnDragEnterPreview;
                    _editor.KeyUp += OnKeyUp;
                    _editor.OnPasting += OnPasting;

                }
                switch (State)
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
                        cnn.ServerType
                        , false)
                    { DatabaseName = cnn.Database.Name });

                    _sourceDocument = null;

                    _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, nameof(DocumentViewModel), nameof(OnViewLoaded), ex.Message);
                OutputError($"Error opening a new query tab: {ex.Message}");
            }

        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (QueryBuilder.IsVisible && QueryBuilder.AutoGenerate)
            {
                QueryBuilder.AutoGenerate = false;
                OutputMessage("Edits made to query text, disabling Query Builder auto generate");
            }
        }

        private void OnPasting(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                // this check strips out unicode non-breaking spaces and replaces them
                // with a "normal" space. This is helpful when pasting code from other 
                // sources like web pages or word docs which may have non-breaking
                // which would normally cause the tabular engine to throw an error
                string content = e.DataObject.GetData("UnicodeText", true) as string;

                if (content == null) return;

                var sm = new LongLineStateMachine(Constants.MaxLineLength);
                var newContent = sm.ProcessString(content);
                if (sm.SqlQueryCommentFound)
                {
                    switch (ShowStripDirectQueryDialog(sm.SqlQueryCommentPosition, newContent.Length))
                    {
                        case MultipleQueriesDetectedDialogResult.RemoveDirectQuery:
                            // remove the direct query code from the text we are pasting in
                            newContent = newContent.Substring(0, sm.SqlQueryCommentPosition);
                            break;
                        case MultipleQueriesDetectedDialogResult.KeepDirectQuery:
                            break;
                        case MultipleQueriesDetectedDialogResult.Cancel:
                            e.CancelCommand();
                            return;
                    }
                }
                
                var dataObject = new DataObject(newContent);
                e.DataObject = dataObject;

            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while Pasting: {message}", ex.Message);
                OutputError($"Error while Pasting: {ex.Message}");
            }
        }

        private MultipleQueriesDetectedDialogResult ShowStripDirectQueryDialog(int commentPosition, int stringLength)
        {
            // check in options if we should prompt or return a default option
            if (Options.EditorMultipleQueriesDetectedOnPaste == MultipleQueriesDetectedOnPaste.AlwaysKeepOnlyDax)
                return MultipleQueriesDetectedDialogResult.RemoveDirectQuery;
            if (Options.EditorMultipleQueriesDetectedOnPaste == MultipleQueriesDetectedOnPaste.AlwaysKeepBoth)
                return MultipleQueriesDetectedDialogResult.KeepDirectQuery;

            // if we get here we should prompt the user
            const int commentLength = 12;
            var charactersAfterComment = stringLength - commentPosition - commentLength;
            
            var stripDirectQueryDialog = new MultipleQueriesDetectedDialogViewModel(Options)
            {
                CharactersBeforeComment = commentPosition,
                CharactersAfterComment = charactersAfterComment
            };

            _windowManager.ShowDialogBox(stripDirectQueryDialog, settings: new Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None},
                { "ShowInTaskbar", false},
                { "ResizeMode", ResizeMode.NoResize},
                { "Background", Brushes.Transparent},
                { "AllowsTransparency",true}

            });

            return stripDirectQueryDialog.Result;
        }

        public async void ShowViewAsDialog()
        {
            var viewAsDialog = new ViewAsDialogViewModel(Connection);
        

            _windowManager.ShowDialogBox(viewAsDialog, settings: new Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None},
                { "ShowInTaskbar", false},
                { "ResizeMode", ResizeMode.NoResize},
                { "Background", Brushes.Transparent},
                { "AllowsTransparency",true}

            });

            // if result is OK then change connection to ViewAs mode
            // else do nothing
            if (viewAsDialog.Result == DialogResult.OK)
            {
                // exit here if no restrictions were specified
                if (viewAsDialog.Unrestricted) return;

                // cache any active traces
                List<ITraceWatcher> activeTraces = new List<ITraceWatcher>();
                foreach (var t in TraceWatchers)
                {
                    if (t.IsChecked) activeTraces.Add(t);

                }

                try
                {
                    string roles = string.Empty;
                    if (viewAsDialog.RoleList.Count(r => r.Selected) > 0)
                        roles = viewAsDialog.RoleList.Where(r => r.Selected).Select(r => r.Name).Aggregate((current, next) => current + "," + next);
                    
                    await Connection.SetViewAsAsync(viewAsDialog.OtherUserName.Trim(), roles, activeTraces);
                    SetViewAsDescription(viewAsDialog.OtherUserName, roles);                    
                }
                catch (Exception ex)
                {
                    
                    var msg = $"Error Setting ViewAs: {ex.Message}";
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(ShowViewAsDialog), msg);
                    OutputError( msg);
                }

            }
        }

        private string _viewAsDescription = string.Empty;
        public string ViewAsDescription  => _viewAsDescription;

        private bool _informationBarIconSpin = false;
        public bool InformationBarIconSpin { get => _informationBarIconSpin;
            set { 
                _informationBarIconSpin = value;
                NotifyOfPropertyChange();
            }
        }

        private string _informationBarIcon = "Shield";
        public string InformationBarIcon { get => _informationBarIcon;
            set
            {
                _informationBarIcon = value;
                NotifyOfPropertyChange();
            }
        }

        public void StopViewAs()
        {
            var activeTraces = TraceWatchers.Where<ITraceWatcher>(tw => tw.IsChecked).ToList();
            Connection.StopViewAs(activeTraces);
            InformationBarIcon = "Spinner";
            InformationBarIconSpin = true;
            _viewAsDescription = "Reconnecting...";
            NotifyOfPropertyChange(nameof(ViewAsDescription));
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (_editor.SelectionLength == 0)
            {

                e.Handled = true;

                if (e.Data.GetDataPresent(typeof(string)))
                {
                    var data = (string)e.Data.GetData(typeof(string));
                    InsertTextAtCaret(data);
                }

                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null) return;

                var file = files[0];

                if (file.EndsWith(".dax", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".msdax", StringComparison.InvariantCultureIgnoreCase))
                {
                    _eventAggregator.PublishOnUIThread(new OpenDaxFileEvent(files[0]));
                    _eventAggregator.PublishOnUIThread(new FileOpenedEvent(files[0]));  // add this file to the recently used list
                } else
                {
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "You can only drop .dax or .msdax files"));
                }
                if (files.Length > 1)
                {
                    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "You can only drop a single file at a time"));
                }
            }
        }


        private void OnDocumentChanged(object sender, EventArgs e)
        {
            //Log.Debug("{Class} {Event} {@EventArgs}", "DocumentViewModel", "OnDocumentChanged", e);          
            _logger.Info("In OnDocumentChanged");
            IsDirty = _editor.Text.Length > 0;
            ShowHelpWatermark = !IsDirty;
            LastModifiedUtcTime = DateTime.UtcNow;
            NotifyOfPropertyChange(() => IsDirty);
            NotifyOfPropertyChange(() => DisplayName);
            
        }

        private void OnPositionChanged(object sender, EventArgs e)
        {
            if (sender is Caret caret)
                _eventAggregator.PublishOnUIThread(new EditorPositionChangedMessage(caret.Column, caret.Line));
        }

        #endregion

        #region Properties
        public ImageSource IconSource { get; set; }

        private bool _isDirty;
        public bool IsDirty
        {
            get => _isDirty;

            set
            {
                _isDirty = value;
                NotifyOfPropertyChange(() => IsDirty);
                NotifyOfPropertyChange(() => DisplayName);
            }
        }


        private IQueryTrace _tracer;

        public IQueryTrace Tracer => _tracer;

        public bool HighlightXmSqlCallbacks => Options.HighlightXmSqlCallbacks;

        public bool SimplifyXmSqlSyntax => Options.SimplifyXmSqlSyntax;

        public bool ReplaceXmSqlColumnNames => Options.ReplaceXmSqlColumnNames;

        public bool ReplaceXmSqlTableNames => Options.ReplaceXmSqlTableNames;

        public bool WordWrap => Options.EditorWordWrap;

        public bool ConvertTabsToSpaces => Options.EditorConvertTabsToSpaces;

        public int IndentationSize => Options.EditorIndentationSize;

        #endregion


        #region Methods
        public void CreateTracer()
        {
            try
            {
                if (!Connection.IsConnected)
                {
                    var msg = "Cannot start trace, the current window is not connected";
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, msg));
                    Log.Error(Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(CreateTracer), msg);
                    return;
                }

                if (_tracer == null) // && _connection.Type != AdomdType.Excel)
                {
                    if (Connection.IsPowerPivot)
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create RemoteQueryTrace", Connection.ConnectionString, Connection.Type.ToString(), Host.Proxy.Port);
                        _tracer = QueryTraceEngineFactory.CreateRemote(Connection, GetTraceEvents(TraceWatchers), Host.Proxy.Port, Options, ShouldFilterForCurrentSession(TraceWatchers));
                    }
                    else
                    {
                        Log.Verbose("{class} {method} {event} ConnStr: {connectionString} Type: {type} port: {port}", "DocumentViewModel", "Tracer", "about to create LocalQueryTrace", Connection.ConnectionString, Connection.Type.ToString());
                        _tracer = QueryTraceEngineFactory.CreateLocal(Connection, GetTraceEvents(TraceWatchers), Options, ShouldFilterForCurrentSession(TraceWatchers));
                    }
                    //_tracer.TraceEvent += TracerOnTraceEvent;
                    _tracer.TraceStarted += TracerOnTraceStarted;
                    _tracer.TraceCompleted += TracerOnTraceCompleted;
                    _tracer.TraceError += TracerOnTraceError;
                    _tracer.TraceWarning += TracerOnTraceWarning;
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

        #endregion

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
            if (_tracer == null)
            {
                Log.Warning("{class} {method} {message}", nameof(DocumentViewModel), nameof(UpdateTraceEvents), "_trace field was null, calling CreateTracer()");
                CreateTracer();
            }
            else
            {
                _tracer.Events.Clear();
                _tracer.Events.Add(DaxStudioTraceEventClass.DiscoverBegin);
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
        }

        private void TracerOnTraceError(object sender, string e)
        {
            OutputError(e);
            _eventAggregator.PublishOnUIThread(new TraceChangedEvent(QueryTraceStatus.Error));
            ShutDownTraces();
        }

        private void TracerOnTraceWarning(object sender, string e)
        {
            OutputWarning(e);
        }

        private List<DaxStudioTraceEventClass> GetTraceEvents(BindableCollection<ITraceWatcher> traceWatchers)
        {
            var events = new List<DaxStudioTraceEventClass>();
            foreach (var tw in traceWatchers.Where(t => t.IsChecked))
            {
                foreach (var e in tw.MonitoredEvents)
                {
                    // Don't add DirectQueryEvent if the server does not support direct query session filters 
                    // and the options has not been enabled in the options screen
                    if (e == DaxStudioTraceEventClass.DirectQueryEnd && !Options.TraceDirectQuery && !Connection.ServerVersion.SupportsDirectQueryFilters()) continue;

                    // if the server version does not support Aggregate Table Events do not add them
                    if (e == DaxStudioTraceEventClass.AggregateTableRewriteQuery && !Connection.ServerVersion.SupportsAggregateTables()) continue;

                    // Add the even to the collection if we don't already have it
                    if (!events.Contains(e))
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
                                       where tw.IsChecked
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
                    foreach (var fac in TraceWatcherFactories)
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
        public IObservableCollection<object> ToolWindows =>
            _toolWindows ?? (_toolWindows = new BindableCollection<object>
            {
                MetadataPane,
                FunctionPane,
                DmvPane,
                OutputPane,
                QueryResultsPane,
                QueryHistoryPane,
                QueryBuilder
            });

        public void OpenQueryBuilder()
        {
            // Only allow the Query Builder to be shown if we are connected
            // (otherwise there is nothing to drag in from the metadata pane)
            if (!Connection.IsConnected)
            {
                MessageBox.Show(
                    "The current window is not connected to a data model. You must be connected before you can open the Query Builder",
                    "Query Builder", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ShowQueryBuilder = true;
        }

        public bool ShowQueryBuilder {
            get => QueryBuilder?.IsVisible ?? false;
            set {
                QueryBuilder.IsVisible = value;
                NotifyOfPropertyChange(nameof(ShowQueryBuilder));
            }
        }

        private DAXEditorControl.DAXEditor GetEditor()
        {
            if (ShowMeasureExpressionEditor)
            {
                // If we are showing the Expression editor return that as the edit control
                MeasureExpressionEditorView v = (MeasureExpressionEditorView)MeasureExpressionEditor.GetView();
                return v?.daxExpressionEditor;
            }
            else
            {
                DocumentView v = (DocumentView)GetView();
                return v?.daxEditor;
            }
        }

        private DockingManager GetDockManager()
        {
            DocumentView v = (DocumentView)GetView();
            return v?.Document;
        }

        public TextDocument Document { get; set; }

        public void ActivateResults()
        {
            ActivateResults(false);
        }

        public void ActivateResults(bool forceActivation)
        {
            if (!TraceWatchers.Any(tw => tw.IsChecked) || forceActivation)
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
            _queryStopWatch?.Stop();
            IsQueryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            QueryResultsPane.IsBusy = false;  // TODO - this should be some sort of collection of objects with a specific interface, not a hard coded object reference
            if (_currentQueryDetails != null)
            {
                _currentQueryDetails.ClientDurationMs = _queryStopWatch?.ElapsedMilliseconds ?? -1;
                _currentQueryDetails.RowCount = ResultsDataSet.RowCounts();
            }
            bool svrTimingsEnabled = false;
            foreach (var tw in TraceWatchers)
            {
                if (tw.IsChecked) tw.QueryCompleted(isCancelled, _currentQueryDetails);
                if (tw is ServerTimesViewModel ) { svrTimingsEnabled = true; }

            }
            if (!svrTimingsEnabled && _currentQueryDetails != null)
            {
                _eventAggregator.BeginPublishOnUIThread(_currentQueryDetails);
            }

        }

        public IDaxStudioHost Host => _host;

        private string _selectedWorksheet = "";
        public string SelectedWorksheet { get => _selectedWorksheet;
            set { _selectedWorksheet = value;
                NotifyOfPropertyChange(() => SelectedWorksheet);
            }
        }



        //public string ConnectionString { get { return _connection.ConnectionString; } }

        public string ConnectionStringWithInitialCatalog => Connection != null ? Connection.ConnectionStringWithInitialCatalog : string.Empty;

        public MetadataPaneViewModel MetadataPane { get; set; }

        public FunctionPaneViewModel FunctionPane { get; private set; }


        protected override void OnDeactivate(bool close)
        {
            Log.Debug("{Class} {Event} Close:{Value} Doc:{Document}", "DocumentViewModel", "OnDeactivated (close)", close, DisplayName);
            base.OnDeactivate(close);
            _eventAggregator.Unsubscribe(this);
            _eventAggregator.Unsubscribe(QueryResultsPane);
            _eventAggregator.Unsubscribe(MetadataPane);
            _eventAggregator.Unsubscribe(DmvPane);
            _eventAggregator.Unsubscribe(FunctionPane);
            _eventAggregator.Unsubscribe(Connection);
            _eventAggregator.Unsubscribe(IntellisenseProvider);
            _eventAggregator.Unsubscribe(MeasureExpressionEditor.IntellisenseProvider);
            _eventAggregator.Unsubscribe(HelpWatermark);
            _eventAggregator.Unsubscribe(QueryBuilder);
            foreach (var tw in this.TraceWatchers)
            {
                _eventAggregator.Unsubscribe(tw);
            }
            
        }

        internal void CloseIntellisenseWindows()
        {
            IntellisenseProvider?.CloseCompletionWindow();
        }

        protected override async void OnActivate()
        {
            Log.Debug("{Class} {Event} {Document}", "DocumentViewModel", "OnActivate", $"Activating {DisplayName}");
            _logger.Info("In OnActivate");
            base.OnActivate();
            try
            {
                _eventAggregator.Unsubscribe(this);

                _eventAggregator.Subscribe(this);
                _eventAggregator.Subscribe(QueryResultsPane);
                _eventAggregator.Subscribe(MetadataPane);
                _eventAggregator.Subscribe(DmvPane);
                _eventAggregator.Subscribe(FunctionPane);
                _eventAggregator.Subscribe(Connection);
                _eventAggregator.Subscribe(IntellisenseProvider);
                _eventAggregator.Subscribe(MeasureExpressionEditor.IntellisenseProvider);
                _eventAggregator.Subscribe(HelpWatermark);
                _eventAggregator.Subscribe(QueryBuilder);
                //this.ToolWindows.Apply(tool => _eventAggregator.Subscribe(tool));
                foreach (var tw in TraceWatchers)
                {
                    _eventAggregator.Subscribe(tw);
                }
                _ribbon.SelectedTarget = SelectedTarget;
                SelectedRunStyle = _ribbon.SelectedRunStyle;
                var loc = Document.GetLocation(0);
                //SelectedWorksheet = QueryResultsPane.SelectedWorksheet;

                // exit here if we are not in a state to run a query
                // means something is using the connection like
                // either a query is running or a trace is starting
                if (CanRunQuery)
                {
                    await CheckForMetadataUpdatesAsync();
                    // TODO - look at removing this as it breaks some connections
                    Connection.Ping();
                }

                try
                {
                    await _eventAggregator.PublishOnUIThreadAsync(new EditorPositionChangedMessage(loc.Column, loc.Line));
                    await _eventAggregator.PublishOnUIThreadAsync(new ActivateDocumentEvent(this));
                }
                catch (Exception ex)
                {
                    Log.Error("{Class} {Method} {Exception}", "DocumentViewModel", "OnActivate", ex);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(OnActivate), ex.Message);
            }

        }

        public async Task CheckForMetadataUpdatesAsync()
        {
            try
            {
                if (await ShouldAutoRefreshMetadataAsync())
                {
                    OutputMessage("Updated Model Metadata detected");
                    if (Options.ShowMetadataRefreshPrompt)
                    {
                        MetadataPane.ShowMetadataRefreshPrompt = true;
                    }
                    else
                    {
                        RefreshMetadata();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("{Class} {Method} {Exception}", "DocumentViewModel", "OnActivate [Updating Metadata]", ex);
                OutputError($"Error Refreshing Metadata - {ex.Message}");
            }
        }

        public override void CanClose(Action<bool> callback)
        {
            DoCloseCheck(callback);
        }

        internal void SwapDelimiters()
        {
            try
            {
                var editor = GetEditor();
                if (editor.SelectionLength > 0)
                {
                    editor.SelectedText = SwapDelimiters(editor.SelectedText);
                }
                else
                {
                    editor.Document.BeginUpdate();
                    editor.Document.Text = SwapDelimiters(editor.Text);
                    editor.Document.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(SwapDelimiters), $"ERROR: {ex.Message}");
                OutputError($"The following error occurred attempting to swap delimiters: {ex.Message}");
            }
        }

        private string SwapDelimiters(string selectedText)
        {
            var dsm = new DelimiterStateMachine();
            return dsm.ProcessString(selectedText);
        }

        // Moves the commas at the end of a line as the first character in the following line
        internal void MoveCommasToDebugMode()
        {
            try
            {
                var editor = GetEditor();
                if (editor.SelectionLength > 0)
                {
                    editor.SelectedText = MoveCommasToDebugMode(editor.SelectedText);
                }
                else
                {
                    editor.Document.BeginUpdate();
                    editor.Document.Text = MoveCommasToDebugMode(editor.Text);
                    editor.Document.EndUpdate();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(SwapDelimiters), $"ERROR: {ex.Message}");
                OutputError($"The following error occurred attempting to move commas to debug mode: {ex.Message}");
            }
        }

        private string MoveCommasToDebugMode( string text )
        {
            return FormatDebugMode.ToggleDebugCommas(text);
        }

        public bool Close()
        {
            // Close the document's connection 
            Connection.Close(true);

            var docTab = Parent as DocumentTabViewModel;
            docTab.CloseItem(this);
            docTab?.Items.Remove(this);
            return true;
        }

        public ConnectionManager Connection { get; } 
        


        private void UpdateConnections(ConnectEvent message)
        {
            _logger.Info("In UpdateConnections");
            OutputPane.AddInformation("Establishing Connection");
            Log.Debug("{Class} {Event} {Connection}", "DocumentViewModel", "UpdateConnections", message?.ConnectionString??"<null>");

            using (NewStatusBarMessage("Refreshing Metadata..."))
            {
                try
                {
                    if (message == null) return;
                    MetadataPane.IsBusy = true;
                    Connection.Connect(message) ;
                    if (string.IsNullOrEmpty(ViewAsDescription)) 
                        UpdateViewAsDescription(message.ConnectionString);

                    NotifyOfPropertyChange(() => IsAdminConnection);
                    var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                    // enable/disable traces depending on the current connection
                    foreach (var traceWatcher in TraceWatchers)
                    {
                        // on change of connection we need to disable traces as they will
                        // be pointing to the old connection
                        traceWatcher.IsChecked = false;
                        // then we need to check if the new connection can be traced
                        traceWatcher.CheckEnabled(Connection, activeTrace);
                    }

                    // re-connect any traces that were previously active
                    if (message.ActiveTraces != null)
                    {
                        foreach (var traceWatcher in message.ActiveTraces)
                        {
                            traceWatcher.IsChecked = true;
                        }
                    }

                    Execute.OnUIThread(() =>
                    {
                        try
                        {
                            if (_editor == null) _editor = GetEditor();
                        //    _editor.UpdateKeywordHighlighting(_connection.Keywords);
                        _editor.UpdateFunctionHighlighting(Connection.AllFunctions);
                            Log.Information("{class} {method} {message}", "DocumentViewModel", "UpdateConnections", "SyntaxHighlighting updated");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "UpdateConnections", "Error Updating SyntaxHighlighting: " + ex.Message);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _eventAggregator.PublishOnUIThread(new ConnectFailedEvent());

                    // if the xmla endpoint is not enabled it returns a generic WebException so we add extra info 
                    // to the error to help people potentially mitigate the issue.
                    string extraInfo = string.Empty;
                    if (ex is System.Net.WebException && ConnectionManager.IsPbiXmlaEndpoint(message.ConnectionString) )
                    {
                        extraInfo = "\nPlease check your Tenant / PPU admin settings to make sure the XMLA Endpoint is enabled\n";
                    }

                    var msg = $"The following error occurred while updating the connection{extraInfo}: {ex.Message}";
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(UpdateConnections), msg);
                    OutputError(msg);
                    ActivateOutput();
                    // if we've had an error and there are RLS "View As" settings active we need to switch those off
                    // since they could be the cause of the error and there is most likely no way to recover with
                    // them enabled
                    if (Connection.IsTestingRls) Connection.StopViewAs(null);

                    // if there was an error we bail out here
                    return;
                }
            }
            if (Connection.Databases.Count == 0) {
                var msg = $"No Databases were found when connecting to {Connection.ServerName} ({Connection.ServerType})"
                + (Connection.ServerType == ServerType.PowerBIDesktop ? "\nIf your Power BI File is using a Live Connection please connect directly to the source model instead." : "");
                OutputWarning(msg);
            }
        }

        private void UpdateViewAsDescription(string connectionString)
        {
            var builder = new System.Data.OleDb.OleDbConnectionStringBuilder(connectionString);
            var effUser = builder.ContainsKey("EffectiveUserName")? builder["EffectiveUserName"].ToString(): string.Empty;
            var roles = builder.ContainsKey("Roles") ? builder["Roles"].ToString() : string.Empty;
            SetViewAsDescription(effUser, roles);
        }

        private void SetViewAsDescription(string user, string roles)
        {
            var userSection = string.IsNullOrWhiteSpace(user) ? string.Empty : $" User: {user}";
            var roleSection = string.IsNullOrEmpty(roles) ? string.Empty : $" Roles: {roles}";
            _viewAsDescription = $"{userSection}{roleSection}";
            InformationBarIconSpin = false;
            InformationBarIcon = "Shield";
            NotifyOfPropertyChange(nameof(ViewAsDescription));
        }

        public string SelectedText { get {
                var editor = GetEditor();
                if (editor == null) return "";
                return editor.SelectedText;
            }
        }
        public string Text { get; set; }

        private string _fileName = String.Empty;
        public string FileName {
            get => _fileName;
            set { _fileName = value;
                NotifyOfPropertyChange(() => FileName);
                NotifyOfPropertyChange(() => FileAndExtension);
                NotifyOfPropertyChange(() => Title);
            }
        }

        public async void ChangeConnection()
        {
            await _eventAggregator.PublishOnUIThreadAsync(new ConnectionPendingEvent(this));
            Log.Debug("{class} {method} {event}", "DocumentViewModel", "ChangeConnection", "start");
            var connStr = Connection == null ? string.Empty : Connection.ConnectionString;

            if (TryConnectToCommandLineServer()) return;

            var msg = NewStatusBarMessage("Checking for PowerPivot model...");
            Log.Debug("{class} {method} {Event} ", "DocumentViewModel", "ChangeConnection", "starting async call to check for a PowerPivot connection");

            await Task.Run(() => Host.Proxy.HasPowerPivotModel).ContinueWith(x =>
            {

                // todo - should we be checking for exceptions in this continuation
                try
                {
                    
                    bool hasPowerPivotModel = x.Result;

                    Log.Information("{class} {method} Has PowerPivotModel: {hasPpvtModel} ", "DocumentViewModel", "ChangeConnection", hasPowerPivotModel);
                    msg.Dispose();

                    Execute.OnUIThread(() =>
                    {
                        var connDialog = new ConnectionDialogViewModel(connStr, _host, _eventAggregator, hasPowerPivotModel, this, SettingProvider, Options);

                        _windowManager.ShowDialogBox(connDialog, settings: new Dictionary<string, object>
                                        {
                                            {"Top", 40},
                                            { "WindowStyle", WindowStyle.None},
                                            { "ShowInTaskbar", false},
                                            { "ResizeMode", ResizeMode.NoResize},
                                            { "Background", Brushes.Transparent},
                                            { "AllowsTransparency",true}
                                        });

                        IsFocused = true;
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

        private bool TryConnectToCommandLineServer()
        {
            var _app = Application.Current;
            if (!string.IsNullOrEmpty(_app.Args().Server) && !_app.Properties.Contains("InitialQueryConnected"))
            {
                // we only want to run this code to default connection to the server name and database arguments
                // on the first window that is connected. After that the user can use the copy connection option
                // so if they start a new window chances are that they want to connect to another source
                // Setting this property on the app means this code should only run once
                _app.Properties.Add("InitialQueryConnected", true);

                var server = _app.Args().Server;
                var database = _app.Args().Database;
                var initialCatalog = string.Empty;
                if (!string.IsNullOrEmpty(_app.Args().Database)) initialCatalog = $";Initial Catalog={database}";
                Log.Information("{class} {method} {message}", nameof(DocumentViewModel), nameof(TryConnectToCommandLineServer), $"Connecting to Server: {server} Database:{database}");
                _eventAggregator.PublishOnUIThreadAsync(new ConnectEvent($"Data Source={server}{initialCatalog}", false, string.Empty, string.Empty, 
                    database, server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase)?ServerType.PowerBIDesktop:ServerType.AnalysisServices,
                    server.Trim().StartsWith("localhost:",StringComparison.OrdinalIgnoreCase)
                    ));
                _eventAggregator.PublishOnUIThreadAsync(new SetFocusEvent());
                return true;
            }
            
            return false;
        }

        public bool IsConnected => Connection.IsConnected;

        public bool IsQueryRunning
        {
            get => _queryRunning;
            set {
                _queryRunning = value;
                NotifyOfPropertyChange(() => IsQueryRunning);
                NotifyOfPropertyChange(() => CanRunQuery);
            }
        }

        public bool IsTraceChanging
        {
            get => _traceChanging;
            set
            {
                _traceChanging = value;
                NotifyOfPropertyChange(() => IsTraceChanging);
                NotifyOfPropertyChange(() => CanRunQuery);
            }
        }

        public DmvPaneViewModel DmvPane { get; private set; }
        public QueryBuilderViewModel QueryBuilder { get; private set; }
        public OutputPaneViewModel OutputPane { get; set; }

        public QueryResultsPaneViewModel QueryResultsPane { get; set; }
        public MeasureExpressionEditorViewModel MeasureExpressionEditor { get; private set; }
        public QueryInfo QueryInfo { get; set; }

        private DialogResult PreProcessQuery(IQueryTextProvider textProvider, bool injectEvaluate, bool injectRowFunction)
        {

            // merge in any parameters
            textProvider.QueryInfo = new QueryInfo(textProvider.EditorText, injectEvaluate, injectRowFunction, _eventAggregator);
            DialogResult paramDialogResult = DialogResult.Skip;
            if (textProvider.QueryInfo.NeedsParameterValues)
            {
                var paramDialog = new QueryParametersDialogViewModel(this, textProvider.QueryInfo);


                _windowManager.ShowDialogBox(paramDialog, settings: new Dictionary<string, object>
                        {
                            { "WindowStyle", WindowStyle.None},
                            { "ShowInTaskbar", false},
                            { "ResizeMode", ResizeMode.NoResize},
                            { "Background", Brushes.Transparent},
                            { "AllowsTransparency",true}

                        });
                paramDialogResult = paramDialog.DialogResult;
            }

            return paramDialogResult;
        }

        public string QueryText=> QueryInfo?.ProcessedQuery ?? string.Empty;


        public List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> ParameterCollection
        {
            get
            {
                var coll = new List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>();
                foreach (var p in QueryInfo?.Parameters?.Values)
                {
                    coll.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter( p.Name, p.Value));
                }
                return coll;
            }
        }
        public Dictionary<string, QueryParameter> QueryParameters => QueryInfo?.Parameters ?? new Dictionary<string, QueryParameter>();

        public string EditorText
        {
            get
            {
                string qry = string.Empty;
                if (!Dispatcher.CurrentDispatcher.CheckAccess())
                {
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    { qry = GetQueryTextFromEditor();

                        return qry;
                    });
                } else
                    qry = GetQueryTextFromEditor();


                // swap delimiters if not using default style
                if (Options.DefaultSeparator != DelimiterType.Comma)
                {
                    qry = SwapDelimiters(qry);
                }
                return qry;

            }
        }

        #region Text Formatting Functions
        public void MergeParameters()
        {
            var editor = GetEditor();
            var txt = GetQueryTextFromEditor();
            var queryProcessor = new QueryInfo(txt, false, false, _eventAggregator); 
            txt = DaxHelper.replaceParamsInQuery(queryProcessor.ProcessedQuery, queryProcessor.Parameters);
            if (editor.Dispatcher.CheckAccess())
            {
                if (editor.SelectionLength == 0)
                {
                    editor.Document.BeginUpdate();
                    editor.Document.Text = txt;
                    editor.Document.EndUpdate();
                }
                else
                {
                    editor.SelectedText = txt;
                }
            }
            else
            {
                editor.Dispatcher.Invoke(() =>
                {
                    if (editor.SelectionLength == 0)
                    {
                        editor.Document.BeginUpdate();
                        editor.Document.Text = txt;
                        editor.Document.EndUpdate();
                    }
                    else
                    {
                        editor.SelectedText = txt;
                    }
                });
            }
        }

        public void Undo()
        {
            var editor = GetEditor();

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
                editor.Dispatcher.Invoke(() =>
                {
                    if (editor.CanUndo) editor.Undo();
                });
            }
        }

        public void Redo()
        {
            var editor = GetEditor();

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
                editor.Dispatcher.Invoke(() =>
                {
                    if (editor.CanRedo) editor.Redo();
                });
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
                editor.Dispatcher.Invoke(() => { txt = GetQueryTextFromEditorInternal(editor); });
            }
            return txt;
        }

        private void SelectedTextToUpperInternal(DAXEditorControl.DAXEditor editor)
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
                editor.Dispatcher.Invoke(() => SelectedTextToUpperInternal(editor));
            }
        }

        private void SelectedTextToLowerInternal(DAXEditorControl.DAXEditor editor)
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
                editor.Dispatcher.Invoke(() => SelectedTextToLowerInternal(editor));
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
                editor.Dispatcher.Invoke(() => editor.CommentSelectedLines());
            }
        }

        public void ToggleCommentSelection()
        {
            var editor = GetEditor();
            if (editor.Dispatcher.CheckAccess())
            {
                editor.ToggleCommentSelectedLines();
            }
            else
            {
                editor.Dispatcher.Invoke(() => editor.ToggleCommentSelectedLines());
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
                editor.Dispatcher.Invoke(() => editor.UncommentSelectedLines());
            }
        }

        private string GetQueryTextFromEditorInternal(DAXEditorControl.DAXEditor editor)
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
            try
            {
                NotifyOfPropertyChange(() => ElapsedQueryTime);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(RefreshElapsedTime), "Error updating elapsed time");
            }
        }

        public async Task<DataTable> ExecuteDataTableQueryAsync(string daxQuery)
        {

            int row = 0;
            int col = 0;
            Timer timer = new Timer(300);
            try
            {
                var editor = GetEditor();
                editor.Dispatcher.Invoke(() =>
                    {
                        // capture the row and column location for error reporting (if needed)
                        if (editor.SelectionLength > 0)
                        {
                            var loc = editor.Document.GetLocation(editor.SelectionStart);
                            row = loc.Line;
                            col = loc.Column;
                        }
                    }
                );

                var c = Connection;
                if (daxQuery != Constants.RefreshSessionQuery)
                {
                    foreach (var tw in TraceWatchers)
                    {
                        if (tw.IsChecked && !tw.IsPaused)
                        {
                            tw.IsBusy = true;
                        }
                    }
                }
                if (Options.DefaultSeparator != DelimiterType.Comma) {
                    var dsm = new DelimiterStateMachine(DelimiterType.Comma);
                    daxQuery = dsm.ProcessString(daxQuery);
                }
                timer.Elapsed += _timer_Elapsed;
                timer.Start();
                _queryStopWatch = new Stopwatch();
                _queryStopWatch.Start();
                DataTable dt;
                return await Task.Run(() => { 
                    dt = c.ExecuteDaxQueryDataTable(daxQuery);
                    dt.FixColumnNaming(daxQuery);
                    return dt;
                });
                
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
                timer?.Stop();
                timer.Elapsed -= _timer_Elapsed;
                timer.Dispose();
                NotifyOfPropertyChange(() => ElapsedQueryTime);
                _eventAggregator.PublishOnUIThread(new UpdateTimerTextEvent(ElapsedQueryTime));


                // if this is an internal refresh session query don't  
                //if (!daxQuery.StartsWith(Constants.InternalQueryHeader))
                //{
                //    NotifyOfPropertyChange(() => ElapsedQueryTime);
                //    _eventAggregator.PublishOnUIThread(new UpdateTimerTextEvent(ElapsedQueryTime));
                //    QueryCompleted();
                //}
            }

        }

        public AdomdDataReader ExecuteDataReaderQuery(string daxQuery, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            int row = 0;
            int col = 0;
            var editor = GetEditor();
            Timer _timer = new Timer(300);
            editor.Dispatcher.Invoke(() =>
            {
                if (editor.SelectionLength > 0)
                {
                    var loc = editor.Document.GetLocation(editor.SelectionStart);
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
                _timer.Elapsed += _timer_Elapsed;
                _timer.Start();
                _queryStopWatch = new Stopwatch();
                _queryStopWatch.Start();
                var dr = c.ExecuteReader(daxQuery, paramList);

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

        public string ElapsedQueryTime => _queryStopWatch == null ? "" : _queryStopWatch.Elapsed.ToString(Constants.StatusBarTimerFormat);

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

        public async void Handle(RunQueryEvent message)
        {
            // if we can't run the query then do nothing 
            // (ribbon button will be disabled, but the following check blocks hotkey requests)
            if (!CanRunQuery) return;
            
            // the benchmark run style will pop up it's own dialog
            if (message.IsBenchmark)
            {
                BenchmarkQuery();
                return;
            }

            IsQueryRunning = true;

            NotifyOfPropertyChange(() => CanRunQuery);
            if (message.RunStyle.ClearCache) await ClearDatabaseCacheAsync();

            // if the query provider is not set use the current document
            message.QueryProvider = message.QueryProvider ?? this;

            await RunQueryInternalAsync(message);

            // if the query did not run exit here
            if (CurrentQueryInfo == null) return;

            int durationSecs = CurrentQueryInfo.ClientDurationMs>int.MaxValue? int.MaxValue / 1000: (int)CurrentQueryInfo.ClientDurationMs/ 1000;
            Options.PlayLongOperationSound(durationSecs );

        }

        private void BenchmarkQuery()
        {

            try
            {
                IsBenchmarkRunning = true;

                var serverTimingsTrace = TraceWatchers.FirstOrDefault(tw => tw is ServerTimesViewModel);

                var serverTimingsInitialState = serverTimingsTrace?.IsChecked??false;
                
                //using (var dialog = new ExportDataDialogViewModel(_eventAggregator, ActiveDocument))
                using (var dialog = new BenchmarkViewModel(_eventAggregator, this, _ribbon, Options))
                {

                    _windowManager.ShowDialogBox(dialog, settings: new Dictionary<string, object>
                    {
                        { "WindowStyle", WindowStyle.None},
                        { "ShowInTaskbar", false},
                        { "ResizeMode", ResizeMode.NoResize},
                        { "Background", Brushes.Transparent},
                        { "AllowsTransparency",true}

                    });

                    if (dialog.IsCancelled) return;

                }

                // reset status of ServerTimings
                serverTimingsTrace.IsChecked = serverTimingsInitialState;

                // once the dialog closes we should activate the results pane
                ActivateResults(true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(BenchmarkQuery), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Running BenchmarkQuery: {ex.Message}"));
            }
            finally
            {
                IsBenchmarkRunning = false;
            }
        }

        private async Task RunQueryInternalAsync(RunQueryEvent message)
        {
            using (var msg = NewStatusBarMessage("Running Query..."))
            {
                // somehow people are getting into this method while the connection is not open
                // even though the CanRun state should be false so this is a double check
                //if (Connection.State != ConnectionState.Open)
                //{
                //    Log.Error("{class} {method} Attempting run a query on a connection which is not open", "DocumentViewMode", "RunQueryInternal");
                //    _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "You cannot run a query on a connection which is not open"));
                //    _eventAggregator.PublishOnUIThread(new ConnectionChangedEvent(Connection, this));
                //    return;
                //}


                try {


                    // if there is no query text in the editor and the QueryProvider is not the Query Builder check to 
                    // see if the query builder is active and try and use that to get the Query text.
                    if (EditorText.Trim().Length == 0 && !(message.QueryProvider is QueryBuilderViewModel))
                    {
                        if (ShowQueryBuilder && QueryBuilder.Columns.Count > 0)
                        {
                            OutputMessage("There is no text in the editor, redirecting the run command to the Query Builder");
                            message.QueryProvider = QueryBuilder;
                        }
                        else if (this.MetadataPane.SelectedItems.Count() == 1)
                        {
                            // if there is no text and the query builder is not active, but the user has a metadata item selected
                            // we can offer to generate a query for that object
                            var selectedItem = this.MetadataPane.SelectedItems.ToList()[0];
                            var queryGenerated = MetadataPane.GenerateQueryForSelectedMetadataItem(selectedItem);
                            if (string.IsNullOrEmpty(queryGenerated))
                            {
                                OutputWarning("There is no query text in the edit window which can be executed.");
                                ActivateOutput();
                                IsQueryRunning = false;
                                return;
                            } else
                            {
                                const string unknownValue = "<UNKNOWN>";
                                string objectType = unknownValue;
                                string objectName = unknownValue;
                                var selection = this.MetadataPane.SelectedItems.ToList()[0];
                                switch (selection)
                                {
                                    case TreeViewTable t:
                                        objectType = "Table";
                                        objectName = t.Caption;
                                        break;
                                    case TreeViewColumn c when c.IsColumn:
                                        objectType = "Column";
                                        objectName = c.Caption;
                                        break;
                                    case TreeViewColumn m when m.IsMeasure:
                                        objectType = "Measure";
                                        objectName = m.Caption;
                                        break;
                                    case TreeViewColumn h when h.Column is ADOTabularHierarchy:
                                        objectType = "Hierarchy";
                                        objectName = h.Caption;
                                        break;
                                    default:

                                        break;
                                }
                                var noQueryMessage = $"There is no query text in the editor and the Query Builder is not open, but you do have the \"{objectName}\" {objectType} selected in the Metadata Pane.\n\nWould you like DAX Studio to generate a query to show a preview of the data for the selected {objectType}?";
                                if (MessageBox.Show(noQueryMessage, "No Query Text Found", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                                {

                                    InsertTextAtSelection(queryGenerated, false, false);

                                }
                                else
                                {
                                    OutputError("There is no query text in the editor that can be executed");
                                    ActivateOutput();
                                    IsQueryRunning = false;
                                    return;
                                }
                            }
                            
                        }
                        else
                        {
                            OutputWarning("There is no query text in the edit window which can be executed.");
                            ActivateOutput();
                            IsQueryRunning = false;
                            return;
                        }

                    }

                    if (PreProcessQuery(message.QueryProvider, message.RunStyle.InjectEvaluate, message.RunStyle.InjectRowFunction) == DialogResult.Cancel)
                    {
                        IsQueryRunning = false;
                    }
                    else
                    {
                        

                        await _eventAggregator.PublishOnUIThreadAsync(new QueryStartedEvent());

                        if (message.QueryProvider is ISaveState)
                            _currentQueryDetails = CreateQueryHistoryEvent((ISaveState)message.QueryProvider, message.QueryProvider.QueryText.Trim() , ParameterHelper.GetParameterXml(message.QueryProvider.QueryInfo));
                        else
                            _currentQueryDetails = CreateQueryHistoryEvent(message.QueryProvider.QueryText.Trim() , ParameterHelper.GetParameterXml(message.QueryProvider.QueryInfo));

                        await message.ResultsTarget.OutputResultsAsync(this, message.QueryProvider);

                        // todo - should we be checking for exceptions in this continuation
                        IsQueryRunning = false;
                        NotifyOfPropertyChange(() => CanRunQuery);

                        // if the server times trace watcher is not active then just record client timings
                        if (!TraceWatchers.OfType<ServerTimesViewModel>().First().IsChecked && _currentQueryDetails != null)
                        {
                            _currentQueryDetails.ClientDurationMs = _queryStopWatch?.ElapsedMilliseconds ?? 0;
                            _currentQueryDetails.RowCount = ResultsDataSet?.RowCounts();
                            await _eventAggregator.PublishOnUIThreadAsync(_currentQueryDetails);
                        }
                        _queryStopWatch?.Reset();
                        await _eventAggregator.PublishOnUIThreadAsync(new QueryFinishedEvent());
                        msg.Dispose();
                       
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(RunQueryInternalAsync), ex.Message);
                    await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error running query: {ex.Message}"));

                }
            }
        }


        private bool GenerateQueryForSelectedMetadataItem()
        {
            const string unknownValue = "<UNKNOWN>";
            const string queryHeader = "// Generated DAX Query\n";
            string objectType = unknownValue ;
            string objectName = unknownValue;
            string query = string.Empty;
            var selection = this.MetadataPane.SelectedItems.ToList()[0];
            switch (selection){ 
                case TreeViewTable t:
                    objectType = "Table";
                    objectName = t.Caption;
                    query = $"{queryHeader}EVALUATE {t.DaxName}";
                    break;
                case TreeViewColumn c when c.IsColumn:
                    objectType = "Column";
                    objectName = c.Caption;
                    query = $"{queryHeader}EVALUATE VALUES({c.DaxName})";
                    break;
                case TreeViewColumn m when m.IsMeasure:
                    objectType = "Measure";
                    objectName = m.Caption;
                    if (this.Connection.SelectedModel.Capabilities.TableConstructor)
                        query = $"{queryHeader}EVALUATE {{ {m.DaxName} }}";
                    else 
                        query = $"{queryHeader}EVALUATE ROW(\"{m.Caption}\", {m.DaxName})";
                    break;
                case TreeViewColumn h when h.Column is ADOTabularHierarchy:
                    objectType = "Hierarchy";
                    objectName = h.Caption;
                    var hier = ((ADOTabularHierarchy)h.Column);
                    query = $"{queryHeader}EVALUATE GROUPBY({hier.Table.DaxName},\n{ string.Join(",\n", hier.Levels.Select( l => l.Column.DaxName)) }\n)";
                    break;
                default:

                    break;
            }

            if (objectType == unknownValue)
            {
                // todo - do we need a different message box here or is the standard warning enough?
                return false;
            }



            return false;
        }


        private IQueryHistoryEvent CreateQueryHistoryEvent(ISaveState queryProvider, string queryText, string parameters)
        {
            var json = queryProvider.GetJson();

            QueryHistoryEvent qhe = null;
            try
            {
                qhe = new QueryHistoryEvent(
                    json
                    , queryText
                    , parameters
                    , DateTime.Now
                    , Connection.ServerNameForHistory
                    , Connection.SelectedDatabase.Caption
                    , FileName
                    )
                { Status = QueryStatus.Running };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating QueryHistory details");
                OutputWarning("Error saving query details to history pane");
            }
            return qhe;
        }

        private IQueryHistoryEvent CreateQueryHistoryEvent(string queryText, string parameters)
        {

            QueryHistoryEvent qhe = null;
            try
            {
                qhe = new QueryHistoryEvent(queryText
                    , parameters
                    , DateTime.Now
                    , Connection.ServerNameForHistory
                    , Connection.SelectedDatabase.Caption
                    , FileName) {Status = QueryStatus.Running};
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
            get => QueryResultsPane.ResultsDataTable;
            set => QueryResultsPane.ResultsDataTable = value;
        }

        public DataSet ResultsDataSet
        {
            get => QueryResultsPane.ResultsDataSet;
            set => QueryResultsPane.ResultsDataSet = value;
        }

        public bool CanRunQuery =>
            // todo - do we need to track query traces changing?
            !IsQueryRunning && !IsTraceChanging && IsConnected && !ShowMeasureExpressionEditor;

        #region Output Messages
        public void OutputMessage(string message)
        {
            OutputPane?.AddInformation(message);
        }

        public void OutputMessage(string message, double duration)
        {
            OutputPane?.AddInformation(message, duration);
        }

        public void OutputWarning(string warning)
        {
            OutputPane?.AddWarning(warning);
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
                int.TryParse(m.Groups["line"].Value, out var line);
                int.TryParse(m.Groups["col"].Value, out var col);
                OutputError(error, line, col);
            }
            else
            {
                OutputPane?.AddError(error, durationMs);
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
                msgCol += column > 0 ? column - 1 : 0;
                msgRow += row > 0 ? row - 1 : 0;
            }
            var editor = GetEditor();
            editor.Dispatcher.Invoke(() =>
            {
                editor.DisplayErrorMarkings(msgRow, msgCol, 1, error);
            });

            OutputPane?.AddError(error, msgRow, msgCol);
        }

        #endregion

        private void InsertTextAtCaret(string text)
        {
            var editor = GetEditor();
            editor.Document.Insert(editor.CaretOffset, text);
            editor.Focus();
        }

        private void InsertTextAtSelection(string text, bool selectInsertedText, bool replaceQueryBuilderQuery)
        {

            var editor = GetEditor();
            if (editor == null)
            {
                Log.Error("{class} {method} {message}", "DocumentViewModel", "InsertTextAtSelection", "Unable to get a reference to the editor control");
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Failed to insert text into the edit pane, please try the operation again. If this issue persists please post an issue on th"));
                return;
            }
            var startOffset = editor.CaretOffset;
            var textReplaced = false;

            if (replaceQueryBuilderQuery)
            {
                var editorText = editor.Text;
                Regex rex = new Regex("(/\\* START QUERY BUILDER \\*/.*/\\* END QUERY BUILDER \\*/)",RegexOptions.Singleline);
                var matches = rex.Match(editorText);
                if (matches.Success)
                { 
                    editorText = rex.Replace(editorText, text,1);
                    editor.Text = editorText;
                    textReplaced = true;
                    return;
                }
            }
            
            if(!textReplaced)
            {

                if (editor.SelectionLength == 0)
                {
                    editor.Document.Insert(editor.SelectionStart, text);
                }
                else
                {
                    editor.SelectedText = text;
                    startOffset = editor.SelectionStart;
                }

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
        /// Get/set editor caret position
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
                if (Databases.Any( db => db.Name == message.DatabaseName))
                    if (Connection.SelectedDatabaseName != message.DatabaseName)
                    {
                        try
                        {
                            MetadataPane.ChangeDatabase(message.DatabaseName);
                            OutputMessage($"Current Database changed to '{message.DatabaseName}'");
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "Handle<SendTextToEditor>", ex.Message);
                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, $"The following error occurred while attempt to change to the '{message.DatabaseName}': {ex.Message}"));
                        }
                    }
                    else
                        OutputWarning($"Could not switch to the '{message.DatabaseName}' database");
            }

            // make sure that the query does not have excessively long lines
            // as these are both hard to read and they can freeze up the UI
            // while the syntax highlighting runs 
            var sm = new LongLineStateMachine(Constants.MaxLineLength);
            var newContent = sm.ProcessString(message.TextToSend);

            InsertTextAtSelection(newContent, message.RunQuery, message.ReplaceQueryBuilderQuery);

            if (!message.RunQuery) return;  // exit here if we don't want to run the selected text

            //run the query
            _eventAggregator.PublishOnUIThread(new RunQueryEvent(SelectedTarget));

            // un-select text
            var editor = GetEditor();
            editor.SelectionLength = 0;
            editor.SelectionStart = editor.Text.Length;

        }

        public void Handle(DefineMeasureOnEditor message)
        {
            DefineMeasureOnEditor(message.MeasureName, message.MeasureExpression);
        }

        //RRomano: Should this be on DaxEditor?

        const string MODELMEASURES_BEGIN = "---- MODEL MEASURES BEGIN ----";
        const string MODELMEASURES_END = "---- MODEL MEASURES END ----";
        // private Regex defineMeasureRegex = new Regex(@"(?<=DEFINE)((.|\n)*?)(?=EVALUATE|\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Regex defineMeasureRegex_ModelMeasures = new Regex(@"(?<=DEFINE)((.|\n)*?)(?=" + MODELMEASURES_END + @")", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Regex defineMeasureRegex_DefineOnly = new Regex(@"(?<=DEFINE([\s\t])*?)(\w(.|\n)*?)(?=\z)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

            // if the default separator is not the default Comma style
            // then we should switch the separators to the SemiColon style
            if (Options.DefaultSeparator == DelimiterType.SemiColon)
            {
                var dsm = new DelimiterStateMachine(DelimiterType.SemiColon);
                measureExpression = dsm.ProcessString(measureExpression);
            }

            var measureDeclaration = $"MEASURE {measureName} = {measureExpression}";
            // TODO - expand measure expression and generate other measures here!!


            // If found then add the measure inside the DEFINE statement, if not then just paste the measure expression
            if (defineMeasureRegex_ModelMeasures.IsMatch(currentText))
            {
                currentText = defineMeasureRegex_ModelMeasures.Replace(currentText, m =>
                {
                    var measuresText = new StringBuilder(m.Groups[1].Value);

                    measuresText.AppendLine(measureDeclaration);

                    return measuresText.ToString();


                });
                editor.Document.BeginUpdate();
                editor.Document.Text = currentText;
                editor.Document.EndUpdate();

                editor.Focus();
            }
            else if (defineMeasureRegex_DefineOnly.IsMatch(currentText))
            {
                currentText = defineMeasureRegex_DefineOnly.Replace(currentText, m =>
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
                editor.Document.BeginUpdate();
                editor.Document.Text = currentText;
                editor.Document.EndUpdate();

                editor.Focus();
            }
            else
            {
                measureDeclaration =
                    $"DEFINE {Environment.NewLine}{measureDeclaration}{Environment.NewLine}";

                InsertTextAtSelection(measureDeclaration, false, false);
            }
        }

        //public void Handle(UpdateConnectionEvent message)
        //{
        //    _logger.Info("In Handle<UpdateConnectionEvent>");
        //    Log.Debug("{Class} {Event} {ConnectionString} DB: {Database}", "DocumentViewModel", "Handle:UpdateConnectionEvent", message.Connection == null ? "<null>" : message.Connection.ConnectionString);

        //    UpdateConnections(message.Connection);
        //    var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
        //    _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(this, Databases, activeTrace));
        //}


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
                if (watcher is ServerTimesViewModel stvModel && watcher.IsChecked)
                {
                    stvModel.ServerTimingDetails = ServerTimingDetails;
                    stvModel.RemapColumnNames = Connection.DaxColumnsRemapInfo.RemapNames;
                    stvModel.RemapTableNames = Connection.DaxTablesRemapInfo.RemapNames;
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
                    t.StartAsync(Options.TraceStartupTimeout).ContinueWith(p =>
                    {
                        if (p.Exception != null)
                        {
                            p.Exception.Handle(x =>
                            {
                                Log.Error("{class} {method} {message} {stacktrace}", "DocumentViewModel", "Handle<TraceWatcherToggleEvent>", x.Message, x.StackTrace);
                                OutputError("Error Starting Trace: " + x.Message);
                                return false;
                            });
                        }
                    }, TaskScheduler.Default);
                }
                // Disable other tracewatchers with different filter for current session values
                var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                foreach (var tw in TraceWatchers)
                {
                    tw.CheckEnabled(Connection, activeTrace);
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
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                ResetTracerInternal();
            }
            else
            {
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { ResetTracerInternal(); });
            }

        }

        private void ResetTracerInternal()
        {
            if (Dispatcher.CurrentDispatcher.CheckAccess())
                if (Tracer != null)
                {
                    _tracer.TraceCompleted -= TracerOnTraceCompleted;
                    _tracer.TraceError -= TracerOnTraceError;
                    _tracer.TraceStarted -= TracerOnTraceStarted;
                    _tracer.TraceWarning -= TracerOnTraceWarning;
                    Tracer?.Stop();
                    Tracer?.Dispose();
                    _tracer = null;
                }
        }

        internal string AutoSaveFileName => Path.Combine(ApplicationPaths.AutoSavePath, $"{AutoSaveId}.dax");

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

            using (TextWriter tw = new StreamWriter(fileName, false, _defaultFileEncoding))
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
                if (FileName.EndsWith(".daxx",StringComparison.OrdinalIgnoreCase)) SavePackageFile();
                else SaveSingleFiles();
            }
        }

        private void SavePackageFile()
        {
            try
            {
                var package = Package.Open(FileName, FileMode.Create);
                Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.Query, UriKind.Relative));
                using (TextWriter tw = new StreamWriter(package.CreatePart(uriTom, "text/plain", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
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
                        saver.SavePackage(package);
                    }
                }

                package.Close();

                _eventAggregator.PublishOnUIThread(new FileSavedEvent(FileName));
                IsDirty = false;
                NotifyOfPropertyChange(() => DisplayName);
                DeleteAutoSave();
            }
            catch (Exception ex)
            {
                // catch and report any errors while trying to save
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(SavePackageFile), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error saving: {ex.Message}"));
            }
        }
        private void SaveSingleFiles()
        {
            try
            {
                using (TextWriter tw = new StreamWriter(FileName, false, _defaultFileEncoding))
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
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(SaveSingleFiles), ex.Message);
                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error saving: {ex.Message}"));
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
                Options.CanPublishDaxFunctions = false;
                using (var client = GetHttpClient()) {
                    client.Timeout = new TimeSpan(0, 0, 60); // set 30 second timeout
                    Log.Information("{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", string.Format("Ping version {0} to DaxVersioning ", ssasVersion));
                    HttpResponseMessage response = await client.PostAsJsonAsync("api/v1/pingversion", new VersionRequest { SsasVersion = ssasVersion });  // responseTask.Result;
                    //HttpResponseMessage response = await client.PostStreamAsync("api/v1/pingversion", new VersionRequest { SsasVersion = ssasVersion });  // responseTask.Result;
                    if (!response.IsSuccessStatusCode) {
                        publishStopWatch.Stop();
                        string pingResult = $"Error from ping version: {response.StatusCode}";
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

                    string uploadingMessage = string.Format("file {0} ({1} bytes)", metadataFilename, fileContent.Length);
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
                Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "PublishDaxFunctions", ex.Message);
                OutputError("Error Publishing DAX Functions: " + ex.Message);
            }
            finally {
                // Remove temporary filename
                if (File.Exists(metadataFilename)) {
                    File.Delete(metadataFilename);
                }
                Options.CanPublishDaxFunctions = true;
            }
        }
        public void ExportDaxFunctions() {
            if (!IsConnected)
            {
                MessageBoxEx.Show("The active query window is not connected to a data source. You need to be connected to a data source in order to use the export functions option", "Export DAX Functions", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Configure save file dialog box
                var dlg = new SaveFileDialog
                {
                    FileName = "DAX Functions " + DaxMetadataInfo.Version.SSAS_VERSION,
                    DefaultExt = ".zip",
                    Filter = "DAX metadata (ZIP)|*.zip|DAX metadata|*.json"
                };

                // Show save file dialog box
                var result = dlg.ShowDialog();

                // Process save file dialog box results 
                if (result == true)
                {
                    // Save document 
                    try
                    {
                        ExportDaxFunctions(dlg.FileName);
                    }
                    catch (Exception ex)
                    {
                        var msg = $"Error Exporting Functions: {ex.Message}";
                        Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "ExportDaxFunctions", msg);
                        OutputError(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                var msg = $"Error Showing Exporting DAX Functions dialog: {ex.Message}";
                // it can be caught here if there is an error getting the SSAS_VERSION
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(ExportDaxFunctions), msg);
                OutputError(msg);
            }


            


            }
        private void ExportDaxFunctions(string path) {
            string extension = Path.GetExtension(path).ToLower();
            bool compression = (extension == ".zip");
            ExportDaxFunctions(path, compression);
        }

        // Note we don't do exception handling in this private method as this is handled in the calling methods
        private void ExportDaxFunctions(string path, bool compression) {

            var info = DaxMetadataInfo;
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
            }
            else
            {
                using (TextWriter tw2 = new StreamWriter(path, false, Encoding.Unicode))
                {
                    tw2.Write(JsonConvert.SerializeObject(info, Formatting.Indented));
                    tw2.Close();
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
            Uri _baseUri = new Uri("http://daxversioningservice.azurewebsites.net/");
            client.BaseAddress = _baseUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        //
        public void SaveAs()
        {
            // Configure save file dialog box
            var dlg = new SaveFileDialog
            {
                FileName = FileName ?? _displayName,
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

            //LoadFile(FileName);
            //ChangeConnection();
            //IsDirty = false; 


            Execute.OnUIThread(() =>
                {
                    Task.Run(() =>
                    {
                        Execute.OnUIThread(() => { LoadFile(FileName); });
                    }).ContinueWith(previousOutput =>
                    {
                    // todo - should we be checking for exceptions in this continuation
                    if (!FileName.EndsWith(".vpax", StringComparison.OrdinalIgnoreCase))
                    {
                        Execute.OnUIThread(() => { ChangeConnection(); });
                    }
                    }, TaskScheduler.Default).ContinueWith((previousOutput) =>
                 {
                    // todo - should we be checking for exceptions in this continuation
                    Execute.OnUIThread(() => { IsDirty = false; });
                    }, TaskScheduler.Default);
                });

        }

        private void LoadState()
        {
            if (!_isLoadingFile) return;
            // we can only load trace watchers if we are connected to a server
            //if (!this.IsConnected) return;

            foreach (var tw in ToolWindows)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;

                loader.Load(FileName);
            }

            foreach (var tw in TraceWatchers)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;

                loader.Load(FileName);
            }
            _isLoadingFile = false;
        }

        private void LoadState(Package package)
        {
            if (!_isLoadingFile) return;


            foreach (var tw in ToolWindows)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;

                loader.LoadPackage(package);
            }

            foreach (var tw in TraceWatchers)
            {
                var loader = tw as ISaveState;
                if (loader == null) continue;

                loader.LoadPackage(package);
            }

            _isLoadingFile = false;

        }



        public void LoadFile(string fileName)
        {
            
            if (File.Exists(FileName))
            {
                if (FileName.EndsWith(".vpax", StringComparison.OrdinalIgnoreCase))
                {
                    ImportAnalysisData(fileName);
                    return;
                }

                FileName = fileName;
                _isLoadingFile = true;
                _displayName = Path.GetFileName(FileName);
                IsDiskFileName = true;
                if (FileName.EndsWith(".daxx", StringComparison.OrdinalIgnoreCase))
                {
                    var package = LoadPackageFile();
                    LoadState(package);
                }
                else
                {
                    LoadSingleFile();
                    LoadState();
                }
            }
            else
            {
                Log.Warning("{class} {method} {message}", "DocumentViewModel", "LoadFile", $"File not found {FileName}");
                OutputError(string.Format("The file '{0}' was not found", FileName));
            }

            
            IsDirty = false;
            State = DocumentState.Loaded;
        }

        private Package LoadPackageFile()
        {
            Package package = null;
            try
            {
                var editor = GetEditor();

                package = Package.Open(FileName);

                Uri uriTom = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.Query, UriKind.Relative));
                if (!package.PartExists(uriTom)) return package;

                var part = package.GetPart(uriTom);
                using (TextReader tr = new StreamReader(part.GetStream(), Encoding.UTF8))
                {
                    // put contents in edit window
                    editor.Dispatcher.Invoke(() =>
                    {
                        editor.Document.BeginUpdate();
                        editor.Document.Text = tr.ReadToEnd();
                        editor.Document.EndUpdate();
                    });
                    tr.Close();

                }

                return package;

            }
            catch (Exception ex)
            {
                // todo - need to test what happens if we enter this catch block
                //        
                _isLoadingFile = false;
                Log.Error(ex, "{class} {method} {message}", "DocumentViewModel", "LoadFile", ex.Message);
                OutputError($"Error opening file: {ex.Message}");
            }

            return package;
        }

        private void LoadSingleFile()
        {
            try
            {
                using (TextReader tr = new StreamReader(FileName, true))
                {
                    var editor = GetEditor();
                    // put contents in edit window
                    editor.Dispatcher.Invoke(() =>
                    {
                        editor.Document.BeginUpdate();
                        editor.Document.Text = tr.ReadToEnd();
                        editor.Document.EndUpdate();
                    });
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

        public new string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value;
                NotifyOfPropertyChange(() => DisplayName);
                NotifyOfPropertyChange(() => FileAndExtension);
                NotifyOfPropertyChange(() => Title);
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


        public async Task Handle(ConnectEvent message)
        {
            Log.Debug(Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<ConnectEvent>","Starting");
            var msg = NewStatusBarMessage("Connecting...");

            await Task.Run(() =>
                {

                    if (message.RefreshDatabases) RefreshConnectionFilename(message);

                    if (Dispatcher.CurrentDispatcher.CheckAccess())
                    {
                        Dispatcher.CurrentDispatcher.Invoke(() => {
                            SetupConnection(message);//, cnn);
                        });
                    }
                    else
                    {
                        SetupConnection(message); //, cnn);
                    }
                    

                }).ContinueWith(taskResult =>
                    {
                        if (taskResult.IsFaulted)
                        {
                            var errMsg = taskResult?.Exception?.InnerException?.Message;

                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error Connecting: {errMsg}"));
                            Log.Error(taskResult?.Exception?.InnerException, "{class} {method} {message}", "DocumentViewModel", "Handle(ConnectEvent message)", errMsg);
                        }
                        else
                        {
                            // todo - should we be checking for exceptions in this continuation
                            var activeTrace = TraceWatchers.FirstOrDefault(t => t.IsChecked);
                            _eventAggregator.PublishOnUIThread(new DocumentConnectionUpdateEvent(Connection, Databases, activeTrace));//,IsPowerPivotConnection));
                            _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));
                            //LoadState();

                        }
                        msg.Dispose(); //reset the status message

                    }, TaskScheduler.Default);

        }

        private void RefreshConnectionFilename(ConnectEvent message)
        {
            try
            {
                var server = ConnectionStringParser.Parse(message.ConnectionString)["Data Source"];
                var port = int.Parse(server.Split(':')[1]);
                var instances = PowerBIHelper.GetLocalInstances(false);
                var selectedInstance = instances.FirstOrDefault(i => i.Port == port);
                message.PowerBIFileName = selectedInstance.Name;
            }
            catch(Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(RefreshConnectionFilename), $"Error getting Power BI Filename: {ex.Message}");
                OutputWarning($"An error occurred while trying to the Power BI Desktop filename:\n{ex.Message}");
            }
        }

        private void SetupConnection(ConnectEvent message) //, ADOTabularConnection cnn)
        {
            
            UpdateConnections(message);
            Log.Debug("{Class} {Event} {Connection}", "DocumentViewModel", "Publishing ConnectionChangedEvent", Connection == null ? "<null>" : Connection.ConnectionString);
            NotifyOfPropertyChange(() => IsConnected);
            NotifyOfPropertyChange(() => IsAdminConnection);
            NotifyOfPropertyChange(() => IsViewAsActive);

            if (IsConnected)
                _eventAggregator.PublishOnUIThread(new ConnectionChangedEvent( this, Connection.IsPowerBIorSSDT));

            

            Spid = Connection.SPID;
            //this.SelectedDatabase = cnn.Database.Name;
            CurrentWorkbookName = message.WorkbookName;

            //SelectedDatabase = message.DatabaseName;

            Databases = Connection.Databases.ToBindableCollection();


            if (Connection == null)
            { ServerName = "<Not Connected>"; }
            else
            {

                if (!Connection.IsConnected)
                {
                    ServerName = "<Not Connected>";
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
                        if (MetadataPane?.SelectedDatabase?.Caption != Connection.Database.Name)
                        {
                            MetadataPane.ChangeDatabase(Connection.Database.Name);
                        }
                    }


                }
            }
        }


        public BindableCollection<DatabaseDetails> Databases { get; private set; }
        public async Task ClearDatabaseCacheAsync()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                _currentQueryDetails = CreateQueryHistoryEvent(string.Empty, string.Empty);

                Connection.ClearCache();
                OutputMessage(string.Format("Evaluating Calculation Script for Database: {0}", Connection.SelectedDatabaseName));

                
                string refreshQuery;
                if (Options.DefaultSeparator == DelimiterType.SemiColon)
                {
                    // switch the default delimiter on the refresh query to the semi-colon style
                    var dsm = new DelimiterStateMachine(DelimiterType.SemiColon);
                    refreshQuery = dsm.ProcessString(Constants.RefreshSessionQuery);
                }
                else
                {
                    refreshQuery = Constants.RefreshSessionQuery;
                }

                await ExecuteDataTableQueryAsync(refreshQuery);

                sw.Stop();
                var duration = sw.ElapsedMilliseconds;
                OutputMessage(string.Format("Cache Cleared for Database: {0}", Connection.SelectedDatabaseName), duration);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(ClearDatabaseCacheAsync), ex.Message);
                OutputError(ex.Message);
            }
        }
        public void Handle(CancelConnectEvent message)
        {
            // make sure any other view models know that this document is the active one
            _eventAggregator.PublishOnUIThread(new ActivateDocumentEvent(this));

        }

        public IResult GetShutdownTask()
        {
            ShutDownTraces();
            
            return IsDirty ? new ApplicationCloseCheck(this, DoCloseCheck) : null;
        }

        private void ShutDownTraces()
        {
            ResetTracer();
        }

        protected virtual void DoCloseCheck(Action<bool> callback)
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
        public DocumentState State { get => _documentState;
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
            get { return Connection?.IsPowerPivot ?? false; }
        }

        private string _statusBarMessage = "Ready";
        //private string _selectedDatabase;
        public string StatusBarMessage => _statusBarMessage;

        public string ServerName { get; private set; }

        public IStatusBarMessage NewStatusBarMessage(string message)
        {
            return new StatusBarMessage(this, message);
        }

        internal void SetStatusBarMessage(string message)
        {
            try
            {
                _statusBarMessage = message;
                NotifyOfPropertyChange(() => StatusBarMessage);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(SetStatusBarMessage), ex.Message);
            }
        }

        private int _spid = -1;
        public int Spid { get => _spid; 
            private set { 
                _spid = value;
                NotifyOfPropertyChange(nameof(Spid));
            } 
        }
        public bool IsAdminConnection => Connection?.IsAdminConnection??false;


        private bool _canCopy = true;
        public bool CanCopy {
            get => _canCopy;
            set { _canCopy = value;
                NotifyOfPropertyChange(() => CanCopy);
            }
        }
        public void Copy() { 
            try
            {
                GetEditor().Copy();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(Copy), ex.Message);
                OutputError($"The following error occurred while copying: {ex.Message}");
            }
        }

        private bool _canCut = true;
        public bool CanCut { get => _canCut;
            set { _canCut = value;
                NotifyOfPropertyChange(() => CanCut);
            } }
        public void Cut() { 
            try
            {
                GetEditor().Cut();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(Cut), ex.Message);
                OutputError($"The following error occurred while cutting: {ex.Message}");
            }
        }
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
        public void Paste() {
            try
            {
                GetEditor().Paste();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(Paste), ex.Message);
                OutputError($"The following error occurred while pasting: {ex.Message}");
            }
        }

        public void SetResultsMessage(string message, OutputTarget icon)
        {
            QueryResultsPane.ResultsMessage = message;
            QueryResultsPane.ResultsIcon = icon;
        }

        public FindReplaceDialogViewModel FindReplaceDialog { get; set; }
        public GotoLineDialogViewModel GotoLineDialog { get; set; }

        #region Highlighting

        //private HighlightDelegate _defaultHighlightFunction;

        /// <summary>
        /// This function highlights all other occurances of the currently selected text in the editor
        /// </summary>
        /// <param name="text"></param>
        /// <param name="startOffset"></param>
        /// <param name="endOffset"></param>
        /// <returns></returns>
        private List<HighlightPosition> InternalDefaultHighlightFunction(string text, int startOffset, int endOffset)
        {
            var list = new List<HighlightPosition>();
            try
            {
                if (string.IsNullOrWhiteSpace(TextToHighlight)) return null; ;
                var editor = GetEditor();

                var start = 0;
                var selStart = editor.SelectionStart;
                var lineSelStart = -1;
                if (selStart >= startOffset && selStart <= endOffset)
                {
                    lineSelStart = selStart - startOffset;
                }
                while (true)
                {
                    var idx = -1;
                    try
                    {
                        idx = text.IndexOf(TextToHighlight, start, StringComparison.InvariantCultureIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(InternalDefaultHighlightFunction), $"Error: {ex.Message} /n while finding text to highlight while searching for '{TextToHighlight}' in '{text}'");
                        _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "An error occurred while trying to highlight search text"));
                        break;
                    }
                    if (idx == -1) break;              // stop search if we did not find any more occurances
                    start = idx + 1;
                    if (idx == lineSelStart) continue; // skip the currently selected text

                    // check that the index and length are inside the bounds of the text
                    if (idx >= 0 && idx + TextToHighlight.Length <= text.Length)
                        list.Add(new HighlightPosition { Index = idx, Length = TextToHighlight.Length });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(InternalDefaultHighlightFunction), ex.Message);
            }
            return list;
        }

        private void SetDefaultHighlightFunction()
        {
            SetHighlightFunction(InternalDefaultHighlightFunction);
        }

        private void SetHighlightFunction(HighlightDelegate highlightFunction)
        {
            var editor = GetEditor();
            editor.HighlightFunction = highlightFunction;
        }
        #endregion

        public string TextToHighlight { 
            get {
                var editor = GetEditor();
                return editor.SelectedText; 
            } 
        }

        public void GotoLine()
        {

            Log.Debug("{class} {method} {event}", "DocumentViewModel", "GotoLine", "start");

            try
            {
                // create a gotoLineDialog view model                     
                var gotoLineDialog = new GotoLineDialogViewModel(GetEditor());

                // show the dialog
                _windowManager.ShowDialogBox(gotoLineDialog, settings: new Dictionary<string, object>
                                {
                                    {"Top", 40},
                                    { "WindowStyle", WindowStyle.None},
                                    { "ShowInTaskbar", false},
                                    { "ResizeMode", ResizeMode.NoResize},
                                    { "Background", Brushes.Transparent},
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
                    if (message.DurationMs > 0) OutputMessage(message.Text, message.DurationMs);
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


            editor.Dispatcher.BeginInvoke(new ThreadStart(() =>
          {
              editor.Focus();
              editor.TextArea.Focus();
              editor.TextArea.TextView.Focus();
              Keyboard.Focus(editor);
          }), DispatcherPriority.Input);

        }


        public void FormatQuery(bool formatAlternateStyle)
        {
            if (Options.BlockExternalServices)
            {
                OutputWarning("Unable to access DaxFormatter.com, permission to access external services blocked in Options");
                return;
            }

            using (var msg = new StatusBarMessage(this, "Formatting Query..."))
            {

                Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Start");
                int colOffset = 1;
                int rowOffset = 1;
                Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "Getting Query Text");
                // todo - do I want to disable the editor control while formatting is in progress???

                string qry;
                DAXEditorControl.DAXEditor editor;

                try
                {
                    
                    editor = GetEditor();
                    // if there is a selection send that to DocumentViewModel.com otherwise send all the text
                    qry = editor.SelectionLength == 0 ? editor.Text : editor.SelectedText;
                    if (editor.SelectionLength > 0)
                    {
                        var loc = editor.Document.GetLocation(editor.SelectionStart);
                        colOffset = loc.Column;
                        rowOffset = loc.Line;
                    }
                }
                catch(Exception ex)
                {
                    Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(FormatQuery), ex.Message);
                    OutputError($"The following error occurred while attempting to get the query text from the edit window:\n{ex.Message}");
                    return;
                }
                Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "About to Call daxformatter.com");


                ServerDatabaseInfo info = new ServerDatabaseInfo();
                if (Connection.IsConnected)
                {
                    var serverName = Connection.IsPowerPivot | Connection.IsPowerBIorSSDT ? Connection.FileName : Connection.ServerName;
                    var databaseName = Connection.IsPowerPivot | Connection.IsPowerBIorSSDT ? Connection.FileName : Connection.Database?.Name;
                    try
                    {
                        info.ServerName = serverName ?? "";
                        info.ServerEdition = Connection.ServerEdition ?? "";
                        info.ServerType = Connection.ServerType.GetDescription() ?? "";
                        info.ServerMode = Connection.ServerMode ?? "";
                        info.ServerLocation = Connection.ServerLocation ?? "";
                        info.ServerVersion = Connection.ServerVersion ?? "";
                        info.DatabaseName = databaseName ?? "";
                        info.DatabaseCompatibilityLevel = Connection.Database?.CompatibilityLevel ?? "";
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "{class} {method} Unable to get server details for daxformatter call", "DocumentViewModel", "FormatQuery");
                    }
                }

                if (qry.Trim().Length == 0) return; // no query text to format so exit here

                // if we are showing the ExpressionEditor we need to inject an = sign at the start of the query
                // so that DaxFormatter knows this is a scalar expression
                if (ShowMeasureExpressionEditor) { qry = "=" + qry; }

                DaxFormatterProxy.FormatDaxAsync(qry, info, Options, _eventAggregator, formatAlternateStyle).ContinueWith(res =>
               {
                    // todo - should we be checking for exceptions in this continuation
                    Log.Verbose("{class} {method} {event}", "DocumentViewModel", "FormatQuery", "daxformatter.com call complete");

                   try
                   {
                       if ((res.Result.errors == null) || (res.Result.errors.Count == 0))
                       {
                           var formattedText = res.Result.FormattedDax.TrimEnd();

                           // if we are showing the ExpressionEditor we need to remove the = sign we injected at the start of the query
                           // so that DaxFormatter would know this was a scalar expression
                           if (ShowMeasureExpressionEditor) { formattedText = formattedText.TrimStart('=').Trim(); }

                           editor.Dispatcher.Invoke(() =>
                           {
                               editor.IsReadOnly = true;
                               if (editor.SelectionLength == 0)
                               {
                                   editor.IsEnabled = false;
                                   editor.Document.BeginUpdate();
                                   editor.Document.Text = formattedText;
                                   editor.Document.EndUpdate();
                                   editor.IsEnabled = true;
                               }
                               else
                               {

                                   editor.SelectedText = res.Result.FormattedDax.TrimEnd();
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

                               // if we are showing the Expression Editor and the error is on Line 1 
                               // we don't add 1 to the column as we strip out the leading = symbol
                               int errColOffset = ShowMeasureExpressionEditor && errLine == 1 ? 0 : 1;
                               int errCol = err.column + errColOffset;

                               editor.Dispatcher.Invoke(() =>
                               {
                                    // if the error is past the last line of the document
                                    // move back to the last character of the last line
                                    if (errLine > editor.LineCount)
                                   {
                                       errLine = editor.LineCount;
                                       errCol = editor.Document.Lines[errLine - 1].TotalLength + 1;
                                   }
                                    // if the error is at the end of text then we need to move in 1 character
                                    var errOffset = editor.Document.GetOffset(errLine, errCol);
                                   if (errOffset == editor.Document.TextLength && !editor.Text.EndsWith(" ", StringComparison.InvariantCultureIgnoreCase))
                                   {
                                       editor.Document.Insert(errOffset, " ");
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
                       editor.Dispatcher.Invoke(() =>
                       {
                           editor.IsReadOnly = false;
                       });
                       msg.Dispose();
                       Log.Verbose("{class} {method} {end}", "DocumentViewModel", "FormatDax:End");
                   }
               }, TaskScheduler.Default);
            }
        }


        private bool _isCheckForSchemaUpdateRunning;
        private readonly object _checkForSchemaUpdateLock = new object();

        private async Task<bool> ShouldAutoRefreshMetadataAsync()
        {
            lock (_checkForSchemaUpdateLock)
            {
                if (_isCheckForSchemaUpdateRunning) return false;
                _isCheckForSchemaUpdateRunning = true;
            }

            try
            {
                if (IsQueryRunning) return false; // if query is running schema cannot have changed (and this connection will be busy with the query)
                if (IsBenchmarkRunning) return false;
                if (Connection == null) return false;
                if (!IsConnected && !string.IsNullOrWhiteSpace(ServerName))
                {
                    Log.Error("{class} {method} {message} ", nameof(DocumentViewModel), nameof(ShouldAutoRefreshMetadataAsync), "Connection is not open");
                    OutputError($"Error Connecting to server: {ServerName}");
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
                if (Connection.IsConnecting) return false;
                if (Connection.Database == null) return false;
                if (!Connection.ShouldAutoRefreshMetadata(Options)) return false;

                Log.Information("{class} {method} {message}", nameof(DocumentViewModel), nameof(ShouldAutoRefreshMetadataAsync), "Starting call to HasSchemaChangedAsync");

                
                bool hasChanged = await Connection.HasSchemaChangedAsync();

                Log.Information("{class} {method} {message}", nameof(DocumentViewModel), nameof(ShouldAutoRefreshMetadataAsync), $"Finished call to HasSchemaChangedAsync: {hasChanged}");

                return hasChanged;
            }
            // AdomdConnectionException
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", nameof(DocumentViewModel), nameof(ShouldAutoRefreshMetadataAsync), ex.Message, ex.StackTrace);
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
            finally
            {
                _isCheckForSchemaUpdateRunning = false;
            }
        }

        internal void RefreshMetadata()
        {
            try
            {
                Connection.Refresh();
                MetadataPane.RefreshDatabases();// = CopyDatabaseList(this.Connection);
                Databases = MetadataPane.Databases;
                //MetadataPane.ModelList = Connection.Database.Models;
                MetadataPane.RefreshMetadata();
                
                //this.MetadataPane.RefreshMetadata();
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
                OutputError("Error Refreshing Metadata: " + msg);
            }
        }
        private bool _isFocused;
        public bool IsFocused { 
            get => _isFocused;  
            set { 
                _isFocused = value;
                // Attempt to set the keyboard focus into the DaxEditor control
                var e = GetEditor();
                if (e != null && _isFocused)
                {
                    e.Focus();
                    //Dispatcher.CurrentDispatcher.BeginInvoke(
                    //    new System.Action(delegate () { e.Focus(); })
                    //    , DispatcherPriority.Background
                    //    , null
                    //);
                }

                NotifyOfPropertyChange(() => IsFocused); } }

        public void Handle(SetSelectedWorksheetEvent message)
        {
            SelectedWorksheet = message.Worksheet;
        }
        public IResultsTarget SelectedTarget { get; private set; }
        public void Handle(QueryResultsPaneMessageEvent message)
        {
            SelectedTarget = message.Target;
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

        public DaxIntellisenseProvider IntellisenseProvider { get; set; }

        public object UniqueID { get { return _uniqueId; } }

        private int _rowCount = -1;
        private string _serverVersion = "";
        public int RowCount {
            get { return _rowCount; }
            set { _rowCount = value; NotifyOfPropertyChange(() => RowCount); }

        }

        public void UpdateSettings()
        {
            var editor = GetEditor();

            if (editor == null) return;

            if (editor.ShowLineNumbers != Options.EditorShowLineNumbers)
            {
                editor.ShowLineNumbers = Options.EditorShowLineNumbers;
            }
            if (editor.FontFamily.Source != Options.EditorFontFamily)
            {
                editor.FontFamily = new FontFamily(Options.EditorFontFamily);
            }
            if (editor.FontSize != Options.EditorFontSizePx)
            {
                editor.FontSize = Options.EditorFontSizePx;
                SizeUnitLabel.SetOneHundredPercentFontSize(Options.EditorFontSizePx);
                SizeUnitLabel.StringValue = "100";
            }

            if (Options.EditorEnableIntellisense)
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

        public DaxMetadata DaxMetadataInfo => Connection?.DaxMetadataInfo;

        public DaxColumnsRemap DaxColumnsRemapInfo => Connection?.DaxColumnsRemapInfo;

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
                var dm = GetDockManager();
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
                var dm = GetDockManager();
                dm.SaveLayout();
                OutputMessage("Window Layout Saved.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<DockManagerSaveLayout>", ex.Message);
                OutputError($"Error Saving Window Layout: {ex.Message}");
            }
        }

        public void PreviewMouseLeftButtonUp()
        {
            // if it's open close the completion window when 
            // the left mouse button is clicked
            var editor = GetEditor();
            editor.DisposeCompletionWindow();
        }

        #region ISaveable 
        public FileIcons Icon { get {

                return !IsDiskFileName || Path.GetExtension(FileName).ToLower() == ".dax" ? FileIcons.Dax : FileIcons.Other; } }
        public string FileAndExtension { get
            {
                if (IsDiskFileName)
                    return Path.GetFileName(FileName);
                return DisplayName.TrimEnd('*');
            }
        }

        public IDaxDocument LayoutElement => this;
        public string Title => FileAndExtension;

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
        #region Export/View Analysis Data (VertiPaq Analyzer)

        private bool _isVertipaqAnalyzerRunning;
        public bool IsVertipaqAnalyzerRunning { get { return _isVertipaqAnalyzerRunning; }
            private set {
                _isVertipaqAnalyzerRunning = value;
                NotifyOfPropertyChange(() => IsVertipaqAnalyzerRunning);
            }
        }

        private VertiPaqAnalyzerViewModel vpaView;

        public async Task ViewAnalysisDataAsync()
        {
            try
            {
                await Task.Run(() => ViewAnalysisData());
            }
            catch (Exception ex)
            {
                var msg = $"The following error occurred while trying to view the metrics for your model:\n{ex.Message}";
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(ViewAnalysisDataAsync), msg);
                OutputError(msg);
            }

        }

        public void ViewAnalysisData()
        {
            Stopwatch sw = new Stopwatch();
            if (!IsConnected)
            {
                OutputError("The active query window is not connected to a data source. You need to be connected to a data source in order to use the export functions option");
                ActivateOutput();
                return;
            }

            if (!Connection.IsAdminConnection)
            {
                OutputError("You do not have admin rights on the current data model. Admin rights are required to view the metrics");
                ActivateOutput();
                return;
            }

            try
            {
                IsVertipaqAnalyzerRunning = true;
                sw.Start();
                var msg2 = new StatusBarMessage(this, "Analyzing Model Metrics");

                // check if PerfData Window is already open and use that
                //vpaView = this.ToolWindows.FirstOrDefault(win => (win as VertiPaqAnalyzerViewModel) != null) as VertiPaqAnalyzerViewModel;

                // var vpaView = new VertiPaqAnalyzerViewModel(viewModel, _eventAggregator, this, Options);
                if (vpaView != null)
                {
                    ToolWindows.Remove(vpaView);
                    vpaView = null;
                }

                vpaView = new VertiPaqAnalyzerViewModel( _eventAggregator, this, Options);
                ToolWindows.Add(vpaView);
                
                vpaView.IsBusy = true;
                vpaView.Activate();

                // SSAS legacy doesn't have UNION and cannot execute readStatisticsFromData
                bool isLegacySsas = Connection.ServerVersion.StartsWith("10.", StringComparison.InvariantCultureIgnoreCase)  // SSAS 2012 RC
                    || Connection.ServerVersion.StartsWith("11.", StringComparison.InvariantCultureIgnoreCase)               // SSAS 2012 SP1
                    || Connection.ServerVersion.StartsWith("12.", StringComparison.InvariantCultureIgnoreCase);              // SSAS 2014

                bool readStatisticsFromData = Options.VpaxReadStatisticsFromData && (!isLegacySsas);

                VpaModel viewModel = null;

                
                    // run Vertipaq Analyzer Async

                    Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    Dax.Metadata.Model model;
                    try
                    {
                        model = TomExtractor.GetDaxModel(
                            Connection.ServerName, Connection.SelectedDatabaseName, 
                            "DaxStudio", version.ToString(), 
                            readStatisticsFromData: readStatisticsFromData, 
                            sampleRows: Options.VpaxSampleReferentialIntegrityViolations );
                    }
                    catch (Exception ex)
                    {
                        // If there is an error reading the statistics from data (e.g. model not processed, bug in SSAS), then retry without statistics
                        if (readStatisticsFromData)
                        {
                            Log.Warning(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(ViewAnalysisData), $"Error loading VPA view with ReadStatisticsFromData enabled: {ex.Message}");
                            OutputWarning($"Error viewing metrics with ReadStatisticsFromData enabled (retry without statistics): {ex.Message}");

                            model = TomExtractor.GetDaxModel(
                                Connection.ServerName, Connection.SelectedDatabaseName,
                                "DaxStudio", version.ToString(),
                                readStatisticsFromData: false, // Disable statistics during retry
                                sampleRows: Options.VpaxSampleReferentialIntegrityViolations);
                        }
                        else
                        {
                            // propagate exception if ReadStatisticsFromData was disabled
                            throw;
                        }
                    }
                    viewModel = new VpaModel(model);
                    var modelName = GetSelectedModelName();
                    viewModel.Model.ModelName = new Dax.Metadata.DaxName(modelName);



                            
                            // update view model
                            vpaView.ViewModel = viewModel;
                            
                        //}
                        //catch (Exception ex)
                        //{
                        //    Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(ViewAnalysisDataAsync), $"Error loading VPA view: {ex.Message}");
                        //    OutputError($"Error viewing metrics : {ex.Message}");
                        //    ActivateOutput();
                        //}

                    //} else
                    //{
                    //    var ex = prevTask.Exception;
                    //    Log.Error(ex, "{class} {method} Error Getting Metrics", "DocumentViewModel", "ViewAnalysisData");
                    //    var exMsg = ex.GetAllMessages();
                    //    OutputError($"Error viewing metrics: {exMsg}");
                    //    ActivateOutput();
                    //}

                    vpaView.IsBusy = false;
                    IsVertipaqAnalyzerRunning = false;
                    msg2.Dispose();
                    //if (prevTask.IsFaulted) throw prevTask.Exception;

                    sw.Stop();
                    Options.PlayLongOperationSound((int)(sw.ElapsedMilliseconds / 1000));
                    

                //}, TaskScheduler.Default);
                //task.Start(TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Error Getting Metrics", nameof(DocumentViewModel), "ViewAnalysisData");
                var exMsg = ex.GetAllMessages();
                OutputError("Error viewing metrics: " + exMsg);
                ActivateOutput();
            }

        }


        public async Task ExportAnalysisDataAsync()
        {
            if (!IsConnected)
            {
                MessageBoxEx.Show("The active query window is not connected to a data source. You need to be connected to a data source in order to use the export functions option", "Export DAX Functions", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Configure save file dialog box
            var dlg = new SaveFileDialog
            {
                FileName = Connection.SelectedDatabaseName,
                DefaultExt = ".vpax",
                Filter = "Analyzer Data (vpax)|*.vpax"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Save document 
                
                    try {
                        IsVertipaqAnalyzerRunning = true;
                        await ExportAnalysisDataAsync(dlg.FileName);
                    }
                    finally
                    {
                        IsVertipaqAnalyzerRunning = false;
                    }


            }
        }

        public void ImportAnalysisData()
        {
            // TODO - FileOpen dialog
            var dlg = new OpenFileDialog
            {
                Multiselect = false,
                DefaultExt = ".vpax",
                Filter = "Analyzer Data (vpax)|*.vpax"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            if (result == true)
            {
                var filename = dlg.FileName;
                ImportAnalysisData(filename);
            }

        }

        private void ImportAnalysisData(string path)
        {

            try
            {
                VpaModel viewModel = ModelAnalyzer.ImportVPAX(path);

                // check if PerfData Window is already open and use that
                var vpaView = ToolWindows.FirstOrDefault(win => (win as VertiPaqAnalyzerViewModel) != null) as VertiPaqAnalyzerViewModel;

                // var vpaView = new VertiPaqAnalyzerViewModel(viewModel, _eventAggregator, this, Options);
                if (vpaView != null)
                {
                    ToolWindows.Remove(vpaView);
                    vpaView = null;
                }

                vpaView = new VertiPaqAnalyzerViewModel( _eventAggregator, this, Options);
                ToolWindows.Add(vpaView);
                
                // update view model
                vpaView.ViewModel = viewModel;
                
                vpaView.Activate();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(DocumentViewModel), nameof(ImportAnalysisData), $"Error loading VPA view: {ex.Message}");
                OutputError($"Error opening metrics: {ex.Message}");
            }

        }

        public async Task ExportAnalysisDataAsync(string path)
        {
            try
            {
                await Task.Run(() => ExportAnalysisData(path));
            }
            catch (Exception ex)
            {
                var msg = $"Error exporting metrics:\n{ex.Message}";
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(ExportAnalysisDataAsync), msg);
                OutputError(msg);
                ActivateOutput();
            }
        }

        public void ExportAnalysisData(string path)
        {
            using (var _ = new StatusBarMessage(this, "Exporting Model Metrics"))
            {
                try
                {

                    OutputMessage(String.Format("Saving {0}...", path));
                    // get current DAX Studio version
                    Version ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                    var modelName = GetSelectedModelName();
                    // SSAS legacy doesn't have UNION and cannot execute readStatisticsFromData
                    bool isLegacySsas = Connection.ServerVersion.StartsWith("10.", StringComparison.InvariantCultureIgnoreCase)  // SSAS 2012 RC
                        || Connection.ServerVersion.StartsWith("11.", StringComparison.InvariantCultureIgnoreCase)               // SSAS 2012 SP1
                        || Connection.ServerVersion.StartsWith("12.", StringComparison.InvariantCultureIgnoreCase);              // SSAS 2014

                    bool readStatisticsFromData = Options.VpaxReadStatisticsFromData && (!isLegacySsas);
                    try
                    {
                        ModelAnalyzer.ExportVPAX(Connection.ServerName, Connection.SelectedDatabaseName, path, Options.VpaxIncludeTom, "DaxStudio", ver.ToString(), readStatisticsFromData, modelName);
                    }
                    catch (Exception ex)
                    {
                        // If there is an error reading the statistics from data (e.g. model not processed, bug in SSAS), then retry without statistics
                        if (readStatisticsFromData)
                        {
                            Log.Warning(ex, "{class} {method} Error Exporting Metrics with ReadStatisticsFromData enabled", "DocumentViewModel", "ExportAnalysisData");
                            var exMsg = ex.GetAllMessages();
                            OutputWarning("Error exporting metrics with ReadStatisticsFromData enabled (retry without statistics): " + exMsg);

                            ModelAnalyzer.ExportVPAX(Connection.ServerName, Connection.SelectedDatabaseName, path, Options.VpaxIncludeTom, "DaxStudio", ver.ToString(), false, modelName); // Disable statistics during retry
                        }
                        else
                        {
                            // propagate excetpion if ReadStatisticsFromData was disabled
                            throw;
                        }
                    }
                    OutputMessage("Model Metrics exported successfully");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{class} {method} Error Exporting Metrics", "DocumentViewModel", "ExportAnalysisData");
                    var exMsg = ex.GetAllMessages();
                    OutputError("Error exporting metrics: " + exMsg);
                    ActivateOutput();
                }
            }
        }

        private string GetSelectedModelName()
        {
            string modelCaption = Connection.Database.Name;
            switch (Connection.ServerType)
            {
                case ServerType.PowerBIDesktop:
                    modelCaption = $"{Connection?.ShortFileName ?? "<unknown>"}.pbix";
                    break;
                case ServerType.PowerPivot:
                    modelCaption = $"{Connection?.ShortFileName ?? "<unknown>"}.xlsx";
                    break;
                case ServerType.SSDT:
                    modelCaption = $"{Connection?.ShortFileName ?? "<unknown>"} (SSDT)";
                    break;
                case ServerType.PowerBIReportServer:
                    modelCaption = $"{Connection?.ShortFileName ?? "<unknown>"} (PBIRS)";
                    break;
                case ServerType.AnalysisServices:
                    modelCaption = $"{Connection?.Database?.Name ?? "<unknown>"}";
                    break;
            }
            return modelCaption;
        }

        internal void AppendText(string paramXml)
        {
            var editor = GetEditor();

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

        public void DragOver(IDropInfo dropInfo)
        {
            IntellisenseProvider?.CloseCompletionWindow();

            DataObject data = dropInfo.Data as DataObject;
            bool stringPresent = data?.GetDataPresent(DataFormats.StringFormat)??false;

            if (dropInfo.DragInfo?.SourceItem is IADOTabularObject || stringPresent)
            {
                dropInfo.Effects = DragDropEffects.Move;
                var pt = dropInfo.DropPosition;
                var pos = _editor.GetPositionFromPoint(pt);
                if (!pos.HasValue) return;
                var off = _editor.Document.GetOffset(pos.Value.Location);
                _editor.CaretOffset = off;
                _editor.Focus();
            }
            else
            {
                dropInfo.Effects = DragDropEffects.None;
            }
        }

        public void Drop(IDropInfo dropInfo)
        {
            var obj = dropInfo.DragInfo?.SourceItem as IADOTabularObject;
            var text = string.Empty;
            if (obj != null)
            {
                text = obj.DaxName;
            }

            DataObject data = dropInfo.Data as DataObject;
            bool stringPresent = data?.GetDataPresent(DataFormats.StringFormat) ?? false;

            if (stringPresent)
            {
                text = data.GetText();
            }
            InsertTextAtCaret(text);
        }

        public void OnDragEnterPreview(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }

            IntellisenseProvider?.CloseCompletionWindow();
        }

        public void Handle(CopyConnectionEvent message)
        {
            _sourceDocument = message.SourceDocument;
        }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(nameof(WordWrap));
            NotifyOfPropertyChange(nameof(ConvertTabsToSpaces));
            NotifyOfPropertyChange(nameof(IndentationSize));
            UpdateTheme();
        }


        public void UpdateTheme()
        {
            NotifyOfPropertyChange(() => AvalonDockTheme);
            _editor?.SetSyntaxHighlightColorTheme(Options.Theme);
        }

        public void Handle(SelectedModelChangedEvent message)
        {
            
            UpdateRunningTraces();
        }

        public void Handle(ReconnectEvent message)
        {
            UpdateRunningTraces();
            Spid = Connection?.SPID??-1;
        }

        private void UpdateRunningTraces()
        {
            // if there is not a running trace exit here
            if (_tracer == null) return;
            if (_tracer.Status != QueryTraceStatus.Started) return;

            // reconnect any running traces to pick up the initial catalog property
            try
            {
                _eventAggregator.PublishOnUIThread(new TraceChangingEvent(QueryTraceStatus.Starting));
                IsTraceChanging = true;
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, "Reconfiguring trace due to connection changes"));
                _tracer.Update(Connection.SelectedDatabaseName, Connection.SessionId);
            }
            finally
            {
                IsTraceChanging = false;
            }
        }


        #endregion



        public void Handle(ShowMeasureExpressionEditor message)
        {
            try
            {
                MeasureExpressionEditor.Column = message.Column;
                ShowMeasureExpressionEditor = true;
                QueryBuilder.IsEnabled = false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), "IHandle<ShowMeasureExpressionEditor>", ex.Message);
                OutputError($"Error showing the Measure Expression Editor:\n{ex.Message}");
                ShowMeasureExpressionEditor = false;
                QueryBuilder.IsEnabled = true;
            }
        }

        public void Handle(SetFocusEvent message)
        {
            IsFocused = true;
        }

        public AvalonDock.Themes.Theme AvalonDockTheme => new GenericTheme();

        private bool _showMeasureExpressionEditor;
        public bool ShowMeasureExpressionEditor
        {
            get => _showMeasureExpressionEditor;
            set { 
                _showMeasureExpressionEditor = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanRunQuery));
            }
        }

        public IGlobalOptions Options { get; set; }
        public IAutoSaver AutoSaver { get; }

        IModelIntellisenseProvider IDaxDocument.Connection => Connection;

        public bool IsBenchmarkRunning {get;set;}

        public void CloseConnection()
        {
            Connection.Close();
        }

        public void OpenConnection()
        {
            Connection.Open();
        }

        public void Handle(ApplicationActivatedEvent message)
        {
            try
            {
                // TODO - this was running synchronously and was causing issues on slow connections (like AAS)

                // ping the connection to make sure we are connected and the session is active
                //if (Connection.IsConnected) Connection.Ping();
            }
            catch (Exception ex)
            {
                OutputError(ex.Message);
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), "Handle<ApplicationActivatedEvent>", ex.Message);
            }
        }

        public bool ShowHelpWatermark
        {
            get => HelpWatermark.ShowHelpWatermark;
            set => HelpWatermark.ShowHelpWatermark = value;
        }

        public void OnEditorSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _eventAggregator.PublishOnUIThread(new EditorResizeEvent(e.NewSize));
        }

        public void Handle(RunStyleChangedEvent message)
        {
            SelectedRunStyle = message.RunStyle;
        }

        public RunStyle SelectedRunStyle { get; set; }

        public void EditorContextMenuOpening()
        {
            NotifyOfPropertyChange(nameof(CanLookupDaxGuide));
            NotifyOfPropertyChange(nameof(LookupDaxGuideHeader));
        }

        public bool CanLookupDaxGuide { 
            get {
                NotifyOfPropertyChange(nameof(LookupDaxGuideHeader));
                if (this.Connection == null) return false;
                if (!this.Connection.IsConnected) return false;
                if (this.Connection.AllFunctions.Contains(_editor.ContextMenuWord, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
                return false;
            } 
        }
        public void LookupDaxGuide() {
            string word = _editor.ContextMenuWord;
            if (this.Connection == null) return;
            if (!this.Connection.IsConnected) return;
            if (this.Connection.AllFunctions.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                System.Diagnostics.Process.Start($"https://dax.guide/{word}/?aff=dax-studio");
            }
        }

        public string LookupDaxGuideHeader => $"Lookup {_editor.ContextMenuWord.ToUpper()} in DAX Guide";

        //private bool _isViewAsActive = false;
        public bool IsViewAsActive { get => Connection.IsTestingRls; 
            //private set { 
            //    _isViewAsActive = value;
            //    NotifyOfPropertyChange();
            //} 
        }

        public void OnEditorHover(object source, MouseEventArgs eventArgs)
        {
            if (!Options.EditorShowFunctionInsightsOnHover) return;
            if (Connection == null) return;
            if (!Connection.IsConnected) return;

            try
            {
                var mousePoint = eventArgs.GetPosition((DAXEditorControl.DAXEditor) eventArgs.Source);
                // get the line and column position
                var pos = _editor.GetPositionFromPoint(mousePoint);
                
                if (pos == null) return;
                var word = _editor.GetCurrentWord((TextViewPosition) pos);
                if ( Connection.AllFunctions.Contains(word, StringComparer.OrdinalIgnoreCase)
                  || Connection.Keywords.Contains(word,StringComparer.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Hovering over '{word}'");
                    Log.Debug(Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(OnEditorHover),
                        $"Hovering over '{word}'");
                    // the function Insight window always positions itself under the cursor
                    // so we need to set the caret position to where the cursor is hovering
                    // or the insight window will appear in the wrong position.
                    // the Insight window also has code to move itself when the parent window is moved
                    // so we can't simply override the positioning
                    //_editor.SetCaretPosition(pos.Value.Line, pos.Value.Column);
                    var offset = _editor.GetOffset(pos.Value.Line, pos.Value.Column);
                    _editor.ShowInsightWindow(word, offset);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, Constants.LogMessageTemplate, nameof(DocumentViewModel), nameof(OnEditorHover),
                    $"The following error occurred: {ex.Message}");

            }

        }

        public void Handle(EditorHotkeyEvent message)
        {
            var editor = GetEditor();
            if (editor == null) return;

            switch (message.Hotkey)
            {
                case EditorHotkey.SelectWord:        
                    editor.SelectCurrentWord();
                    break;
                case EditorHotkey.MoveLineUp:
                    editor.MoveLineUp();
                    break;
                case EditorHotkey.MoveLineDown:
                    editor.MoveLineDown();
                    break;
            }
        }

        public void Handle(ToggleCommentEvent message)
        {
            var editor = GetEditor();
            if (editor.IsInComment()) UnCommentSelection();
            else CommentSelection();
        }

        public void Handle(LoadQueryBuilderEvent message)
        {
            if (QueryBuilder.Columns.Count > 0 || QueryBuilder.Filters.Count > 0)
            {
                if (MessageBox.Show(
                    "Do you want to replace the current content of the Query Builder with the item from the Query History?",
                    "Restore Query History", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }

                QueryBuilder.Clear();
            }
            QueryBuilder.LoadJson(message.Json);
        }
    }
}
