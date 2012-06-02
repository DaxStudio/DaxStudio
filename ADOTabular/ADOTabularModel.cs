using System.Data;

namespace ADOTabular
{
    public class ADOTabularModel
    {
        private readonly ADOTabularConnection _adoTabConn;
        private ADOTabularTableCollection _tableColl;
        public ADOTabularModel(ADOTabularConnection adoTabConn, DataRow dr)
        {
            _adoTabConn = adoTabConn;
            Name = dr["CUBE_NAME"].ToString();
            Description = dr["DESCRIPTION"].ToString();
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public ADOTabularTableCollection Tables
        {
            get { return _tableColl ?? (_tableColl = new ADOTabularTableCollection(_adoTabConn, this)); }
        }
    }
}
