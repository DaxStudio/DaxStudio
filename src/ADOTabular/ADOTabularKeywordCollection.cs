using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularKeywordCollection: IEnumerable<string>
    {
        private IADOTabularConnection _connection;
        private List<string> _keywords = new List<string>();
        public ADOTabularKeywordCollection(IADOTabularConnection adoTabularConnection)
        {
            // TODO: Complete member initialization
            _connection = adoTabularConnection;
            _connection.Visitor.Visit(this);
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (var kw in _keywords)
            {
                yield return kw;
            }
        }

        IEnumerator<string> IEnumerable<string>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal void Add(string keyword)
        {
            if (!_keywords.Contains(keyword))
            {
                _keywords.Add(keyword);
            }
        }

        public bool Contains(string word)
        {
            return _keywords.Contains(word, StringComparer.InvariantCultureIgnoreCase);
        }
        public int Count
        {
            get { return _keywords.Count; }
        }
    }
}
