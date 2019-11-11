using System.Collections.Generic;
using System.Data;
using System.Collections;
using System.Diagnostics.Contracts;

namespace ADOTabular
{
    public class ADOTabularFunctionCollection: IEnumerable<ADOTabularFunction>
    {
        private readonly SortedDictionary<string, ADOTabularFunction> _functions; 
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularFunctionCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
            _functions = new SortedDictionary<string, ADOTabularFunction>();
        }

        public ADOTabularFunctionCollection()
        {
            _functions = new SortedDictionary<string, ADOTabularFunction>();
        }

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

        public void Add(ADOTabularFunction tabularFunc)
        {
            Contract.Requires(tabularFunc != null, "The tabularFunc parameter must not be null");

            _functions.Add(tabularFunc.Caption, tabularFunc);
        }
    }
}
