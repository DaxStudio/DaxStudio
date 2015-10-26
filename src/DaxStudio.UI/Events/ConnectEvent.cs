

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {
        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string workbookName, string connectionType, string powerBIFileName)
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            WorkbookName = workbookName;
            ConnectionType = connectionType;
            PowerBIFileName = powerBIFileName;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
        public string ConnectionType { get; set; }
        public string PowerBIFileName { get; set; }
    }
}
