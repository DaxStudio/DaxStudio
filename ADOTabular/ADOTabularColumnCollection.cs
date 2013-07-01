using System.Collections.Generic;
using System.Collections;

//using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public enum ADOTabularColumnType
    {
        Column,
        Measure
    }

    public class ADOTabularColumnCollection: IEnumerable<ADOTabularColumn>
    {
        private readonly ADOTabularTable _table;
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularColumnCollection(ADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            _table = table;
            _adoTabConn = adoTabConn;
            if (_cols == null)
            {
                _cols = _adoTabConn.Visitor.Visit(this);
            }
        }

        public ADOTabularTable Table {
            get { return _table; }
        }

        public void Add(ADOTabularColumn column)
        {
            _cols.Add(column.Caption,column);
        }

        public void Clear()
        {
            _cols.Clear();
        }
        private readonly Dictionary<string, ADOTabularColumn> _cols;
 
        public IEnumerator<ADOTabularColumn> GetEnumerator()
        {
            foreach (var adoTabularColumn in _cols.Values)
            {
                yield return adoTabularColumn;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
