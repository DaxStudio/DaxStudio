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
            BaseModelName = dr["BASE_CUBE_NAME"].ToString();
        }

        public ADOTabularModel(ADOTabularConnection adoTabConn, string name, string description, string baseModelName)
        {
            _adoTabConn = adoTabConn;
            Name = name;
            Description = description;
            BaseModelName = baseModelName;
        }

        public string BaseModelName { get; private set; }

        public bool IsPerspective { get { return !string.IsNullOrEmpty(BaseModelName); } }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public ADOTabularTableCollection Tables
        {
            get { return _tableColl ?? (_tableColl = new ADOTabularTableCollection(_adoTabConn, this)); }
        }

        public MetadataImages MetadataImage
        {
            get { return IsPerspective? MetadataImages.Perspective : MetadataImages.Model; }
        }
    }
}
