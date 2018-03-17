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
using System.Windows.Data;
using System.Collections;
using System.Threading.Tasks;
using DaxStudio.UI.Extensions;
using DaxStudio.Interfaces;
using System.Data.SqlClient;
using ADOTabular.AdomdClientWrappers;
using System.Text;
using System.Data;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class MetadataPaneViewModel : ToolPaneBaseViewModel
        , IDragSource
    {
        private string _modelName;
        private readonly DocumentViewModel _activeDocument;
        private readonly IGlobalOptions _globalOptions;
        //private readonly IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document, IGlobalOptions globalOptions) : base(connection, eventAggregator)
        {
            _activeDocument = document;
            _activeDocument.PropertyChanged += ActiveDocumentPropertyChanged;
            //    _eventAggregator = eventAggregator;
            _globalOptions = globalOptions;
            NotifyOfPropertyChange(() => ActiveDocument);
            eventAggregator.Subscribe(this);
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
            get { return _selectedModel; }
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

        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
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
                        //using (var conn = Connection.Clone())
                        //{
                        //    conn.Open();
                        //    _treeViewTables = conn.Database.Models[SelectedModel.Name].TreeViewTables();    
                        //}
                        _treeViewTables = SelectedModel.TreeViewTables(_globalOptions, EventAggregator);
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

        public bool ExpandSearch { get { return IsMouseOverSearch || IsKeyboardFocusWithinSearch; } }

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
                                    Caption = Connection.PowerBIFileName.Length > 0 ? Connection.PowerBIFileName : db
                                }).OrderBy(db => db.Name);

            // remove deleted databases
            for (int i = _databasesView.Count - 1; i >= 0; i--)
            {
                var found = newList.Where(db => db.Name == _databasesView[i].Name).Any();
                if (!found) _databasesView.RemoveAt(i);
            }

            // add new databases
            foreach (DatabaseReference dbRef in newList)
            {
                var found = _databasesView.Where(db => db.Name == dbRef.Name).DefaultIfEmpty();
                if (found != null) _databasesView.Add(dbRef);
            }

            NotifyOfPropertyChange(() => DatabasesView);
            if (SelectedDatabase == null) SelectedDatabase = DatabasesView.FirstOrDefault();
        }

        private DatabaseReference _selectedDatabase;
        public DatabaseReference SelectedDatabase
        {
            get { return _selectedDatabase; }
            set
            {
                if (value == null) ActiveDocument.SelectedDatabase = null;

                if (value == _selectedDatabase)
                {
                    NotifyOfPropertyChange(() => SelectedDatabase);
                    return;
                }


                if (value != null) ActiveDocument.SelectedDatabase = value.Name;

                if (Connection != null)
                {
                    if (_selectedDatabase != null && Connection.Database.Name != _selectedDatabase.Name && value != null) //!Connection.Database.Equals(_selectedDatabase))
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "MetadataPaneViewModel", "SelectedDatabase:Set (changing)", value);
                        Connection.ChangeDatabase(value.Name);

                    }
                    if (Connection.Database != null)
                    {
                        ModelList = Connection.Database.Models;
                    }
                }

                _selectedDatabase = value;

                NotifyOfPropertyChange(() => SelectedDatabase);

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
                    // Your Action Code
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
                        // Your Action Code
                        this.Databases.Add(itm);
                    });
                }
                _databasesView.Refresh();
                //NotifyOfPropertyChange(() => Databases);
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

        //private ICollectionView _databasesView;
        //public ICollectionView DatabasesView
        //{
        //    get { 
        //        return _databasesView; 
        //    }
        //}

        private IObservableCollection<DatabaseReference> _databasesView = new BindableCollection<DatabaseReference>();
        public IObservableCollection<DatabaseReference> DatabasesView
        {
            get
            {
                return _databasesView;
            }
        }

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

        public void ColumnTooltipOpening(TreeViewColumn column)
        {
            if (column.Column.GetType() != typeof(ADOTabularColumn)) return;
            ADOTabularColumn col = (ADOTabularColumn)column.Column;
            if (col.ColumnType != ADOTabularColumnType.Column) return;
            // TODO - make an option for the sample size
            if (_globalOptions.ShowTooltipSampleData && !column.HasSampleData) column.GetSampleDataAsync(Connection, 10);
            if (_globalOptions.ShowTooltipBasicStats && !column.HasBasicStats) column.UpdateBasicStatsAsync(Connection);
        }                 
    }


    public class DatabaseComparer : IComparer
    {
        public int Compare(object x, object y)
        {
            String custX = x as String;
            String custY = y as String;
            return custX.CompareTo(custY);
        }
    }


    public class DatabaseReference
    {
        public string Name { get; set; }
        public string Caption { get; set; }
    }
}
