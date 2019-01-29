using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Utils
{
    public class MeasureHelpers
    {
        #region Measure Definition functions

        private string ExpandDependentMeasure(ADOTabularColumn column, IEventAggregator eventAggregator)
        {
            return ExpandDependentMeasure( column, eventAggregator, false);
        }


        private string ExpandDependentMeasure(ADOTabularColumn column, IEventAggregator eventAggregator, bool ignoreNonUniqueMeasureNames)
        {
            string measureName = column.Name;
            //var model = Connection.Database.Models.BaseModel;
            var model = column.Table.Model;
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
                                eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, msg));
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
        public void DefineDependentMeasures(TreeViewColumn item, IEventAggregator eventAggregator)
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
                    eventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measure.DaxName, measure.Expression));
                }
            }
            catch (System.Exception ex)
            {
                Log.Error("{class} {method} {message} {stacktrace}", "ToolPaneBaseViewModel", "DefineMeasureTree", ex.Message, ex.StackTrace);
            }
        }

        public void DefineAllMeasures(TreeViewTable item, string filterTable, IEventAggregator eventAggregator)
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
                    eventAggregator.PublishOnUIThread(new DefineMeasureOnEditor(measure.DaxName, measure.Expression));
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
        public void DefineAllMeasuresAllTables(TreeViewTable item, IEventAggregator eventAggregator)
        {
            DefineAllMeasures(item, null, eventAggregator);
        }
        public void DefineAllMeasuresOneTable(TreeViewTable item, IEventAggregator eventAggregator)
        {
            DefineAllMeasures(item, item.Caption, eventAggregator);
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
                                       where c.ColumnType == ADOTabularColumnType.Column
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

        #endregion
    }
}
