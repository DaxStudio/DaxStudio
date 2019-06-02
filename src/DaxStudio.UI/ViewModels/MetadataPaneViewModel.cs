using System.ComponentModel;
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
using System.Data;
using System.Windows;
using System.Text.RegularExpressions;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class MetadataPaneViewModel : 
        ToolPaneBaseViewModel
        , IHandle<UpdateGlobalOptions>
        , IDragSource
        , IMetadataPane
    {
        private string _modelName;
        private readonly DocumentViewModel _activeDocument;
        private readonly IGlobalOptions _options;
        //private readonly IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) : base(connection, eventAggregator)
        {
            _activeDocument = document;
            _activeDocument.PropertyChanged += ActiveDocumentPropertyChanged;
            //    _eventAggregator = eventAggregator;
            _options = globalOptions;
            NotifyOfPropertyChange(() => ActiveDocument);
            eventAggregator.Subscribe(this);
            ShowHiddenObjects = _options.ShowHiddenMetadata;
            PinSearchOpen = _options.KeepMetadataSearchOpen;
        }

        private void ActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsQueryRunning")
            {
                NotifyOfPropertyChange(() => CanSelectDatabase);
                NotifyOfPropertyChange(() => CanSelectModel);
            }
            if (e.PropertyName == "SelectedDatabase")
            {
                var selectedDB = DatabasesView.FirstOrDefault(db => db.Name == ActiveDocument.SelectedDatabase);
                if (selectedDB != null) SelectedDatabase = selectedDB;
                // TODO - should we log a warning here?
            }
        }

        private bool _pinSearchOpen = false;
        public bool PinSearchOpen
        {
            get => _pinSearchOpen;
            set
            {
                _pinSearchOpen = value;
                NotifyOfPropertyChange(()=>IsMouseOverSearch);
                NotifyOfPropertyChange(() => PinSearchOpenLabel);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }
        public void TogglePinSearchOpen()
        {
            PinSearchOpen = !PinSearchOpen;
        }

        public string PinSearchOpenLabel
        {
            get { return PinSearchOpen ? "Unpin Search" : "Pin Search"; }
        }

        public DocumentViewModel ActiveDocument { get { return _activeDocument; } }


        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "ModelList":
                    try
                    {
                        if (ModelList.Count > 0)
                        {
                            SelectedModel = ModelList.First(m => m.Name == Connection.Database.Models.BaseModel.Name);
                        }
                        Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "OnPropertyChanged:ModelList.Count", Connection.Database.Models.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Fatal(ex, "{class} {method} Error refreshing model list on connection change: {message}", "MetadataPaneViewModel", "OnPropertyChange", ex.Message);
                        EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, "Error refreshing model list: " + ex.Message));
                    }
                    break;
            }
        }

        public string ModelName
        {
            get { return _modelName; }
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
            var _tmpModel = _selectedModel;
            _selectedModel = null;
            SelectedModel = _tmpModel;

        }


        public ADOTabularModel SelectedModel
        {
            get { 
                    return _selectedModel;
                }
            set
            {
                if (_selectedModel != value)
                {
                    try
                    {
                        _selectedModel = value;
                        _treeViewTables = null;
                        if (_selectedModel != null)
                        {
                            if (Connection.IsMultiDimensional)
                            {
                                if (Connection.Is2012SP1OrLater)
                                {
                                    Connection.SetCube(_selectedModel.Name);
                                }
                                else
                                {
                                    _activeDocument.OutputError(string.Format("DAX Studio can only connect to Multi-Dimensional servers running 2012 SP1 CU4 (11.0.3368.0) or later, this server reports a version number of {0}"
                                        , Connection.ServerVersion));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {message} {stacktrace}", "MetadataPaneViewModel", "SelectModel", ex.Message, ex.StackTrace);
                        EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, ex.Message));
                    }
                    finally
                    {
                        EventAggregator.PublishOnUIThread(new SelectedModelChangedEvent(ActiveDocument));
                        NotifyOfPropertyChange(() => SelectedModel);
                        RefreshTables();

                    }
                }
            }
        }

        public string SelectedModelName
        {
            get
            {
                return SelectedModel == null ? "--" : SelectedModel.Name;
            }
        }

        protected override void OnConnectionChanged()
        {
            base.OnConnectionChanged();
            if (Connection == null) return;
            if (ModelList == Connection.Database.Models) return;

            Execute.OnUIThread(() =>
            {
                Databases = Connection.Databases.ToBindableCollection();
            });
            var ml = Connection.Database.Models;
            Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "ConnectionChanged (Database)", Connection.Database.Name);
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(new System.Action(() => ModelList = ml));
            }
            else
            {
                ModelList = ml;
            }

            NotifyOfPropertyChange(() => IsConnected);
            NotifyOfPropertyChange(() => Connection);
            NotifyOfPropertyChange(() => CanSelectDatabase);
            NotifyOfPropertyChange(() => CanSelectModel);
        }

        private IEnumerable<FilterableTreeViewItem> _treeViewTables;
        public IEnumerable<FilterableTreeViewItem> Tables
        {
            get
            {
                return _treeViewTables;
            }
            set
            {
                _treeViewTables = value;
                NotifyOfPropertyChange(() => Tables);
            }
        }

        private void RefreshTables()
        {
            if (SelectedModel == null)
            {
                Tables = null;  // if there is no selected model clear the table collection
                return;
            }
            if (_treeViewTables == null)
            {

                // Load tables async
                Task.Run(() =>
                {
                    try
                    {
                        IsBusy = true;
                        _treeViewTables = SelectedModel.TreeViewTables(_options, EventAggregator, this);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("{class} {method} {error} {stacktrace}", "MetadataPaneViewModel", "Tables", ex.Message, ex.StackTrace);
                        EventAggregator.PublishOnUIThread(new OutputMessage(Events.MessageType.Error, ex.Message));
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }).ContinueWith((taskStatus) =>
                {
                    Tables = _treeViewTables;
                    EventAggregator.PublishOnUIThread(new MetadataLoadedEvent(ActiveDocument, SelectedModel));
                });
            }

        }

        public override string DefaultDockingPane
        {
            get { return "DockLeft"; }
            set { base.DefaultDockingPane = value; }
        }
        public override string Title
        {
            get { return "Metadata"; }
            set { base.Title = value; }
        }

        private ADOTabularModelCollection _modelList;
        public ADOTabularModelCollection ModelList
        {
            get { return _modelList; }
            set
            {
                if (value == _modelList)
                    return;
                _modelList = value;
                NotifyOfPropertyChange(() => ModelList);
                NotifyOfPropertyChange(() => SelectedModel);
            }
        }

        private string _currentCriteria = string.Empty;
        public string CurrentCriteria
        {
            get { return _currentCriteria; }
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
            get { return _isMouseOverSearch; }
            set
            {
                System.Diagnostics.Debug.WriteLine("MouseOver: " + value);
                _isMouseOverSearch = value;
                NotifyOfPropertyChange(() => IsMouseOverSearch);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }
        private bool _isKeyboardFocusWithinSearch;
        public bool IsKeyboardFocusWithinSearch
        {
            get { return _isKeyboardFocusWithinSearch; }
            set
            {
                System.Diagnostics.Debug.WriteLine("KeyboardFocusWithin: " + value);
                _isKeyboardFocusWithinSearch = value;
                NotifyOfPropertyChange(() => IsKeyboardFocusWithinSearch);
                NotifyOfPropertyChange(() => ExpandSearch);
            }
        }

        public bool ExpandSearch => IsMouseOverSearch 
                                 || IsKeyboardFocusWithinSearch 
                                 || _pinSearchOpen; 

        public bool HasCriteria
        {
            get { return _currentCriteria.Length > 0; }
        }

        public void ClearCriteria()
        {
            CurrentCriteria = string.Empty;
        }
        private void ApplyFilter()
        {
            if (Tables == null) return;
            foreach (var node in Tables)
                node.ApplyCriteria(CurrentCriteria, new Stack<FilterableTreeViewItem>());
        }

        // Database Dropdown Properties
        private BindableCollection<string> _databases = new BindableCollection<string>();
        public BindableCollection<string> Databases
        {
            get { return _databases; }
            set
            {
                _databases = value;
                MergeDatabaseView();
                //NotifyOfPropertyChange(() => Databases);
            }
        }

        private void MergeDatabaseView()
        {
            var newList = _databases.Select(
                                db => new DatabaseReference()
                                {
                                    Name = db,
                                    Caption = Connection.FileName.Length > 0 ? Connection.FileName : db
                                }).OrderBy(db => db.Name);

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

            NotifyOfPropertyChange(() => DatabasesView);
            if (SelectedDatabase == null)
                if (Connection?.Database != null )
                    SelectedDatabase = DatabasesView.FirstOrDefault(x => x.Name == Connection.Database.Name);
                else
                    SelectedDatabase = DatabasesView.FirstOrDefault();
        }

        private DatabaseReference _selectedDatabase;
        public DatabaseReference SelectedDatabase
        {
            get { return _selectedDatabase; }
            set
            {
            
                if (Connection != null)
                {
                    if (_selectedDatabase != null && value != null && Connection.Database.Name != value.Name ) //!Connection.Database.Equals(_selectedDatabase))
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "MetadataPaneViewModel", "SelectedDatabase:Set (changing)", value.Name);
                        Connection.ChangeDatabase(value.Name);

                    }
                    if (Connection.Database != null)
                    {
                        ModelList = Connection.Database.Models;
                    }
                }

                if (_selectedDatabase != value)
                {
                    _selectedDatabase = value;

                    NotifyOfPropertyChange(() => SelectedDatabase);
                }

            }
        }

        public bool CanSelectDatabase
        {
            get
            {
                return Connection != null && !Connection.IsPowerPivot && !ActiveDocument.IsQueryRunning;
            }
        }

        public bool CanSelectModel
        {
            get
            {
                return Connection != null && !ActiveDocument.IsQueryRunning;
            }
        }

        public void RefreshDatabases()
        {

            try
            {
                this.Connection.Refresh();
                var sourceSet = this.Connection.Databases.ToBindableCollection();

                var deletedItems = this.Databases.Except(sourceSet);
                var newItems = sourceSet.Except(this.Databases);
                // remove deleted items
                for (var i = deletedItems.Count() - 1; i >= 0; i--)
                {
                    var tmp = deletedItems.ElementAt(i);
                    Execute.OnUIThread(() =>
                    {
                        this.Databases.Remove(tmp);
                    });
                }
                // add new items
                foreach (var itm in newItems)
                {
                    Execute.OnUIThread(() =>
                    {
                        this.Databases.Add(itm);
                    });
                }
                DatabasesView.Refresh();
            
            }
            catch (Exception ex)
            {
                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Error, string.Format("Unable to refresh the list of databases due to the following error: {0}", ex.Message)));
            }

        }

        private SortedSet<string> CopyDatabaseList(ADOTabularConnection cnn)
        {
            var ss = new SortedSet<string>();
            foreach (var dbname in cnn.Databases)
            { ss.Add(dbname); }
            return ss;
        }
        public IObservableCollection<DatabaseReference> DatabasesView { get; } = new BindableCollection<DatabaseReference>();

        #region Busy Overlay
        private bool _isBusy = false;
        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;
                NotifyOfPropertyChange(() => IsBusy);
            }
        }
        public string BusyMessage { get { return "Loading"; } }
        #endregion

        private bool _showHiddenObjects = true;
        public bool ShowHiddenObjects { get => _showHiddenObjects;
            set {
                var changed = (_showHiddenObjects != value);
                _showHiddenObjects = value;
                if (changed)
                {
                    NotifyOfPropertyChange(ShowHiddenObjectsLabel);
                    RefreshMetadata();
                }
            }
        }

        public void ToggleHiddenObjects()
        {
            ShowHiddenObjects = !ShowHiddenObjects;
        }

        public string ShowHiddenObjectsLabel
        {
            get
            {
                return ShowHiddenObjects ? "Hide Hidden Objects" : "Show Hidden Objects";
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
            if (_options.ShowTooltipSampleData && !column.HasSampleData) column.GetSampleDataAsync(Connection, 10);
            if (_options.ShowTooltipBasicStats && !column.HasBasicStats) column.UpdateBasicStatsAsync(Connection);
        }

        internal void ChangeDatabase(string databaseName)
        {
            SelectedDatabase = DatabasesView.Where(db => db.Name == databaseName).FirstOrDefault();
        }

        #region Measure Definition Methods

        private string ExpandDependentMeasure(ADOTabularColumn column)
        {
            return ExpandDependentMeasure(column, false);
        }
        private string ExpandDependentMeasure(ADOTabularColumn column, bool ignoreNonUniqueMeasureNames)
        {
            string measureName = column.Name;
            var model = Connection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 select m).ToList();
            var distinctColumns = (from t in model.Tables
                                   from c in t.Columns
                                   where c.ObjectType == ADOTabularObjectType.Column
                                   select c.Name).Distinct().ToList();

            var finalMeasure = modelMeasures.First(m => m.Name == measureName);

            var resultExpression = finalMeasure.Expression;

            bool foundDependentMeasures;

            do
            {
                foundDependentMeasures = false;
                foreach (var modelMeasure in modelMeasures)
                {
                    //string daxMeasureName = "[" + modelMeasure.Name + "]";
                    //string newExpression = resultExpression.Replace(daxMeasureName, " CALCULATE ( " + modelMeasure.Expression + " )");
                    Regex daxMeasureRegex = new Regex(@"[^\w']?\[" + modelMeasure.Name + "]");

                    string newExpression = daxMeasureRegex.Replace(resultExpression, " CALCULATE ( " + modelMeasure.Expression + " )");

                    if (newExpression != resultExpression)
                    {
                        resultExpression = newExpression;
                        foundDependentMeasures = true;
                        if (!ignoreNonUniqueMeasureNames)
                        {
                            if (distinctColumns.Contains(modelMeasure.Name))
                            {
                                // todo - prompt user to see whether to continue
                                var msg = "The measure name: '" + modelMeasure.Name + "' is also used as a column name in one or more of the tables in this model";
                                EventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, msg));
                                throw new InvalidOperationException(msg);
                            }
                        }
                    }

                }
            } while (foundDependentMeasures);

            return resultExpression;
        }

        private List<ADOTabularMeasure> GetAllMeasures(string filterTable = null)
        {
            bool allTables = (string.IsNullOrEmpty(filterTable));
            var model = Connection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 where (allTables || t.Caption == filterTable)
                                 select m).ToList();
            return modelMeasures;
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
            catch (System.Exception ex)
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
            catch (System.Exception ex)
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
            if (item == null)
            {
                return;
            }
            string measureName = string.Format("'{0}'[{1}]", item.Caption, "DumpFilters" + (allTables ? "" : " " + item.Caption));
            try
            {
                var model = Connection.Database.Models.BaseModel;
                var distinctColumns = (from t in model.Tables
                                       from c in t.Columns
                                       where c.ObjectType == ADOTabularObjectType.Column
                                           && (allTables || t.Caption == item.Caption)
                                       select c).Distinct().ToList();
                string measureExpression = "\r\nVAR MaxFilters = 3\r\nRETURN\r\n";
                bool firstMeasure = true;
                foreach (var c in distinctColumns)
                {
                    if (!firstMeasure) measureExpression += "\r\n & ";
                    measureExpression += string.Format(@"IF ( 
    ISFILTERED ( {0}[{1}] ), 
    VAR ___f = FILTERS ( {0}[{1}] ) 
    VAR ___r = COUNTROWS ( ___f ) 
    VAR ___t = TOPN ( MaxFilters, ___f, {0}[{1}] )
    VAR ___d = CONCATENATEX ( ___t, {0}[{1}], "", "" )
    VAR ___x = ""{0}[{1}] = "" & ___d & IF(___r > MaxFilters, "", ... ["" & ___r & "" items selected]"") & "" "" 
    RETURN ___x & UNICHAR(13) & UNICHAR(10)
)", c.Table.DaxName, c.Name);
                    firstMeasure = false;
                }

                EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measureName, measureExpression));
            }
            catch (System.Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineFilterDumpMeasure", ex.Message, ex.StackTrace);

            }
        }

        public void DefineMeasure(TreeViewColumn item, bool expandMeasure)
        {
            try
            {
                if (item == null)
                {
                    return;
                }

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
            catch (System.Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasure", ex.Message, ex.StackTrace);

            }
        }

        public void Handle(UpdateGlobalOptions message)
        {
            NotifyOfPropertyChange(() => ExpandSearch);
            this.ShowHiddenObjects = _options.ShowHiddenMetadata;
        }

        #endregion

        #region Discover Referencing Objects methods
        public void ShowObjectsThatReferenceColumnOrMeasure(TreeViewColumn item)
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
                        " [REFERENCED_TABLE] AS [Referenced Table], " + Environment.NewLine +
                        " [REFERENCED_OBJECT] AS [Referenced Object], " + Environment.NewLine +
                        " [REFERENCED_OBJECT_TYPE] AS [Referenced Object Type] " + Environment.NewLine +
                        "FROM $SYSTEM.DISCOVER_CALC_DEPENDENCY " + Environment.NewLine +
                        "WHERE [REFERENCED_OBJECT] = '" + txt + "'" + Environment.NewLine +
                        "ORDER BY [OBJECT_TYPE]";
                    EventAggregator.PublishOnUIThread(new SendTextToEditor(thisItem,true));
                }
            }
            catch (System.Exception ex)
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
            catch (System.Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "MetadataPaneViewModel", "DefineObjectsThatReferenceTable", ex.Message, ex.StackTrace);
            }
        }

        #endregion

    }


    //public class DatabaseComparer : IComparer
    //{
    //    public int Compare(object x, object y)
    //    {
    //        String custX = x as String;
    //        String custY = y as String;
    //        return custX.CompareTo(custY);
    //    }
    //}


    public class DatabaseReference
    {
        public string Name { get; set; }
        public string Caption { get; set; }
    }
}
