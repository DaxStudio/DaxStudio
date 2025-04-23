using ADOTabular.Enums;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.Text;
using static Dax.Vpax.Tools.VpaxTools;

namespace DaxStudio.UI.Events
{
    public class ConnectEvent : IConnectEvent
    {


        public ConnectEvent(string connectionString, bool powerPivotModeSelected, string applicationName, string fileName, ServerType serverType, bool refreshDatabases, string databaseName, AccessToken token)
        {
            ConnectionString = connectionString;
            PowerPivotModeSelected = powerPivotModeSelected;
            ApplicationName = applicationName;
            if (powerPivotModeSelected) WorkbookName = fileName; else PowerBIFileName = fileName;
            FileName = fileName;
            ServerType = serverType;
            RefreshDatabases = refreshDatabases;
            DatabaseName = databaseName;
            AccessToken = token;
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

        public string ConnectionString { get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string ApplicationName { get; set; }
        public string FileName { get; set; }
        public ServerType ServerType { get; internal set; }

        public string DatabaseName { get; }
        public bool RefreshDatabases { get; }
        public VpaxContent VpaxContent { get; }

        public List<ITraceWatcher> ActiveTraces { get; set; }
        public string WorkbookName { get; set; }
        public string PowerBIFileName { get; set; }
        public AccessToken AccessToken { get; private set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var prop in this.GetType().GetProperties())
            {
                if (prop.GetValue(this) != null)
                {
                    sb.Append(prop.Name);
                    if (prop.Name == "AccessToken")
                    {
                        sb.Append(" = <redacted>");
                    }
                    else
                    {
                        sb.Append(" = ");
                        sb.Append(prop.GetValue(this).ToString());
                    }

                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }
    }
}
