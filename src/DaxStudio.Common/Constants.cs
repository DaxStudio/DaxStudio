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
        public const System.Windows.Input.Key LoggingHotKey1 = System.Windows.Input.Key.LeftShift;
        public const System.Windows.Input.Key LoggingHotKey2 = System.Windows.Input.Key.RightShift;
        public const string LoggingHotKeyName = "Shift";
        public const int ExcelUIStartupTimeout = 5000;
    }
}
