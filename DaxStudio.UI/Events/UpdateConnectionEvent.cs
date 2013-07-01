using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class UpdateConnectionEvent
    {
        public UpdateConnectionEvent(ADOTabular.ADOTabularConnection connection)
        {
            Connection = connection;
        }

        public  ADOTabularConnection Connection{get; set; }
    }
}
