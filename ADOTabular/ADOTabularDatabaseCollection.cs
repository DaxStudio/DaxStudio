using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularDatabaseCollection:IEnumerable<ADOTabularDatabase>,IEnumerable
    {
        private DataSet dsDatabases;
        private ADOTabularConnection _adoTabConn;
        public ADOTabularDatabaseCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
            
        }

        public ADOTabularDatabase FindByName(string databaseName)
        {
            try
            {
                DataRow dr = GetDatabaseTable().Rows.Find(databaseName);
                return new ADOTabularDatabase(_adoTabConn, databaseName, dr); 
            }
            catch (Exception ex)
            {
                throw new System.Exception(string.Format("The {0} database was not found in the database collection", databaseName));
            }
        }


        private DataTable GetDatabaseTable()
        {
            if (dsDatabases == null)
            {
                dsDatabases = _adoTabConn.GetSchemaDataSet("DBSCHEMA_CATALOGS");
                dsDatabases.Tables[0].PrimaryKey[0] = dsDatabases.Tables[0].Columns["CATALOG_NAME"];
            }
            return dsDatabases.Tables[0];
        }

        public IEnumerator<ADOTabularDatabase> GetEnumerator()
        {
            foreach (DataRow dr in GetDatabaseTable().Rows)
            {
                yield return new ADOTabularDatabase(_adoTabConn, dr["CATALOG_NAME"].ToString(), dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
