using System.Collections.Generic;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularFunctionCollection: IEnumerable<ADOTabularFunction>
    {
        private Dictionary<string, ADOTabularFunction> _functions; 
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularFunctionCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
            _functions = new Dictionary<string, ADOTabularFunction>();
        }

        public ADOTabularFunctionCollection()
        {
            _functions = new Dictionary<string, ADOTabularFunction>();
        }

        /*
        private DataSet _dsFuncs;
        private DataSet GetFunctionsTable()
        {
            if (_dsFuncs == null)
            {
                _dsFuncs = _adoTabConn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS");
            }

            return _dsFuncs;
        }
        */

        public int Count
        {
            get { return _functions.Count; }
        }

        public IEnumerator<ADOTabularFunction> GetEnumerator()
        {
            foreach (var f in _functions.Values)
            {
                yield return f;
            }
        }

        IEnumerator<ADOTabularFunction> IEnumerable<ADOTabularFunction>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(ADOTabularFunction fun)
        {
            _functions.Add(fun.Caption, fun);
        }
    }
}
