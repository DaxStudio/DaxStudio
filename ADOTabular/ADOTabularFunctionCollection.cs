using System.Collections.Generic;
using System.Data;
using System.Collections;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularFunctionCollection: IEnumerable<ADOTabularFunction>, IEnumerable
    {
        private ADOTabularConnection _adoTabConn;
        public ADOTabularFunctionCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
        }

        private DataSet GetFunctionsTable()
        {
            AdomdRestrictionCollection resColl = new AdomdRestrictionCollection();
            resColl.Add("ORIGIN",3);
            resColl.Add("ORIGIN",4);
            
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

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            foreach (DataRow dr in GetFunctionsTable().Tables[0].Rows)
            {
                yield return new ADOTabularFunction(dr);
            }
        }
    }
}
