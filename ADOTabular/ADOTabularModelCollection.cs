using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularModelCollection:IEnumerable<ADOTabularModel>,IEnumerable
    {
        private ADOTabularConnection _adoTabConn;
        private ADOTabularDatabase  _database;
        private DataTable dtModels;
        public ADOTabularModelCollection(ADOTabularConnection adoTabConn, ADOTabularDatabase database)
        {
            _adoTabConn = adoTabConn;
            _database = database;
        }

        public ADOTabularDatabase Database
        {
            get { return _database; }
        }


        private DataTable GetModelsTable()
        {
            if (dtModels == null)
            {
            AdomdRestrictionCollection resColl = new AdomdRestrictionCollection();
            resColl.Add("CUBE_SOURCE",1);
            dtModels = _adoTabConn.GetSchemaDataSet("MDSCHEMA_CUBES", resColl).Tables[0];
            }
            return dtModels;
        }

/*
        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (DataRow dr in GetModelsTable().Rows)
            {
                yield return new ADOTabularModel(_adoTabConn, dr["CUBE_NAME"].ToString());
            }
        }
        */
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (DataRow dr in GetModelsTable().Rows)
            {
                yield return new ADOTabularModel(_adoTabConn, dr["CUBE_NAME"].ToString());
            }
        }
    }
}
