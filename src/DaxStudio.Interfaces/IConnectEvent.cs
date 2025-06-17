using ADOTabular.Enums;
#if NET472
using Microsoft.AnalysisServices.AdomdClient;
#else
using Microsoft.AnalysisServices;
#endif

using System;

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
