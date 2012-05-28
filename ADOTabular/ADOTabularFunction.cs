using System.Data;

namespace ADOTabular
{
    public class ADOTabularFunction
    {
        private readonly string _name;
        private readonly string _desc;
        private readonly string _group;
        private readonly ADOTabularParameterCollection _paramColl;
        public ADOTabularFunction(DataRow dr)
        {
            _name = dr["FUNCTION_NAME"].ToString();
            _desc = dr["DESCRIPTION"].ToString();
            _group = dr["INTERFACE_NAME"].ToString();
            _paramColl = new ADOTabularParameterCollection(dr.GetChildRows("rowsetTablePARAMETERINFO"));
            
        }

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _desc; }
        }

        public string Group
        {
            get { return _group; }
        }

        public ADOTabularParameterCollection Parameters
        {
            get { return _paramColl; }
        }

        public string Signature
        {
            get { return string.Format("{0}({1})", Name, Parameters);  }
        }
    }
}
