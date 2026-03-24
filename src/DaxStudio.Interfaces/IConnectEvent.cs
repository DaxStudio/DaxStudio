using ADOTabular.Enums;
using Microsoft.AnalysisServices.AdomdClient;
using System;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif

namespace DaxStudio.Interfaces
{
    public interface IConnectEvent
    {
        public string ConnectionString { get; set; }
        public bool PowerPivotModeSelected { get; set; }
        public string WorkbookName { get; set; }
        public string ApplicationName { get; set; }
        public string PowerBIFileName { get; set; }
        public ServerType ServerType { get;  }
        public string DatabaseName { get;  }
        public bool RefreshDatabases { get;  }
        public AccessToken AccessToken { get; }
        public string FileName { get; }
    }
}
