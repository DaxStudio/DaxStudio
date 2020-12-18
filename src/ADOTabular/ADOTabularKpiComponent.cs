using System;
using System.Collections.Generic;
using ADOTabular.Interfaces;

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
                switch (ComponentType)
                {
                    case KpiComponentType.Goal:
                        return ADOTabularObjectType.KPIGoal;
                    case KpiComponentType.Status:
                        return ADOTabularObjectType.KPIStatus;
                    case KpiComponentType.Value:
                        return ADOTabularObjectType.KPI;
                }
                return ADOTabularObjectType.Unknown;
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

        public Type DataType => Column.DataType;
        public MetadataImages MetadataImage => MetadataImages.Measure;
        public string MeasureExpression => Column.MeasureExpression;
        public string TableName => Column.TableName;
    }
}
