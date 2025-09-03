using System.Collections.Generic;
using System.Data;
using System.Collections;
using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class ADOTabularDynamicManagementViewCollection : IEnumerable<ADOTabularDynamicManagementView>
    {
        private DataSet _dsDmvs;
        private object _dmvLock = new object();
        private readonly IADOTabularConnection _adoTabConn;
        public ADOTabularDynamicManagementViewCollection(IADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;

        }

        private DataTable GetDmvTable()
        {
            // Prevent 2 threads from trying to refresh the DMV collection
            lock (_dmvLock)
            {
                if (_dsDmvs == null)
                {
                    try
                    {
                        _dsDmvs = _adoTabConn.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS");
                        _dsDmvs.Tables[0].DefaultView.Sort = "SchemaName";
                    }
                    catch
                    {
                        // on error return an empty dataset
                        return new DataTable("Emtpy");
                    }
                }
            }
            return _dsDmvs.Tables[0].DefaultView.ToTable();
        }

        public IEnumerator<ADOTabularDynamicManagementView> GetEnumerator()
        {
            using var dmvTable = GetDmvTable();
            foreach (DataRow dr in dmvTable.Rows)
            {
                yield return new ADOTabularDynamicManagementView(dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(string dmvName)
        {
            var dtDmvs = GetDmvTable();
            return dtDmvs.Select($"[SchemaName] = '{dmvName}'").Length == 1;
        }
    }
}
