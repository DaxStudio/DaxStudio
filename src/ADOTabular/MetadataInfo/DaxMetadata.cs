using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace ADOTabular.MetadataInfo {
    public class DaxMetadata {
        public SsasVersion Version { get; set; }
        public List<DaxFunction> DaxFunctions { get; } = new List<DaxFunction>();
        private readonly ADOTabularConnection _connection;
        public DaxMetadata() { }
        public DaxMetadata(ADOTabularConnection AdoTabularConnection) : this() {
            Contract.Requires(AdoTabularConnection != null, "The AdoTabularConnection parameter must not be null" );
            // TODO: Complete member initialization
            _connection = AdoTabularConnection;
            _connection.Visitor.Visit(this);
        }

    }
}
