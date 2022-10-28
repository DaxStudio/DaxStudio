using Serilog.Core;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows;
using DaxStudio.Common;
using constants = DaxStudio.Common.Constants;

namespace DaxStudio.Launcher
{
    public partial class Startup : Application
    {

        static string _appPath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().GetName().CodeBase), "daxstudio.exe");
        static ILogger _logger;
        static LoggingLevelSwitch _levelSwitch = new LoggingLevelSwitch();
        [STAThread]
        public static void Main(string[] args)
        {
            _levelSwitch.MinimumLevel = Serilog.Events.LogEventLevel.Information;
            ConfigureLogging(_levelSwitch);
            // find a daxstudio.exe process
            // send a wm_copydata message
            Process[] processes = Process.GetProcessesByName("daxstudio");
            if (processes.Length == 0)
            {
                // start a new daxstudio.exe process
                LaunchNewProcess(args);
            }
            else
            {
                // connect to the main app
                var cli = new NamedPipeClientStream(".", "DaxStudioIPC", PipeDirection.Out);
                try
                {
                    cli.Connect(200);
                    var bf = new BinaryFormatter();
                    // serialize and send the command line
                    bf.Serialize(cli, args);
                    cli.Close();
                }
                catch(TimeoutException)
                {
                    Log.Warning(constants.LogMessageTemplate, nameof(Startup), nameof(Main), "Timeout opening named pipe, launching new process");
                    LaunchNewProcess(args);
                }
                catch(Exception ex)
                {
                    Log.Error(ex, constants.LogMessageTemplate, nameof(Startup), nameof(Main), "Error communicating with existing daxstudio.exe process");
                    MessageBox.Show($"The following error occurred trying to open DAX Studio\n\n{ex.Message}", "DAX Studio Launcher");
                }
            }
        }

        private static void LaunchNewProcess(string[] args)
        {
            try
            {
                Log.Information(constants.LogMessageTemplate, nameof(Startup), nameof(LaunchNewProcess), $"Launching: {_appPath} Args: {args}");
                var startInfo = new ProcessStartInfo(_appPath, args.QuoteStringArgs());

                Process.Start(startInfo);
            }
            catch(Exception ex)
            {
                Log.Error(ex, constants.LogMessageTemplate, nameof(Startup), nameof(LaunchNewProcess), "Error starting daxstudio.exe process");
                MessageBox.Show($"The following error occurred trying to open DAX Studio\n\n{ex.Message}", "DAX Studio Launcher");
            }
        }

        private static void ConfigureLogging(LoggingLevelSwitch levelSwitch)
        {
            var config = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch);

            var logPath = Path.Combine(ApplicationPaths.LogPath, constants.LauncherLogFileName);
            config.WriteTo.File(logPath
                , rollingInterval: RollingInterval.Day
            );

            _logger = config.CreateLogger();
            Log.Logger = _logger;
        }
    }

    public static class StringExtensions
    { 
        public static string QuoteStringArgs(this string[] args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Contains(" "))
                {
                    // Quote the string being appended
                    sb.Append('\"');
                    sb.Append(args[i].Replace("\"", "\"\""));
                    sb.Append('\"');
                }
                else
                {
                    sb.Append(args[i]);
                }
                if (i < args.Length-1) sb.Append(' ');
            }
            return sb.ToString();
        }
    }
}
