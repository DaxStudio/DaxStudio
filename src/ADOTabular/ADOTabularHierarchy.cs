using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    
    public class ADOTabularHierarchy:ADOTabularColumn
    {
        private List<ADOTabularLevel> _levels;
        public ADOTabularHierarchy( ADOTabularTable table, string internalName,string name, string caption,  string description,
                                bool isVisible, ADOTabularColumnType columnType, string contents)
        :base(table,internalName,name, caption,description,isVisible,columnType,contents)
        {
            _levels = new List<ADOTabularLevel>();
        }
        public List<ADOTabularLevel> Levels { get { return _levels; } }

        public override string DaxName
        {
            get
            {
                return "";
            }
        }

    }
}
