using ADOTabular.Enums;

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

        public string DatabaseName { get; set; }
        public bool RefreshDatabases { get; set; }

    }
}
