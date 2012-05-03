using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADOTabular
{
    public class ADOTabularTable
    {
        private ADOTabularConnection _adoTabConn;
        private string _tableName;
        private ADOTabularModel _model;
        private ADOTabularColumnCollection columnColl;
        public ADOTabularTable(ADOTabularConnection adoTabConn, string tableName, ADOTabularModel model)
        {
            _tableName = tableName;
            _adoTabConn = adoTabConn;
            _model = model;
        }

        public string Name
        {
            get { return _tableName; }
        }

        public string Identifier
        {
            get { return string.Format("'{0}'",_tableName); }
        }

        public ADOTabularColumnCollection Columns
        {
            get {
                if (columnColl == null)
                {
                    columnColl = new ADOTabularColumnCollection(_adoTabConn, this);
                }
                return columnColl; }
        }

        public ADOTabularModel Model
        {
            get { return _model;}
        }
    }
}
