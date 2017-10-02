using System.Collections.Generic;

namespace ADOTabular.MetadataInfo {
    public class DaxMetadata {
        public SsasVersion Version;
        public List<DaxFunction> DaxFunctions;
        private ADOTabularConnection _connection;
        public DaxMetadata() {
            DaxFunctions = new List<DaxFunction>();
        }
        public DaxMetadata(ADOTabularConnection aDOTabularConnection) : this() {
            // TODO: Complete member initialization
            _connection = aDOTabularConnection;
            _connection.Visitor.Visit(this);
        }

    }
}
