using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using Serilog;
using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.CommandLine.Commands
{
    
    internal class CaptureDiagnosticsCommand:Command<CaptureDiagnosticsCommand.Settings>
    {
        internal class Settings : CommandSettingsBase, IQueryTextProvider
        {

            [CommandArgument(0, "[file]")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            public string Query { get; set; }
            [CommandOption("-o|--out <filename>")]
            [Description("The name of the file for the results")]
            public string Out { get; set; }

            public string EditorText => Query;

            public string QueryText => Query;

            public List<AdomdParameter> ParameterCollection => new List<AdomdParameter>();
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new System.NotImplementedException(); }
        }

        public override int Execute(CommandContext context, Settings settings)
        {
            Log.Information("Starting Capture Diagnostics command");
            if (settings.File != null && settings.Query == null)
            {
                settings.Query = System.IO.File.ReadAllText(settings.File);
            }
            // export to csv

            //var ribbon = new UI.ViewModels.RibbonViewModel(host, eventAggregator, windowManager, options, settings);
            //var vm = new DaxStudio.UI.ViewModels.CaptureDiagnosticsViewModel(ribbon, options, eventAggregator);

            var runner = new QueryRunner(settings);
            var target = new DaxStudio.UI.ResultsTargets.ResultsTargetTextFile();
            target.OutputResultsAsync(runner, settings, settings.Out).Wait();
            Log.Information("Finished CSV command");
            return 0;
        }
    }
    
    
}
