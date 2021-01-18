using System;
using System.Diagnostics;
using System.Windows;
using ADOTabular.AdomdClientWrappers;
using Microsoft.Office.Tools.Ribbon;
using Serilog;
using System.IO;
using DaxStudio.Common;

namespace DaxStudio.ExcelAddin
{
    public partial class ThisAddIn
    {
        private static bool _inShutdown;
        private static DaxStudioLauncher _launcher;
        private bool _debugLogEnabled;
        private ILogger log;
        private void ThisAddInStartup(object sender, EventArgs e)
        {
            try
            {
                var loggingKeyDown = false;
                var currentDomain = AppDomain.CurrentDomain;
                currentDomain.AssemblyResolve += currentDomain_AssemblyResolve;
                currentDomain.UnhandledException += CurrentDomain_UnhandledException;

                var levelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Error);
                var config = new LoggerConfiguration()
                                    .ReadFrom.AppSettings()
                                    .MinimumLevel.ControlledBy(levelSwitch); ;

                if (System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey1)
                    || System.Windows.Input.Keyboard.IsKeyDown(Constants.LoggingHotKey2))
                {
                    loggingKeyDown = true;
                    _debugLogEnabled = true;

                    // increase the default log level when the logging hot key is held down
                    levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Debug;
                    Log.Debug("Debug Logging Enabled");

                    // force logging to rolling log file (in case the app settings has switched this off)
                    var logPath = Path.Combine(ApplicationPaths.LogPath
                                                , Constants.ExcelLogFileName);
                    config.WriteTo.File(logPath, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Debug, retainedFileCountLimit: 10);
                    
                }
                log = config.CreateLogger();
                Log.Logger = log;
                Log.Information("============ Excel Add-in Startup =============");
                ExcelInfo.WriteToLog(this);
                if (loggingKeyDown)
                {
                    if (_ribbon != null) _ribbon.DebugLoggingEnabled = true;
                    Log.Information($"Logging enabled by {Constants.LoggingHotKeyName} Key at startup");
                }
                CreateRibbonObjects();
            } catch (Exception ex)
            {
                CrashReporter.ReportCrash(ex,"DAX Studio Excel Addin fatal startup exception");
            }
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            Log.Fatal(ex, "{class} {method} {message} {stacktrace}", "ThisAddin", "CurrentDomain_UnhandledException", ex.Message, ex.StackTrace);
            CrashReporter.ReportCrash(ex, "DAX Studio Excel Addin - ApplicationDomain.UnhandledException");
        }

        private DaxStudioRibbon _ribbon;
        
        protected override IRibbonExtension[] CreateRibbonObjects()
        {
            if (_ribbon == null)
            {
                _ribbon = new DaxStudioRibbon();
#if DEBUG
                _ribbon.group1.Label = "Debug";
#endif
                _ribbon.btnDax.Tag = _debugLogEnabled;
                _ribbon.DebugLoggingEnabled = _debugLogEnabled;
            }
            return new IRibbonExtension[] {this._ribbon};

            //return base.CreateRibbonObjects();
        }

        //the Microsoft.Excel.AdomdClient.dll used for Excel Data Models in Excel 15 isn't in any of the paths .NET looks for assemblies in... so we have to catch the AssemblyResolve event and manually load that assembly
        //private static AdomdClientWrappers.ExcelAdoMdConnections _helper = new AdomdClientWrappers.ExcelAdoMdConnections();
        static System.Reflection.Assembly currentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                Log.Verbose("{Class} {Method} {args_Name} {args_RequestingAssembly}", "ThisAddIn", "currentDomain_AssemblyResolve", args.Name, args.RequestingAssembly);
                System.Diagnostics.Debug.WriteLine("AssemblyResolve: " + args.Name);
                if (args.Name.Contains("Microsoft.Excel.AdomdClient,"))
                {
                    var ass  = ExcelAdoMdConnections.ExcelAdomdClientAssembly;
                    Log.Verbose("{class} {method} {assembly}", "ThisAddin", "currentDomain_AssemblyResolve", "Microsoft.Excel.AdomdClient Resolved");
                    return ass;
                }

                if (args.Name.Contains("Microsoft.Excel.Amo,"))
                {
                    var ass = DaxStudio.ExcelAddin.Xmla.ExcelAmoWrapper.ExcelAmoAssembly;
                    Log.Verbose("{class} {method} {assembly}", "ThisAddin", "currentDomain_AssemblyResolve", "Microsoft.Excel.Amo Resolved");
                    return ass;
                }



                return null;
            }
            catch (Exception ex)
            {
                if (!_inShutdown)
                {
                    Log.Error("{class} {method} {args_Name} {ex_Message}","ThisAddIn","currentDomain_AssemblyResolve",args.Name,ex.Message + "\n" + ex.StackTrace);
                    
                    MessageBox.Show(
                        string.Format("Problem during AssemblyResolve in Dax Studio\r\nFor Assembly {0} :\r\n{1}\r\n{2} "
                            , args.Name
                            , ex.Message 
                            , ex.StackTrace),
                        "Dax Studio");
                }
                return null;
            }
        }
		

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            Log.Information("============ Excel Add-in Shutdown =============");
            try
            {
                _inShutdown = true;
                
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} Error: {message}", "ThisAddin", "ThisAddin_Shutdown", ex.Message);
                Debug.WriteLine(ex.Message);
            }
            
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            Startup += ThisAddInStartup;
            Shutdown += ThisAddIn_Shutdown;
        }
        
        #endregion

        protected override object RequestComAddInAutomationService()
        {
            if (_launcher == null)
                _launcher = new DaxStudioLauncher(this._ribbon);
            return _launcher;
        }
    }
}
