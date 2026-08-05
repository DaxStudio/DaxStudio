using ADOTabular;
using ADOTabular.Interfaces;
using AsyncAwaitBestPractices;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.Core.Events;
using DaxStudio.UI.Events;
using DaxStudio.Core.Extensions;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using Humanizer;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using FocusManager = DaxStudio.UI.Utils.FocusManager;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ResultsTargets;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class MetadataPaneViewModel : 
        ToolPaneBaseViewModel
        , IHandle<UpdateGlobalOptions>
        , IHandle<SelectedDatabaseChangedEvent>
        , IHandle<QueryStartedEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<ConnectionChangedEvent>
        , IHandle<ConnectionOpenedEvent>
        , IHandle<ConnectFailedEvent>
        , IHandle<TablesRefreshedEvent>
        , IHandle<NavigateToMetadataItemEvent>
        //, IDragSource
        , IMetadataPane
    {
        private string _modelName;
        private readonly IGlobalOptions _options;
        private readonly IMetadataProvider _metadataProvider;
        private List<IExpandedItem> _expandedItems = new List<IExpandedItem>();
        private const int SAMPLE_ROWS = 5;
        private CancellationTokenSource _columnTooltipCts;

        // Server/database identity at the time we last reacted to a
        // ConnectionOpenedEvent. Used to suppress the metadata wipe when the
        // "new" connection actually points at the same server+database (e.g.
        // certain reconnect / reopen flows).
        private string _lastServerName;
        private string _lastDatabaseName;

        [ImportingConstructor]
        public MetadataPaneViewModel(IMetadataProvider metadataProvider, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) 
            : base( eventAggregator)
        {
            _metadataProvider = metadataProvider;
            ActiveDocument = document;

            _options = globalOptions;
            NotifyOfPropertyChange(() => ActiveDocument);
            ShowHiddenObjects = _options.ShowHiddenMetadata;
            SortFoldersFirstInMetadata = _options.SortFoldersFirstInMetadata;

        }

        public IEnumerable<FilterableTreeViewItem> SelectedItems { get; } = new List<FilterableTreeViewItem>();

        public DocumentViewModel ActiveDocument { get; }

        public string ModelName
        {
            get => _modelName;
            set
            {
                if (value == _modelName)
                    return;
                _modelName = value;

                NotifyOfPropertyChange(() => ModelName);
            }
        }

        private ADOTabularModel _selectedModel;

        public void RefreshMetadata()
        {
            try
            {
                if (!_metadataProvider.IsConnected) return;
                _metadataProvider.Refresh();
                var tmpModel = _selectedModel;
                SaveExpandedState();
                
                ModelList = _metadataProvider.GetModels();
                
                ShowMetadataRefreshPrompt = false;
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Metadata Refreshed"));
            }
            catch (Exception ex)
            {
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error,$"Error Refreshing Metadata: {ex.Message}"));
                Log.Error(ex,Common.Constants.LogMessageTemplate,nameof(MetadataPaneViewModel), nameof(RefreshMetadata), ex.Message);
            }
        }

        private void RestoreExpandedState(string tmpModelName)
        {
            RestoreExpandedStateInternal(_treeViewTables, _expandedItems);
            _expandedItems.Clear();
        }

        private void RestoreExpandedStateInternal(IEnumerable<IFilterableTreeViewItem> metadataItems, List<IExpandedItem> expandedItems)
        {
            var filterableTreeViewItems = metadataItems as IFilterableTreeViewItem[] ?? metadataItems.ToArray();
            foreach (var item in expandedItems)
            {
                foreach (var metadataItem in filterableTreeViewItems)
                {
                    if (item.Name == metadataItem.Name)
                    {
                        metadataItem.IsExpanded = true;
                        RestoreExpandedStateInternal(metadataItem.Children, item.Children);
                    }
                }
            }
        }

        private void SaveExpandedState()
        {
            _expandedItems.Clear();
            SaveExpandedStateInternal(Tables, _expandedItems);
        }

        private void SaveExpandedStateInternal(IEnumerable<IFilterableTreeViewItem> items, List<IExpandedItem> expandedItems)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item.IsExpanded)
                {
                    var newExpandedItem = new ExpandedItem(item.Name);
                    expandedItems.Add(newExpandedItem);
                    SaveExpandedStateInternal(item.Children, newExpandedItem.Children);
                }
            }
            
        }

        private bool _showMetadataRefreshPrompt;
        public bool ShowMetadataRefreshPrompt
        {
            get => _showMetadataRefreshPrompt;
            set
            {
                _showMetadataRefreshPrompt = value;
                NotifyOfPropertyChange(nameof(ShowMetadataRefreshPrompt));
            }
        }

        public void DismissRefreshMetadataPrompt()
        {
            ShowMetadataRefreshPrompt = false;
        }

        public ADOTabularModel SelectedModel
        {
            get => _selectedModel;
            set
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(SelectedModel), $"Set Start {value?.Name??"<null>"}");
                if (_selectedModel != value)
                {
                    SetBusy(true);
                    _selectedModel = value;
                    NotifyOfPropertyChange(nameof(SelectedModel));
                    
                    // clear table list
                    _treeViewTables = null;
                    NotifyOfPropertyChange(nameof(Tables));

                    _metadataProvider.SetSelectedModelAsync(SelectedModel).ContinueWith((prev) => {
                        Log.Verbose(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), "SelectedModel.Set", "Clearing IsBusy in continuation");
                        try
                        {
                            NotifyOfPropertyChange(nameof(Tables));
                        }
                        finally
                        {
                            // Always clear the busy state even if the notification
                            // above throws, otherwise the loading overlay would be
                            // orphaned over already-loaded metadata.
                            SetBusy(false);
                        }
                    }
                    ).SafeFireAndForget(onException: ex =>
                        Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), "SelectedModel.Set", "Error in SetSelectedModelAsync continuation"));
                    
                }
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(SelectedModel), "Set End");
            }
        }

        public string SelectedModelName => SelectedModel == null ? "--" : SelectedModel.Name;

        private IEnumerable<IFilterableTreeViewItem> _treeViewTables;
        public IEnumerable<IFilterableTreeViewItem> Tables
        {
            get => _treeViewTables;
            set
            {
                _treeViewTables = value;
                NotifyOfPropertyChange(() => Tables);
            }
        }

        internal async Task RefreshTablesAsync()
        {
            if (SelectedModel == null)
            {
                Tables = null;  // if there is no selected model clear the table collection
                return;
            }
            if (_treeViewTables == null)
            {

                // Load tables async
                await Task.Run(async () =>
                {
                    try
                    {

                        var sw = new Stopwatch();
                        sw.Start();
                        SetBusy(true);
                        IsNotifying = false;
                        Log.Information(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(RefreshTablesAsync), "Starting Refresh of Tables ");
                        _treeViewTables = _metadataProvider.GetTreeViewTables(this, _options);
                        sw.Stop();
                        Log.Information("{class} {method} {message}", "MetadataPaneViewModel", nameof(RefreshTablesAsync), $"Finished Refresh of {_treeViewTables.Count()} tables (duration: {sw.ElapsedMilliseconds}ms)");
                        RestoreExpandedState("");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {error} {stacktrace}", "MetadataPaneViewModel", "RefreshTables.Task", ex.Message, ex.StackTrace);
                        await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, ex.Message));
                    }
                    finally
                    {
                        ShowMetadataRefreshPrompt = false;
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(RefreshTablesAsync), "Setting IsBusy = false");

                        IsNotifying = true;
                        try
                        {
                            Refresh(); // force all data bindings to update
                            NotifyOfPropertyChange(nameof(Tables));
                        }
                        finally
                        {
                            // Always clear the busy state even if Refresh() or the
                            // notification above throws, otherwise the loading
                            // overlay would be orphaned over already-loaded metadata.
                            SetBusy(false);
                        }
                        await EventAggregator.PublishAsync(new MetadataLoadedEvent(ActiveDocument, SelectedModel));
                    }

                });
                
            }

        }

        public override string DefaultDockingPane => "DockLeft";
        public override string ContentId => "metadata";
        //public override ImageSource IconSource
        //{
        //    get
        //    {
        //        var imgSourceConverter = new ImageSourceConverter();
        //        return imgSourceConverter.ConvertFromInvariantString(
        //            @"pack://application:,,,/DaxStudio.UI;component/images/Metadata/hierarchy.png") as ImageSource;

        //    }
        //}
        public override string Title => "Metadata";

        private ADOTabularModelCollection _modelList;
        public ADOTabularModelCollection ModelList
        {
            get => _modelList;
            set
            {
                if (value == _modelList)
                    return;
                _modelList = value;
                // Reading BaseModel forces the (potentially blocking) model DISCOVER query if the
                // collection has not been materialized yet. Log the thread so any UI-thread stall
                // caused by this is easy to spot in the log (it should run on a background thread).
                if (_modelList != null && !_modelList.IsMaterialized && Application.Current != null
                    && Application.Current.Dispatcher.CheckAccess())
                {
                    Log.Warning(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(ModelList),
                        $"Materializing model collection on the UI thread (thread {System.Threading.Thread.CurrentThread.ManagedThreadId}) - this may block the UI");
                }
                SelectedModel = _modelList?.BaseModel;
                NotifyOfPropertyChange(() => ModelList);
                NotifyOfPropertyChange(() => SelectedModel);
            }
        }

        private string _currentCriteria = string.Empty;
        public string CurrentCriteria
        {
            get => _currentCriteria;
            set
            {
                _currentCriteria = value??string.Empty;
                if (_currentCriteria.Length >= 2 || _currentCriteria.Length == 0)
                {
                    NotifyOfPropertyChange(() => CurrentCriteria);
                    NotifyOfPropertyChange(() => HasCriteria);
                    ApplyFilter();
                }
            }
        }

        public bool HasCriteria => _currentCriteria.Length > 0;

        public void ClearCriteria()
        {
            CurrentCriteria = string.Empty;
        }
        private void ApplyFilter()
        {
            if (Tables == null) return;
            foreach (var node in Tables)
                node.ApplyCriteria(CurrentCriteria, new Stack<IFilterableTreeViewItem>());
            
        }

        // Database Dropdown Properties
        private BindableCollection<DatabaseDetails> _databases = new BindableCollection<DatabaseDetails>();
        public BindableCollection<DatabaseDetails> Databases
        {
            get => _databases;
            set
            {
                _databases = value;
                if (value == null) {
                    DatabasesView.Clear();
                    NotifyOfPropertyChange(() => DatabasesView);
                    return;
                }
                MergeDatabaseView();
            }
        }

        private void MergeDatabaseView()
        {
            var newList = _databases.Select(
                                db => new DatabaseReference()
                                {
                                    Name = db.Name,
                                    Caption = db.Caption,
                                    Description = ( _metadataProvider.IsPowerPivot || _metadataProvider.IsPowerBIorSSDT) && _metadataProvider?.FileName?.Length > 0 ? _metadataProvider.FileName : ""
                                }).OrderBy(db => db.Name);
            
            DatabasesView.IsNotifying = false;
            // remove deleted databases
            for (int i = DatabasesView.Count - 1; i >= 0; i--)
            {
                var found = newList.Where(db => db.Name == DatabasesView[i].Name).Any();
                if (!found) DatabasesView.RemoveAt(i);
            }

            // add new databases
            foreach (DatabaseReference dbRef in newList)
            {
                var found = DatabasesView.Where(db => db.Name == dbRef.Name).FirstOrDefault();
                if (found == null) DatabasesView.Add(dbRef);
            }
            DatabasesView.IsNotifying = true;
            DatabasesView.Refresh();
            //NotifyOfPropertyChange(() => DatabasesView);
            if (SelectedDatabase == null)
                if (!string.IsNullOrEmpty(_metadataProvider.DatabaseName))
                    SelectedDatabase = DatabasesView.FirstOrDefault(x => x.Name == _metadataProvider.DatabaseName);
                else
                    SelectedDatabase = DatabasesView.FirstOrDefault();
        }

        private IDatabaseReference _selectedDatabase;
        public IDatabaseReference SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (_selectedDatabase != value)
                {
                    _selectedDatabase = value;
                    if (_selectedDatabase == null) return;
                    NotifyOfPropertyChange(nameof(SelectedDatabase));
                    var _step = "start";
                    Task.Run(() => {
                        SetBusy(true);
                        _step = "step 1";
                        _metadataProvider.SetSelectedDatabase(_selectedDatabase);
                        _step = "step 2";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseObject));
                        _step = "step 3";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseCaption));
                        _step = "step 4";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseDescription));
                        _step = "step 5";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseCulture));
                        _step = "step 6";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseIsAdmin));
                        _step = "step 7";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseRoles));
                        _step = "step 8";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseDurationSinceUpdate));
                        _step = "step 9";
                        NotifyOfPropertyChange(nameof(SelectedDatabaseLastUpdateLocalTime));
                        _step = "step 10";
                        ModelList = _metadataProvider.GetModels();
                        _step = "step 11";
                        SetBusy( false);
                    }).SafeFireAndForget(onException: ex =>
                    {
                        Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(SelectedDatabase), $"error setting Selected Database ({_step})");
                        EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error setting selected database ({_step}): {ex.Message} "));
                        SetBusy( false);
                    });

                }
                

            }
        }

        public ADOTabularDatabase SelectedDatabaseObject => _metadataProvider.Database;
        public string SelectedDatabaseCaption => _metadataProvider.Database?.Caption??"";
        public string SelectedDatabaseDescription => _metadataProvider.Database?.Description??"";
        public string SelectedDatabaseRoles => _metadataProvider.Database?.Roles??"";
        public string SelectedDatabaseCulture => _metadataProvider.Database?.Culture ?? "";
        public string SelectedDatabaseCompatibilityLevel => _metadataProvider.Database?.CompatibilityLevel ?? "";
        public bool SelectedDatabaseIsAdmin => _metadataProvider.Database?.IsAdmin??false;


        public string SelectedDatabaseDurationSinceUpdate {
            get
            {
                if (SelectedDatabaseObject == null) return string.Empty;
                var timespan = DateTime.UtcNow - SelectedDatabaseObject.LastUpdate;
                return $"({timespan.Humanize(1)} ago)";
            }
        }
        public DateTime SelectedDatabaseLastUpdateLocalTime => SelectedDatabaseObject?.LastUpdate.ToLocalTime() ?? DateTime.MinValue;

        public bool CanSelectDatabase => !_metadataProvider.IsPowerPivot && !ActiveDocument.IsQueryRunning;

        public bool CanSelectModel => _metadataProvider.IsConnected && !ActiveDocument.IsQueryRunning;

        public void RefreshDatabases()
        {

            try
            {
                _metadataProvider.Refresh();
                var sourceSet = _metadataProvider.GetDatabases().ToBindableCollection();

                var deletedItems = Databases.Except(sourceSet);
                var newItems = sourceSet.Except(Databases);
                // remove deleted items
                for (var i = deletedItems.Count() - 1; i >= 0; i--)
                {
                    var tmp = deletedItems.ElementAt(i);
                    Execute.OnUIThread(() =>
                    {
                        Databases.Remove(tmp);
                    });
                }
                // add new items
                foreach (var itm in newItems)
                {
                    Execute.OnUIThread(() =>
                    {
                        Databases.Add(itm);
                    });
                }
                Execute.OnUIThread(() =>
                {
                    MergeDatabaseView();
                    NotifyOfPropertyChange(() => DatabasesView);
                    DatabasesView.Refresh();
                });
            
            }
            catch (Exception ex)
            {
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, string.Format("Unable to refresh the list of databases due to the following error: {0}", ex.Message)));
            }

        }

        //private SortedList<string,DatabaseDetails> CopyDatabaseList(ADOTabularConnection cnn)
        //{
        //    var ss = new SortedList<string,DatabaseDetails>();
        //    foreach (var dbname in cnn.Databases)
        //    { ss.Add(dbname); }
        //    return ss;
        //}
        public IObservableCollection<DatabaseReference> DatabasesView { get; } = new BindableCollection<DatabaseReference>();

        #region Busy Overlay
        private int _isBusyCnt;
        public bool IsBusy
        {
            get => _isBusyCnt != 0;
            //private set
            //{
            //    if (value) Interlocked.Increment(ref _isBusyCnt);
            //    else Interlocked.Decrement(ref _isBusyCnt);
            //    Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(IsBusy), $"Cnt: {_isBusyCnt}");
            //    NotifyOfPropertyChange();
            //}
        }

        public  void SetBusy(bool value, [CallerMemberName] string caller = null)
        {
            if (value) Interlocked.Increment(ref _isBusyCnt);
            else Interlocked.Decrement(ref _isBusyCnt);
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(SetBusy), $"Cnt: {_isBusyCnt} | Caller: {caller}");
            NotifyOfPropertyChange(() => IsBusy);
        }

        private string GetCaller([CallerMemberName] string caller = null)
        {
            return caller ?? "Unknown";
        }
        public string BusyMessage => "Loading";

        #endregion

        private bool _showHiddenObjects = true;
        public bool ShowHiddenObjects { get => _showHiddenObjects;
            set {
                var changed = (_showHiddenObjects != value);
                _showHiddenObjects = value;
                if (changed)
                {
                    NotifyOfPropertyChange(()=>ShowHiddenObjectsLabel);
                    RefreshMetadata();
                }
            }
        }

        public bool AutoHideMetadataVerticalScrollbars => _options.AutoHideMetadataVerticalScrollbars;

        private bool _sortFoldersFirstInMetadata = true;
        private IFilterableTreeViewItem _selectedTreeViewItem;

        public bool SortFoldersFirstInMetadata
        {
            get => _sortFoldersFirstInMetadata;
            set
            {
                var changed = (_sortFoldersFirstInMetadata != value);
                _sortFoldersFirstInMetadata = value;
                if (changed)
                {
                    NotifyOfPropertyChange(()=>SortFoldersFirstInMetadata);
                    RefreshMetadata();
                }
            }
        }

        public void ToggleHiddenObjects()
        {
            ShowHiddenObjects = !ShowHiddenObjects;
        }

        public string ShowHiddenObjectsLabel => ShowHiddenObjects ? "Hide Hidden Objects" : "Show Hidden Objects";

        public void CopyDatabaseName()
        {
            try
            {
                ClipboardManager.SetText(SelectedDatabase.Name);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, $"Copied Database Name '{SelectedDatabase.Name}' to clipboard"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Class} {Method} {Message}", nameof(MetadataPaneViewModel), nameof(CopyDatabaseName), ex.Message);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"The following Error occured while copying the database name to the clipboard - {ex.Message} "));
            }
            
        }

        public void CopyDatabaseId()
        {
            try
            {
                ClipboardManager.SetText(SelectedDatabaseObject.Id);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, $"Copied Database Id '{SelectedDatabaseObject.Id}' to clipboard"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Class} {Method} {Message}", nameof(MetadataPaneViewModel), nameof(CopyDatabaseId), ex.Message);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"The following Error occured while copying the database Id to the clipboard - {ex.Message} "));
            }

        }

        public void ColumnTooltipOpening(TreeViewColumn column)
        {
            if (column == null) return;
            if (column?.Column?.GetType() != typeof(ADOTabularColumn)) return;
            ADOTabularColumn col = (ADOTabularColumn)column?.Column;
            if (col.ObjectType != ADOTabularObjectType.Column) return;
            if (_options == null) return;

            // Cancel any previous tooltip operations
            _columnTooltipCts?.Cancel();
            _columnTooltipCts = new CancellationTokenSource();
            var token = _columnTooltipCts.Token;
            var tasks = new List<Task>();
            if (_options.ShowTooltipSampleData && !column.HasSampleData)
            {
                var sampleTask =  _metadataProvider.UpdateColumnSampleDataAsync(column, SAMPLE_ROWS, token);
                sampleTask.OnTimeout(task => { 
                    _metadataProvider.CancelUpdatingColumnSampleData();
                    EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Timeout while updating column sample data"));
                }, 5000).FireAndForget();
                

            }
            if (_options.ShowTooltipBasicStats && !column.HasBasicStats)
            {
                var statTask = _metadataProvider.UpdateColumnBasicStatsAsync(column, token);
                statTask.OnTimeout(task => { 
                    _metadataProvider.CancelUpdatingColumnBasicStats();
                    EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Timeout while updating column basic stats"));
                }, 5000).FireAndForget();
            }


        }

        public void TableTooltipOpening(TreeViewTable table)
        {
            if (table == null) return;

            if (_options == null) return;

            if (_options.ShowTooltipBasicStats && !table.HasBasicStats) {

                var task = _metadataProvider.UpdateTableBasicStatsAsync(table); 
                task.OnTimeout((task) => {                     
                    _metadataProvider.CancelUpdatingTableBasicStats();
                    EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Timeout while updating table basic stats"));
                }, 5000).FireAndForget();
            }
        }

        internal void ChangeDatabase(string databaseName)
        {
            if (_metadataProvider.IsConnected && DatabasesView.Count == 0) { 
                RefreshDatabases(); 
            }
            SelectedDatabase = DatabasesView.Where(db => db.Name == databaseName).FirstOrDefault();
        }

        internal async Task ChangeDatabaseAsync(string databaseName)
        {
            if (_metadataProvider.IsConnected && DatabasesView.Count == 0)
            {
                // RefreshDatabases() runs the DBSCHEMA_CATALOGS DMV query which is a blocking
                // network round-trip, so keep it off the UI thread to avoid freezing the UI.
                await Task.Run(() => RefreshDatabases());
            }
            SelectedDatabase = DatabasesView.Where(db => db.Name == databaseName).FirstOrDefault();
        }

        #region Measure Definition Methods

        private string ExpandDependentMeasure(ADOTabularColumn column)
        {
            return _metadataProvider.ExpandDependentMeasure(column.Name, false);
        }

        private string ExpandDependentMeasure(ADOTabularColumn column, bool ignoreNonUniqueMeasureNames)
        {
            return _metadataProvider.ExpandDependentMeasure(column.Name, ignoreNonUniqueMeasureNames);
        }

        private List<ADOTabularMeasure> GetAllMeasures(string filterTable = null)
        {
            return _metadataProvider.GetAllMeasures(filterTable);
        }

        private List<ADOTabularMeasure> FindDependentMeasures(string measureName)
        {
            return _metadataProvider.FindDependentMeasures(measureName);
        }

        private DependentObjects FindDependentObjects(string measureName)
        {
            return _metadataProvider.FindDependentObjects(measureName);
        }

        private void DefineFunctionsOnEditor(IEnumerable<ADOTabularUserDefinedFunction> functions)
        {
            foreach (var function in functions)
            {
                EventAggregator.PublishAsync(new DefineFunctionOnEditor(function));
            }
        }

        // publishes the definitions of any user defined functions that the specified measure
        // (or any of the measures it depends on) references
        private void DefineDependentFunctions(string measureName)
        {
            try
            {
                DefineFunctionsOnEditor(FindDependentObjects(measureName).Functions);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(DefineDependentFunctions), $"Unable to find the user defined functions that '{measureName}' depends on");
            }
        }

        // mrusso: create a list of all the measures that have to be included in the query 
        //         in order to have all the dependencies local to the query (it's easier to debug)
        //         potential issue: we'll create multiple copies of the same measures if the user executes
        //         this request multiple time for the same measure
        //         we could avoid that by parsing existing local measures in the query, but it could be 
        //         a future improvement, having this feature without such a control is already useful
        public void DefineDependentMeasures(TreeViewColumn item)
        {
            try
            {
                if (item == null)
                {
                    return;
                }

                ADOTabularColumn column;

                if (item.Column is ADOTabularKpiComponent)
                {
                    var kpiComponent = (ADOTabularKpiComponent)item.Column;
                    column = (ADOTabularColumn)kpiComponent.Column;
                }
                else
                {
                    column = (ADOTabularColumn)item.Column;
                }

                var dependentObjects = FindDependentObjects(column.Name);

                DefineFunctionsOnEditor(dependentObjects.Functions);

                foreach (var measure in dependentObjects.Measures)
                {
                    EventAggregator.PublishAsync(new DefineMeasureOnEditor(measure));
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasureTree", ex.Message, ex.StackTrace);
            }
        }

        public void DefineAllMeasures(TreeViewTable item, string filterTable)
        {
            if (item == null)
            {
                return;
            }
            try
            {
                var measures = GetAllMeasures(filterTable);

                foreach (var measure in measures)
                {
                    var measureFullName = $"{measure.Table.DaxName}{measure.DaxName}";
                    var formatStringFullName = $"{measure.Table.DaxName}{measure.FormatStringDaxName}";
                    EventAggregator.PublishAsync(new DefineMeasureOnEditor(measureFullName, measure.Expression, formatStringFullName, measure.FormatStringExpression));
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasureTree", ex.Message, ex.StackTrace);
            }
        }

        public void DefineExpandMeasure(TreeViewColumn item)
        {
            DefineMeasure(item, true);
        }


        //RRomano: Needed to set the TreeViewColumn as Public, if I do $dataContext.Column always sends NULL to DefineMeasure (Caliburn issue?)
        public void DefineMeasure(TreeViewColumn item)
        {
            DefineMeasure(item, false);
        }
        public void DefineAllMeasuresAllTables(TreeViewTable item)
        {
            DefineAllMeasures(item, null);
        }
        public void DefineAllMeasuresOneTable(TreeViewTable item)
        {
            DefineAllMeasures(item, item.Caption);
        }

        public void DefineFilterDumpMeasureAllTables(TreeViewTable item)
        {
            DefineFilterDumpMeasure(item, true);
        }
        public void DefineFilterDumpMeasureOneTable(TreeViewTable item)
        {
            DefineFilterDumpMeasure(item, false);
        }

        public void DefineFilterDumpMeasure(TreeViewTable item, bool allTables)
        {
            if (item == null) return;
            
            try 
            {
                string measureName = string.Format("'{0}'[{1}]", item.Caption, "DumpFilters" + (allTables ? "" : " " + item.Caption));
                string measureExpression = _metadataProvider.DefineFilterDumpMeasureExpression(item.Caption, allTables);

                EventAggregator.PublishAsync(new DefineMeasureOnEditor(measureName, measureExpression, null, null));
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineFilterDumpMeasure", ex.Message, ex.StackTrace);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error defining filter dump measure: {ex.Message}"));
            }
        }

        public void DefineMeasure(TreeViewColumn item, bool expandMeasure)
        {
            try
            {
                if (item == null) return;

                ADOTabularColumn column; string measureExpression = null, measureName = null, measureFormatStringName = null, formatStringExpression = null;

                if (item.Column is ADOTabularKpiComponent)
                {
                    var kpiComponent = (ADOTabularKpiComponent)item.Column;

                    column = (ADOTabularColumn)kpiComponent.Column;

                    // The KPI Value dont have an expression and points to a measure

                    if (kpiComponent.ComponentType == KpiComponentType.Value && string.IsNullOrEmpty(column.MeasureExpression))
                    {
                        measureName = string.Format("{0}[{1} {2}]", column.Table.DaxName, column.Name.Replace("]","]]"), kpiComponent.ComponentType.ToString());

                        measureExpression = column.DaxName;
                    }
                }
                else if (item.Column is ADOTabularKpi kpi)
                {
                    column = (ADOTabularColumn)item.Column;
                    measureExpression = kpi.MeasureExpression;
                }
                else
                {
                    column = (ADOTabularColumn)item.Column;
                }

                if (string.IsNullOrEmpty(measureName))
                {
                    measureName = string.Format("{0}{1}", column.Table.DaxName, column.DaxName);
                    measureFormatStringName = string.Format("{0}{1}", column.Table.DaxName, column.DaxFormatStringName);
                }

                if (expandMeasure)
                {
                    try
                    {
                        // We intentionally do not expand format strings of dependent measures
                        measureExpression = ExpandDependentMeasure(column);
                    }
                    catch (InvalidOperationException ex)
                    {
                        string msg = ex.Message + "\nThis may lead to incorrect results in cases where the column is referenced without explicitly specifying the table name.\n\nDo you want to continue with the expansion anyway?";
                        if (MessageBox.Show(msg, "Expand Measure Error", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                        {
                            measureExpression = ExpandDependentMeasure(column, true);
                        }
                        else return;
                    }

                    // expanding a measure only inlines the dependent measures, so we still need to
                    // define any user defined functions that the expanded expression calls
                    DefineDependentFunctions(column.Name);
                }

                if (string.IsNullOrEmpty(measureExpression))
                {
                    measureExpression = column.MeasureExpression;
                    formatStringExpression = column.FormatStringExpression;
                }

                EventAggregator.PublishAsync(new DefineMeasureOnEditor(measureName, measureExpression, measureFormatStringName, formatStringExpression));
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasure", ex.Message, ex.StackTrace);

            }
        }

        public Task HandleAsync(UpdateGlobalOptions message, CancellationToken cancellationToken)
        {
            
            ShowHiddenObjects = _options.ShowHiddenMetadata;
            SortFoldersFirstInMetadata = _options.SortFoldersFirstInMetadata;
            NotifyOfPropertyChange(nameof(AutoHideMetadataVerticalScrollbars));
            return Task.CompletedTask;
        }

        #endregion

        #region Discover Referencing Objects methods
        public void ShowObjectsThatReferenceColumnOrMeasure(TreeViewColumn item)
        {
            try
            {
                if (item != null)
                {
                    var criteria = $"WHERE [REFERENCED_OBJECT] = '{item.Name.Replace("'","''")}'";

                    // if the current item is a column we should also include the table name
                    if ( item.IsColumn)
                    {
                        criteria += Environment.NewLine + $" AND [REFERENCED_TABLE] = '{item.InternalColumn.TableName}'";
                    }

                    var thisItem =
                        Environment.NewLine +
                        "SELECT " + Environment.NewLine +
                        " [OBJECT_TYPE] AS [Object Type], " + Environment.NewLine +
                        " [TABLE] AS [Object's Table], " + Environment.NewLine +
                        " [OBJECT] AS [Object], " + Environment.NewLine +
                        " [REFERENCED_TABLE] AS [Referenced Table], " + Environment.NewLine +
                        " [REFERENCED_OBJECT] AS [Referenced Object], " + Environment.NewLine +
                        " [REFERENCED_OBJECT_TYPE] AS [Referenced Object Type] " + Environment.NewLine +
                        "FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY " + Environment.NewLine +
                        criteria + Environment.NewLine +
                        "ORDER BY [OBJECT_TYPE]" + Environment.NewLine;
                    EventAggregator.PublishAsync(new SendTextToEditor(thisItem,true));
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "MetadataPaneViewModel", "DefineObjectsThatReferenceMeasure", ex.Message, ex.StackTrace);
            }
        }

        public void ShowObjectsThatReferenceTable(TreeViewTable item)
        {
            try
            {
                if (item != null)
                {
                    var txt = item.Name.Replace("'","''");
                    var thisItem =
                        "SELECT " + Environment.NewLine +
                        " [OBJECT_TYPE] AS [Object Type], " + Environment.NewLine +
                        " [TABLE] AS [Object's Table], " + Environment.NewLine +
                        " [OBJECT] AS [Object], " + Environment.NewLine +
                        " [REFERENCED_OBJECT] AS [Referenced Object], " + Environment.NewLine +
                        " [REFERENCED_OBJECT_TYPE] AS [Referenced Object Type] " + Environment.NewLine +
                        "FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY " + Environment.NewLine +
                        "WHERE [REFERENCED_TABLE] = '" + txt + "'" + Environment.NewLine +
                        "ORDER BY [OBJECT_TYPE]";
                    EventAggregator.PublishAsync(new SendTextToEditor(thisItem,true));
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "MetadataPaneViewModel", "DefineObjectsThatReferenceTable", ex.Message, ex.StackTrace);
            }
        }

        #endregion

        public override void StartDrag(IDragInfo dragInfo)
        {
            //base.StartDrag(dragInfo);
            if (dragInfo.SourceItem as IADOTabularObject != null)
            {
                //dragInfo.Data = ((IADOTabularObject)dragInfo.SourceItem).DaxName;
                dragInfo.DataObject = new DataObject(typeof(string), ((IADOTabularObject)dragInfo.SourceItem).DaxName);
            }
             
            dragInfo.DataObject =  dragInfo.SourceItem;
            //dragInfo.DataObject = new DataObject(typeof(string), ((IADOTabularObject)dragInfo.SourceItem).DaxName);

            dragInfo.Effects = DragDropEffects.Move;

        }


        public void MetadataKeyUp(IFilterableTreeViewItem selectedItem, KeyEventArgs args)
        {
            switch (args.Key)
            {
                case Key.Enter:
                case Key.C:
                    if (selectedItem is ITreeviewColumn col)
                    {
                        EventAggregator.PublishAsync(new SendColumnToQueryBuilderEvent(col, QueryBuilderItemType.Column));
                        SelectedTreeViewItem = null;
                        if (!string.IsNullOrWhiteSpace(CurrentCriteria))
                        {
                            CurrentCriteria = string.Empty;
                            FocusManager.SetFocus(this, nameof(CurrentCriteria));
                        }
                    }
                    break;
                case Key.Space:
                case Key.F:
                    if (selectedItem is ITreeviewColumn filter)
                    {
                        EventAggregator.PublishAsync(new SendColumnToQueryBuilderEvent(filter, QueryBuilderItemType.Filter));
                        SelectedTreeViewItem = null;
                        if (!string.IsNullOrWhiteSpace(CurrentCriteria))
                        {
                            CurrentCriteria = string.Empty;
                            FocusManager.SetFocus(this, nameof(CurrentCriteria));
                        }
                    }
                    break;
                case Key.B:
                    if (selectedItem is ITreeviewColumn item)
                    {
                        EventAggregator.PublishAsync(new SendColumnToQueryBuilderEvent(item, QueryBuilderItemType.Both));
                        SelectedTreeViewItem = null;
                        if (!string.IsNullOrWhiteSpace(CurrentCriteria))
                        {
                            CurrentCriteria = string.Empty;
                            FocusManager.SetFocus(this, nameof(CurrentCriteria));
                        }
                    }
                    break;
            }
        }

        public void SetFocusToMetadata()
        {
            Debug.WriteLine("Setting focus to Tables");
            FocusManager.SetFocus(this, nameof(Tables));
            var firstItem = Tables?.FirstOrDefault(t => t.IsMatch);
            if (firstItem != null) firstItem.IsSelected = true;
        }

        public IFilterableTreeViewItem SelectedTreeViewItem
        {
            get => _selectedTreeViewItem;
            set
            {
                _selectedTreeViewItem = value; 
                NotifyOfPropertyChange();
            }
        }

        public string GenerateQueryForSelectedMetadataItem(object selection)
        {
            const string unknownValue = "<UNKNOWN>";
            const string queryHeader = "// Generated DAX Query\n";
            string objectType = unknownValue;
            string objectName = unknownValue;
            string query = string.Empty;
            string topnPrefix = string.Empty;
            string topnSuffix = string.Empty;

            if (_options.PreviewDataRowLimit > 0)
            {
                topnPrefix = $"\nTOPN( {_options.PreviewDataRowLimit}, ";
                topnSuffix = " )";
            }

            switch (selection)
            {
                case TreeViewTable t:
                    objectType = "Table";
                    objectName = t.Caption;
                    query = $"{queryHeader}EVALUATE {topnPrefix}{t.DaxName}{topnSuffix}\n";
                    break;
                case TreeViewColumn c when c.IsColumn:
                    objectType = "Column";
                    objectName = c.Caption;
                    query = $"{queryHeader}EVALUATE {topnPrefix}VALUES({c.DaxName}){topnSuffix}\n";
                    break;
                case TreeViewColumn m when m.IsMeasure:
                    objectType = "Measure";
                    objectName = m.Caption;
                    if (  ActiveDocument.Connection.SelectedModel.Capabilities.TableConstructor)
                        query = $"{queryHeader}EVALUATE {{ {m.DaxName} }}\n";
                    else
                        query = $"{queryHeader}EVALUATE ROW(\"{m.Caption}\", {m.DaxName})\n";
                    break;
                case TreeViewColumn h when h.Column is ADOTabularHierarchy:
                    objectType = "Hierarchy";
                    objectName = h.Caption;
                    var hier = ((ADOTabularHierarchy)h.Column);
                    query = $"{queryHeader}EVALUATE {topnPrefix}\n    GROUPBY({hier.Table.DaxName},\n        { string.Join(",\n        ", hier.Levels.Select(l => l.Column.DaxName)) }\n    )\n{topnSuffix}\n";
                    break;
                default:
                    // do nothing if we do not match one of the above cases
                    break;
            }

            if (objectType == unknownValue)
            {
                // todo - do we need a different message box here or is the standard warning enough?
                return string.Empty;
            }

            return query;
        }

        public void PreviewDataForSelectedMetadataItem(object selectedItem)
        {
            var query = GenerateQueryForSelectedMetadataItem(selectedItem);
            if (!string.IsNullOrEmpty(query))
            {
                // run query
                EventAggregator.PublishAsync(new SendTextToEditor(query, true));
            }
            else
            {
                // throw error
                ActiveDocument.OutputError("The selected metadata object does not support query generation");
                ActiveDocument.ActivateOutput();
            }

        }

        public void SelectDatabaseByName(string databaseName)
        {
            var dbRef = Databases.FirstOrDefault(db => db.Name == databaseName);
            SelectedDatabase = dbRef;
        }

        public async Task ProcessDatabaseDefrag()
        {
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Starting Process Defragment"));
            await ActiveDocument.Connection.ProcessDatabaseAsync("ProcessDefragment");
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Finished Process Defragment"));

        }

        public async Task ProcessDatabaseFull()
        {
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Starting Process Full"));
            await ActiveDocument.Connection.ProcessDatabaseAsync("ProcessFull");
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Finished Process Full"));

        }

        public async Task ProcessDatabaseRecalc()
        {
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Starting Process Calculate"));
            await ActiveDocument.Connection.ProcessDatabaseAsync("calculate");
            await EventAggregator.PublishAsync(new OutputMessage(MessageType.Information, "Finished Process Calculate"));

        }

        public async Task ShowLastUpdated()
        {
            await RunTimestampShowAsync(ShowType.LastUpdated);
        }

        public async Task ShowMaxUpdated()
        {
            await RunTimestampShowAsync(ShowType.MaxUpdated);
        }

        // Builds and displays the SHOW LAST_UPDATED / SHOW MAX_UPDATED timestamp tree from the metadata
        // pane database context menu, producing the same result tab as running the equivalent
        // "--> SHOW" comment-script command. The tree is built off the UI thread as it issues DMV
        // queries against the connection.
        private async Task RunTimestampShowAsync(ShowType showType)
        {
            if (!_metadataProvider.IsConnected)
            {
                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "Cannot show metadata timestamps - there is no open connection"));
                return;
            }

            try
            {
                await Task.Run(() => ResultsTargetGrid.RunMetadataTimestampShow(ActiveDocument, showType));
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(RunTimestampShowAsync), ex.Message);
                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error showing metadata timestamps: {ex.Message}"));
            }
        }

        public Task HandleAsync(QueryStartedEvent message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
            return Task.CompletedTask;
        }

        public Task HandleAsync(QueryFinishedEvent message, CancellationToken cancellationToken)
        {
            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
            return Task.CompletedTask;
        }

        public Task HandleAsync(SelectedDatabaseChangedEvent message, CancellationToken cancellationToken)
        {
            try
            {
                var selectedDB = DatabasesView.FirstOrDefault(db => db.Name == message.SelectedDatabase);
                if (selectedDB != null) SelectedDatabase = selectedDB;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "{class} {method} Error setting SelectedDatabase: {message}", nameof(MetadataPaneViewModel), "IHandle<SelectedDatabaseChangedEvent>", ex.Message);
                EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, "Error setting SelectedDatabase: " + ex.Message),cancellationToken);
            }
            return Task.CompletedTask;
        }

        public async Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {

            try
            {
                //SelectedDatabase = null;
                //SelectedModel = null;
                //DatabasesView.Clear();
                //ModelList?.Clear();
                Tables = Enumerable.Empty<IFilterableTreeViewItem>();
                NotifyOfPropertyChange(nameof(SelectedDatabase));
                NotifyOfPropertyChange(nameof(SelectedModel));
                NotifyOfPropertyChange(nameof(DatabasesView));
                NotifyOfPropertyChange(nameof(ModelList));
                NotifyOfPropertyChange(nameof(Tables));

                Databases.IsNotifying = false;
                Databases = _metadataProvider.GetDatabases().ToBindableCollection();
                Databases.IsNotifying = true;
                NotifyOfPropertyChange(nameof(Databases));

                var ml = _metadataProvider.GetModels();

                // Materialize the model collection on a background thread BEFORE assigning it to
                // ModelList. The ModelList setter reads ModelCollection.BaseModel, which lazily
                // runs a blocking DISCOVER metadata query the first time the collection is read.
                // Against a remote powerbi:// endpoint that query can take several seconds, so if
                // it ran on the UI thread it would freeze the app (see MetadataPaneViewModel /
                // ADOTabularModelCollection). Pre-warming here caches the models so the subsequent
                // ModelList assignment is instant regardless of which thread it runs on.
                if (ml != null)
                {
                    var sw = Stopwatch.StartNew();
                    await Task.Run(() => { var _ = ml.BaseModel; });
                    Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(HandleAsync),
                        $"Pre-warmed model collection off the UI thread (duration: {sw.ElapsedMilliseconds}ms, thread {System.Threading.Thread.CurrentThread.ManagedThreadId})");
                }

                // Assign on the UI thread. This is now cheap because the models are already cached.
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    ModelList = ml;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(new System.Action(() => ModelList = ml));
                }

                NotifyOfPropertyChange(() => CanSelectDatabase);
                NotifyOfPropertyChange(() => CanSelectModel);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "{class} {method} Error while changing the connection: {message}", nameof(MetadataPaneViewModel), "IHandle<ConnectionChangedEvent>", ex.Message);
                await EventAggregator.PublishAsync(new OutputMessage(MessageType.Error, "Error while changing the connection: " + ex.Message), cancellationToken);
            }

        }

        public Task HandleAsync(ConnectionOpenedEvent message, CancellationToken cancellationToken)
        {
            // ConnectionOpenedEvent is broadcast through the global event
            // aggregator and is therefore received by every MetadataPaneViewModel.
            // Each pane should only react to events raised by its own document's
            // ConnectionManager, otherwise opening a connection on a different
            // document (e.g. the temporary document spawned by Capture
            // Diagnostics) would blank this pane's metadata even though our
            // connection has not changed.
            if (message.Source != null && !ReferenceEquals(message.Source, _metadataProvider))
            {
                return Task.CompletedTask;
            }

            // Even when the event came from our own ConnectionManager, only
            // wipe the metadata if the new connection actually points at a
            // different server or database. Reconnecting to the same place
            // (e.g. when Capture Diagnostics re-opens our connection) should
            // leave the existing metadata intact.
            var newServer = ActiveDocument?.Connection?.ServerName ?? string.Empty;
            var newDatabase = ActiveDocument?.Connection?.DatabaseName ?? string.Empty;
            if (string.Equals(newServer, _lastServerName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(newDatabase, _lastDatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            _lastServerName = newServer;
            _lastDatabaseName = newDatabase;

            ModelList?.Clear();
            Databases.Clear();
            SelectedModel = null;
            SelectedDatabase = null;

            return Task.CompletedTask;
        }

        public Task HandleAsync(ConnectFailedEvent message, CancellationToken cancellationToken)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), "Handle<ConnectionFailedEvent>", "Setting IsBusy = false");
            //IsBusy = false;
            return Task.CompletedTask;
        }

        public async Task HandleAsync(TablesRefreshedEvent message, CancellationToken cancellationToken)
        {
            // TablesRefreshedEvent is broadcast through the global event
            // aggregator and is therefore received by every MetadataPaneViewModel.
            // Each pane should only react to events raised by its own document's
            // ConnectionManager, otherwise refreshing the tables on a different
            // document would drive this pane's busy overlay and table reload.
            if (message.Source != null && !ReferenceEquals(message.Source, _metadataProvider))
            {
                return;
            }

            await RefreshTablesAsync();
        }

        /// <summary>
        /// Handles navigation to a specific table (and optionally column) in the metadata pane.
        /// Expands the table and selects it/the column.
        /// </summary>
        public Task HandleAsync(NavigateToMetadataItemEvent message, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(message.TableName)) return Task.CompletedTask;
                if (Tables == null) return Task.CompletedTask;

                // Find the table in the tree
                var table = Tables.FirstOrDefault(t => 
                    t.Name.Equals(message.TableName, StringComparison.OrdinalIgnoreCase) ||
                    (t is TreeViewTable tvt && tvt.Caption.Equals(message.TableName, StringComparison.OrdinalIgnoreCase)));

                if (table == null) return Task.CompletedTask;

                // Expand the table
                table.IsExpanded = true;

                // If no column specified, select the table
                if (string.IsNullOrEmpty(message.ColumnName))
                {
                    // Collapse other tables for clarity
                    foreach (var otherTable in Tables.Where(t => t != table))
                    {
                        otherTable.IsExpanded = false;
                    }
                    table.IsSelected = true;
                }
                else
                {
                    // Find and select the column
                    var column = table.Children?.FirstOrDefault(c => 
                        c.Name.Equals(message.ColumnName, StringComparison.OrdinalIgnoreCase));
                    
                    if (column != null)
                    {
                        column.IsSelected = true;
                    }
                    else
                    {
                        table.IsSelected = true;
                    }
                }

                Log.Debug("{Class} {Method} Navigated to table: {TableName}, column: {ColumnName}", 
                    nameof(MetadataPaneViewModel), nameof(HandleAsync), message.TableName, message.ColumnName ?? "(none)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Class} {Method} Error navigating to metadata item", 
                    nameof(MetadataPaneViewModel), nameof(HandleAsync));
            }

            return Task.CompletedTask;
        }

        public bool ShowDataRefreshMenu
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

    }



    public class DatabaseReference : IDatabaseReference
    {
        public string Name { get; set; }
        public string Caption { get; set; }

        public string Description { get; set; }
    }

    class ExpandedItem: IExpandedItem
    {
        public ExpandedItem(string name)
        {
            Name = name;
            Children = new List<IExpandedItem>();
        }
        public string Name { get; set; }
        public List<IExpandedItem>Children { get; set; }
    }

    interface IExpandedItem
    {
        string Name { get; set; }
        List<IExpandedItem> Children { get; set; }
    }
}
