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

        public string DaxName
        {
            get { return DefaultQuery; }
        }

        public string DefaultQuery
        {
            get { return String.Format("select * from $SYSTEM.{0}", _caption); }
        }

        public MetadataImages MetadataImage
        {
            get { return MetadataImages.DmvTable; }
        }
    }
}
