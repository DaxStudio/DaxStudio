using System.Data;
using System.Diagnostics.Contracts;

namespace ADOTabular
{
    public class ADOTabularFunction: IADOTabularObject
    {
        private readonly ADOTabularParameterCollection _paramColl;
        public ADOTabularFunction(DataRow dr)
        {
            Contract.Requires(dr != null, "The dr parameter must not be null");

            Caption = dr["FUNCTION_NAME"].ToString();
            Description = dr["DESCRIPTION"].ToString();
            Group = dr["INTERFACE_NAME"].ToString();
            _paramColl = new ADOTabularParameterCollection(dr.GetChildRows("rowsetTablePARAMETERINFO"));
            
        }

        public ADOTabularFunction(string caption, string description, string groupName, ADOTabularParameterCollection param)
        {
            Caption = caption;
            Description = description;
            Group = groupName;
            _paramColl = param;
        }

        public string Caption { get; }

        // functions are not translated so there is no difference between the Name and Caption
        public string Name => Caption;

        public string Description { get; }

        public string Group { get; }

        public ADOTabularParameterCollection Parameters => _paramColl;

        public ADOTabularObjectType ObjectType => ADOTabularObjectType.Function;
        public string DaxName => $"{Caption}({Parameters})";
        public MetadataImages MetadataImage => MetadataImages.Function;
        public bool IsVisible => true;
    }
}
