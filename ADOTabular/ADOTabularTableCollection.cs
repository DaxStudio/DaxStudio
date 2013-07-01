using System.Collections.Generic;
using System.Collections;
//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularTableCollection:IEnumerable<ADOTabularTable>
    {
        
        private readonly ADOTabularConnection _adoTabConn;
        private readonly ADOTabularModel  _model;
        private readonly Dictionary<string, ADOTabularTable> _tables;

        public ADOTabularTableCollection(ADOTabularConnection adoTabConn, ADOTabularModel model)
        {
            _adoTabConn = adoTabConn;
            _model = model;
            _tables = _adoTabConn.Visitor.Visit(this);
        }

        public ADOTabularModel Model
        {
            get { return _model; }
        }
        
        public ADOTabularTable GetById(string internalId)
        {
            foreach (var t in _tables.Values)
            {
                if (t.InternalId == internalId)
                {
                    return t;
                }
            }
            return null;
        }

        public IEnumerator<ADOTabularTable> GetEnumerator()
        {
            foreach (var t in _tables.Values)
            {
                yield return t;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}

