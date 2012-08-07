using System.Collections.Generic;
using System.Data;
using System.Collections;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularFunctionCollection: IEnumerable<ADOTabularFunction>
    {
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularFunctionCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
        }

        private DataSet _dsFuncs;
        private DataSet GetFunctionsTable()
        {
            if (_dsFuncs == null)
            {
                _dsFuncs = _adoTabConn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS");
            }

            return _dsFuncs;
        }
        

        public int Count
        {
            get { return GetFunctionsTable().Tables[0].Rows.Count; }
        }

        IEnumerator<ADOTabularFunction> IEnumerable<ADOTabularFunction>.GetEnumerator()
        {
            foreach (DataRow dr in GetFunctionsTable().Tables[0].Select("ORIGIN=3 OR ORIGIN=4"))
            {
                yield return new ADOTabularFunction(dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (DataRow dr in GetFunctionsTable().Tables[0].Select("ORIGIN=3 OR ORIGIN=4"))
            {
                yield return new ADOTabularFunction(dr);
            }
        }
    }
}
