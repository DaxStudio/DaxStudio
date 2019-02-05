using System.Collections.Generic;
using System.Collections;

//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{

    public class ADOTabularMeasureCollection: IEnumerable<ADOTabularMeasure>
    {
        private readonly ADOTabularTable _table;
        private readonly IADOTabularConnection _adoTabConn;
        public ADOTabularMeasureCollection(IADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            _table = table;
            _adoTabConn = adoTabConn;
            if (_measures == null)
            {
                _measures = _adoTabConn.Visitor.Visit(this);
            }
        }

        public ADOTabularTable Table {
            get { return _table; }
        }

        public void Add(ADOTabularMeasure column)
        {
            _measures.Add(column.Name,column);
        }

        public bool ContainsKey(string index)
        {
            return _measures.ContainsKey(index);
        }

        public void Clear()
        {
            _measures.Clear();
        }
        //private readonly Dictionary<string, ADOTabularColumn> _cols;
        private readonly SortedDictionary<string, ADOTabularMeasure> _measures;

        public ADOTabularMeasure this[string index]
        {
            get { return _measures[index]; }
            set { _measures[index] = value; }
        }

        public ADOTabularMeasure this[int index]
        {
            get { 
                string[] sKeys = new string[_measures.Count];
                _measures.Keys.CopyTo(sKeys,0);
                return _measures[sKeys[index]];
            }
            //set { _cols[index] = value; }
        }

        public ADOTabularMeasure GetByPropertyRef(string referenceName)
        {
            foreach (var c in _measures)
            {
                if (c.Value.InternalReference.Equals(referenceName, System.StringComparison.InvariantCultureIgnoreCase))
                {
                    return c.Value;
                }
            }
            return null;
        }
        public IEnumerator<ADOTabularMeasure> GetEnumerator()
        {
            foreach (var adoTabularColumn in _measures.Values)
            {
                // rownumber cannot be referenced in queries so we exclude it from the collection
                if (adoTabularColumn.Contents == "RowNumber") continue;
                // the KPI components are available through the parent KPI object
                if (adoTabularColumn.ObjectType == ADOTabularObjectType.KPIGoal) continue;
                if (adoTabularColumn.ObjectType == ADOTabularObjectType.KPIStatus) continue;

                yield return adoTabularColumn;
            }
        }

        public int Count
        {
            get { return _measures.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
