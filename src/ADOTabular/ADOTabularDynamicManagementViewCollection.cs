using System.Collections.Generic;
using System.Data;
using System.Collections;
using System;

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
                try
                {
                    // TODO - on error should we return an empty dataset?
                    _dsDmvs = _adoTabConn.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS");
                }
                catch 
                {
                    return new DataTable("Emtpy");
                }
            }
            _dsDmvs.Tables[0].DefaultView.Sort = "SchemaName";
            return _dsDmvs.Tables[0].DefaultView.ToTable();
        }

        public IEnumerator<ADOTabularDynamicManagementView> GetEnumerator()
        {
            using (var dmvTable = GetDmvTable())
            {
                foreach (DataRow dr in dmvTable.Rows)
                {
                    yield return new ADOTabularDynamicManagementView(dr);
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
