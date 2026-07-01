using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using DaxStudio.Common.Cli;
using DaxStudio.Common.Extensions;
using Serilog;
using Spectre.Console.Cli;

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
            if (args == null) return;

            // Detect a help request up front so the launcher (EntryPoint.Main)
            // can skip starting the WPF UI. Spectre will still do the actual
            // help rendering via DaxStudioHelpProvider below.
            var helpRequested = IsHelpRequested(args);
            if (helpRequested)
            {
                this.ShowHelp = true;
                Log.Information(Constants.LogMessageTemplate, nameof(CmdLineArgs), nameof(Parse), "Printing CommandLine Help");
            }

            try
            {
                var app = new CommandApp<LaunchCommand>();
                app.Configure(config =>
                {
                    config.PropagateExceptions();
                    config.SetHelpProvider(new DaxStudioHelpProvider(config.Settings));
                });
                using (LaunchContext.Use(this))
                {
                    var normalized = NormalizeArgs(SkipFirstArgIfExecutablePath(args));
                    if (helpRequested)
                    {
                        // Force Spectre's help branch regardless of which help
                        // syntax the user typed (-?, /?, /help, -h, etc.).
                        normalized = new[] { "--help" };
                    }
                    app.Run(normalized);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(CmdLineArgs), nameof(Parse), "Failed to parse command line arguments");
            }
        }

        // Option names that the launcher recognises. Used by NormalizeArgs to
        // translate DOS-style /option syntax into Spectre's POSIX -o/--option
        // form without accidentally rewriting file paths.
        private static readonly HashSet<string> KnownShortOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "P", "L", "F", "S", "D", "R", "U",
#if DEBUG
            "C",
#endif
        };

        private static readonly HashSet<string> KnownLongOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PORT", "LOG", "FILE", "SERVER", "DATABASE", "RESET", "NOPREVIEW", "URI",
#if DEBUG
            "CRASHTEST",
#endif
        };

        private static string[] NormalizeArgs(string[] args)
        {
            if (args == null || args.Length == 0) return args;

            var result = new string[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                result[i] = NormalizeArg(args[i]);
            }
            return result;
        }

        private static string NormalizeArg(string arg)
        {
            if (string.IsNullOrEmpty(arg) || arg[0] == '-') return arg.ToLowerInvariant();
            if (string.IsNullOrEmpty(arg) || arg[0] != '/') return arg;

            // Split on the first '=' so /server=localhost also normalizes.
            var equalsIndex = arg.IndexOf('=');
            var name = equalsIndex > 0 ? arg.Substring(1, equalsIndex - 1).ToLowerInvariant() : arg.Substring(1).ToLowerInvariant();
            var tail = equalsIndex > 0 ? arg.Substring(equalsIndex) : string.Empty;

            // Only translate when the name matches a known option to avoid
            // mangling forward-slash file paths.
            if (KnownLongOptions.Contains(name))
            {
                return "--" + name + tail;
            }
            if (name.Length == 1 && KnownShortOptions.Contains(name))
            {
                return "-" + name + tail;
            }

            return arg;
        }

        private static readonly HashSet<string> HelpTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "-?", "/?", "--?",
            "-h", "/h", "--h",
            "-help", "/help", "--help",
        };

        /// <summary>
        /// Returns true when any of the supplied command-line tokens is a
        /// recognised help request (eg. -?, /?, /help, --help). Exposed so
        /// startup code can decide whether to attach a console window before
        /// the parser actually runs.
        /// </summary>
        public static bool IsHelpRequested(string[] args)
        {
            if (args == null) return false;
            return args.Any(a => a != null && HelpTokens.Contains(a));
        }

        private static string[] SkipFirstArgIfExecutablePath(string[] args)
        {
            // Environment.GetCommandLineArgs() includes the executable path as
            // the first element; Spectre.Console.Cli expects raw arguments only.
            if (args.Length == 0) return args;
            var first = args[0];
            if (!string.IsNullOrEmpty(first)
                && (first.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || first.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                return args.Skip(1).ToArray();
            }
            return args;
        }

        public void Clear()
        {
            _argDict.Clear();
        }
    }
}
