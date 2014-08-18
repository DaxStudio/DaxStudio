using ADOTabular;

namespace DaxStudio.UI.Events
{
    public class UpdateConnectionEvent
    {
        
        public UpdateConnectionEvent(ADOTabular.ADOTabularConnection connection) //, bool isPowerPivotConnection)
        {
            Connection = connection;
            DatabaseName = connection==null?string.Empty:connection.Database.Name;
            //IsPowerPivotConnection = isPowerPivotConnection;
        }
        
        public UpdateConnectionEvent(ADOTabular.ADOTabularConnection connection,string databaseName)//, bool isPowerPivotConnection)
        {
            Connection = connection;
            DatabaseName = databaseName;
        //    IsPowerPivotConnection = isPowerPivotConnection;
        }

        public  ADOTabularConnection Connection{get; set; }
        public string DatabaseName { get; set; }
        //public bool IsPowerPivotConnection { get; set; }
    }
}
