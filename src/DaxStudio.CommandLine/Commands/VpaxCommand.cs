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

            [CommandOption("-t|--exncludeTom")]
            [Description("Setting this flag will exclude a .bim file inside the vpax file (which just contains additional metadata)")]
            public bool ExcludeTom { get; set; }
            [CommandOption("-r|--donotreadstatsfromdata")]
            [Description("Setting this flag will prevent the standard distinctcount queries that read the statistics from the data model")]
            public bool DoNotReadStatsFromData { get; set; }
            
            [CommandOption("-q|--readstatsfromdirectquery")]
            [Description("Setting this flag will force the execution of distinctcount queries that read the statistics from the data model (which is normally suppressed for Direct Query models)")]
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

                    
                    ModelAnalyzer.ExportVPAX(settings.FullConnectionString, settings.OutputFile, !settings.ExcludeTom, "DAX Studio Command Line", appVersion, !settings.DoNotReadStatsFromData, "Model", settings.ReadStatsFromDirectQuery);
                    
                    // Omitted
                    //ctx.Refresh();
                });
            AnsiConsole.MarkupLine("[green]Done![/]");
            Log.Information("Finished VPAX Command");
            return 0;
        }
    }
}
