

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {
        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string workbookName, string connectionType)
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            WorkbookName = workbookName;
            ConnectionType = connectionType;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
        public string ConnectionType { get; set; }
    }
}
