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

        public string Caption => _caption;

        // functions are not translated so there is no difference between the Name and Caption
        public string Name => _caption; 

        public string Description =>  _desc;

        public string Group => _group; 

        public ADOTabularParameterCollection Parameters => _paramColl;

        public ADOTabularObjectType ObjectType => ADOTabularObjectType.Function;
        public string DaxName => string.Format("{0}({1})", Caption, Parameters);
        public MetadataImages MetadataImage => MetadataImages.Function;
        public bool IsVisible => true;
    }
}
