using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using Serilog;

namespace DaxStudio.UI.ViewModels
{

    public class ToolPaneBaseViewModel : ToolWindowBase, IDragSource
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

        public ADOTabularConnection Connection
        {
            get { return _connection; }
            set
            {
                if (_connection == null && value == null) return;
                _connection = value;
                OnConnectionChanged();//isSameServer);
            }
        }

        protected virtual void OnConnectionChanged()
        { }

        public void MouseDoubleClick(IADOTabularObject item)
        {
            if (item != null)
            {
                var txt = item.DaxName;
                EventAggregator.PublishOnUIThread(new SendTextToEditor(txt));
            }
        }

        //RRomano: Needed to set the TreeViewColumn as Public, if I do $dataContext.Column always sends NULL to DefineMeasure (Caliburn issue?)

        public void DefineMeasure(TreeViewColumn item)
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


        public void DragCancelled()
        { }

        public void Dropped(IDropInfo dropInfo)
        { }

        public bool CanStartDrag(IDragInfo dragInfo)
        {
            return true;
        }

        public bool TryCatchOccurredException(Exception exception)
        {
            Log.Error(exception, "An uncaught exception occurred during the drag-drop operation");
            return false; // indicates that the exception has not been handled here
        }
    }

}
