using System.Collections.Generic;
using System.Collections;


namespace ADOTabular
{
    public enum ADOTabularObjectType
    {
        Column,
        Folder,
        Measure,
        KPI,
        KPIStatus,
        KPIGoal,
        Hierarchy,
        Level,
        UnnaturalHierarchy, 
        Table,
        DMV,
        Function,
        Unknown
    }

    public class ADOTabularColumnCollection: IEnumerable<ADOTabularColumn>
    {
        private readonly IADOTabularConnection _adoTabConn;
        public ADOTabularColumnCollection(IADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            Table = table;
            _adoTabConn = adoTabConn;
            if (_cols == null)
            {
                _cols = _adoTabConn.Visitor.Visit(this);
            }
        }

        public ADOTabularTable Table { get; }

        public void Add(ADOTabularColumn column)
        {
            _cols.Add(column.Name,column);
        }

        public void Remove(ADOTabularColumn column)
        {
            _cols.Remove(column.Name);
        }

        public void Remove(string columnName)
        {
            _cols.Remove(columnName);
        }

        public bool ContainsKey(string index)
        {
            return _cols.ContainsKey(index);
        }

        public void Clear()
        {
            _cols.Clear();
        }
        //private readonly Dictionary<string, ADOTabularColumn> _cols;
        private readonly SortedDictionary<string, ADOTabularColumn> _cols;

        public ADOTabularColumn this[string index]
        {
            get { return _cols[index]; }
            set { _cols[index] = value; }
        }

        public ADOTabularColumn this[int index]
        {
            get { 
                string[] sKeys = new string[_cols.Count];
                _cols.Keys.CopyTo(sKeys,0);
                return _cols[sKeys[index]];
            }
            //set { _cols[index] = value; }
        }

        public ADOTabularColumn GetByPropertyRef(string referenceName)
        {
            foreach (var c in _cols)
            {
                if (c.Value.InternalReference.Equals(referenceName, System.StringComparison.InvariantCultureIgnoreCase))
                {
                    return c.Value;
                }
            }
            return null;
        }
        public IEnumerator<ADOTabularColumn> GetEnumerator()
        {
            foreach (var adoTabularColumn in _cols.Values)
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
            get { return _cols.Count; }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
