using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using GongSolutions.Wpf.DragDrop;

namespace DaxStudio.UI.ViewModels
{
    [Export]
    public class MetadataPaneViewModel:ToolPaneBaseViewModel, IDragSource
    {
        private string _modelName;
        //private ADOTabularConnection _connection;
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator):base(connection,eventAggregator)
        {
        }

        /*
        public MetadataPaneViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator)
        {
            PropertyChanged += OnPropertyChanged;
            Connection = connection;
            _eventAggregator = eventAggregator;
        }
        */

        private readonly IEventAggregator _eventAggregator;

        public override void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            switch (propertyChangedEventArgs.PropertyName)
            {
                case "Connection":
                    
                        if (Connection != null)
                        {
                            ModelList = Connection.Database.Models;
                            //SelectedModel = Connection.Database.Models.BaseModel;

                        }
                    break;
                case "ModelList":
                    //SelectedModel = Connection.Database.Models.BaseModel;
                    SelectedModel = ModelList.First(m => m.Name == Connection.Database.Models.BaseModel.Name);
                              
//                    NotifyOfPropertyChange(() => SelectedModel);
//                    NotifyOfPropertyChange(() => SelectedModelName);
//                    NotifyOfPropertyChange(() => SelectedIndex);

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
            set { _selectedModel = value;
                //SelectedIndex = 0;
  /*              for (int i = 0; i <= ModelList.Count - 1; i++)
                {
                    if (!ModelList[i].IsPerspective)
                    {
                        SelectedIndex = i;
                        break;
                    }
                }
   */ 
                NotifyOfPropertyChange(() => SelectedModel);
   //             NotifyOfPropertyChange(() => SelectedModelName);
                NotifyOfPropertyChange(() => Tables);
            }
        }

        public string SelectedModelName
        {
            get
            {
                return SelectedModel == null ? "--":SelectedModel.Name; 
            }
        }
        /*
        public bool IsConnected
        {
            get { return Connection != null; }
        }

        public ADOTabularConnection Connection {
            get { return _connection; } 
            set
            {
                if (_connection == value)
                    return;
                _connection = value;
                ModelList = _connection.Database.Models;
                NotifyOfPropertyChange(() => IsConnected);
                NotifyOfPropertyChange(() => Connection);
            }
        }
        */

        protected override void OnConnectionChanged()
        {
            base.OnConnectionChanged();
            if (Connection == null)
                return;
            var ml = Connection.Database.Models;
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

        //private IObservableCollection<string> _modelList;

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
        
    /*
        public void MouseDoubleClick(IADOTabularObject item)
        {
            var txt = item.DaxName;
            _eventAggregator.Publish(new SendTextToEditor(txt) );
        }
        
        public IADOTabularObject SelectedItem { get; set; }
        
        public void SetSelectedItem(object item)
        {
            SelectedItem = (IADOTabularObject)item;
        }
        
        public int SelectedIndex { get; set; }
        */
        //private BindableCollection<ADOTabularModel> _modelList;
        //public BindableCollection<ADOTabularModel> ModelList
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
        /*
        public void StartDrag(IDragInfo dragInfo)
        {
            dragInfo.Data = ((IADOTabularObject) dragInfo.SourceItem).DaxName;
            dragInfo.DataObject = new DataObject(typeof(string), ((IADOTabularObject)dragInfo.SourceItem).DaxName);
            dragInfo.Effects = DragDropEffects.Move;
        }
        
        public void Dropped(IDropInfo dropInfo)
        {
            throw new System.NotImplementedException();
        }
         */ 
    }

}
