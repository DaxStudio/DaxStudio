using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public enum KpiComponentType
    {
        Value,
        Goal,
        Status
    }
        
    public class ADOTabularKpiComponent : IADOTabularObject
    {
        public ADOTabularKpiComponent(ADOTabularColumn column, KpiComponentType type)
        {
            Column = column;
            ComponentType = type;
        }
        // Need to be Public to grab the KPI measure expressions
        public ADOTabularColumn Column;
        public KpiComponentType ComponentType { get; set; }

        public string Caption => ComponentType.ToString(); 
        public string Name => Column.Name;
        public string DaxName { get { return Column.DaxName;} }
        public string DataTypeName { get { return Column.DataTypeName; } }
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
    }
}
