using System.Data;

namespace ADOTabular
{
    public class ADOTabularDynamicManagementView
    {
        public ADOTabularDynamicManagementView(DataRow dr)
        {
            _name = dr["SchemaName"].ToString();
        }

        private readonly string _name;
        public string Name
        {
            get { return _name; }
        }

        public string DefaultQuery
        {
            get { return string.Format("select * from $SYSTEM.{0}", _name); }
        }

    }
}
