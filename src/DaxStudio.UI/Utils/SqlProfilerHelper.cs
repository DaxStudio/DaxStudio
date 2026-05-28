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
            return ExtractExecutablePath(profilerCommand);
        }

        // Extracts the executable path from a registry "shell\open\command" value.
        // Typical formats:
        //   "C:\Program Files\...\profiler.exe" /F "%1"
        //   C:\PROGRA~1\...\profiler.exe /F %1
        // Returns the executable path with any surrounding quotes removed so that the
        // result can be passed directly as the FileName argument to Process.Start.
        internal static string ExtractExecutablePath(string registryCommand)
        {
            if (string.IsNullOrWhiteSpace(registryCommand)) return string.Empty;

            var s = registryCommand.TrimStart();
            if (s.Length == 0) return string.Empty;

            if (s[0] == '"')
            {
                // quoted exe path - take everything between the opening and closing quote
                var closing = s.IndexOf('"', 1);
                if (closing < 0) return s.Substring(1).Trim(); // malformed but be forgiving
                return s.Substring(1, closing - 1);
            }

            // unquoted - take everything up to the first whitespace (paths in the registry
            // are typically short 8.3 form when unquoted, so this is safe and avoids the
            // bug where Split('/') would chop a path containing forward slashes)
            var space = s.IndexOf(' ');
            return space < 0 ? s : s.Substring(0, space);
        }

        // Quotes a single command-line argument so it survives CommandLineToArgvW parsing.
        // Required for values that may contain spaces or other special characters, such as
        // a Power BI XMLA endpoint like "powerbi://api.powerbi.com/v1.0/myorg/My Workspace".
        public static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            if (value.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return value;
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
