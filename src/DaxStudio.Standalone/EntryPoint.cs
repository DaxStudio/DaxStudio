using System;
using System.Reflection;
using System.Windows;
using DaxStudio.UI;
using Serilog;
using DaxStudio.UI.Utils;
using System.IO;
using Fclp;
using DaxStudio.Common;
using System.Windows.Controls;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;

namespace DaxStudio.Standalone
{
    public static class EntryPoint 
    {
        public static ILogger log;
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
            System.Diagnostics.Debug.WriteLine(string.Format("ReqAss: {0}, Name{1}", args.RequestingAssembly, args.Name));
            if (args.Name.StartsWith("Microsoft.AnalysisServices")) return SsasAssemblyResolver.Instance.Resolve(args.Name);
            return null;
        }
        
        
        // All WPF applications should execute on a single-threaded apartment (STA) thread
        [STAThread]
        public static void Main()
        {
            try
            {
                // Setup logging
                var levelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Error);
                var config = new LoggerConfiguration()
                    .ReadFrom.AppSettings()
                    .MinimumLevel.ControlledBy(levelSwitch);

                var logPath = Path.Combine(Environment.ExpandEnvironmentVariables(Constants.LogFolder),
                                            Constants.StandaloneLogFileName);

                // if we have a local settings.json file we are running in "portable" mode
                if (JsonSettingProvider.SettingsFileExists())
                {
                    logPath = Path.Combine(JsonSettingProvider.LogPath, Constants.StandaloneLogFileName);
                }


                config.WriteTo.RollingFile(logPath
                        , retainedFileCountLimit: 10);

                log = config.CreateLogger();

                // need to create application first
                var app = new Application();
                //var app2 = IoC.Get<Application>();

                // add the custom DAX Studio accent color theme
                app.AddDaxStudioAccentColor();


                // load selected theme


                // TODO: Theme - read from settings

                var theme = "Light"; // settingProvider.GetValue<string>("Theme", "Light");
                if (theme == "Dark") app.LoadDarkTheme();
                else app.LoadLightTheme();
                

                // add unhandled exception handler
                app.DispatcherUnhandledException += App_DispatcherUnhandledException;

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

                //if (RegistryHelper.IsFileLoggingEnabled() || isLoggingKeyDown || logCmdLineSwitch)
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

                //RegistryHelper.IsFileLoggingEnabled();

#if DEBUG
                Serilog.Debugging.SelfLog.Enable(Console.Out);
#endif
                Log.Logger = log;
                Log.Information("============ DaxStudio Startup =============");
                //SsasAssemblyResolver.Instance.BuildAssemblyCache();
                SystemInfo.WriteToLog();
                if (isLoggingKeyDown) log.Information($"Logging enabled due to {Constants.LoggingHotKeyName} key being held down");
                if (logCmdLineSwitch) log.Information("Logging enabled by Excel Add-in");
                Log.Information("Startup Parameters Port: {Port} File: {FileName} LoggingEnabled: {LoggingEnabled}", app.Args().Port, app.Args().FileName, app.Args().LoggingEnabled);

                AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

                if (app.Args().TriggerCrashTest) throw new ArgumentException("Test Exception triggered by command line argument");

                // force control tooltips to display even if disabled
                ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                    typeof(Control),
                    new FrameworkPropertyMetadata(true));

                app.Run();
            }
            catch (ArgumentOutOfRangeException argEx)
            {
                var st = new System.Diagnostics.StackTrace(argEx);
                var sf = st.GetFrame(0);
                if (sf.GetMethod().Name == "GetLineByOffset")
                {
                    if (_eventAggregator != null) _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Editor syntax highlighting attempted to scan byond the end of the current line"));
                    log.Warning(argEx, "{class} {method} AvalonEdit TextDocument.GetLineByOffset: {message}", "EntryPoint", "Main", "Argument out of range exception");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Class: {0} Method: {1} Error: {2} Stack: {3}", "EntryPoint", "Main", ex.Message, ex.StackTrace);
#if DEBUG
                MessageBox.Show(ex.Message, "DAX Studio Standalone unhandled exception");
#else
                // use CrashReporter.Net to send bug to DrDump
                CrashReporter.ReportCrash(ex,"DAX Studio Standalone Fatal crash in Main() method" );
#endif

            }
            finally
            {
                Log.Information("============ DaxStudio Shutdown =============");
                Log.CloseAndFlush();
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
                        break;
                }

            }
            else
            {
                Log.Fatal(e.Exception, "{class} {method} Unhandled exception", "EntryPoint", "App_DispatcherUnhandledException");
                // use CrashReporter.Net to send bug to DrDump
                //CrashReporter.ReportCrash(e.Exception, "DAX Studio Standalone DispatcherUnhandledException crash");
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
                

            p.Parse(args);
            
        }

        
    }
}
