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

namespace DaxStudio.Standalone
{
    public static class EntryPoint 
    {
        public static ILogger log;
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
                config.WriteTo.RollingFile(logPath
                        , retainedFileCountLimit: 10);

                log = config.CreateLogger();

                // need to create application first
                var app = new Application();
                // then load Caliburn Micro bootstrapper
                var bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)), true);

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
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                    Log.Debug("Debug Logging Enabled");
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
            catch (Exception ex)
            {
                Log.Fatal(ex, "Class: {0} Method: {1} Error: {2} Stack: {3}", "EntryPoint", "Main", ex.Message, ex.StackTrace);
#if DEBUG 
                MessageBox.Show(ex.Message, "DAX Studio Standalone unhandled exception");
#else
                // use CrashReporter.Net to send bug to DrDump
                CrashReporter.ReportCrash(ex,"DAX Studio Standalone Fatal startup crash" );
#endif

            }
            finally
            {
                Log.Information("============ DaxStudio Shutdown =============");
                Log.CloseAndFlush();
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

            //
            //int port;

            //for (int i = 1; i < args.Length;i++)
            //{
            //    if (args[i].ToLower() == "log" )
            //    {
            //        app.Properties.Add("LoggingEnabledByCommandLine", true);
            //    }
            //    if (int.TryParse( args[i], out port))
            //    {
            //        app.Properties.Add("Port", port);
            //    }
            //}
            
        }
    }
}
