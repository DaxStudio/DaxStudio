using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Microsoft.AnalysisServices.AdomdClient;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularTableCollection:IEnumerable<ADOTabularTable>,IEnumerable
    {
        
            private ADOTabularConnection _adoTabConn;
        private ADOTabularModel  _model;
        private DataTable dtTables;
        public ADOTabularTableCollection(ADOTabularConnection adoTabConn, ADOTabularModel model)
        {
            _adoTabConn = adoTabConn;
            _model = model;
        }

        public ADOTabularModel Model
        {
            get { return _model; }
        }


        private DataTable GetTablesTable()
        {
            if (dtTables == null)
            {
            AdomdRestrictionCollection resColl = new AdomdRestrictionCollection();
            //resColl.Add("TABLE_TYPE","SYSTEM TABLE");
            //resColl.Add("TABLE_SCHEMA",Model.Name);
            //dtTables = _adoTabConn.GetSchemaDataSet("DBSCHEMA_TABLES", resColl).Tables[0];
            
            resColl.Add("CUBE_NAME", Model.Name);
            dtTables = _adoTabConn.GetSchemaDataSet("MDSCHEMA_MEASUREGROUPS", resColl).Tables[0];
            }
            return dtTables;
        }

        
        public IEnumerator<ADOTabularTable> GetEnumerator()
        {
            foreach (DataRow dr in GetTablesTable().Rows)
            {
                yield return new ADOTabularTable(_adoTabConn, dr["MEASUREGROUP_NAME"].ToString(),_model);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

