

using ADOTabular.Enums;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;

namespace DaxStudio.UI.Events
{
    public class ConnectEvent
    {


        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string workbookName, string applicationName, string powerBIFileName, ServerType serverType, bool refreshDatabases) 
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            WorkbookName = workbookName;
            ApplicationName = applicationName;
            PowerBIFileName = powerBIFileName;
            ServerType = serverType;
            RefreshDatabases = refreshDatabases;
        }

        public string ConnectionString{get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
        public string ApplicationName { get; set; }
        public string PowerBIFileName { get; set; }
        public ServerType ServerType { get; internal set; }

        public string DatabaseName { get; set; }
        public bool RefreshDatabases { get; set; }

        public List<ITraceWatcher> ActiveTraces { get; set; }
    }
}
