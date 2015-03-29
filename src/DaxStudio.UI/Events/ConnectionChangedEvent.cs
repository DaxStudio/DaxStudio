using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class ConnectionChangedEvent
    { 
        public ConnectionChangedEvent(ADOTabularConnection connection)
        {
            Connection = connection;
        }

        public ADOTabularConnection Connection { get; set; }
    }
}
