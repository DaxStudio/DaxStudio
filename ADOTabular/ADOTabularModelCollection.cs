using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
//using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.AdomdClientWrappers;

namespace ADOTabular
{
    public class ADOTabularModelCollection:IEnumerable<ADOTabularModel>
    {
        private readonly ADOTabularConnection _adoTabConn;
        private readonly ADOTabularDatabase  _database;
        private DataTable _dtModels;
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
            if (_dtModels == null)
            {
            var resColl = new AdomdRestrictionCollection {{"CUBE_SOURCE", 1}};
                _dtModels = _adoTabConn.GetSchemaDataSet("MDSCHEMA_CUBES", resColl).Tables[0];
            }
            return _dtModels;
        }

        public ADOTabularModel this[string modelName]
        {
            get
            {
                return (from dr in GetModelsTable().Rows.Cast<DataRow>() where string.Compare(modelName, dr["CUBE_NAME"].ToString(), StringComparison.InvariantCultureIgnoreCase) == 0 select new ADOTabularModel(_adoTabConn, dr)).FirstOrDefault();
                // todo - should we return a model not found exception instead of null?
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<ADOTabularModel> GetEnumerator()
        {
            foreach (DataRow dr in GetModelsTable().Rows)
            {
                yield return new ADOTabularModel(_adoTabConn, dr);
            }
        }

        
    }
}
