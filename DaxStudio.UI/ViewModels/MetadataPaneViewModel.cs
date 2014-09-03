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

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class MetadataPaneViewModel:ToolPaneBaseViewModel, IDragSource
    {
        private string _modelName;
        
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator):base(connection,eventAggregator)
        {  }

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "ModelList":
                    SelectedModel = ModelList.First(m => m.Name == Connection.Database.Models.BaseModel.Name);
                    Log.Debug("{Class} {Event} {Value}", "MetadataPaneViewModel", "OnPropertyChanged:ModelList", Connection.Database.Models.BaseModel.Name);          
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
        
        protected override void OnConnectionChanged(bool isSameServer)
        {
            base.OnConnectionChanged(isSameServer);
            if (Connection == null)
                return;
            if (ModelList == Connection.Database.Models)
                return;
            
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
        }

        public ADOTabularTableCollection Tables {
            get
            {
                return SelectedModel == null ? null : SelectedModel.Tables;
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
        
    }
}
