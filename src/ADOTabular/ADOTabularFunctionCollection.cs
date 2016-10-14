using System.Collections.Generic;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularFunctionCollection: IEnumerable<ADOTabularFunction>
    {
        private SortedDictionary<string, ADOTabularFunction> _functions; 
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

        public void Add(ADOTabularFunction fun)
        {
            _functions.Add(fun.Caption, fun);
        }
    }
}
