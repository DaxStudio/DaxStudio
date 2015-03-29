

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {
        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string workbookName)
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            WorkbookName = workbookName;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
    }
}
