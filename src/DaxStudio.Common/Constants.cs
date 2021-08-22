namespace DaxStudio.Common
{
    public static class Constants
    {
        
        //public const string AppDataSettingsFolder = @"%APPDATA%\DaxStudio";
        //public const string LogFolder = @"%APPDATA%\DaxStudio\log\";
        public const string ExcelLogFileName = "DaxStudioExcel-.log";
        public const string StandaloneLogFileName = "DaxStudio-.log";
        //public const string AutoSaveIndexPath = @"%APPDATA%\DaxStudio\AutoSaveMasterIndex.json";
        //public const string AutoSaveFolder = @"%APPDATA%\DaxStudio\AutoSaveFiles";
        //public const string AvalonDockLayoutFile = @"%APPDATA%\DaxStudio\WindowLayouts\Custom.xml";
        public const string AvalonDockDefaultLayoutFile = @"DaxStudio.UI.Resources.AvalonDockLayout-Default.xml";

        public const System.Windows.Input.Key LoggingHotKey1 = System.Windows.Input.Key.LeftShift;
        public const System.Windows.Input.Key LoggingHotKey2 = System.Windows.Input.Key.RightShift;
        public const string LoggingHotKeyName = "Shift";
        public const int ExcelUIStartupTimeout = 10000;

        public const string FormatString = "FormatString";
        public const string LocaleId = "LocaleId";
        public const string IsUnique = "IsUnique";
        public const string AllowDbNull = "AllowDBNull";

        public const string StatusBarTimerFormat = "mm\\:ss\\.f";

        public const int AutoSaveIntervalMs = 10000; // autosave every 30 seconds
        public const string RefreshSessionQuery =  "EVALUATE " + InternalQueryHeader + " ROW(\"DAX Studio Session Refresh\",0)";

        public const string InternalQueryHeader = "/* <<DAX Studio Internal>> */";
        public const string IsoDateMask = "yyyy-MM-dd HH:mm:ss{0}fff";
        public const string IsoDateFormat = "yyyy-MM-ddTHH:mm:ssZ"; // this is an Excel friendly ISO date format for csv files
        public const string IsoDateFormatPaste = "yyyy-MM-dd HH:mm:ss"; // this is an Excel friendly ISO date format
        
        public const int MaxRecentFiles = 25;
        public const int MaxRecentServers = 25;
        public const int MaxMruSize = 25;
        public const string DownloadUrl = "https://daxstudio.org/downloads";
        public const string LogMessageTemplate = "{class} {method} {message}";

        public const string SessionsDmv = "$SYSTEM.DISCOVER_SESSIONS";
        public const string SessionSpidColumn = "SESSION_SPID";
        //public const int TraceStartTimeoutSeconds = 30;

        public const int MaxLineLength = 500;
    }
}
