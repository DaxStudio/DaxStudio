

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {
        public ConnectEvent(string connectionString, bool powerPivotModeSelected)
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
    }
}
