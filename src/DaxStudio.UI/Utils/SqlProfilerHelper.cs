using Microsoft.Win32;

namespace DaxStudio.UI.Utils
{
    public static class SqlProfilerHelper
    {
        public static string GetSqlProfilerLaunchCommand()
        {
            var regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Classes\SQLServerProfilerTraceData\shell\open\command", false);
            if (regKey == null) return "";

            var profilerCommand = (string)regKey.GetValue("");
            var commandParts = profilerCommand.Split('/'); // split at the /f command

            return commandParts[0].Trim();

        }
    }
}
