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
            _column = column;
            ComponentType = type;
        }
        private ADOTabularColumn _column;
        public KpiComponentType ComponentType { get; set; }

        public string Caption { get { return ComponentType.ToString(); } }
        public string DaxName { get { return _column.DaxName; } }

        public string DataTypeName { get { return _column.DataTypeName; } }

    }
}
