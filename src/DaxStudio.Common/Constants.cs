using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public class Constants
    {
        public const string LogFolder = @"%APPDATA%\DaxStudio\log\";
        public const string ExcelLogFileName = "DaxStudioExcel-{Date}.log";
        public const string StandaloneLogFileName = "DaxStudio-{Date}.log";
        public const string AutoSaveIndexPath = @"%APPDATA%\DaxStudio\AutoSaveMasterIndex.json";
        public const string AutoSaveFolder = @"%APPDATA%\DaxStudio\AutoSaveFiles";
        public const string AvalonDockLayoutFile = @"%APPDATA%\DaxStudio\WindowLayouts\Custom.xml";
        public const string AvalonDockDefaultLayoutFile = @"DaxStudio.UI.Resources.AvalonDockLayout-Default.xml";

        public const System.Windows.Input.Key LoggingHotKey1 = System.Windows.Input.Key.LeftShift;
        public const System.Windows.Input.Key LoggingHotKey2 = System.Windows.Input.Key.RightShift;
        public const string LoggingHotKeyName = "Shift";
        public const int ExcelUIStartupTimeout = 10000;

        public const string FORMAT_STRING = "FormatString";
        public const string LOCALE_ID = "LocaleId";
        public const string IS_UNIQUE = "IsUnique";
        public const string ALLOW_DBNULL = "AllowDBNull";

        public const string StatusBarTimerFormat = "mm\\:ss\\.f";

        public const int AutoSaveIntervalMs = 10000; // autosave every 30 seconds
        
        //public const int TraceStartTimeoutSeconds = 30;
    }
}
