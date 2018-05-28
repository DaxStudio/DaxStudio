using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using GongSolutions.Wpf.DragDrop;
using Serilog;
using System.Linq;
using System.Text.RegularExpressions;

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

        private string ExpandDependentMeasure(ADOTabularColumn column)
        {
            return ExpandDependentMeasure(column, false);
        }
        private string ExpandDependentMeasure(ADOTabularColumn column, bool ignoreNonUniqueMeasureNames) {
            string measureName = column.Name;
            var model = Connection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 select m).ToList();
            var distinctColumns = (from t in model.Tables
                               from c in t.Columns
                               where c.ColumnType == ADOTabularColumnType.Column
                               select c.Name).Distinct().ToList();

            var finalMeasure = modelMeasures.First(m => m.Name == measureName);

            var resultExpression = finalMeasure.Expression;
            
            bool foundDependentMeasures;
            
            do {
                foundDependentMeasures = false;
                foreach (var modelMeasure in modelMeasures) {
                    //string daxMeasureName = "[" + modelMeasure.Name + "]";
                    //string newExpression = resultExpression.Replace(daxMeasureName, " CALCULATE ( " + modelMeasure.Expression + " )");
                    Regex daxMeasureRegex = new Regex(@"[^\w']?\[" + modelMeasure.Name + "]");
                    
                    string newExpression = daxMeasureRegex.Replace(resultExpression, " CALCULATE ( " + modelMeasure.Expression + " )");
        
                    if (newExpression != resultExpression) {
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

        

        private List<ADOTabularMeasure> FindDependentMeasures( string measureName ) {
            var model = Connection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 select m).ToList();

            var dependentMeasures = new List<ADOTabularMeasure>();
            dependentMeasures.Add(modelMeasures.First(m => m.Name == measureName ));

            bool foundDependentMeasures; 
            do {
                foundDependentMeasures = false;
                foreach ( var modelMeasure in modelMeasures ) {
                    string daxMeasureName = "[" + modelMeasure.Name + "]";
                    // Iterates a copy so the original list can be modified
                    foreach ( var scanMeasure in dependentMeasures.ToList() ) {
                        if (modelMeasure == scanMeasure) continue;
                        string dax = scanMeasure.Expression;
                        if (dax.Contains( daxMeasureName )) {
                            if (!dependentMeasures.Contains(modelMeasure)) {
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
        public void DefineDependentMeasures (TreeViewColumn item) {
            try {
                if (item == null) {
                    return;
                }

                ADOTabularColumn column;

                if (item.Column is ADOTabularKpiComponent) {
                    var kpiComponent = (ADOTabularKpiComponent)item.Column;
                    column = (ADOTabularColumn)kpiComponent.Column;
                }
                else {
                    column = (ADOTabularColumn)item.Column;
                }

                var dependentMeasures = FindDependentMeasures(column.Name);
                foreach ( var measure in dependentMeasures ) {
                    EventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measure.DaxName, measure.Expression));
                }
            }
            catch (System.Exception ex) {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasureTree", ex.Message, ex.StackTrace);
            }
        }

        public void DefineExpandMeasure(TreeViewColumn item) {
            DefineMeasure(item, true);
        }


        //RRomano: Needed to set the TreeViewColumn as Public, if I do $dataContext.Column always sends NULL to DefineMeasure (Caliburn issue?)
        public void DefineMeasure(TreeViewColumn item) {
            DefineMeasure(item, false);
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

                if (string.IsNullOrEmpty(measureName)) {
                    measureName = string.Format("{0}[{1}]", column.Table.DaxName, column.Name);
                }

                if (expandMeasure) {
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

                if (string.IsNullOrEmpty(measureExpression)) {
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
