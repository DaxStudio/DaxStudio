using System;
using System.Reflection;
using System.Windows;
using DaxStudio.UI;
using Serilog;
using DaxStudio.UI.Utils;
using DaxStudio.Common;
using System.Windows.Controls;
using Caliburn.Micro;
using DaxStudio.Core.Events;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System.Threading.Tasks;
using System.IO;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Views;
using Serilog.Core;
using Constants = DaxStudio.Common.Constants;
using System.Text;
using System.Windows.Media;
using System.Configuration;
using DaxStudio.Common.Extensions;
using System.IO.Pipes;
using System.Windows.Shell;
using System.ComponentModel;
using System.Globalization;

#if NET472
using Windows.Management.Update;
#endif
//using Microsoft.Identity.Client;

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
            // DaxStudio.exe is a WPF (Windows subsystem) executable, so it has no
            // console of its own. When the user is asking for help we attach to
            // the parent process's console so that Spectre's rendered help text
            // is visible. We only do this when help was requested so the normal
            // launch path stays clean.
            var startupArgs = Environment.GetCommandLineArgs();
            var helpRequested = CmdLineArgs.IsHelpRequested(startupArgs);
            if (helpRequested)
            {
                ConsoleHandler.RedirectToParent();
            }

            // add unhandled exception handler
            App.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

            //ConsoleHandler.RedirectToParent();

            // Setup logging, default to information level to start with to log the startup and key system information
            var levelSwitch = new Serilog.Core.LoggingLevelSwitch(Serilog.Events.LogEventLevel.Information);

            // Detect whether the Shift key was held down at startup to enable verbose logging.
            // This value is stored in App.Args() (backed by Application.Properties) but is cleared
            // again by ReadCommandLineArgs() below, so we capture it here and re-apply it afterwards.
            var isLoggingKeyDown = ConfigureLogging(levelSwitch);
            Log.Information("============ DaxStudio Startup =============");

            // check if the config file has been set to force software rendering
            // NOTE: the equivalent user option is applied further below, once the options
            //       have been loaded. ProcessRenderMode is a live preference (see
            //       SetSoftwareRendering) so the ordering here is for clarity, not correctness.
            bool.TryParse(ConfigurationManager.AppSettings["ForceSoftwareRendering"], out var forceSoftwareRendering);
            if (forceSoftwareRendering) SetSoftwareRendering("app.config ForceSoftwareRendering setting");

            // then load Caliburn Micro bootstrapper
            Log.Debug("Loading Caliburn.Micro bootstrapper");
            AppBootstrapper bootstrapper = new AppBootstrapper(Assembly.GetAssembly(typeof(DaxStudioHost)), true);

            _eventAggregator = bootstrapper.GetEventAggregator();
            // read command line arguments
            App.ReadCommandLineArgs(startupArgs);

            // ReadCommandLineArgs() clears App.Args() before parsing, which wipes the
            // LoggingEnabledByHotKey flag set during ConfigureLogging(). Re-apply it so that
            // IDaxStudioHost.DebugLogging (and the log level check below) reflect the Shift key.
            App.Args().LoggingEnabledByHotKey = isLoggingKeyDown;

            var settingProvider = IoC.Get<ISettingProvider>();
            if (App.Args().Reset) settingProvider.Reset();
                
            // force control tooltips to display even if disabled
            ToolTipService.ShowOnDisabledProperty.OverrideMetadata(
                typeof(Control),
                new FrameworkPropertyMetadata(true));

            // get the global options
            _options = bootstrapper.GetOptions(); 
            _options.Initialize();
            _options.LoggingLevelSwitch = levelSwitch;
            Log.Information("User Options initialized");

            // apply the user level software rendering option as early as possible so that we
            // never build a hardware render target we would only tear down again
            if (_options.ForceSoftwareRendering) SetSoftwareRendering("user option");

            // if the cmdline or hotkey have not been set then read the log level from the options
            if (!App.Args().LoggingEnabled) UpdateLoggingLevelFromOptions(_options, ref levelSwitch);

            // check if we are running portable that we have write access to the settings
            if (_options.IsRunningPortable)
                if (CanWriteToSettings(settingProvider))
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

            // log startup switches
            if (_options.AnyExternalAccessAllowed())
            {
                var appArgs = App.Args().AsDictionaryForTelemetry();
                Telemetry.TrackEvent("App.Startup", appArgs);
            }

            // only used for testing of crash reporting UI
            if (App.Args().TriggerCrashTest) throw new ArgumentException("Test Exception triggered by command line argument");

            if (!App.Args().ShowHelp)
            {
                // Default web requests like AAD Auth to use windows credentials for proxy auth
                // Queued at Background priority so the dispatcher processes it after the
                // first frame renders, avoiding a potential WPAD proxy auto-detection delay
                // blocking the UI from appearing.
                // Similarly we can configure the jumplist on a background thread rather than blocking the UI
                App.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    new System.Action(() =>
                    {
                        System.Net.WebRequest.DefaultWebProxy.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;
                        JumpListHelper.ConfigureJumpList(App);
                    }));

                // Launch the User Interface
                Log.Information("Launching User Interface");
                App.Run();
            }
            else if (helpRequested)
            {
                // When a WPF (windowed-subsystem) exe attaches to a parent
                // console the shell has already returned its prompt, so its
                // next prompt redraws on top of our last line. Posting an
                // Enter to the console window forces the shell to render a
                // fresh prompt on its own line after the help text.
                ConsoleHandler.PostEnterToParentConsole();
            }

            levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            Log.Information("============ DaxStudio Shutdown =============");
            Log.CloseAndFlush();
            
        }

        private static void UpdateLoggingLevelFromOptions(IGlobalOptions options, ref LoggingLevelSwitch levelSwitch)
        {
            if (options.LoggingLevel >= levelSwitch.MinimumLevel) return;
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

        private static bool ConfigureLogging(LoggingLevelSwitch levelSwitch)
        {
            var config = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .MinimumLevel.ControlledBy(levelSwitch);

            var logPath = Path.Combine(ApplicationPaths.LogPath, Constants.StandaloneLogFileName);
            config.WriteTo.File(logPath
                , rollingInterval: RollingInterval.Day
                , formatProvider: CultureInfo.InvariantCulture
                );
#if DEBUG
            // if we are debugging write to the log window
            config.WriteTo.DaxStudioOutput(formatProvider: CultureInfo.InvariantCulture);
#endif
            _log = config.CreateLogger();
            Log.Logger = _log;

            // check if user is holding shift key down
            bool isLoggingKeyDown = (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift)
                                     || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift));

            App.Args().LoggingEnabledByHotKey = isLoggingKeyDown;

            var logCmdLineSwitch = App.Args().LoggingEnabled;
            var isPreviewBuild = false;
#if PREVIEW
            isPreviewBuild = true;
#endif

#if DEBUG
            Serilog.Debugging.SelfLog.Enable(Console.Out);
#endif
            // write basic information about the current PC to the log file
            SystemInfo.WriteToLog();

            if (isLoggingKeyDown) Log.Information($"Verbose Logging enabled due to {Constants.LoggingHotKeyName} key being held down");
            if (isPreviewBuild) Log.Information($"Verbose Logging enabled due to being a PREVIEW build");
            if (logCmdLineSwitch) Log.Information("Verbose Logging enabled by Excel Add-in");
            Log.Information("CommandLine Args: {args}", Environment.GetCommandLineArgs());
            Log.Information($"Portable Mode: {ApplicationPaths.IsInPortableMode}");

            // Set the default logging level
            if (isLoggingKeyDown || logCmdLineSwitch || isPreviewBuild)
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

            return isLoggingKeyDown;
        }

        private static bool CanWriteToSettings(ISettingProvider settingProvider)
        {
            
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
            if (Application.Current?.Dispatcher?.HasShutdownStarted??true) return;
            e.SetObserved();
            Log.Error(e.Exception, "{class} {method} {message}", nameof(EntryPoint), nameof(TaskSchedulerOnUnobservedTaskException), "Unobserved task exception");
        }

        private static bool IsInvalidWindowHandleGetMessage(Exception ex)
        {
            const int ErrorInvalidWindowHandle = 1400;
            if (!(ex is Win32Exception win32Exception) || win32Exception.NativeErrorCode != ErrorInvalidWindowHandle)
                return false;

            var stackTrace = ex.StackTrace ?? string.Empty;
            return stackTrace.Contains("MS.Win32.UnsafeNativeMethods.GetMessageW", StringComparison.Ordinal)
                && stackTrace.Contains("System.Windows.Threading.Dispatcher.GetMessage", StringComparison.Ordinal);
        }

        private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = "DAX Studio Standalone CurrentDomainOnUnhandledException";
            Exception ex = e.ExceptionObject as Exception;

            if (ex == null)
            {
                App?.Shutdown(2);
                return;
            }

            if (IsInvalidWindowHandleGetMessage(ex))
            {
                Log.Warning(ex, "{class} {method} Ignoring known WPF invalid HWND exception while shutting down",
                    nameof(EntryPoint), nameof(CurrentDomainOnUnhandledException));
                App?.Shutdown(0);
                return;
            }

            if ((Application.Current?.Dispatcher?.HasShutdownStarted ?? false))
            {
                Log.Error(ex, nameof(EntryPoint), nameof(CurrentDomainOnUnhandledException), "Error during shutdown");
                App?.Shutdown(2);
                return;
            }

            // check that we are not already shutting down
            var stackTrace = ex.StackTrace ?? string.Empty;
            if (!stackTrace.Contains("System.Windows.Threading.Dispatcher.ShutdownImpl", StringComparison.Ordinal) 
                && !(Application.Current?.Dispatcher?.HasShutdownStarted??true))
                    LogFatalCrash(ex, msg, _options);
            
            if (App?.Dispatcher?.CheckAccess()??true)
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
            Log.CloseAndFlush();

            if (_options?.BlockCrashReporting??true)
            {
                Application.Current.Dispatcher.Invoke(()=>{
                    // Show a dialog to let the user know there was a fatal crash
                    // but we are unable to automatically log the crash due to their privacy settings
                    var blockedDlg = new CrashReportingBlockedDialogView {ErrorMessage = {Text = $"{ex.Message}\n\n{ex.StackTrace}"}};
                    blockedDlg.ShowDialog();
                });

                return;
            }

            Execute.OnUIThread(() => {

                // Application must be shutting down so just exit
                if (Application.Current == null || App == null) return;

                // add a property to the application indicating that we have crashed
                if (!App.Properties.Contains("HasCrashed"))
                    App.Properties.Add("HasCrashed", true);

                if ((Application.Current?.Dispatcher?.CheckAccess()??true) == true)
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

        private static int _inUnhandledExceptionHandler;

        private static void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // Reentrancy guard: if the handler itself (or any code it invokes) throws another
            // unhandled exception on the dispatcher, we must not recurse back into ourselves —
            // doing so risks stack overflow, hangs in the crash-reporter, or a runaway loop of
            // crash dialogs. Mark the exception handled and fail fast instead.
            if (System.Threading.Interlocked.Exchange(ref _inUnhandledExceptionHandler, 1) != 0)
            {
                try
                {
                    Log.Fatal(e.Exception, "{class} {method} Reentrant unhandled dispatcher exception — terminating immediately",
                        nameof(EntryPoint), nameof(App_DispatcherUnhandledException));
                }
                catch
                {
                    // intentionally swallowed — we're already in a fatal failure path
                }
                e.Handled = true;
                Environment.FailFast("DAX Studio: reentrant unhandled dispatcher exception", e.Exception);
                return;
            }

            try
            {
                if ((Application.Current?.Dispatcher?.HasShutdownStarted??false))
                {
                    Log.Error(e.Exception, nameof(EntryPoint), nameof(App_DispatcherUnhandledException), "Error during shutdown");
                    App?.Shutdown(3);
                    return;
                }

                var decision = UnhandledExceptionTriage.Default.Triage(e.Exception);

                if (decision != null && decision.IsRecoverable)
                {
                    e.Handled = true;

                    _log.Warning(e.Exception, "{class} {method} {message}", nameof(EntryPoint),
                        nameof(App_DispatcherUnhandledException), decision.LogMessage);

                    if (!string.IsNullOrEmpty(decision.UserMessage))
                        _eventAggregator?.PublishAsync(new OutputMessage(MessageType.Warning, decision.UserMessage));

                    return;
                }

                if (decision != null && decision.Action == UnhandledExceptionAction.FatalRenderThreadFailure)
                {
                    e.Handled = true;
                    HandleRenderThreadFailure(e.Exception, decision);
                    return;
                }

                if (e.Exception is System.Runtime.InteropServices.COMException)
                {
                    // an unrecognized COM exception - we don't know if the app is in a valid
                    // state so log a crash report and exit
                    Log.Fatal(e.Exception, "{class} {method} Unhandled exception", "EntryPoint", "App_DispatcherUnhandledException");
                    LogFatalCrash(e.Exception, "DAX Studio Standalone DispatcherUnhandledException Unhandled COM Exception", _options);
                    e.Handled = true;

                    Application.Current.Shutdown(1);
                }
                else
                {
                    LogFatalCrash(e.Exception, "DAX Studio Standalone App_DispatcherUnhandledException crash",_options);
                    e.Handled = true;
                    App?.Shutdown(3);
                }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _inUnhandledExceptionHandler, 0);
            }
        }

        /// <summary>
        /// Handles a fatal WPF render thread failure.
        /// <para>
        /// The composition partition is zombied and WPF has no way to reconnect it, so the UI is
        /// dead even though the process and dispatcher are still alive. We deliberately do NOT
        /// call Application.Shutdown() here: that runs the normal close-all path, which puts up a
        /// "save changes?" dialog (invisible on a dead render thread, so the process would just
        /// hang) and deletes the auto-save files. Instead we terminate the process hard, which
        /// leaves the auto-save index intact so the next launch offers to recover the user's
        /// queries.
        /// </para>
        /// </summary>
        private static void HandleRenderThreadFailure(Exception exception, UnhandledExceptionDecision decision)
        {
            var alreadySoftware = RenderOptions.ProcessRenderMode == System.Windows.Interop.RenderMode.SoftwareOnly;

            try
            {
                Log.Fatal(exception, Constants.LogMessageTemplate, nameof(EntryPoint),
                    nameof(HandleRenderThreadFailure), decision.LogMessage);
            }
            catch
            {
                // already in a fatal path - never let logging stop the shutdown
            }

            // The most effective mitigation (and Microsoft's first recommendation) is to stop
            // using the graphics hardware, so arm that for the next launch.
            if (!alreadySoftware) PersistSoftwareRenderingOption();

            try
            {
                var msg = alreadySoftware
                    ? "DAX Studio has to close because the Windows graphics components it uses to draw its screen have failed.\r\n\r\n" +
                      "DAX Studio is already running with hardware acceleration disabled, so this is unlikely to be a video driver problem. Installing the latest Windows updates may help.\r\n\r\n" +
                      "Any unsaved queries will be offered for recovery the next time DAX Studio starts."
                    : "DAX Studio has to close because the Windows graphics components it uses to draw its screen have failed.\r\n\r\n" +
                      "This is usually caused by a video driver problem. Hardware acceleration has been switched off, so the next time DAX Studio starts it will use software rendering. Updating your video driver is recommended.\r\n\r\n" +
                      "Any unsaved queries will be offered for recovery the next time DAX Studio starts.";

                // MessageBox is a thin wrapper over the Win32 message box so it does not depend
                // on the (now dead) WPF composition partition and will still be visible.
                MessageBox.Show(msg, "DAX Studio - Graphics Failure", MessageBoxButton.OK,
                    MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
            catch
            {
                // if we can't even show the message box just exit
            }

            // hard exit - see the remarks above for why Application.Shutdown() is not used
            Environment.Exit(4);
        }

        /// <summary>
        /// Switches WPF to software rendering.
        /// </summary>
        /// <remarks>
        /// Contrary to popular belief this is NOT a startup-only setting. The setter is a
        /// simple p/invoke to MilCore's RenderOptions_ForceSoftwareRenderingModeForProcess,
        /// which stores a mutable global. The compositor re-reads that flag on every render
        /// pass and, when it changes, calls UpdateRenderTargetFlags() which releases and
        /// rebuilds the existing HWND render targets. So it can safely be toggled at any
        /// point in the process lifetime. We still apply it as early as we can to avoid
        /// needlessly creating a hardware render target first.
        /// </remarks>
        private static void SetSoftwareRendering(string reason)
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            Log.Information(Constants.LogMessageTemplate, nameof(EntryPoint), nameof(SetSoftwareRendering),
                $"Hardware rendering disabled (ProcessRenderMode = SoftwareOnly) due to {reason}");
        }

        /// <summary>
        /// Persists the software rendering option so that the next launch starts without
        /// hardware acceleration.
        /// </summary>
        private static void PersistSoftwareRenderingOption()
        {
            try
            {
                if (_options != null && !_options.ForceSoftwareRendering)
                {
                    _options.ForceSoftwareRendering = true;
                    Log.Warning(Constants.LogMessageTemplate, nameof(EntryPoint), nameof(PersistSoftwareRenderingOption),
                        "WPF render-thread failure - enabling the ForceSoftwareRendering option for the next startup");
                }
            }
            catch (Exception ex)
            {
                // never let the recovery path itself take the app down
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(EntryPoint), nameof(PersistSoftwareRenderingOption),
                    "Unable to persist the ForceSoftwareRendering option");
            }
        }

        private static void ReadCommandLineArgs(this Application app, string[] args)
        {
            app.Args().Clear();
            Application.Current.Args().Parse(args);
        }


        private static void AddResourceDictionary(this Application app, string src)
        {
            app.Resources.MergedDictionaries.Add(new ResourceDictionary() { Source = new Uri(src, UriKind.RelativeOrAbsolute) });
        }

        private static void ProcessMessage(NamedPipeServerStream pipe, Application app)
        {
            var inargs = DaxStudio.Common.WMHelper.DeserializeStringArray(pipe);
            app.ReadCommandLineArgs(inargs);
            if (!string.IsNullOrEmpty(app.Args().FileName))
            {
                _eventAggregator.PublishAsync(new OpenDaxFileEvent(app.Args().FileName));
            }
            else
            {
                _eventAggregator.PublishAsync(new NewDocumentEvent(null));
            }
        }


    }
}
