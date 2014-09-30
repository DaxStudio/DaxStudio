using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;

namespace DaxStudio.UI.ViewModels
{
    
    public class ToolPaneBaseViewModel:ToolWindowBase, IDragSource
    {
        private ADOTabularConnection _connection;

        public ToolPaneBaseViewModel(ADOTabularConnection connection, IEventAggregator eventAggregator)
        {
            PropertyChanged += OnPropertyChanged;
            Connection = connection;
            EventAggregator = eventAggregator;
        }

        protected IEventAggregator EventAggregator { get; set; }
        
        public virtual void OnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {

        }
        
        public bool IsConnected
        {
            get { return Connection != null; }
        }

        public ADOTabularConnection Connection {
            get { return _connection; } 
            set
            {
                //bool isSameDatabase = false;
                //if (_connection == value)
                //    return;
                if (_connection == null && value == null) return;
                //var isSameServer = false;
                //if (_connection != null && value != null)
                //{
                //    isSameServer = _connection.ServerName.Equals(value.ServerName, System.StringComparison.InvariantCultureIgnoreCase);
                //    isSameDatabase = _connection.Database.Name.Equals(value.Database.Name, System.StringComparison.InvariantCultureIgnoreCase);
                //}
                //if (isSameServer && isSameDatabase) return;
                _connection = value;
                //// TODO - should i notify here if it is the same server?
                ////NotifyOfPropertyChange(()=> Connection);
                OnConnectionChanged();//isSameServer);
            }
        }

        protected virtual void OnConnectionChanged()//bool isSameServer)
        {}
    
        public void MouseDoubleClick(IADOTabularObject item)
        {
            if (item != null)
            {
                var txt = item.DaxName;
                EventAggregator.Publish(new SendTextToEditor(txt));
            }
        }

        public IADOTabularObject SelectedItem { get; set; }

        public void SetSelectedItem(object item)
        {
            SelectedItem = (IADOTabularObject)item;
        }

        public int SelectedIndex { get; set; }

        public void StartDrag(IDragInfo dragInfo)
        {
            if (dragInfo.SourceItem as IADOTabularObject != null)
            {
                dragInfo.Data = ((IADOTabularObject)dragInfo.SourceItem).DaxName;
                dragInfo.DataObject = new DataObject(typeof(string), ((IADOTabularObject)dragInfo.SourceItem).DaxName);
                dragInfo.Effects = DragDropEffects.Move;
            }
            else
            { dragInfo.Effects = DragDropEffects.None; }
        }

        public void Dropped(IDropInfo dropInfo)
        {
            // do nothing
        }

        public void DragCancelled()
        {
            // do nothing
        }
    }

}
