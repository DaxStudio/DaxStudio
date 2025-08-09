using Caliburn.Micro;
using DaxStudio.CommandLine.Commands;
using DaxStudio.CommandLine.Extensions;
using DaxStudio.CommandLine.Help;
using DaxStudio.CommandLine.Infrastructure;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Common.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StringExtensions = DaxStudio.CommandLine.Extensions.StringExtensions;


namespace DaxStudio.CommandLine
{
    internal class Program
    {

        static IEventAggregator EventAggregator { get; set; } = new EventAggregator();
        static ConsoleLogger connLogger = new ConsoleLogger();
        static IGlobalOptions Options { get; set; }
        static async Task<int> Main(string[] args)
        {

            var settingProvider = SettingsProviderFactory.GetSettingProvider();
            Options = new OptionsViewModel(EventAggregator, settingProvider);
            Options.Initialize();

            // Create a type registrar and register any dependencies.
            // A type registrar is an adapter for a DI framework.
            var registrations = new ServiceCollection();
            registrations.AddSingleton<IEventAggregator, EventAggregator>();
            registrations.AddSingleton<IGlobalOptions, OptionsViewModel>();
            registrations.AddSingleton(typeof(ISettingProvider), settingProvider);
            var registrar = new TypeRegistrar(registrations);
            var verboseLogging = IsVerbose(args);
            ConfigureLogging(verboseLogging);
            EventAggregator.SubscribeOnPublishedThread(connLogger);

            //ParseArgs(args);
            var app = CreateCommands(registrar);
            try
            {
                var result = await app.RunAsync(args).ConfigureAwait(true);
                return result;
            }
            catch (AggregateException ex2)
            {
                Log.Error(ex2, "Error: {message}", ex2.GetAllMessages());

                return 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error: {message}",ex.Message);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

//#if DEBUG
//            Console.ReadKey();
//#endif
            
        }

        private static bool IsVerbose(string[] arr)
        {
            for (var idx = arr.Length - 1; idx >= 0; idx--)
            {
                var item = arr[idx];
                if (string.Compare(item, "-v", true) == 0 || string.Compare(item, "--verbose", true) == 0)
                {
                    StringExtensions.RemoveAt(ref arr, idx, 1);
                    return true;
                }
            }
            return false;
        }

        private static void ConfigureLogging(bool verboseLogging)
        {
            /*
            var config = new LoggerConfiguration();

            config.WriteTo.SpectreConsole(minLevel: Serilog.Events.LogEventLevel.Information);
            _log = config.CreateLogger();
   
            Log.Logger = _log;
            */
            var outputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}";
            LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();
            levelSwitch.MinimumLevel = verboseLogging ? LogEventLevel.Verbose : LogEventLevel.Information;
            if (verboseLogging) { outputTemplate = "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"; }

            Log.Logger = new LoggerConfiguration()
                        //.WriteTo.Spectre(outputTemplate, restrictedToMinimumLevel: LogEventLevel.Information)
                        //.WriteTo.Spectre(outputTemplate: outputTemplate, restrictedToMinimumLevel: verboseLogging ? LogEventLevel.Verbose : LogEventLevel.Information)
                        .WriteTo.Console(outputTemplate: outputTemplate, restrictedToMinimumLevel: verboseLogging ? LogEventLevel.Verbose : LogEventLevel.Information, theme: AnsiConsoleTheme.Code)
                        .MinimumLevel.ControlledBy(levelSwitch)
                        .CreateLogger();
            
            //Log.Information("Logger Initialized");
        }

        static CommandApp CreateCommands(TypeRegistrar registrar)
        {
            var app = new CommandApp(registrar);
            app.Configure(config =>
            {
                config.SetExceptionHandler((ex, resolver) =>
                {
                    Log.Error(ex, "Error: {message}", ex.GetInnerExceptionMessages());
                });

                config.SetHelpProvider(new CustomHelpProvider(config.Settings));

                config.AddBranch<CommandSettings>("export", export =>
                {
                    export.AddCommand<ExportSqlCommand>("sql")
                        .WithDescription("Exports specified tables to a SQL Server")
                        .WithExample(new[] { "export", "sql", "\"Data Source=localhost\\sql;Initial Catalog=DataDump;Integrated Security=SSPI\"", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"" })
                        .WithExample(new[] { "export", "sql", "\"Data Source=localhost\\sql;Initial Catalog=DataDump;Integrated Security=SSPI\"", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"", "-t", "Product \"Product Category\" \"Reseller Sales\"" })
                        ;

                    export.AddCommand<ExportCsvCommand>("csv")
                        .WithDescription("Exports specified tables to csv files in a folder")
                        .WithExample(new[] { "export", "csv", "c:\\temp\\export", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"" })
                        .WithExample(new[] { "export", "csv", "c:\\temp\\export", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"", "-t", "Product \"Product Category\" \"Reseller Sales\"" });
                });

            config.AddCommand<CsvCommand>("csv")
                .WithDescription("Writes query results out to a .csv file")
                .WithExample(new[] { "csv", "c:\\temp\\export\\myresults.csv", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"", "-q","\"EVALUATE 'Product Categories'\"" });

            config.AddCommand<XlsxCommand>("xlsx")
                .WithDescription("Writes query results out to an .xlsx file")
                .WithExample(new[] { "xlsx", "c:\\temp\\export\\myresults.xlsx" , "-s", "localhost\\tabular", "-d", "\"Adventure Works\"", "-q", "\"EVALUATE 'Product Categories'\""});

            config.AddCommand<VpaxCommand>("vpax")
                .WithDescription("Generates a vpax file")
                .WithExample(new[] { "vpax", "c:\\temp\\export\\model.vpax", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"" })
                .WithExample(new[] { "vpax", "c:\\temp\\export\\model.vpax", "-c", "\"Data Source=localhost\\tabular;Initial Catalog=Adventure Works\"" });

            config.AddCommand<AccessTokenCommand>("accesstoken")
                            .WithDescription("Returns an access token that can be used to run other commands without repeated authentication prompts")
                            .WithExample(new[] { "accesstoken", "-s", "asazure://australiasoutheast.asazure.windows.net/myserver", "-d", "\"Adventure Works\"" })
                            .WithExample(new[] { "accesstoken", "-c", "\"Data Source=asazure://australiasoutheast.asazure.windows.net/myserver;Initial Catalog=Adventure Works\"" });
#if DEBUG
                // Custom Trace
                config.AddCommand<CustomTraceCommand>("customtrace")
    .WithDescription("Starts a custom trace")
    .WithExample(new[] { "customtrace", "refresh trace", "c:\\temp\\refresh.json", "-s", "localhost\\tabular", "-d", "\"Adventure Works\"" })
    .WithExample(new[] { "customtrace", "refresh trace", "c:\\temp\\refresh.json", "-c", "\"Data Source=localhost\\tabular;Initial Catalog=Adventure Works\"" });


                // Capture Diagnostics
#endif
            });
            
            return app;
        }

        private static int OnException(Exception arg)
        {
            throw new NotImplementedException();
        }

        private static string[] FixArgs(string[] asEntered)
        {
            List<string> newArgs = new List<string>(asEntered);
            if ((newArgs[0].ToUpperInvariant() == "--HELP") ||
                (newArgs[0].ToUpperInvariant() == "-H"))
            {
                newArgs.RemoveAt(0);
                newArgs.Add("--help");
            }
            return newArgs.ToArray();
        }

    }
}
