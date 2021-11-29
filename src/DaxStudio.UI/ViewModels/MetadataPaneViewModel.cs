using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Threading;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using GongSolutions.Wpf.DragDrop;
using Serilog;
using DaxStudio.UI.Model;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using DaxStudio.UI.Extensions;
using DaxStudio.Interfaces;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using ADOTabular.Interfaces;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using Humanizer;
using FocusManager = DaxStudio.UI.Utils.FocusManager;

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
        , IHandleWithTask<TablesRefreshedEvent>
        //, IDragSource
        , IMetadataPane
    {
        private string _modelName;
        private readonly IGlobalOptions _options;
        private readonly IMetadataProvider _metadataProvider;
        private List<IExpandedItem> _expandedItems = new List<IExpandedItem>();
        
        [ImportingConstructor]
        public MetadataPaneViewModel(IMetadataProvider metadataProvider, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) 
            : base( eventAggregator)
        {
            _metadataProvider = metadataProvider;
            ActiveDocument = document;

            _options = globalOptions;
            NotifyOfPropertyChange(() => ActiveDocument);
            // TODO - is this a possible resource leak, should we unsubscribe when closing the document for this metadatapane??
            //eventAggregator.Subscribe(this);  
            ShowHiddenObjects = _options.ShowHiddenMetadata;
            SortFoldersFirstInMetadata = _options.SortFoldersFirstInMetadata;
            PinSearchOpen = _options.KeepMetadataSearchOpen;
        }





        public IEnumerable<FilterableTreeViewItem> SelectedItems { get; } = new List<FilterableTreeViewItem>();

        private bool _pinSearchOpen;
        public bool PinSearchOpen
        {
            get => _pinSearchOpen;
            set
            {
                var changed = _pinSearchOpen != _options.KeepMetadataSearchOpen;
                if (!changed) return;

                _pinSearchOpen = value;
                NotifyOfPropertyChange(() => IsMouseOverSearch);
                NotifyOfPropertyChange(() => PinSearchOpenLabel);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }
        public void TogglePinSearchOpen()
        {
            PinSearchOpen = !PinSearchOpen;
        }

        public string PinSearchOpenLabel => PinSearchOpen ? "Unpin Search" : "Pin Search";

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
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Information, "Metadata Refreshed"));
            }
            catch (Exception ex)
            {
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error,$"Error Refreshing Metadata: {ex.Message}"));
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
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    NotifyOfPropertyChange(nameof(SelectedModel));
                    //EventAggregator.PublishOnBackgroundThread(new SelectedModelChangedEvent( SelectedModelName));
                    
                    // clear table list
                    _treeViewTables = null;
                    _metadataProvider.SetSelectedModel(SelectedModel);
                }
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

        private async Task RefreshTablesAsync()
        {
            if (SelectedModel == null)
            {
                Tables = null;  // if there is no selected model clear the table collection
                return;
            }
            if (_treeViewTables == null)
            {

                // Load tables async
                await Task.Run(() =>
                {
                    try
                    {

                        var sw = new Stopwatch();
                        sw.Start();
                        IsBusy = true;
                        Log.Information(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(RefreshTablesAsync), "Starting Refresh of Tables ");
                        _treeViewTables = _metadataProvider.GetTreeViewTables(this, _options);
                        sw.Stop();
                        Log.Information("{class} {method} {message}", "MetadataPaneViewModel", "RefreshTables", $"Finished Refresh of tables (duration: {sw.ElapsedMilliseconds}ms)");
                        RestoreExpandedState("");
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {error} {stacktrace}", "MetadataPaneViewModel", "RefreshTables.Task", ex.Message, ex.StackTrace);
                        EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                    }
                    finally
                    {
                        ShowMetadataRefreshPrompt = false;
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), nameof(RefreshTablesAsync), "Setting IsBusy = false");
                        IsBusy = false;
                    }
                });

                try
                {
                    IsNotifying = false;
                    Tables = _treeViewTables;
                    EventAggregator.PublishOnUIThread(new MetadataLoadedEvent(ActiveDocument, SelectedModel));
                }
                catch(Exception ex)
                {
                    Log.Error("{class} {method} {error} {stacktrace}", "MetadataPaneViewModel", "RefreshTables.ContinueWith", ex.Message, ex.StackTrace);
                    EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                }
                finally
                {
                    IsNotifying = true;
                    Refresh(); // force all data bindings to update
                }
                
            }

        }

        public override string DefaultDockingPane => "DockLeft";
        public override string ContentId => "metadata";
        public override ImageSource IconSource
        {
            get
            {
                var imgSourceConverter = new ImageSourceConverter();
                return imgSourceConverter.ConvertFromInvariantString(
                    @"pack://application:,,,/DaxStudio.UI;component/images/Metadata/hierarchy.png") as ImageSource;

            }
        }
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
                SelectedModel = _modelList.BaseModel;
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
                _currentCriteria = value;
                if (_currentCriteria.Length >= 2 || _currentCriteria.Length == 0)
                {
                    NotifyOfPropertyChange(() => CurrentCriteria);
                    NotifyOfPropertyChange(() => HasCriteria);
                    ApplyFilter();
                }
            }
        }

        private bool _isMouseOverSearch;
        public bool IsMouseOverSearch
        {
            get => _isMouseOverSearch;
            set
            {
                Debug.WriteLine("MouseOver: " + value);
                _isMouseOverSearch = value;
                NotifyOfPropertyChange(() => IsMouseOverSearch);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }
        private bool _isKeyboardFocusWithinSearch;
        public bool IsKeyboardFocusWithinSearch
        {
            get => _isKeyboardFocusWithinSearch;
            set
            {
                Debug.WriteLine("KeyboardFocusWithin: " + value);
                _isKeyboardFocusWithinSearch = value;
                NotifyOfPropertyChange(() => IsKeyboardFocusWithinSearch);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }

        public bool ExpandSearch => IsMouseOverSearch 
                                 || IsKeyboardFocusWithinSearch 
                                 || PinSearchOpen; 

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

            NotifyOfPropertyChange(() => DatabasesView);
            if (SelectedDatabase == null)
                if (!string.IsNullOrEmpty(_metadataProvider.SelectedDatabaseName))
                    SelectedDatabase = DatabasesView.FirstOrDefault(x => x.Name == _metadataProvider.SelectedDatabaseName);
                else
                    SelectedDatabase = DatabasesView.FirstOrDefault();
        }

        private DatabaseReference _selectedDatabase;
        public DatabaseReference SelectedDatabase
        {
            get => _selectedDatabase;
            set
            {
                if (_selectedDatabase != value)
                {
                    _selectedDatabase = value;
                    if (_selectedDatabase == null) return;
                    NotifyOfPropertyChange(nameof(SelectedDatabase));
                    _metadataProvider.SetSelectedDatabase(_selectedDatabase);
                    NotifyOfPropertyChange(nameof(SelectedDatabaseObject));
                    NotifyOfPropertyChange(nameof(SelectedDatabaseDurationSinceUpdate));
                    NotifyOfPropertyChange(nameof(SelectedDatabaseLastUpdateLocalTime));
                    ModelList = _metadataProvider.GetModels();
                }
                

            }
        }

        public ADOTabularDatabase SelectedDatabaseObject => _metadataProvider.SelectedDatabase;

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
                DatabasesView.Refresh();
            
            }
            catch (Exception ex)
            {
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Unable to refresh the list of databases due to the following error: {0}", ex.Message)));
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
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                NotifyOfPropertyChange(() => IsBusy);
            }
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
                Clipboard.SetText(SelectedDatabase.Name);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Copied Database Name '{SelectedDatabase.Name}' to clipboard"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Class} {Method} {Message}", nameof(MetadataPaneViewModel), nameof(CopyDatabaseName), ex.Message);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following Error occured while copying the database name to the clipboard - {ex.Message} "));
            }
            
        }

        public void CopyDatabaseId()
        {
            try
            {
                Clipboard.SetText(SelectedDatabaseObject.Id);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Copied Database Id '{SelectedDatabaseObject.Id}' to clipboard"));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Class} {Method} {Message}", nameof(MetadataPaneViewModel), nameof(CopyDatabaseId), ex.Message);
                EventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"The following Error occured while copying the database Id to the clipboard - {ex.Message} "));
            }

        }

        public void ColumnTooltipOpening(TreeViewColumn column)
        {
            if (column == null) return;

            if (column?.Column?.GetType() != typeof(ADOTabularColumn)) return;
            ADOTabularColumn col = (ADOTabularColumn)column?.Column;
            
            if (col.ObjectType != ADOTabularObjectType.Column) return;
            // TODO - make an option for the sample size
            if (_options == null) return;
            if (_options.ShowTooltipSampleData && !column.HasSampleData) _metadataProvider.UpdateColumnSampleData(column,10) ;
            if (_options.ShowTooltipBasicStats && !column.HasBasicStats) _metadataProvider.UpdateColumnBasicStats(column); 
        }

        public void TableTooltipOpening(TreeViewTable table)
        {
            if (table == null) return;

            // TODO - make an option for the sample size
            if (_options == null) return;

            if (_options.ShowTooltipBasicStats && !table.HasBasicStats) _metadataProvider.UpdateTableBasicStats(table);
        }

        internal void ChangeDatabase(string databaseName)
        {
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
            var modelMeasures = GetAllMeasures();

            var dependentMeasures = new List<ADOTabularMeasure>();
            dependentMeasures.Add(modelMeasures.First(m => m.Name == measureName));

            bool foundDependentMeasures;
            do
            {
                foundDependentMeasures = false;
                foreach (var modelMeasure in modelMeasures)
                {
                    string daxMeasureName = "[" + modelMeasure.Name + "]";
                    // Iterates a copy so the original list can be modified
                    foreach (var scanMeasure in dependentMeasures.ToList())
                    {
                        if (modelMeasure == scanMeasure) continue;
                        string dax = scanMeasure.Expression;
                        if (dax.Contains(daxMeasureName))
                        {
                            if (!dependentMeasures.Contains(modelMeasure))
                            {
                                dependentMeasures.Add(modelMeasure);
                                foundDependentMeasures = true;
                            }
                        }
                    }
                }
            } while (foundDependentMeasures);

            return dependentMeasures;
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

                var dependentMeasures = FindDependentMeasures(column.Name);
                foreach (var measure in dependentMeasures)
                {
                    EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measure.DaxName, measure.Expression));
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
                    EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measure.DaxName, measure.Expression));
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

                EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measureName, measureExpression));
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineFilterDumpMeasure", ex.Message, ex.StackTrace);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, $"Error defining filter dump measure: {ex.Message}"));
            }
        }

        public void DefineMeasure(TreeViewColumn item, bool expandMeasure)
        {
            try
            {
                if (item == null) return;

                ADOTabularColumn column; string measureExpression = null, measureName = null;

                if (item.Column is ADOTabularKpiComponent)
                {
                    var kpiComponent = (ADOTabularKpiComponent)item.Column;

                    column = (ADOTabularColumn)kpiComponent.Column;

                    // The KPI Value dont have an expression and points to a measure

                    if (kpiComponent.ComponentType == KpiComponentType.Value && string.IsNullOrEmpty(column.MeasureExpression))
                    {
                        measureName = string.Format("{0}[{1} {2}]", column.Table.DaxName, column.Name, kpiComponent.ComponentType.ToString());

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
                    measureName = string.Format("{0}[{1}]", column.Table.DaxName, column.Name);
                }

                if (expandMeasure)
                {
                    try
                    {
                        measureExpression = ExpandDependentMeasure(column);
                    }
                    catch (InvalidOperationException ex)
                    {
                        string msg = ex.Message + "\nThis may lead to incorrect results in cases where the column is referenced without explicitly specifying the table name.\n\nDo you want to continue with the expansion anyway?";
                        if (MessageBox.Show(msg, "Expand Measure Error", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                        {
                            ExpandDependentMeasure(column, true);
                        }
                        else return;
                    }
                }

                if (string.IsNullOrEmpty(measureExpression))
                {
                    measureExpression = column.MeasureExpression;
                }

                EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measureName, measureExpression));
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasure", ex.Message, ex.StackTrace);

            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            
            ShowHiddenObjects = _options.ShowHiddenMetadata;
            SortFoldersFirstInMetadata = _options.SortFoldersFirstInMetadata;
            PinSearchOpen = _options.KeepMetadataSearchOpen;
            NotifyOfPropertyChange(() => ExpandSearch);
        }

        #endregion

        #region Discover Referencing Objects methods
        public void ShowObjectsThatReferenceColumnOrMeasure(TreeViewColumn item)
        {
            try
            {
                if (item != null)
                {
                    var criteria = $"WHERE [REFERENCED_OBJECT] = '{item.Name}'";

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
                    EventAggregator.PublishOnUIThread(new SendTextToEditor(thisItem,true));
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
                    var txt = item.Name;
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
                    EventAggregator.PublishOnUIThread(new SendTextToEditor(thisItem,true));
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
                        EventAggregator.PublishOnUIThread(new SendColumnToQueryBuilderEvent(col, QueryBuilderItemType.Column));
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
                        EventAggregator.PublishOnUIThread(new SendColumnToQueryBuilderEvent(filter, QueryBuilderItemType.Filter));
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
                        EventAggregator.PublishOnUIThread(new SendColumnToQueryBuilderEvent(item, QueryBuilderItemType.Both));
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
            var firstItem = Tables.FirstOrDefault(t => t.IsMatch);
            firstItem.IsSelected = true;
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
                EventAggregator.PublishOnUIThread(new SendTextToEditor(query, true));
            }
            else
            {
                // throw error
                ActiveDocument.OutputError("The selected metadata object does not support query generation");
                ActiveDocument.ActivateOutput();
            }

        }

        public void Handle(QueryStartedEvent message)
        {
            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
        }

        public void Handle(QueryFinishedEvent message)
        {
            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
        }

        public void Handle(SelectedDatabaseChangedEvent message)
        {
            var selectedDB = DatabasesView.FirstOrDefault(db => db.Name == message.SelectedDatabase);
            if (selectedDB != null) SelectedDatabase = selectedDB;

            
            // TODO - should we log a warning here?

            // refresh model list
            try
            {
                //if (ModelList == null) return;
                //if (Connection == null) return;
                //if (Connection?.Database?.Models == null) return;

                //if (ModelList.Count > 0)
                //{
                //    SelectedModel = ModelList.First(m => m.Name == Connection.Database.Models.BaseModel.Name);
                //}
                //Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "OnPropertyChanged:ModelList.Count", Connection.Database.Models.Count);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "{class} {method} Error refreshing model list on connection change: {message}", "MetadataPaneViewModel", "OnPropertyChange", ex.Message);
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error refreshing model list: " + ex.Message));
            }

        }

        public void Handle(ConnectionChangedEvent message)
        {

            Execute.OnUIThread(() =>
            {
                Databases.IsNotifying = false;
                Databases = _metadataProvider.GetDatabases().ToBindableCollection();
                Databases.IsNotifying = true;
                NotifyOfPropertyChange(nameof(Databases));
            });
            var ml = _metadataProvider.GetModels();
            //Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "ConnectionChanged (Database)", Connection.Database.Name);
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(new System.Action(() => ModelList = ml));
            }
            else
            {
                ModelList = ml;
            }

            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
        }

        public void Handle(ConnectionOpenedEvent message)
        {
            IsBusy = true;
            NotifyOfPropertyChange(nameof(Databases));
        }

        public void Handle(ConnectFailedEvent message)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(MetadataPaneViewModel), "Handle<ConnectionFailedEvent>", "Setting IsBusy = false");
            IsBusy = false;
        }

        public async Task Handle(TablesRefreshedEvent message)
        {
            await RefreshTablesAsync();
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
