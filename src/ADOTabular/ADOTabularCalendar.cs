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
            this.table = table;
            IsVisible = isVisible;

        }

        public void AddTimeUnit(ADOTabularTimeUnit timeUnit)
        {
            TimeUnits.Add(timeUnit);
        }

        public List<ADOTabularTimeUnit> TimeUnits { get; }

        public List<ADOTabularColumn> TimeRelatedColumns { get; }
        public string DaxName
        {
            get
            {
                return "";
            }
        }

        private ADOTabularTable table;

        public string Caption { get; }

        public string Name { get; }
        
        public string Description { get; }
        
        public bool IsVisible { get; }

        public ADOTabularObjectType ObjectType => ADOTabularObjectType.Calendar;
    }
}
