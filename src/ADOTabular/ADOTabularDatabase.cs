using System;

namespace ADOTabular
{
    public class ADOTabularDatabase
    {
        private readonly ADOTabularConnection _adoTabConn;
        private string _databaseName;
        private readonly string _databaseId;
        private ADOTabularModelCollection _modelColl;
        private DateTime? _lastUpdate = null;

        public ADOTabularDatabase(ADOTabularConnection adoTabConn, string databaseName, string databaseId, DateTime lastUpdate)
        {
            _adoTabConn = adoTabConn;
            _databaseName = databaseName;
            _databaseId = databaseId;
            _lastUpdate = lastUpdate;
        }

        public bool HasSchemaChanged()
        {
            var ddColl = _adoTabConn.Databases.GetDatabaseDictionary(_adoTabConn.SPID, true);
            var dd = ddColl[_databaseName];
            if (dd.LastUpdate > _lastUpdate)
            {
                _lastUpdate = dd.LastUpdate;
                return true;
            }
            return false;
        }

        public string Id
        {
            get { return _databaseId; }
        }
        
        public string Name
        {
            get { return _databaseName; }
            //get { return _adoTabConn.PowerBIFileName == string.Empty? _databaseName: _adoTabConn.PowerBIFileName; }
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
                ", _adoTabConn.Database.Id));
        }
        public MetadataImages MetadataImage
        {
            get { return MetadataImages.Database; }
        }
    }
}
