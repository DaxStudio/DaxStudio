using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using GongSolutions.Wpf.DragDrop;
using Serilog;
using DaxStudio.UI.Model;
using System.Collections.Generic;
using System;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class MetadataPaneViewModel:ToolPaneBaseViewModel
        , IDragSource

    {
        private string _modelName;
        private readonly DocumentViewModel _activeDocument;
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator, DocumentViewModel document):base(connection,eventAggregator)
        {
            _activeDocument = document;
            _activeDocument.PropertyChanged += ActiveDocumentPropertyChanged;
            NotifyOfPropertyChange(() => ActiveDocument);
        }

        private void ActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsQueryRunning")
            {
                NotifyOfPropertyChange(() => CanSelectDatabase);
                NotifyOfPropertyChange(() => CanSelectModel);
            }
        }

        public DocumentViewModel ActiveDocument { get { return _activeDocument; }  }


        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "ModelList":
                    if (ModelList.Count > 0)
                    {
                        SelectedModel = ModelList.First(m => m.Name == Connection.Database.Models.BaseModel.Name);
                    }
                    Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "OnPropertyChanged:ModelList.Count", Connection.Database.Models.Count);          
                    break;
            }
        }

        public string ModelName {get {return _modelName; }
            set
            {
                if (value == _modelName)
                    return;
                _modelName = value;
                
               NotifyOfPropertyChange(() => ModelName);
            }
        }

        private ADOTabularModel _selectedModel;

        public ADOTabularModel SelectedModel {
            get { return _selectedModel; } 
            set {
                if (_selectedModel != value)
                {
                    _selectedModel = value;
                    _treeViewTables = null;
                    if (Connection.ServerMode == "Multidimensional")
                    {
                        Connection.SetCube(_selectedModel.Name);
                    }
                    NotifyOfPropertyChange(() => SelectedModel);
                    NotifyOfPropertyChange(() => Tables);
                }
            }
        }

        public string SelectedModelName
        {
            get
            {
                return SelectedModel == null ? "--":SelectedModel.Name; 
            }
        }
        
        protected override void OnConnectionChanged()//bool isSameServer)
        {
            base.OnConnectionChanged();//isSameServer);
            if (Connection == null)
                return;
            if (ModelList == Connection.Database.Models)
                return;

            Databases = Connection.Databases.ToSortedSet();

            var ml = Connection.Database.Models;
            Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "ConnectionChanged (Database)", Connection.Database.Name);          
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Dispatcher.CurrentDispatcher.Invoke(new System.Action(()=> ModelList = ml));
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
        public IEnumerable<FilterableTreeViewItem> Tables {
            get
            {
                if (SelectedModel == null) return null;
                if (_treeViewTables == null)
                {
                    try
                    {
                        _treeViewTables = SelectedModel.TreeViewTables();
                    }
                    catch (Exception ex)
                    {
                        EventAggregator.PublishOnUIThread(new OutputMessage(Events.MessageType.Error,ex.Message));
                    }
                }
                return _treeViewTables;
                //return SelectedModel == null ? null : SelectedModel.TreeViewTables();
                
            }
        }

        public override string DefaultDockingPane
        {
            get { return "DockLeft"; }
            set { base.DefaultDockingPane = value; }
        }
        public override string  Title
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
        public string CurrentCriteria  { 
            get { return _currentCriteria; }
            set { _currentCriteria = value;
                NotifyOfPropertyChange(() => CurrentCriteria);
                NotifyOfPropertyChange(() => HasCriteria);
                ApplyFilter();
            }
        }

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
        
        private string _selectedDatabase;
        public string SelectedDatabase
        {
            get { return _selectedDatabase; }
            set
            {
                
                if (value == _selectedDatabase)
                {
                    NotifyOfPropertyChange(() => SelectedDatabase);
                    return;
                }

                _selectedDatabase = value;
                ActiveDocument.SelectedDatabase = value;
                
                if (Connection != null)
                {
                    if (_selectedDatabase == null || !Connection.Database.Equals(_selectedDatabase))
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "MetadataPaneViewModel", "SelectedDatabase:Set (changing)", value);
                        Connection.ChangeDatabase( _selectedDatabase);
                        ModelList = Connection.Database.Models;
                    }
                }

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

    }
}
