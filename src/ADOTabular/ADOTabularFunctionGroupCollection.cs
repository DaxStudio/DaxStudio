using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularFunctionGroupCollection : IEnumerable<ADOTabularFunctionGroup>
    {
        private readonly SortedDictionary<string, ADOTabularFunctionGroup> _funcGroups;
        private readonly Dictionary<string, ADOTabularFunction> _funcDict;
        private readonly ADOTabularConnection _connection;
        public ADOTabularFunctionGroupCollection(ADOTabularConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _funcGroups = new SortedDictionary<string, ADOTabularFunctionGroup>();
            _funcDict = new Dictionary<string, ADOTabularFunction>( StringComparer.OrdinalIgnoreCase);
            _connection.Visitor.Visit(this);
        }

        public int Count
        {
            get { return _funcGroups.Values.Count; }
        }

        public void Add(ADOTabularFunctionGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (_funcGroups.ContainsKey(group.Caption))
                return;
            _funcGroups.Add(group.Caption,group);
        }

        public void AddFunction(string groupName, string functionName, string description, DataRow[] parameters)
        {
            var fun = new ADOTabularFunction(functionName, description, groupName, new ADOTabularFunctionArgumentCollection(parameters));
            if (!_funcGroups.ContainsKey(groupName))
                _funcGroups.Add(groupName, new ADOTabularFunctionGroup(groupName,_connection));
            ADOTabularFunctionGroup grp = _funcGroups[groupName];
            if (grp == null && !grp.Functions.ContainsKey(functionName))
            {
                grp.Functions.Add(fun);
                _funcDict.Add(fun.Caption, fun);
            }
        }

        public void AddFunction(DataRow functionDataRow)
        {
            var fun = new ADOTabularFunction(functionDataRow);
            if (!_funcGroups.ContainsKey(fun.Group))
                _funcGroups.Add(fun.Group, new ADOTabularFunctionGroup(fun.Group, _connection));
            ADOTabularFunctionGroup grp = _funcGroups[fun.Group];
            grp.Functions.Add(fun);
            _funcDict.Add(fun.Caption, fun);
        }

        IEnumerator<ADOTabularFunctionGroup> IEnumerable<ADOTabularFunctionGroup>.GetEnumerator()
        {
            foreach (ADOTabularFunctionGroup grp in _funcGroups.Values)
            {
                yield return grp;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            foreach (var grp in _funcGroups.Values)
            {
                yield return grp;
            }
        }

        public ADOTabularFunction GetByName(string name)
        {
            _ = _funcDict.TryGetValue(name, out ADOTabularFunction fun);
            return fun;
        }
    }
}
