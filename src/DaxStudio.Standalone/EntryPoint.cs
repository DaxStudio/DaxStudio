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
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using System.Threading.Tasks;
using System.IO;

namespace DaxStudio.Standalone
{
    public static class EntryPoint 
    {
        private static ILogger log;
        private static  IEventAggregator _eventAggregator;

        static EntryPoint()
        {
//            log = new LoggerConfiguration().ReadAppSettings().CreateLogger();
            
//            //log = new LoggerConfiguration().WriteTo.Loggly().CreateLogger();
//#if DEBUG
//            Serilog.Debugging.SelfLog.Out =  Console.Out;
//#endif
//            Log.Logger = log;
//            Log.Information("============ DaxStudio Startup =============");
//            //AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
            
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
            try
            {
                // need to create application first
                var app = new Application();

                // add unhandled exception handler
                app.DispatcherUnhandledException += App_DispatcherUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
                TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;
                
                // Setup logging
                var levelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Error);
                var config = new LoggerConfiguration()
                    .ReadFrom.AppSettings()
                    .MinimumLevel.ControlledBy(levelSwitch);

                var logPath = Path.Combine(ApplicationPaths.LogPath, Constants.StandaloneLogFileName);
                config.WriteTo.RollingFile(logPath
                        , retainedFileCountLimit: 10);

                log = config.CreateLogger();
                Log.Logger = log;

                // add the custom DAX Studio accent color theme
                app.AddDaxStudioAccentColor();

                // TODO - do we need to customize the navigator window to fix the styling?
                //app.AddResourceDictionary("pack://application:,,,/DaxStudio.UI;Component/Resources/Styles/AvalonDock.NavigatorWindow.xaml");
                
                // then load Caliburn Micro bootstrapper
                AppBootstrapper bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)), true);

                _eventAggregator = bootstrapper.GetEventAggregator();
                // read command line arguments
                app.ReadCommandLineArgs();

                // check if user is holding shift key down
                bool isLoggingKeyDown = (System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey1)
                                    || System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey2));

                app.Args().LoggingEnabledByHotKey = isLoggingKeyDown;

                var logCmdLineSwitch = app.Args().LoggingEnabled;

#if DEBUG
                levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
                Log.Debug("Information Logging Enabled due to running in debug mode");
#endif

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


#if DEBUG
                Serilog.Debugging.SelfLog.Enable(Console.Out);
#endif

                Log.Information("============ DaxStudio Startup =============");
                //SsasAssemblyResolver.Instance.BuildAssemblyCache();
                SystemInfo.WriteToLog();

                if (isLoggingKeyDown) Log.Information($"Logging enabled due to {Constants.LoggingHotKeyName} key being held down");
                if (logCmdLineSwitch) Log.Information("Logging enabled by Excel Add-in");
                Log.Information("Startup Parameters Port: {Port} File: {FileName} LoggingEnabled: {LoggingEnabled}", app.Args().Port, app.Args().FileName, app.Args().LoggingEnabled);

                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

                if (app.Args().TriggerCrashTest) throw new ArgumentException("Test Exception triggered by command line argument");

                // force control tooltips to display even if disabled
                ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                    typeof(Control),
                    new FrameworkPropertyMetadata(true));

                // get the global options
                var options = bootstrapper.GetOptions(); 
                options.Initialize();


                // load selected theme
                var themeManager = bootstrapper.GetThemeManager();
                themeManager.SetTheme(options.Theme);

                //var theme = options.Theme;// "Light"; 
                //if (theme == "Dark") app.LoadDarkTheme();
                //else app.LoadLightTheme();

                // Launch the User Interface
                app.Run();
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                var st = new System.Diagnostics.StackTrace(argEx);
                var sf = st.GetFrame(0);
                if (sf.GetMethod().Name == "GetLineByOffset")
                {
                    if (_eventAggregator != null) _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Editor syntax highlighting attempted to scan byond the end of the current line"));
                    Log.Warning(argEx, "{class} {method} AvalonEdit TextDocument.GetLineByOffset: {message}", "EntryPoint", "Main", "Argument out of range exception");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Class: {0} Method: {1} Error: {2} Stack: {3}", "EntryPoint", "Main", ex.Message, ex.StackTrace);
                Log.CloseAndFlush();
//#if DEBUG
//                MessageBox.Show(ex.Message, "DAX Studio Standalone unhandled exception");
//#else
                // use CrashReporter.Net to send bug to DrDump
                CrashReporter.ReportCrash(ex,"DAX Studio Standalone Fatal crash in Main() method" );
//#endif

            }
            finally
            {
                Log.Information("============ DaxStudio Shutdown =============");
                Log.CloseAndFlush();
            }
        }

        private static void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var msg = "DAX Studio Standalone TaskSchedulerOnUnobservedException";
            //e.Exception.InnerExceptions
            LogFatalCrash(e.Exception, msg);
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = "DAX Studio Standalone CurrentDomainOnUnhandledException";
            Exception ex = e.ExceptionObject as Exception;   
            LogFatalCrash(ex, msg);
        }

        private static void LogFatalCrash(Exception ex, string msg)
        {
            Log.Fatal(ex, "{class} {method} {message}", nameof(EntryPoint), nameof(CurrentDomainOnUnhandledException), msg);
            if (Application.Current.Dispatcher.CheckAccess())
            {
                CrashReporter.ReportCrash(ex, msg);
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => CrashReporter.ReportCrash(ex, msg));
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
                        if (_eventAggregator != null) _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "CLIPBRD_E_BAD_DATA Error - Clipboard operation failed, please try again"));
                        log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "CLIPBRD_E_BAD_DATA");
                        return;
                    case -2147221040: // catch 0x800401D0 (CLIPBRD_E_CANT_OPEN) errors when wpf datagrid can't access clipboard 
                        e.Handled = true;
                        if (_eventAggregator != null) _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "CLIPBRD_E_CANT_OPEN Error - Clipboard operation failed, please try again"));
                        log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "CLIPBRD_E_CANT_OPEN");
                        return;
                    case unchecked((int)0x8001010E)://2147549454): // 0x_8001_010E:
                        e.Handled = true;
                        if (_eventAggregator != null) _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "RPC_E_WRONG_THREAD Error - Clipboard operation failed, please try again"));
                        log.Warning(e.Exception, "{class} {method} COM Error while accessing clipboard: {message}", "EntryPoint", "App_DispatcherUnhandledException", "RPC_E_WRONG_THREAD");
                        return;
                    default:
                        Log.Fatal(e.Exception, "{class} {method} Unhandled exception", "EntryPoint", "App_DispatcherUnhandledException");
                        CrashReporter.ReportCrash(e.Exception, "DAX Studio Standalone DispatcherUnhandledException Unhandled COM Exception");
                        e.Handled = true;
                        Application.Current.Shutdown(1);
                        break;
                }

            }
            else
            {
                Log.Fatal(e.Exception, "{class} {method} Unhandled exception", "EntryPoint", "App_DispatcherUnhandledException");
                // use CrashReporter.Net to send bug to DrDump
                CrashReporter.ReportCrash(e.Exception, "DAX Studio Standalone DispatcherUnhandledException crash");
                e.Handled = true;
                Application.Current?.Shutdown(1);
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

            p.Setup<bool>('c', "crashtest")
                .Callback(crashtest => app.Args().TriggerCrashTest = crashtest)
                .WithDescription("Used for testing the Crash Test reporting")
                .SetDefault(false);

            p.Setup<string>('s', "server")
                .Callback(server => app.Args().Server = server)
                .WithDescription("Server to connect to");

            p.Setup<string>('d', "database")
                .Callback(database => app.Args().Database = database)
                .WithDescription("Database to connect to");

            p.Parse(args);
            
        }

        private static void AddResourceDictionary(this Application app, string src)
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(src, UriKind.RelativeOrAbsolute) });
        }

    }
}
