using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using Caliburn.Micro;
using DaxStudio.UI.Utils;
using System.Reflection;
using DaxStudio.CommandLine.Infrastructure;


namespace DaxStudio.CommandLine.Commands
{
    internal class VpaxCommand : Command<VpaxCommand.Settings>
    {
        public IEventAggregator EventAggregator { get; }

        // todo - cannot pass in connectionstring as vpax library does not support it
        internal class Settings : CommandSettingsFileBase
        {

            [CommandOption("-i|--includeTom")]
            [Description("Setting this flag will include a .bim file inside the vpax file (which just contains additional metadata)")]
            public bool IncludeTom { get; set; }
            [CommandOption("-r|--readstatsfromdata")]
            public bool ReadStatsFromData { get; set; }
            [CommandOption("-q|--readstatsfromdirectquery")]
            public bool ReadStatsFromDirectQuery { get; set; }
        }
        
        public VpaxCommand(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public ValidationResult Validate(CommandContext context, CommandSettings settings)
        {
            return ValidationResult.Success();
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            VersionInfo.Output();
            AnsiConsole.MarkupLine("Starting VPAX command");
            Log.Information("Starting VPAX command");
            AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .Start("Generating VPAX file...", ctx =>
                {
                    var appVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();

                    if (string.IsNullOrEmpty(settings.ConnectionString)) {
                        ModelAnalyzer.ExportVPAX(settings.Server, settings.Database, settings.OutputFile, settings.IncludeTom, "DAX Studio Command Line", appVersion, settings.ReadStatsFromData, "Model", settings.ReadStatsFromDirectQuery);
                    }
                    else {
                        ModelAnalyzer.ExportVPAX(settings.ConnectionString, settings.OutputFile, settings.IncludeTom, "DAX Studio Command Line", appVersion, settings.ReadStatsFromData, "Model", settings.ReadStatsFromDirectQuery);
                    }
                    // Omitted
                    //ctx.Refresh();
                });
            AnsiConsole.MarkupLine("[green]Done![/]");
            Log.Information("Finished VPAX Command");
            return 0;
        }
    }
}
