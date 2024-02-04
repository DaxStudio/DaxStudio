using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.Model;

namespace DaxStudio.CommandLine.Commands
{
    internal class CsvCommand : Command<CsvCommand.Settings>
    {
        internal class Settings : CommandSettingsFileBase,IQueryTextProvider
        {

            [CommandOption("-f|--file <file>")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            public string Query { get; set; }

            //[CommandArgument(0,"[filename]")]
            //[Description("The name of the file for the results")]
            //public string Out { get; set; }

            public string EditorText => Query;

            public string QueryText => Query;

            public List<AdomdParameter> ParameterCollection => new List<AdomdParameter>();
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new System.NotImplementedException(); }
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {

            if (string.IsNullOrWhiteSpace(settings.Server)) return ValidationResult.Error("You must specify a Server option");
            if (string.IsNullOrWhiteSpace(settings.Database)) return ValidationResult.Error("You must specify a Database option");
            if (string.IsNullOrWhiteSpace(settings.OutputFile)) return ValidationResult.Error("You must specify an Out option");
            var result = base.Validate(context, settings);
            return result;
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            Log.Information("Starting CSV command");
            if (settings.File != null && settings.Query == null)
            {
                settings.Query = System.IO.File.ReadAllText(settings.File);
            }
            // export to csv
            var runner = new QueryRunner(settings.Server, settings.Database);
            var target = new DaxStudio.UI.ResultsTargets.ResultsTargetTextFile();
            target.OutputResultsAsync(runner, settings, settings.OutputFile).Wait();
            Log.Information("Finished CSV command");
            return 0;
        }
    }
}
