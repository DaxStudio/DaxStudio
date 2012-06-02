using System.Data;

namespace ADOTabular
{
    public class ADOTabularTable
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly ADOTabularModel _model;
        private ADOTabularColumnCollection _columnColl;

        public ADOTabularTable(ADOTabularConnection adoTabConn, DataRow dr, ADOTabularModel model)
        {
            Caption = dr["DIMENSION_NAME"].ToString();
            IsVisible = bool.Parse(dr["DIMENSION_IS_VISIBLE"].ToString());
            Description = dr["DESCRIPTION"].ToString();
            _adoTabConn = adoTabConn;
            _model = model;
        }

        public string Name
        {
            get { return string.Format("'{0}'", Caption); }
        }

        public string Caption { get; private set; }

        public string Description { get; private set; }

        public bool IsVisible { get; private set; }

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
