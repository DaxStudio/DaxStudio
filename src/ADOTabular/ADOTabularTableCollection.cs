using System.Collections.Generic;
using System.Collections;
using ADOTabular.Interfaces;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{
    public class ADOTabularTableCollection:IEnumerable<ADOTabularTable>
    {
        
        private readonly IADOTabularConnection _adoTabConn;
        private SortedDictionary<string, ADOTabularTable> _tables = new SortedDictionary<string, ADOTabularTable>();

        public ADOTabularTableCollection(IADOTabularConnection adoTabConn, ADOTabularModel model)
        {
            _adoTabConn = adoTabConn;
            Model = model;
            _adoTabConn.Visitor.Visit(this);
        }

        private SortedDictionary<string,ADOTabularTable> InternalTableCollection
        {
            get
            {
                return _tables;
            }
        }

        public ADOTabularModel Model
        {
            get;
        }

        public int Count
        {
            get { return InternalTableCollection.Count; }
        }

        public void Add(ADOTabularTable table)
        {
            if (table == null) return;

            _tables.Add(table.Name, table);

            Model.TOMModel.Tables.Add(new Table(){Name = table.Name, Description = table.Description, DataCategory = table.DataCategory});
        }

        public ADOTabularTable this[string index] => InternalTableCollection[index];

        public bool ContainsKey(string index)
        {
            return InternalTableCollection.ContainsKey(index);
        }

        public bool TryGetValue(string index, out ADOTabularTable table)
        {
            return InternalTableCollection.TryGetValue(index, out table);
        }

        public ADOTabularTable this[int index]
        {
            get
            {
                string[] keys = new string[InternalTableCollection.Count];
                InternalTableCollection.Keys.CopyTo(keys, 0);
                return InternalTableCollection[keys[index]];
            }

        }
     
        public ADOTabularTable GetById(string internalId)
        {
            foreach (var t in InternalTableCollection.Values)
            {
                if (t.InternalReference == internalId)
                {
                    return t;
                }
            }
            return null;
        }

        public IEnumerator<ADOTabularTable> GetEnumerator()
        {
            foreach (var t in InternalTableCollection.Values)
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

