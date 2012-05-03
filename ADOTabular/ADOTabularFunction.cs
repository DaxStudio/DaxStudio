using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularFunction
    {
        private string name;
        private string desc;
        private string group;
        private ADOTabularParameterCollection paramColl;
        public ADOTabularFunction(DataRow dr)
        {
            name = dr["FUNCTION_NAME"].ToString();
            desc = dr["DESCRIPTION"].ToString();
            group = dr["INTERFACE_NAME"].ToString();
            paramColl = new ADOTabularParameterCollection(dr.GetChildRows("rowsetTablePARAMETERINFO"));
            
        }

        public string Name
        {
            get { return name; }
        }

        public string Description
        {
            get { return desc; }
        }

        public string Group
        {
            get { return group; }
        }

        public ADOTabularParameterCollection Parameters
        {
            get { return paramColl; }
        }

        public string Signature
        {
            get { return string.Format("{0}({1})", Name, Parameters.ToString());  }
        }
    }
}
