using System.Collections.Generic;
using System.Collections;
using System.Diagnostics.Contracts;
using ADOTabular.Extensions;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

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
        Unknown,
        MeasureFormatString,
        Calendar
    }

    public class ADOTabularColumnCollection: IEnumerable<ADOTabularColumn>
    {
        private readonly IADOTabularConnection _adoTabConn;
        public ADOTabularColumnCollection(IADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            Contract.Requires(adoTabConn != null, "The adoTabConn parameter must not be null");

            Table = table;
            _adoTabConn = adoTabConn;
            _colsByRef = new SortedDictionary<string, ADOTabularColumn>();
            _cols ??= _adoTabConn.Visitor.Visit(this);
        }

        public ADOTabularTable Table { get; }

        public void Add(ADOTabularColumn column)
        {
            if (column == null) return;
            _cols.Add(column.Name,column);
            _colsByRef.Add(column.InternalReference, column);

            // add TOM columns / measures
            // we don't need any other types, this is just for intellisense support
            var tomTable = Table.Model.TOMModel.Tables[Table.Name];
            switch (column.ObjectType)
            {
                case ADOTabularObjectType.Column:
                    tomTable.Columns.Add(new DataColumn() { 
                        Name = column.Name, 
                        Description = column.Description, 
                        DataType = column.DataType, 
                        IsHidden = !column.IsVisible});
                    break;
                case ADOTabularObjectType.Measure:
                    var measure = new Measure()
                    {
                        Name = column.Name,
                        Description = column.Description,
                        IsHidden = !column.IsVisible
                    };
                    measure.SetDataType(column.DataType);
                    //var t = typeof(Measure);
                    //t.GetProperty("DataType")
                    //    .SetValue(measure, column.DataType, null);
                    tomTable.Measures.Add(measure);
                    break;

            }

            
        }

        public void Remove(ADOTabularColumn column)
        {
            if (column == null) return;
            _cols.Remove(column.Name);
            _colsByRef.Remove(column.InternalReference);
        }

        public void Remove(string columnName)
        {
            var col = _cols[columnName];
            _cols.Remove(columnName);
            _colsByRef.Remove(col.InternalReference);
        }

        public bool ContainsKey(string index)
        {
            return _cols.ContainsKey(index);
        }

        public void Clear()
        {
            _cols.Clear();
            _colsByRef.Clear();
        }
        //private readonly Dictionary<string, ADOTabularColumn> _cols;
        private readonly SortedDictionary<string, ADOTabularColumn> _cols;
        private readonly SortedDictionary<string, ADOTabularColumn> _colsByRef;

        public ADOTabularColumn this[string index]
        {
            get => _cols[index];
            set => _cols[index] = value;
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
            return _colsByRef[referenceName];
            //foreach (var c in _cols)
            //{
            //    if (c.Value.InternalReference.Equals(referenceName, System.StringComparison.InvariantCultureIgnoreCase))
            //    {
            //        return c.Value;
            //    }
            //}
            //return null;
        }
        public IEnumerator<ADOTabularColumn> GetEnumerator()
        {
            foreach (var adoTabularColumn in _cols.Values)
            {
                // RowNumber cannot be referenced in queries so we exclude it from the collection
                if (adoTabularColumn.Contents == "RowNumber") continue;
                // the KPI components are available through the parent KPI object
                if (adoTabularColumn.ObjectType == ADOTabularObjectType.KPIGoal) continue;
                if (adoTabularColumn.ObjectType == ADOTabularObjectType.KPIStatus) continue;

                yield return adoTabularColumn;
            }
        }

        public int Count => _cols.Count;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGetValue(string columnName, out ADOTabularColumn column)
        {
            return _cols.TryGetValue(columnName, out column);
        }
    }
}
