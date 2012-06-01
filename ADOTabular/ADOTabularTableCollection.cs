using System.Collections.Generic;
using System.Data;
using Microsoft.AnalysisServices.AdomdClient;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularTableCollection:IEnumerable<ADOTabularTable>
    {
        
        private readonly ADOTabularConnection _adoTabConn;
        private readonly ADOTabularModel  _model;
        private DataTable _dtTables;
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
            if (_dtTables == null)
            {
                var resColl = new AdomdRestrictionCollection { 
                                    { "CUBE_NAME", Model.Name }, 
                                    { "DIMENSION_VISIBILITY", _adoTabConn.ShowHiddenObjects ? (int)(MdschemaVisibility.Visible | MdschemaVisibility.NonVisible) : (int)(MdschemaVisibility.Visible)} };
                _dtTables = _adoTabConn.GetSchemaDataSet("MDSCHEMA_DIMENSIONS", resColl).Tables[0];
            }
            return _dtTables;
        }

        
        public IEnumerator<ADOTabularTable> GetEnumerator()
        {
            foreach (DataRow dr in GetTablesTable().Rows)
            {
                if (dr["DIMENSION_NAME"].ToString().ToUpper()!="MEASURES")
                yield return new ADOTabularTable(_adoTabConn, dr,_model);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

