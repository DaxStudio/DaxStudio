using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ADOTabular.MetadataInfo
{
    public class DaxColumnsRemap
    {
        private readonly ADOTabularConnection _connection;
        public Dictionary<string, string> RemapNames { get; } = new Dictionary<string, string>();
        /// <summary>
        /// Set of COLUMN_IDs (the xmSQL-level identifiers) whose logical data type is DateTime.
        /// Used to restrict date annotation in xmSQL formatting to actual date columns.
        /// </summary>
        public HashSet<string> DateColumnIds { get; } = new HashSet<string>();

        public DaxColumnsRemap(ADOTabularConnection AdoTabularConnection) : this()
        {
            Contract.Requires(AdoTabularConnection != null, "The AdoTabularConnection parameter must not be null");
            _connection = AdoTabularConnection;
            _connection.Visitor.Visit(this);
        }

        public DaxColumnsRemap() { }
    }
}
