using System.Collections.Generic;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularDynamicManagementViewCollection : IEnumerable<ADOTabularDynamicManagementView>
    {
        private DataSet _dsDmvs;
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularDynamicManagementViewCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;

        }

        private DataTable GetDmvTable()
        {
            if (_dsDmvs == null)
            {
                _dsDmvs = _adoTabConn.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS");
            }
            return _dsDmvs.Tables[0];
        }

        public IEnumerator<ADOTabularDynamicManagementView> GetEnumerator()
        {
            foreach (DataRow dr in GetDmvTable().Rows)
            {
                //yield return new ADOTabularDatabase(_adoTabConn, dr["CATALOG_NAME"].ToString());//, dr);
                yield return new ADOTabularDynamicManagementView(dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
