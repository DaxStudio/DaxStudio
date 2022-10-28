using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using DaxStudio.Common.Extensions;

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
                else
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
                else
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
                else
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
                else
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
                else
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
                else
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
                else
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
                else
                    _app.Properties.Add(AppProperties.ShowHelp, value);
            }
        }

        public bool Reset
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.Reset))
                    return (bool)_app.Properties[AppProperties.Reset];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.Reset))
                    _app.Properties[AppProperties.Reset] = value;
                else
                    _app.Properties.Add(AppProperties.Reset, value);
            }
        }

        public bool NoPreview
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.NoPreview))
                    return (bool)_app.Properties[AppProperties.NoPreview];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.NoPreview))
                    _app.Properties[AppProperties.NoPreview] = value;
                else
                    _app.Properties.Add(AppProperties.NoPreview, value);

            }
        }

        public string Query
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.Query))
                    return (string)_app.Properties[AppProperties.Query];
                return string.Empty;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.Query))
                    _app.Properties[AppProperties.Query] = value;
                else
                    _app.Properties.Add(AppProperties.Query, value);

            }
        }

        public bool FromUri
        {
            get
            {
                if (_app.Properties.Contains(AppProperties.FromUri))
                    return (bool)_app.Properties[AppProperties.FromUri];
                return false;
            }
            set
            {
                if (_app.Properties.Contains(AppProperties.FromUri))
                    _app.Properties[AppProperties.FromUri] = value;
                else
                    _app.Properties.Add(AppProperties.FromUri, value);
            }
        }

        public static void ParseUri(ref Application app, string input)
        {
            var uri = new Uri(input);
            var args = app.Args();
            args.FromUri = true;
            Type type = args.GetType();
            NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);
            var keys = app.Args().AsDictionary().Keys;
            // map the URI query parameters to commandline parameters
            foreach (var key in keys)
            {
                var value = queryParams[key];
                if (value != null)
                {
                    PropertyInfo prop = type.GetProperty(key);
                    var val = Convert.ChangeType(value, prop.PropertyType);
                    if (string.Equals(key, "Query", StringComparison.OrdinalIgnoreCase))
                    {
                        val = ((string)val).Base64Decode();
                    }
                    prop.SetValue(args, val, null);

                }
            }
        }

        public void Clear()
        {
            _app.Properties.Clear();
        }
    }
}
