using System;

namespace ADOTabular
{
    public class ADOTabularDatabase
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly string _databaseName;
        private ADOTabularModelCollection _modelColl;

        public ADOTabularDatabase(ADOTabularConnection adoTabConn, string databaseName)
        {
            _adoTabConn = adoTabConn;
            _databaseName = databaseName;
        }
        /*
        public string Id
        {
            get {
                var resColl = new AdomdClientWrappers.AdomdRestrictionCollection();
                resColl.Add( new AdomdClientWrappers.AdomdRestriction("ObjectExpansion", "ExpandObject"))
                var ds = _adoTabConn.GetSchemaDataSet("DISCOVER_XML_METADATA",resColl); 
            
            }
        }
        */
        public string Name
        {
            get { return _databaseName; }
        }

        public ADOTabularModelCollection Models
        {
            get { return _modelColl ?? (_modelColl = new ADOTabularModelCollection(_adoTabConn, this)); }
        }

        public ADOTabularConnection Connection
        {
            get { return _adoTabConn; }
        }

        public void ClearCache()
        {
            _adoTabConn.ExecuteCommand(String.Format(@"
                <Batch xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
                   <ClearCache>
                     <Object>
                       <DatabaseID>{0}</DatabaseID>   
                    </Object>
                   </ClearCache>
                 </Batch>
                ", _adoTabConn.Database.Name));
        }
        public MetadataImages MetadataImage
        {
            get { return MetadataImages.Database; }
        }
    }
}
