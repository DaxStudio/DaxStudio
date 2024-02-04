using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using DaxStudio.Common.Extensions;
using Fclp;
using Serilog;

namespace DaxStudio.Common
{
    public class CmdLineArgs
    {

        private HybridDictionary _argDict; 
        public CmdLineArgs(IDictionary dict)
        {
            _argDict = (HybridDictionary)dict;
        }

        public  int Port
        {
            get {
                if (_argDict.Contains(AppProperties.PortNumber))
                    return (int)_argDict[AppProperties.PortNumber];
                return 0;
            }
            set
            {
                if (_argDict.Contains(AppProperties.PortNumber))
                    _argDict[AppProperties.PortNumber] = value;
                else
                    _argDict.Add(AppProperties.PortNumber, value);
            }
        }

        public string FileName
        {
            get
            {
                if (_argDict.Contains(AppProperties.FileName))
                    return (string)_argDict[AppProperties.FileName];
                return string.Empty;
            }
            set
            {
                if (_argDict.Contains(AppProperties.FileName))
                    _argDict[AppProperties.FileName] = value;
                else
                    _argDict.Add(AppProperties.FileName, value);
            }
        }

        public bool LoggingEnabledByCommandLine
        {
            get
            {
                if (_argDict.Contains(AppProperties.LoggingEnabledByCommandLine))
                    return (bool)_argDict[AppProperties.LoggingEnabledByCommandLine];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.LoggingEnabledByCommandLine))
                    _argDict[AppProperties.LoggingEnabledByCommandLine] = value;
                else
                    _argDict.Add(AppProperties.LoggingEnabledByCommandLine, value);
            }
        }

        public bool LoggingEnabledByHotKey
        {
            get
            {
                if (_argDict.Contains(AppProperties.LoggingEnabledByHotKey))
                    return (bool)_argDict[AppProperties.LoggingEnabledByHotKey];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.LoggingEnabledByHotKey))
                    _argDict[AppProperties.LoggingEnabledByHotKey] = value;
                else
                    _argDict.Add(AppProperties.LoggingEnabledByHotKey, value);
            }
        }

        public bool LoggingEnabled { get {
                return LoggingEnabledByCommandLine || LoggingEnabledByHotKey;
            }
        }

        public bool TriggerCrashTest {
            get
            {
                if (_argDict.Contains(AppProperties.CrashTest))
                    return (bool)_argDict[AppProperties.CrashTest];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.CrashTest))
                    _argDict[AppProperties.CrashTest] = value;
                else
                    _argDict.Add(AppProperties.CrashTest, value);
            }
        }

        public string Server
        {
            get
            {
                if (_argDict.Contains(AppProperties.Server))
                    return (string)_argDict[AppProperties.Server];
                return string.Empty;
            }
            set
            {
                if (_argDict.Contains(AppProperties.Server))
                    _argDict[AppProperties.Server] = value;
                else
                    _argDict.Add(AppProperties.Server, value);
            }
        }
        public string Database
        {
            get
            {
                if (_argDict.Contains(AppProperties.Database))
                    return (string)_argDict[AppProperties.Database];
                return string.Empty;
            }
            set
            {
                if (_argDict.Contains(AppProperties.Database))
                    _argDict[AppProperties.Database] = value;
                else
                    _argDict.Add(AppProperties.Database, value);
            }
        }


        public bool ShowHelp
        {
            get
            {
                if (_argDict.Contains(AppProperties.ShowHelp))
                    return (bool)_argDict[AppProperties.ShowHelp];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.ShowHelp))
                    _argDict[AppProperties.ShowHelp] = value;
                else
                    _argDict.Add(AppProperties.ShowHelp, value);
            }
        }

        public bool Reset
        {
            get
            {
                if (_argDict.Contains(AppProperties.Reset))
                    return (bool)_argDict[AppProperties.Reset];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.Reset))
                    _argDict[AppProperties.Reset] = value;
                else
                    _argDict.Add(AppProperties.Reset, value);
            }
        }

        public bool NoPreview
        {
            get
            {
                if (_argDict.Contains(AppProperties.NoPreview))
                    return (bool)_argDict[AppProperties.NoPreview];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.NoPreview))
                    _argDict[AppProperties.NoPreview] = value;
                else
                    _argDict.Add(AppProperties.NoPreview, value);

            }
        }

        public string Query
        {
            get
            {
                if (_argDict.Contains(AppProperties.Query))
                    return (string)_argDict[AppProperties.Query];
                return string.Empty;
            }
            set
            {
                if (_argDict.Contains(AppProperties.Query))
                    _argDict[AppProperties.Query] = value;
                else
                    _argDict.Add(AppProperties.Query, value);

            }
        }

        public bool FromUri
        {
            get
            {
                if (_argDict.Contains(AppProperties.FromUri))
                    return (bool)_argDict[AppProperties.FromUri];
                return false;
            }
            set
            {
                if (_argDict.Contains(AppProperties.FromUri))
                    _argDict[AppProperties.FromUri] = value;
                else
                    _argDict.Add(AppProperties.FromUri, value);
            }
        }

        public void ParseUri(string input)
        {
            var uri = new Uri(input);
            
            this.FromUri = true;
            Type type = this.GetType();
            NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);
            var dict = this.AsDictionary();
            // map the URI query parameters to commandline parameters
            foreach (var key in dict.Keys)
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
                    prop.SetValue(this, val, null);

                }
            }
        }

        public void Parse(string[] args)
        {

            var p = new FluentCommandLineParser();
            p.Setup<int>('p', "port")
                .Callback(port => this.Port = port);

            p.Setup<bool>('l', "log")
                .Callback(log => this.LoggingEnabledByCommandLine = log)
                .WithDescription("Enable Debug Logging")
                .SetDefault(false);

            p.Setup<string>('f', "file")
                .Callback(file => this.FileName = file)
                .WithDescription("Name of file to open");
#if DEBUG
            // only include the crashtest parameter on debug builds
            p.Setup<bool>('c', "crashtest")
                .Callback(crashTest => this.TriggerCrashTest = crashTest)
                .SetDefault(false);
#endif
            p.Setup<string>('s', "server")
                .Callback(server => this.Server = server)
                .WithDescription("Server to connect to");

            p.Setup<string>('d', "database")
                .Callback(database => this.Database = database)
                .WithDescription("Database to connect to");

            p.Setup<bool>('r', "reset")
                .Callback(reset => this.Reset = reset)
                .WithDescription("Reset user preferences to the default settings");

            p.Setup<bool>("nopreview")
                .Callback(nopreview => this.NoPreview = nopreview)
                .WithDescription("Hides version information");

            p.Setup<string>('u', "uri")
                .Callback(uri => this.ParseUri(uri))
                .WithDescription("used by the daxstudio:// uri handler");


            p.SetupHelp("?", "help")
                .Callback(text =>
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(CmdLineArgs), nameof(Parse), "Printing CommandLine Help");
                    Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                    string formattedHelp = HelpFormatter.Format(p.Options);
                    Console.WriteLine("");
                    Console.WriteLine($"DAX Studio {ver.ToString(3)} (build {ver.Revision})");
                    Console.WriteLine("--------------------------------");
                    Console.WriteLine("");
                    Console.WriteLine("Supported command line parameters:");
                    Console.WriteLine(formattedHelp);
                    Console.WriteLine("");
                    Console.WriteLine("Note: parameters can either be passed with a short name or a long name form");
                    Console.WriteLine("eg.  DaxStudio -f myfile.dax");
                    Console.WriteLine("     DaxStudio --file myfile.dax");
                    Console.WriteLine("");
                    //app.Args().HelpText = text;
                    this.ShowHelp = true;
                });

            p.Parse(args);

        }
        public void Clear()
        {
            _argDict.Clear();
        }
    }
}
