using ADOTabular.Interfaces;
using ADOTabular.MetadataInfo;
using System;
using System.Collections.Generic;
using Dax.ViewModel;
using Dax.Metadata;
using System.Linq;
using Microsoft.AnalysisServices;
using System.ComponentModel;

namespace ADOTabular
{
    public class MetadataVisitorVpax : IMetaDataVisitor
    {

        private readonly Model _daxModel;
        private readonly Microsoft.AnalysisServices.Tabular.Database _tomDatabase;
        private readonly IADOTabularConnection _conn;
        private ADOTabularDatabase db;

        public MetadataVisitorVpax(IADOTabularConnection conn, Dax.Metadata.Model daxModel, Microsoft.AnalysisServices.Tabular.Database tomDatabase )
        {

            _daxModel = daxModel;
            _tomDatabase = tomDatabase;
            _conn = conn;
        }

        public ADOTabularDatabase Visit(ADOTabularConnection conn)
        {
            var model = _daxModel;
            db = new ADOTabularDatabase(_conn, model.ModelName?.Name??"<Unknown>", model.ModelName?.Name??"<Unknown>", model.LastUpdate, model.CompatibilityLevel.ToString(), string.Empty, string.Empty);
            db.LastUpdate = model.LastDataRefresh;
            return db;
        }

        public SortedDictionary<string, ADOTabularModel> Visit(ADOTabularModelCollection models)
        {
            var ret = new SortedDictionary<string, ADOTabularModel>();
            var model = _daxModel;
            var modelName = model.ModelName?.Name ?? "<Unknown>";
            ret.Add(modelName,
                new ADOTabularModel(_conn
                ,db
                , modelName
                , modelName
                , string.Empty
                , String.Empty
                ));
            return ret;

        }

        public void Visit(ADOTabularTableCollection tables)
        {
            var model = tables.Model;
            foreach (var table in _daxModel.Tables)
            {
                var t = new ADOTabularTable(_conn, model, table.TableName.Name, table.TableName.Name, table.TableName.Name, table.Description.Note, !table.IsHidden, table.IsPrivate, false);
                tables.Add(t);


                foreach (var col in table.Columns)
                {
                    //TODO - do we need code to identify KPI columns
                    var c = new ADOTabularColumn(t, col.ColumnName.Name, col.ColumnName.Name, col.ColumnName.Name, col.Description.Note, !col.IsHidden, ADOTabularObjectType.Column, string.Empty);
                    c.DistinctValues = col.ColumnCardinality;
                    c.DataType = (Microsoft.AnalysisServices.Tabular.DataType)Enum.Parse(typeof(Microsoft.AnalysisServices.Tabular.DataType), col.DataType);
                    t.Columns.Add(c);

                }
                var expressionDict = t.Model.MeasureExpressions;
                var formatStringExpressionsDict = t.Model.MeasureFormatStringExpressions;
                foreach (var m in table.Measures)
                {
                    //TODO - do we need code to identify KPI columns
                    var measureType = ADOTabularObjectType.Measure;
                    if (m.KpiTargetExpression?.Expression != null
                     || m.KpiTrendExpression?.Expression != null
                     || m.KpiStatusExpression?.Expression != null)
                    {
                        var kpiDetails = new KpiDetails()
                        {
                            Goal = $"_{m.MeasureName.Name} Goal",
                            Status = $"_{m.MeasureName.Name} Status",
                            Graphic = ""
                        };
                        var kpi = new ADOTabularKpi(t, m.MeasureName.Name, m.MeasureName.Name, m.MeasureName.Name,m.Description.Note,!m.IsHidden, ADOTabularObjectType.KPI,null, kpiDetails);
                        kpi.DataType = (Microsoft.AnalysisServices.Tabular.DataType)Enum.Parse(typeof(Microsoft.AnalysisServices.Tabular.DataType), m.DataType);
                        ProcessDisplayFolders(t, m, kpi);
                        expressionDict.Add(m.MeasureName.Name, m.MeasureExpression.Expression);
                        expressionDict.Add(kpiDetails.Goal, m.KpiTargetExpression.Expression);
                        expressionDict.Add(kpiDetails.Status, m.KpiStatusExpression.Expression);

                        t.Columns.Add(kpi);

                        var goal = new ADOTabularColumn(t, kpiDetails.Goal, kpiDetails.Goal, kpiDetails.Goal, String.Empty, true, ADOTabularObjectType.KPIGoal, String.Empty);
                        goal.DataType = (Microsoft.AnalysisServices.Tabular.DataType)Enum.Parse(typeof(Microsoft.AnalysisServices.Tabular.DataType), m.DataType);
                        t.Columns.Add(goal);

                        var status = new ADOTabularColumn(t, kpiDetails.Status, kpiDetails.Status, kpiDetails.Status, String.Empty, true, ADOTabularObjectType.KPIStatus, String.Empty);
                        status.DataType = (Microsoft.AnalysisServices.Tabular.DataType)Enum.Parse(typeof(Microsoft.AnalysisServices.Tabular.DataType), m.DataType);
                        t.Columns.Add(status);
                    }
                    else { 
                        var measure = new ADOTabularColumn(t, m.MeasureName.Name, m.MeasureName.Name, m.MeasureName.Name, m.Description.Note, !m.IsHidden, measureType, String.Empty);
                        measure.DataType = (Microsoft.AnalysisServices.Tabular.DataType)Enum.Parse(typeof(Microsoft.AnalysisServices.Tabular.DataType), m.DataType);
                        // TODO - add display folder handling
                        ProcessDisplayFolders(t, m, measure);
                        expressionDict.Add(m.MeasureName.Name, m.MeasureExpression?.Expression??string.Empty);
                        if (!string.IsNullOrEmpty(m.FormatStringExpression?.Expression))
                        {
                            formatStringExpressionsDict.Add(m.MeasureName.Name, m.FormatStringExpression.Expression);
                        }
                        t.Columns.Add(measure);
                    }

                    
                }
            }

            // Populate relationships from the VPAX model
            foreach (var rel in _daxModel.Relationships)
            {
                if (rel.FromColumn?.Table?.TableName?.Name == null || rel.ToColumn?.Table?.TableName?.Name == null)
                    continue;

                var fromTableName = rel.FromColumn.Table.TableName.Name;
                var toTableName = rel.ToColumn.Table.TableName.Name;

                ADOTabularTable fromTable = null;
                ADOTabularTable toTable = null;
                foreach (var t in tables)
                {
                    if (t.Name == fromTableName) fromTable = t;
                    if (t.Name == toTableName) toTable = t;
                }

                if (fromTable == null || toTable == null) continue;

                var fromMultiplicity = MapCardinalityType(rel.FromCardinalityType);
                var toMultiplicity = MapCardinalityType(rel.ToCardinalityType);

                var adoRel = new ADOTabularRelationship
                {
                    InternalName = rel.Name ?? string.Empty,
                    FromTable = fromTable,
                    ToTable = toTable,
                    FromColumn = rel.FromColumn.ColumnName?.Name,
                    ToColumn = rel.ToColumn.ColumnName?.Name,
                    FromColumnMultiplicity = fromMultiplicity,
                    ToColumnMultiplicity = toMultiplicity,
                    CrossFilterDirection = rel.CrossFilteringBehavior ?? string.Empty,
                    IsActive = rel.IsActive
                };
                fromTable.Relationships.Add(adoRel);

                // Also populate the TOM model relationships for model diagram support
                if (!string.IsNullOrWhiteSpace(adoRel.FromColumn) && !string.IsNullOrWhiteSpace(adoRel.ToColumn))
                {
                    try
                    {
                        var tomFromTable = model.TOMModel.Tables[fromTable.Name];
                        var tomToTable = model.TOMModel.Tables[toTable.Name];
                        var tomRel = new Microsoft.AnalysisServices.Tabular.SingleColumnRelationship
                        {
                            FromColumn = tomFromTable.Columns.First(c => c.Name == adoRel.FromColumn),
                            ToColumn = tomToTable.Columns.First(c => c.Name == adoRel.ToColumn),
                            FromCardinality = GetCardinality(fromMultiplicity),
                            ToCardinality = GetCardinality(toMultiplicity),
                            CrossFilteringBehavior = GetCrossFilteringBehavior(adoRel.CrossFilterDirection),
                            IsActive = adoRel.IsActive
                        };
                        model.TOMModel.Relationships.Add(tomRel);
                    }
                    catch (Exception)
                    {
                        // Skip TOM relationship if table/column lookup fails
                    }
                }
            }
        }

        public SortedDictionary<string, ADOTabularColumn> Visit(ADOTabularColumnCollection columns)
        {
            var ret = new SortedDictionary<string, ADOTabularColumn>();
            return ret;
        }

        private static void ProcessDisplayFolders(ADOTabularTable table, Dax.Metadata.Measure sourceMeasure, ADOTabularColumn targetMeasure)
        {
            var folderPath = sourceMeasure.DisplayFolder.Note;
            if (string.IsNullOrEmpty(folderPath)) return;
            
            targetMeasure.IsInDisplayFolder = true;

            var fi = GetDisplayFolder(table, folderPath);

            fi.FolderItems.Add(new ADOTabularObjectReference("",  targetMeasure.Name));
            
        }

        private static IADOTabularFolderReference GetDisplayFolder(IADOTabularObjectReference table, string folderPath)
        {
            List<IADOTabularObjectReference> folderItems = null; 

            if (table is ADOTabularTable tbl)
            {
                folderItems = tbl.FolderItems;
            }

            if (table is ADOTabularDisplayFolder fldr)
            {
                folderItems = fldr.FolderItems;
            }

            if (folderItems == null) return null;

            var parent = table;
            var folders = folderPath.Split('\\');
            string folderId = null;
            foreach( string f in folders)
            {
                folderId = string.Join("\\", (folderId, f));
                var foundFolder = folderItems.FirstOrDefault(fi => fi.Name == f);
                if (foundFolder == null)
                {
                    foundFolder = new ADOTabularDisplayFolder(f, folderId);
                    folderItems.Add(foundFolder);
                }

                parent = foundFolder;
            }

            return parent as IADOTabularFolderReference;
        }

        public SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures)
        {
            var ret = new SortedDictionary<string, ADOTabularMeasure>();
            var t = _daxModel.Tables.FirstOrDefault(t => t.TableName.Name == measures.Table.Name);
            foreach (var m in t.Measures)
            {
                //TODO - do we need code to identify KPI columns
                var measure = new ADOTabularMeasure(measures.Table, m.MeasureName.Name, m.MeasureName.Name, m.MeasureName.Name, m.Description.Note, !m.IsHidden, m.MeasureExpression.Expression, m.FormatStringExpression?.Expression ); 
                ret.Add(measure.Name, measure);
            }
            return ret;
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            // do nothing
        }

        public void Visit(ADOTabularKeywordCollection keywords)
        {
            // do nothing
        }

        public void Visit(DaxMetadata daxMetadata)
        {
            // TODO - read daxMetadata from vpax
        }

        public void Visit(DaxColumnsRemap daxColumnsRemap)
        {
            // TODO - read column remap from vpax
        }

        public void Visit(DaxTablesRemap daxColumnsRemap)
        {
            // TODO - read table remap from vpax
        }

        public void Visit(ADOTabularCalendarCollection calendars)
        {
            // TODO - read calendars from vpax
        }

        private static string MapCardinalityType(string cardinalityType)
        {
            switch (cardinalityType)
            {
                case "Many": return "*";
                case "One": return "1";
                case "None": return "0..1";
                default: return cardinalityType ?? string.Empty;
            }
        }

        private static Microsoft.AnalysisServices.Tabular.RelationshipEndCardinality GetCardinality(string multiplicity)
        {
            return multiplicity switch
            {
                "*" => Microsoft.AnalysisServices.Tabular.RelationshipEndCardinality.Many,
                "0..1" => Microsoft.AnalysisServices.Tabular.RelationshipEndCardinality.One,
                "1" => Microsoft.AnalysisServices.Tabular.RelationshipEndCardinality.One,
                _ => Microsoft.AnalysisServices.Tabular.RelationshipEndCardinality.None,
            };
        }

        private static Microsoft.AnalysisServices.Tabular.CrossFilteringBehavior GetCrossFilteringBehavior(string crossFilterDirection)
        {
            return crossFilterDirection switch
            {
                "Both" => Microsoft.AnalysisServices.Tabular.CrossFilteringBehavior.BothDirections,
                "BothDirections" => Microsoft.AnalysisServices.Tabular.CrossFilteringBehavior.BothDirections,
                _ => Microsoft.AnalysisServices.Tabular.CrossFilteringBehavior.OneDirection,
            };
        }
    }
}
