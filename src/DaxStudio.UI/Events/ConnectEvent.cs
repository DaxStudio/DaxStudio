

using ADOTabular.Enums;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using static Dax.Vpax.Tools.VpaxTools;

namespace DaxStudio.UI.Events
{
    public class ConnectEvent : IConnectEvent
    {


        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string applicationName, string fileName, ServerType serverType, bool refreshDatabases, string databaseName) 
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            ApplicationName = applicationName;
            if (powerPivotModeSelected) WorkbookName = fileName; else PowerBIFileName = fileName;
            FileName = fileName;
            ServerType = serverType;
            RefreshDatabases = refreshDatabases;
            DatabaseName = databaseName;
        }

        public ConnectEvent(string applicationName, VpaxContent vpaxContent)
        {
            ConnectionString = string.Empty;
            PowerPivotModeSelected = false;
            ApplicationName = applicationName;
            ServerType = ServerType.Offline;
            RefreshDatabases = false;
            VpaxContent = vpaxContent;
        }

        public string ConnectionString{ get; set; }
        public bool PowerPivotModeSelected { get; set;  }
        public string ApplicationName { get; set; }
        public string FileName { get; set; }
        public ServerType ServerType { get; internal set; }

        public string DatabaseName { get;  }
        public bool RefreshDatabases { get; }
        public VpaxContent VpaxContent { get;  }

        public List<ITraceWatcher> ActiveTraces { get; set; }
        public string WorkbookName { get; set; }
        public string PowerBIFileName { get; set; }
    }
}
