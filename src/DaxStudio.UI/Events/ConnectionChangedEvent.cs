using ADOTabular;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Events
{
    public class ConnectionChangedEvent
    { 
        public ConnectionChangedEvent(ADOTabularConnection connection, DocumentViewModel document)
        {
            Document = document;
            Connection = connection;
        }

        public ADOTabularConnection Connection { get; set; }
        public DocumentViewModel Document { get; private set; }
    }
}
