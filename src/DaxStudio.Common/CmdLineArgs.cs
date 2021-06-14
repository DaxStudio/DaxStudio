using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.Common
{
    public class CmdLineArgs
    {
        private Application _app;
        public CmdLineArgs(Application app)
        {
            _app = app;
        }
        public  int Port
        {
            get {
                if (_app.Properties.Contains(AppProperties.PortNumber))
                    return (int)_app.Properties[AppProperties.PortNumber];
                return 0;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.PortNumber))
                    _app.Properties[AppProperties.PortNumber] = value;
                _app.Properties.Add(AppProperties.PortNumber, value);
            }
        }

        public string FileName
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.FileName))
                    return (string)_app.Properties[AppProperties.FileName];
                return string.Empty;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.FileName))
                    _app.Properties[AppProperties.FileName] = value;
                _app.Properties.Add(AppProperties.FileName, value);
            }
        }

        public bool LoggingEnabledByCommandLine
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.LoggingEnabledByCommandLine))
                    return (bool)_app.Properties[AppProperties.LoggingEnabledByCommandLine];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.LoggingEnabledByCommandLine))
                    _app.Properties[AppProperties.LoggingEnabledByCommandLine] = value;
                _app.Properties.Add(AppProperties.LoggingEnabledByCommandLine, value);
            }
        }

        public bool LoggingEnabledByHotKey
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.LoggingEnabledByHotKey))
                    return (bool)_app.Properties[AppProperties.LoggingEnabledByHotKey];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.LoggingEnabledByHotKey))
                    _app.Properties[AppProperties.LoggingEnabledByHotKey] = value;
                _app.Properties.Add(AppProperties.LoggingEnabledByHotKey, value);
            }
        }

        public bool LoggingEnabled { get {
                return LoggingEnabledByCommandLine || LoggingEnabledByHotKey;
            }
        }

        public bool TriggerCrashTest {
            get
            {
                if (_app.Properties.Contains(AppProperties.CrashTest))
                    return (bool)_app.Properties[AppProperties.CrashTest];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.CrashTest))
                    _app.Properties[AppProperties.CrashTest] = value;
                _app.Properties.Add(AppProperties.CrashTest, value);
            }
        }

        public string Server
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.Server))
                    return (string)_app.Properties[AppProperties.Server];
                return string.Empty;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.Server))
                    _app.Properties[AppProperties.Server] = value;
                _app.Properties.Add(AppProperties.Server, value);
            }
        }
        public string Database
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.Database))
                    return (string)_app.Properties[AppProperties.Database];
                return string.Empty;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.Database))
                    _app.Properties[AppProperties.Database] = value;
                _app.Properties.Add(AppProperties.Database, value);
            }
        }


        public bool ShowHelp
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.ShowHelp))
                    return (bool)_app.Properties[AppProperties.ShowHelp];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.ShowHelp))
                    _app.Properties[AppProperties.ShowHelp] = value;
                _app.Properties.Add(AppProperties.ShowHelp, value);
            }
        }
    }
}
