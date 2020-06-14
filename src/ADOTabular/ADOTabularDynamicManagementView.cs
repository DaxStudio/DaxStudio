using System;
using System.Data;
using System.Diagnostics.Contracts;

namespace ADOTabular
{
    public class ADOTabularDynamicManagementView:IADOTabularObject
    {
        public ADOTabularDynamicManagementView(DataRow dr)
        {
            Contract.Requires(dr != null, "The dr parameter must not be null");

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
        public string DefaultQuery => $"select * from $SYSTEM.{Caption}";
        public MetadataImages MetadataImage => MetadataImages.DmvTable;
        public bool IsVisible => true;

        public string Description => string.Empty;
    }
}
