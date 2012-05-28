namespace ADOTabular
{
    public class ADOTabularTable
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly string _tableName;
        private readonly ADOTabularModel _model;
        private ADOTabularColumnCollection _columnColl;
        public ADOTabularTable(ADOTabularConnection adoTabConn, string tableName, ADOTabularModel model)
        {
            _tableName = tableName;
            _adoTabConn = adoTabConn;
            _model = model;
        }

        public string Name
        {
            get { return string.Format("'{0}'", _tableName); }
        }

        public string Caption
        {
            get { return _tableName; }
        }

        public ADOTabularColumnCollection Columns
        {
            get { return _columnColl ?? (_columnColl = new ADOTabularColumnCollection(_adoTabConn, this)); }
        }

        public ADOTabularModel Model
        {
            get { return _model;}
        }
    }
}
