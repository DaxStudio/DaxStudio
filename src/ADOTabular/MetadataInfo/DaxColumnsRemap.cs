using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ADOTabular.MetadataInfo
{
    public class DaxColumnsRemap
    {
        private readonly ADOTabularConnection _connection;
        public Dictionary<string, string> RemapNames { get; } = new Dictionary<string, string>();

        public DaxColumnsRemap(ADOTabularConnection AdoTabularConnection) : this()
        {
            Contract.Requires(AdoTabularConnection != null, "The AdoTabularConnection parameter must not be null");
            _connection = AdoTabularConnection;
            _connection.Visitor.Visit(this);
        }

        public DaxColumnsRemap() { }
    }
}
