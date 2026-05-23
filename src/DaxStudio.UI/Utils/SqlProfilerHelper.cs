using Microsoft.Expression.Shapes;
using Microsoft.Win32;

namespace DaxStudio.UI.Utils
{
    public static class SqlProfilerHelper
    {
        public static string GetSqlProfilerLaunchCommand()
        {
            // older SQL Profiler versions registered a SQLServerProfilerTraceData class
            var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\SQLServerProfilerTraceData\shell\open\command", false);

            if (regKey == null)
            {
                // try looking up the class for .trc files and then looking up the command for that class
                regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\.trc\OpenWithProgids", false);
                if (regKey == null) return string.Empty;

                var className = regKey.GetValueNames();
                if (className.Length == 0) return string.Empty;

                regKey = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Classes\{className[0]}\shell\open\command", false);

            }

            if (regKey == null) return string.Empty;

            var profilerCommand = (string)regKey.GetValue("");
            var commandParts = profilerCommand.Split('/'); // split at the /f command

            return commandParts[0].Trim();

            
        }
    }
}
