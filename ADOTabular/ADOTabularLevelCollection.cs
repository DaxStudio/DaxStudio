using System.Collections.Generic;
using System.Collections;

//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
  

    public class ADOTabularLevelCollection: IEnumerable<ADOTabularLevel>
    {
        private readonly ADOTabularTable _table;
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularLevelCollection(ADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            _table = table;
            _adoTabConn = adoTabConn;
            //if (_cols == null)
            //{
            //    _cols = _adoTabConn.Visitor.Visit(this);
            //}
        }

        public ADOTabularTable Table {
            get { return _table; }
        }

        public void Add(ADOTabularLevel level)
        {
            _lvls.Add(level.Column.InternalReference,level);
        }

        public void Clear()
        {
            _lvls.Clear();
        }
        private readonly Dictionary<string, ADOTabularLevel> _lvls;
 
        public ADOTabularLevel this[string index]
        {
            get { return _lvls[index]; }
            set { _lvls[index] = value; }
        }

        public ADOTabularLevel this[int index]
        {
            get { 
                string[] sKeys = new string[_lvls.Count];
                _lvls.Keys.CopyTo(sKeys,0);
                return _lvls[sKeys[index]];
            }
            //set { _cols[index] = value; }
        }
        public IEnumerator<ADOTabularLevel> GetEnumerator()
        {
            foreach (var adoTabularColumn in _lvls.Values)
            {
                // rownumber cannot be referenced in queries so we exclude it from the collection
                //if (adoTabularColumn.Contents != "RowNumber") 
                    yield return adoTabularColumn;
            }
        }

        public int Count
        {
            get { return _lvls.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
