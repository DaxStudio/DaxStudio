using System;
using System.Collections.Generic;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{
    public enum KpiComponentType
    {
        Value,
        Goal,
        Status
    }
        
    public class ADOTabularKpiComponent : IADOTabularObject, IADOTabularColumn
    {
        public ADOTabularKpiComponent(ADOTabularColumn column, KpiComponentType type)
        {
            Column = column;
            ComponentType = type;
        }
        // Need to be Public to grab the KPI measure expressions
        public ADOTabularColumn Column { get; set; }
        public KpiComponentType ComponentType { get; set; }

        public string Caption => ComponentType.ToString(); 
        public string Name => Column.Name;
        public string DaxName { get { return Column.DaxName;} }
        public string DataTypeName { get { return Column.DataTypeName; } }
        public string Description => Column.Description;
        public ADOTabularObjectType ObjectType {
            get {
                return ComponentType switch
                {
                    KpiComponentType.Goal => ADOTabularObjectType.KPIGoal,
                    KpiComponentType.Status => ADOTabularObjectType.KPIStatus,
                    KpiComponentType.Value => ADOTabularObjectType.KPI,
                    _ => ADOTabularObjectType.Unknown,
                };
            }
        }
        public bool IsVisible => true;
        public string MinValue => Column.MinValue;
        public string MaxValue => Column.MaxValue;
        public long DistinctValues => Column.DistinctValues;
        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            // Do Nothing
        }

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            // Do Nothing
            return null;
        }

        public DataType DataType => Column.DataType;
        public MetadataImages MetadataImage => MetadataImages.Measure;
        public string MeasureExpression => Column.MeasureExpression;
        public string TableName => Column.TableName;

        public Type SystemType => Column.SystemType;
    }
}
