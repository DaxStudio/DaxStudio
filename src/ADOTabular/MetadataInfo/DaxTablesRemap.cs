using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ADOTabular.MetadataInfo
{
    public class DaxTablesRemap
    {
        private readonly ADOTabularConnection _connection;
        public Dictionary<string, string> RemapNames { get; } = new Dictionary<string, string>();

        public DaxTablesRemap(ADOTabularConnection AdoTabularConnection) : this()
        {
            Contract.Requires(AdoTabularConnection != null, "The AdoTabularConnection parameter must not be null");
            _connection = AdoTabularConnection;
            _connection.Visitor.Visit(this);
        }

        public DaxTablesRemap() { }
    }
}
