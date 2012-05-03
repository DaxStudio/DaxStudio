using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularParameter
    {
        private DataRow _dr;
        public ADOTabularParameter(DataRow dr)
        {
            _dr = dr;
        }

        public string Name
        {
            get {return _dr["NAME"].ToString();}
        }
        public string Description
        {
            get {return _dr["DESCRIPTION"].ToString();}
        }
        public bool Optional
        {
            get {return bool.Parse(_dr["OPTIONAL"].ToString());}
        }
        public bool Repeatable
        {
            get {return bool.Parse(_dr["REAPEATABLE"].ToString());}
        }
        public int RepeatGroup
        {
            get {return int.Parse(_dr["REPEATGROUP"].ToString());}
        }
    }
}
