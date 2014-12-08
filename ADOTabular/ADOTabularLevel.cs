using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    
    public class ADOTabularLevel: IADOTabularObject
    {
        

        public ADOTabularLevel( ADOTabularColumn column)
        {
            Column = column;
        }

        public ADOTabularColumn Column {get; private set;}

        public string LevelName { get; set; }
        private string _caption;
        public string Caption { 
            get {return string.IsNullOrEmpty(_caption) ? LevelName : _caption;} 
            set { _caption = value; } 
        }

        public string DaxName
        {
            get { return Column.DaxName; }
        }
    }
}
