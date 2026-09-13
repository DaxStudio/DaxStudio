using Caliburn.Micro;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.Core.Assertions;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
using DaxStudio.Parsers.CommentScript;
using Spectre.Console;
using Spectre.Console.Cli;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class TestCommand : AsyncCommand<TestCommand.Settings>
    {
        internal class Settings : CommandSettingsRawBase, IQueryTextProvider
        {
            [CommandOption("-f|--file <file>")]
            [Description("A text file containing DAX assertions to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            [Description("A DAX query and assertions to be executed")]
            public string Query { get; set; }

            [CommandOption("-m|--parameter <PARAMETER=VALUE>")]
            public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

            private List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> _parameters = new List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter>();
            public List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> ParameterCollection
            {
                get
                {
                    if (_parameters.Count == 0 && Parameters.Count > 0)
                    {
                        foreach (var p in Parameters)
                        {
                            Serilog.Log.Information("Setting parameter {name} to {value}", p.Key, p.Value);
                            _parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(p.Key, p.Value));
                        }
                    }
                    return _parameters;
                }
            }

            [CommandOption("--test-report <file>")]
            [Description("Writes assertion results to a .xml (JUnit), .trx, or .json test report")]
            public string TestReport { get; set; }

            public string EditorText => Query;
            public string QueryText => Query;
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new System.NotImplementedException(); }
        }

        public ValidationResult ValidatePublic(CommandContext context, Settings settings) => Validate(context, settings);

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.File) && string.IsNullOrWhiteSpace(settings.Query))
                return ValidationResult.Error("You must specify either a --file or --query option");
            if (!string.IsNullOrWhiteSpace(settings.File) && !string.IsNullOrWhiteSpace(settings.Query))
                return ValidationResult.Error("You cannot specify both --file and --query");
            return base.Validate(context, settings);
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(settings.File))
            {
                if (!File.Exists(settings.File))
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Query file not found: {Markup.Escape(settings.File)}");
                    return 2;
                }
                settings.Query = File.ReadAllText(settings.File);
            }

            var adaptedSettings = Adapt(settings);
            var runner = new QueryRunner(settings);
            runner.Options.UseNewDaxParser = true;
            var batches = new QueryInfo(settings.Query, new EventAggregator(), runner.Options).ScriptBatches;
            ScriptVariableExpander.ExpandBatches(batches);
            var reportCommands = batches.SelectMany(b => b.Commands).OfType<ExportCommand>()
                .Where(c => c.Target == ExportTarget.TestResults).ToList();
            if (!batches.Any(FileCommand.BatchHasAsserts))
            {
                AnsiConsole.MarkupLine("[red]No assertions were found; no test report was written.[/]");
                return 2;
            }

            var baseDirectory = string.IsNullOrEmpty(settings.File) ? null : Path.GetDirectoryName(Path.GetFullPath(settings.File));
            try
            {
                var results = await new FileCommand().EvaluateAssertionsAsync(runner, adaptedSettings, batches, baseDirectory, cancellationToken);
                RenderResults(results);
                FileCommand.WriteTestReports(results, settings.TestReport, reportCommands, baseDirectory, settings.File);
                return results.Any(r => r.Outcome != TestOutcome.Passed) ? 1 : 0;
            }
            catch (System.Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Test execution failed:[/] {Markup.Escape(ex.Message)}");
                return 2;
            }
        }

        private static FileCommand.Settings Adapt(Settings settings) => new FileCommand.Settings
        {
            Server = settings.Server, Database = settings.Database, UserID = settings.UserID, Password = settings.Password,
            NonInteractive = settings.NonInteractive, ConnectionString = settings.ConnectionString, PowerBIFileName = settings.PowerBIFileName,
            File = settings.File, Query = settings.Query, Parameters = settings.Parameters,
        };

        private static void RenderResults(IReadOnlyList<TestResult> results)
        {
            var table = new Table().Title("[bold]Test Results[/]");
            table.AddColumn("Test").AddColumn("Assertion").AddColumn("Expected").AddColumn("Actual").AddColumn("Result");
            foreach (var result in results)
                table.AddRow(Markup.Escape(result.TestName ?? string.Empty), Markup.Escape(result.Description ?? string.Empty), Markup.Escape(result.Expected ?? string.Empty), Markup.Escape(result.Actual ?? string.Empty), result.Outcome == TestOutcome.Passed ? "[green]Passed[/]" : result.Outcome == TestOutcome.Failed ? "[red]Failed[/]" : "[yellow]Error[/]");
            AnsiConsole.Write(table);
        }
    }
}