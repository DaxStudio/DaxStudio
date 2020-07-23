using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class UpdateConnectionEvent
    {
        
        public UpdateConnectionEvent(ADOTabular.ADOTabularConnection connection) //, bool isPowerPivotConnection)
        {
            Connection = connection;
        }
        

        public  ADOTabularConnection Connection{get; set; }
        
    }
}
