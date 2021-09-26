using System;
using System.Reflection;
using System.Windows;
using DaxStudio.UI;
using Serilog;
using DaxStudio.UI.Utils;
using Fclp;
using DaxStudio.Common;
using System.Windows.Controls;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Views;
using Serilog.Core;
using Constants = DaxStudio.Common.Constants;
using System.Text;
using System.Collections.Generic;

namespace DaxStudio.Standalone
{
    public static class EntryPoint 
    {
        private static ILogger _log;
        private static  IEventAggregator _eventAggregator;
        private static IGlobalOptions _options;
        // need to create application first
        private static readonly Application App = new Application();
        static EntryPoint()
        {

            
        }

        
        private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            //Log.Debug("Class {0} Method {1} RequestingAssembly: {2} Name: {3}", "EntryPoint", "ResolveAssembly", args.RequestingAssembly, args.Name);
            System.Diagnostics.Debug.WriteLine($"ReqAss: {args.RequestingAssembly}, Name{args.Name}");
            if (args.Name.StartsWith("Microsoft.AnalysisServices", StringComparison.InvariantCultureIgnoreCase)) return SsasAssemblyResolver.Instance.Resolve(args.Name);
            return null;
        }
        
        
        // All WPF applications should execute on a single-threaded apartment (STA) thread
        [STAThread]
        public static void Main()
        {
            
            // add unhandled exception handler
            App.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            ConsoleHandler.RedirectToParent();

            // Setup logging, default to information level to start with to log the startup and key system information
            var levelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);

            ConfigureLogging(levelSwitch);
            Log.Information("============ DaxStudio Startup =============");

            // Default web requests like AAD Auth to use windows credentials for proxy auth
            System.Net.WebRequest.DefaultWebProxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            // add the custom DAX Studio accent color theme
            App.AddDaxStudioAccentColor();

            // TODO - do we need to customize the navigator window to fix the styling?
            //app.AddResourceDictionary("pack://application:,,,/DaxStudio.UI;Component/Resources/Styles/AvalonDock.NavigatorWindow.xaml");

            // then load Caliburn Micro bootstrapper
            Log.Debug("Loading Caliburn.Micro bootstrapper");
            AppBootstrapper bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)), true);

            _eventAggregator = bootstrapper.GetEventAggregator();
            // read command line arguments
            App.ReadCommandLineArgs();

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

                

            // force control tooltips to display even if disabled
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                typeof(Control),
                new FrameworkPropertyMetadata(true));

            // get the global options
            _options = bootstrapper.GetOptions(); 
            _options.Initialize();
            _options.LoggingLevelSwitch = levelSwitch;
            Log.Information("User Options initialized");

            UpdateLoggingLevelFromOptions(_options, ref levelSwitch);

            // check if we are running portable that we have write access to the settings
            if (_options.IsRunningPortable)
                if (CanWriteToSettings())
                {
                    Log.Information(Constants.LogMessageTemplate, nameof(EntryPoint), nameof(Main), "Test for read/write access to Settings.json: PASS");
                }
                else
                {
                    Log.Error(Constants.LogMessageTemplate, nameof(EntryPoint),nameof(Main),"Test for read/write access to Settings.json: FAIL");

                    ShowSettingPermissionErrorDialog();
                    App.Shutdown(3);
                    return; 
                }

            // load selected theme
            var themeManager = bootstrapper.GetThemeManager();
            themeManager.SetTheme(_options.Theme);
            Log.Information("ThemeManager configured");

            //var theme = options.Theme;// "Light"; 
            //if (theme == "Dark") app.LoadDarkTheme();
            //else app.LoadLightTheme();

            // log startup switches
            if (_options.AnyExternalAccessAllowed())
            {
                var args = App.Args().AsDictionaryForTelemetry();
                Telemetry.TrackEvent("App.Startup", args);
            }

            // only used for testing of crash reporting UI
            if (App.Args().TriggerCrashTest) throw new ArgumentException("Test Exception triggered by command line argument");

            if (!App.Args().ShowHelp)
            {
                // Launch the User Interface
                Log.Information("Launching User Interface");
                bootstrapper.DisplayShell();
                App.Run();
            }
            
            Log.Information("============ DaxStudio Shutdown =============");
            Log.CloseAndFlush();
            
        }

        private static void UpdateLoggingLevelFromOptions(IGlobalOptions options, ref LoggingLevelSwitch levelSwitch)
        {
            if (options.LoggingLevel == levelSwitch.MinimumLevel) return;
            Log.Information(Constants.LogMessageTemplate, nameof(EntryPoint), nameof(UpdateLoggingLevelFromOptions), $"Setting Logging level to {options.LoggingLevel}");
            levelSwitch.MinimumLevel = options.LoggingLevel;
        }

        private static void ShowSettingPermissionErrorDialog()
        {
            var msg = "Write Access is denied on the settings.json file.\n\n" +
                      "When running in portable mode DAX Studio needs Read/Write access to the current folder.\n\n"+ 
                      "If you want to put the application in a protected location like 'c:\\Program Files' then you should use the installer.";

            MessageBox.Show( msg, "Fatal Startup Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            
        }

        private static void ConfigureLogging(LoggingLevelSwitch levelSwitch)
        {
            var config = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .MinimumLevel.ControlledBy(levelSwitch);

            var logPath = Path.Combine(ApplicationPaths.LogPath, Constants.StandaloneLogFileName);
            config.WriteTo.File(logPath
                , rollingInterval: RollingInterval.Day
                );

            _log = config.CreateLogger();
            Log.Logger = _log;

            // check if user is holding shift key down
            bool isLoggingKeyDown = (System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey1)
                                     || System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey2));

            App.Args().LoggingEnabledByHotKey = isLoggingKeyDown;

            var logCmdLineSwitch = App.Args().LoggingEnabled;

#if DEBUG
            Serilog.Debugging.SelfLog.Enable(Console.Out);
#endif
            // write basic information about the current PC to the log file
            SystemInfo.WriteToLog();

            if (isLoggingKeyDown) Log.Information($"Logging enabled due to {Constants.LoggingHotKeyName} key being held down");
            if (logCmdLineSwitch) Log.Information("Logging enabled by Excel Add-in");
            Log.Information("CommandLine Args: {args}", Environment.GetCommandLineArgs());
            Log.Information($"Portable Mode: {ApplicationPaths.IsInPortableMode}");

            // Set the default logging level
            if (isLoggingKeyDown || logCmdLineSwitch)
            {
#if DEBUG
                levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                Log.Debug("Verbose Logging Enabled");

#else
                levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                Log.Debug("Debug Logging Enabled");
#endif
            }
            else
            {
#if DEBUG
                levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Verbose;
                Log.Information($"{levelSwitch.MinimumLevel} Logging Enabled due to running in debug mode");
#else
                Log.Information("Changing minimum log event to Warning");
                levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Warning;
#endif
            }

        }

        private static bool CanWriteToSettings()
        {
            var settingProvider = IoC.Get<ISettingProvider>() ;
            var fileLocation = settingProvider.SettingsFile;

            try
            {
                // try to open the file in read/write access
                using (File.Open(fileLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    Log.Debug(Constants.LogMessageTemplate,nameof(EntryPoint), nameof(CanWriteToSettings), "Settings file opened for read/write access");

                    return true;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // ignored
            }

            return false;
        }

        private static bool IsNotSet(object value)
        {
            switch (value)
            {
                case string s: return string.IsNullOrEmpty(s);
                case bool b: return b == false;
                case int i: return i == 0;
            }
            return false;
        }

        private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var msg = "DAX Studio Standalone TaskSchedulerOnUnobservedException";
            //e.Exception.InnerExceptions
            e.SetObserved();
            LogFatalCrash(e.Exception, msg, _options);
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = "DAX Studio Standalone CurrentDomainOnUnhandledException";
            Exception ex = e.ExceptionObject as Exception;   
            LogFatalCrash(ex, msg, _options);
            if (App.Dispatcher.CheckAccess())
            {
                App.Shutdown(2);
            }
            else
            {
                App.Dispatcher.Invoke(() => App.Shutdown(2));

            }
        }

        private static void LogFatalCrash(Exception ex, string msg, IGlobalOptions options)
        {
            // add a property to the application indicating that we have crashed
            if (!App.Properties.Contains("HasCrashed"))
                App.Properties.Add("HasCrashed", true);

            UpdateErrorForLoaderExceptions(ref msg, ex);

            Log.Error(ex, "{class} {method} {message}", nameof(EntryPoint), nameof(LogFatalCrash), msg);

            if (_options.BlockCrashReporting)
            {
                Application.Current.Dispatcher.Invoke(()=>{
                    // Show a dialog to let the user know there was a fatal crash
                    // but we are unable to automatically log the crash due to their privacy settings
                    var blockedDlg = new CrashReportingBlockedDialogView {ErrorMessage = {Text = msg}};
                    blockedDlg.ShowDialog();
                });

                return;
            }

            Execute.OnUIThread(() => { 
                // add a property to the application indicating that we have crashed
                if (!App.Properties.Contains("HasCrashed"))
                    App.Properties.Add("HasCrashed", true);

                Log.Error(ex, "{class} {method} {message}", nameof(EntryPoint), nameof(LogFatalCrash), msg);
                Log.CloseAndFlush();
                if (Application.Current.Dispatcher.CheckAccess())
                {
                    CrashReporter.ReportCrash(ex, msg);
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() => CrashReporter.ReportCrash(ex, msg));
                }
            });
        }

        private static void UpdateErrorForLoaderExceptions(ref string msg, Exception ex)
        {
            // if this is a type load exception we need to list out the LoaderException messages.
            if (ex is ReflectionTypeLoadException loaderEx)
            {

                var loaderExceptions = loaderEx.LoaderExceptions;
                var sbError = new StringBuilder();
                foreach (var innerEx in loaderEx.LoaderExceptions)
                {
                    sbError.AppendLine(innerEx.Message);
                }
                msg += '\n' +  sbError.ToString();
            }
        }

        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {


            if (e.Exception is System.Runtime.InteropServices.COMException comException)
            {
                
                switch (comException.ErrorCode)
                {
                    case -2147221037: // Data on clipboard is invalid (Exception from HRESULT: 0x800401D3 (CLIPBRD_E_BAD_DATA))
                        e.Handled = true;
                        _eventAggregator?.PublishOnUIThread(new OutputMessage(MessageType.Warning, "CLIPBRD_E_BAD_DATA Error - Clipboard operation failed, please try again"));
                        _log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "CLIPBRD_E_BAD_DATA");
                        return;
                    case -2147221040: // catch 0x800401D0 (CLIPBRD_E_CANT_OPEN) errors when wpf DataGrid can't access clipboard 
                        e.Handled = true;
                        _eventAggregator?.PublishOnUIThread(new OutputMessage(MessageType.Warning, "CLIPBRD_E_CANT_OPEN Error - Clipboard operation failed, please try again"));
                        _log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "CLIPBRD_E_CANT_OPEN");
                        return;
                    case unchecked((int)0x8001010E)://2147549454): // 0x_8001_010E:
                        e.Handled = true;
                        _eventAggregator?.PublishOnUIThread(new OutputMessage(MessageType.Warning, "RPC_E_WRONG_THREAD Error - Clipboard operation failed, please try again"));
                        _log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "RPC_E_WRONG_THREAD");
                        return;
                    default:
                        Log.Fatal(e.Exception, "{class} {method} Unhandled exception", "EntryPoint", "App_DispatcherUnhandledException");
                        LogFatalCrash(e.Exception, "DAX Studio Standalone DispatcherUnhandledException Unhandled COM Exception",_options);
                        e.Handled = true;
                        
                        Application.Current.Shutdown(1);
                        break;
                }

            }
            else
            {
                LogFatalCrash(e.Exception, "DAX Studio Standalone App_DispatcherUnhandledException crash",_options);
                e.Handled = true;
                App?.Shutdown(3);
            }
        }

        private static void ReadCommandLineArgs(this Application app)
        {
            string[] args = Environment.GetCommandLineArgs();

            var p = new FluentCommandLineParser();
            p.Setup<int>('p', "port")
                .Callback(port => app.Args().Port = port);

            p.Setup<bool>('l', "log")
                .Callback(log => app.Args().LoggingEnabledByCommandLine = log)
                .WithDescription("Enable Debug Logging")
                .SetDefault(false);

            p.Setup<string>('f', "file")
                .Callback(file => app.Args().FileName = file)
                .WithDescription("Name of file to open");
#if DEBUG
            // only include the crashtest parameter on debug builds
            p.Setup<bool>('c', "crashtest")
                .Callback(crashTest => app.Args().TriggerCrashTest = crashTest)
                .SetDefault(false);
#endif
            p.Setup<string>('s', "server")
                .Callback(server => app.Args().Server = server)
                .WithDescription("Server to connect to");

            p.Setup<string>('d', "database")
                .Callback(database => app.Args().Database = database)
                .WithDescription("Database to connect to");

            p.SetupHelp("?", "help")
                .Callback(text => {
                    Log.Information(Constants.LogMessageTemplate, nameof(EntryPoint), nameof(ReadCommandLineArgs), "Printing CommandLine Help");
                    Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                    string formattedHelp = HelpFormatter.Format(p.Options);
                    Console.WriteLine("");
                    Console.WriteLine($"DAX Studio { ver.ToString(3) } (build { ver.Revision })");
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
                    app.Args().ShowHelp = true;
                });

            p.Parse(args);
            
        }


        private static void AddResourceDictionary(this Application app, string src)
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(src, UriKind.RelativeOrAbsolute) });
        }

    }
}
