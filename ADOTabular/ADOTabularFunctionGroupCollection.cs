using System.Collections;
using System.Collections.Generic;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularFunctionGroupCollection : IEnumerable<ADOTabularFunctionGroup>
    {
        private readonly Dictionary<string, ADOTabularFunctionGroup> _funcGroups;
        private readonly ADOTabularConnection _connection;
        public ADOTabularFunctionGroupCollection(ADOTabularConnection connection)
        {
            _connection = connection;
            _funcGroups = new Dictionary<string, ADOTabularFunctionGroup>();
            _connection.Visitor.Visit(this);
        }

        public int Count
        {
            get { return _funcGroups.Values.Count; }
        }

        public void Add(ADOTabularFunctionGroup group)
        {
            if (_funcGroups.ContainsKey(group.Caption))
                return;
            _funcGroups.Add(group.Caption,group);
        }

        public void AddFunction(string groupName, string functionName, string description, DataRow[] parameters)
        {
            var fun = new ADOTabularFunction(functionName, description, groupName, new ADOTabularParameterCollection(parameters));
            if (_funcGroups.ContainsKey(groupName))
                _funcGroups.Add(groupName, new ADOTabularFunctionGroup(groupName,_connection));
            ADOTabularFunctionGroup grp = _funcGroups[groupName];
            grp.Functions.Add(fun);
        }

        public void AddFunction(DataRow functionDataRow)
        {
            var fun = new ADOTabularFunction(functionDataRow);
            if (!_funcGroups.ContainsKey(fun.Group))
                _funcGroups.Add(fun.Group, new ADOTabularFunctionGroup(fun.Group, _connection));
            ADOTabularFunctionGroup grp = _funcGroups[fun.Group];
            grp.Functions.Add(fun);
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
    }
}
