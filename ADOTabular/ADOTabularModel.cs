namespace ADOTabular
{
    public class ADOTabularModel
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly string _modelName;
        private ADOTabularTableCollection _tableColl;
        public ADOTabularModel(ADOTabularConnection adoTabConn, string modelName)
        {
            _adoTabConn = adoTabConn;
            _modelName = modelName;
        }

        public string Name
        {
            get { return _modelName; }
        }

        public ADOTabularTableCollection Tables
        {
            get { return _tableColl ?? (_tableColl = new ADOTabularTableCollection(_adoTabConn, this)); }
        }
    }
}
