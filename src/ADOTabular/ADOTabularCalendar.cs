using ADOTabular.Interfaces;
using System;
using System.Collections.Generic;

namespace ADOTabular
{
    
    public class ADOTabularCalendar: IADOTabularObject
    {
        public ADOTabularCalendar(
            ADOTabularTable table,
            string name,
            bool isVisible)
        {
            TimeUnits = new List<ADOTabularTimeUnit>();
            TimeRelatedColumns = new List<ADOTabularColumn>();
            Name = name;
            table = table;
            isVisible = isVisible;

        }

        public List<ADOTabularTimeUnit> TimeUnits { get; }

        public List<ADOTabularColumn> TimeRelatedColumns { get; }
        public override string DaxName
        {
            get
            {
                return "";
            }
        }

        private ADOTabularTable table;
    }
}
