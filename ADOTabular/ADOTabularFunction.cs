using System.Data;

namespace ADOTabular
{
    public class ADOTabularFunction: IADOTabularObject
    {
        private readonly string _caption;
        private readonly string _desc;
        private readonly string _group;
        private readonly ADOTabularParameterCollection _paramColl;
        public ADOTabularFunction(DataRow dr)
        {
            _caption = dr["FUNCTION_NAME"].ToString();
            _desc = dr["DESCRIPTION"].ToString();
            _group = dr["INTERFACE_NAME"].ToString();
            _paramColl = new ADOTabularParameterCollection(dr.GetChildRows("rowsetTablePARAMETERINFO"));
            
        }

        public ADOTabularFunction(string caption, string description, string groupName, ADOTabularParameterCollection param)
        {
            _caption = caption;
            _desc = description;
            _group = groupName;
            _paramColl = param;
        }

        public string Caption
        {
            get { return _caption; }
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

        public string DaxName
        {
            get { return string.Format("{0}({1})", Caption, Parameters);  }
        }
        public MetadataImages MetadataImage
        {
            get { return MetadataImages.Function; }
        }
    }
}
