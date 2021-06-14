using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public static class AppProperties
    {
        internal const string LoggingEnabledByHotKey = "LoggingEnabledByHotKey";
        internal const string LoggingEnabledByCommandLine = "LoggingEnabledByCommandLine";
        internal const string PortNumber = "PortNumber";
        internal const string FileName = "FileName";
        internal const string CrashTest = "CrashTest";
        internal const string Database = "Database";
        internal const string Server = "Server";
        internal const string ShowHelp = "ShowHelp";

        public static CmdLineArgs _args;

        public static CmdLineArgs Args(this System.Windows.Application app)
        {
            return new CmdLineArgs(app);
            //if (_args == null) _args = new CmdLineArgs(app);
            //return _args;
        }
    }
}
