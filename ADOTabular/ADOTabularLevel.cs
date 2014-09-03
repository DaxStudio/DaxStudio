using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    
    public class ADOTabularLevel
    {
        

        public ADOTabularLevel( ADOTabularColumn column)
        {
            Column = column;
        }

        public ADOTabularColumn Column {get; private set;}

        public string LevelName { get; set; }
        public string LevelCaption { get; set; }

    }
}
