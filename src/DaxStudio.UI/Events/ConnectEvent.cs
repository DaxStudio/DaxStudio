

using ADOTabular.Enums;

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {


        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string workbookName, string connectionType, string powerBIFileName, ServerType serverType) 
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            WorkbookName = workbookName;
            ConnectionType = connectionType;
            PowerBIFileName = powerBIFileName;
            ServerType = serverType;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
        public string ConnectionType { get; set; }
        public string PowerBIFileName { get; set; }
        public ServerType ServerType { get; internal set; }

        public string DatabaseName { get; set; }
    }
}
