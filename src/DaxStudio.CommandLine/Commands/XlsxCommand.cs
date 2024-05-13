using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.Model;
using Caliburn.Micro;
using DaxStudio.CommandLine.Infrastructure;

namespace DaxStudio.CommandLine.Commands
{
    internal class XlsxCommand : Command<XlsxCommand.Settings>
    {
        public IEventAggregator EventAggregator { get; }

        internal class Settings : CommandSettingsFileBase, IQueryTextProvider
        {

            [CommandOption("-f|--file <file>")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            [Description("A DAX query to be executed")]
            public string Query { get; set; }

            public string EditorText => Query;

            public string QueryText => Query;

            public List<AdomdParameter> ParameterCollection => new List<AdomdParameter>();
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new System.NotImplementedException(); }
        }

        public XlsxCommand(IEventAggregator eventAggregator)
        {
            EventAggregator = eventAggregator;
        }

        public override ValidationResult Validate(CommandContext context, XlsxCommand.Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.Query) && string.IsNullOrWhiteSpace(settings.File))
            { return ValidationResult.Error("You must specify either <query> or <file>"); }
            return base.Validate(context, settings);
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            VersionInfo.Output();
            Log.Information("Starting XLSX command");
            if (settings.File != null && settings.Query == null)
            {
                settings.Query = System.IO.File.ReadAllText(settings.File);
            }
            // export to csv
            var host = new CmdLineHost();
            var runner = new QueryRunner(settings);
            var target = new DaxStudio.UI.ResultsTargets.ResultsTargetExcelFile(host, EventAggregator);
            target.OutputResultsAsync(runner, settings, settings.OutputFile).Wait();
            
            Log.Information("Finished XLSX command");
            return 0;
        }
    }
}
