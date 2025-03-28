using ADOTabular.Enums;
using DaxStudio.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using System;


namespace DaxStudio.CommandLine.UIStubs
{
    public class ConnectEvent : IConnectEvent
    {
        public string ConnectionString { get ; set ; }
        public bool PowerPivotModeSelected { get ; set; } = false;
        public string WorkbookName { get ; set ; }
        public string ApplicationName { get ; set ; }
        public string PowerBIFileName { get ; set ; } = string.Empty;

        public ServerType ServerType { get; }

        public string DatabaseName { get ; set ; }
        public bool RefreshDatabases { get ; set ; }
        public AccessToken AccessToken { get; set; }
        public string FileName { get; set; }
    }
}
