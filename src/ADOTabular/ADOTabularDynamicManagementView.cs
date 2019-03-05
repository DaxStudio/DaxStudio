using System;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularDynamicManagementView:IADOTabularObject
    {
        public ADOTabularDynamicManagementView(DataRow dr)
        {
            _caption = dr["SchemaName"].ToString();
        }

        private readonly string _caption;
        public string Caption
        {
            get { return _caption; }
        }
        // DMV names are not translated so the Name and Caption are the same
        public string Name => _caption;
        public string DaxName
        {
            get { return DefaultQuery; }
        }
        public ADOTabularObjectType ObjectType => ADOTabularObjectType.DMV;
        public string DefaultQuery
        {
            get { return String.Format("select * from $SYSTEM.{0}", _caption); }
        }
        public MetadataImages MetadataImage
        {
            get { return MetadataImages.DmvTable; }
        }
        public bool IsVisible => true;
    }
}
