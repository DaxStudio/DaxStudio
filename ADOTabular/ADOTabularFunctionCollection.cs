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

        private DataSet GetFunctionsTable()
        {
            var resColl = new AdomdRestrictionCollection {{"ORIGIN", 3}, {"ORIGIN", 4}};

            return _adoTabConn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", resColl);

        }
        

        public int Count
        {
            get { return GetFunctionsTable().Tables[0].Rows.Count; }
        }

        IEnumerator<ADOTabularFunction> IEnumerable<ADOTabularFunction>.GetEnumerator()
        {
            foreach (DataRow dr in GetFunctionsTable().Tables[0].Rows)
            {
                yield return new ADOTabularFunction(dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (DataRow dr in GetFunctionsTable().Tables[0].Rows)
            {
                yield return new ADOTabularFunction(dr);
            }
        }
    }
}
