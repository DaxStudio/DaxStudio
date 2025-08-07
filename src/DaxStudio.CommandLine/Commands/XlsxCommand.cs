using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.UI.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.UI.Model;
using Caliburn.Micro;
using DaxStudio.CommandLine.UIStubs;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class XlsxCommand : AsyncCommand<XlsxCommand.Settings>
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
            [CommandOption("-m|--parameter <PARAMETER=VALUE>")]
            public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

            private List<AdomdParameter> _parameters = new List<AdomdParameter>();
            public List<AdomdParameter> ParameterCollection
            {
                get
                {
                    if (_parameters.Count == 0 && Parameters.Count > 0)
                    {
                        foreach (var p in Parameters)
                        {
                            _parameters.Add(new AdomdParameter(p.Key, p.Value));
                        }
                    }
                    return _parameters;
                }
            }
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

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            Log.Information("Starting XLSX command");
            if (settings.File != null && settings.Query == null)
            {
                settings.Query = System.IO.File.ReadAllText(settings.File);
            }
            // export to xlsx
            var host = new CmdLineHost();
            var runner = new QueryRunner(settings);
            var target = new DaxStudio.UI.ResultsTargets.ResultsTargetExcelFile(host, EventAggregator);

            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .StartAsync("Exporting to file...", async ctx =>
                {   
                    await target.OutputResultsAsync(runner, settings, settings.OutputFile);
                });
       
            
            Log.Information("Finished XLSX command");
            return 0;
        }
    }
}
