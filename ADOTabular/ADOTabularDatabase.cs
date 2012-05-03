using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularDatabase
    {
        private ADOTabularConnection _adoTabConn;
        private string _databaseName;
        private DataRow _drSchemaRowset;
        private ADOTabularModelCollection modelColl;
        public ADOTabularDatabase(ADOTabularConnection adoTabConn, string databaseName, DataRow drSchemaRowset)
        {
            _adoTabConn = adoTabConn;
            _databaseName = databaseName;
            _drSchemaRowset = drSchemaRowset;
        }

        public ADOTabularDatabase(ADOTabularConnection adoTabConn, string databaseName)
        {
            _adoTabConn = adoTabConn;
            _databaseName = databaseName;
            _drSchemaRowset = null;
        }

        public string Name
        {
            get { return _databaseName; }
        }

        public ADOTabularModelCollection Models
        {
            get { 
                if (modelColl == null)
                {
                    modelColl = new ADOTabularModelCollection(_adoTabConn, this);
                }
                return modelColl; 
            }
        }

        public ADOTabularConnection Connection
        {
            get { return _adoTabConn; }
        }
    }
}
