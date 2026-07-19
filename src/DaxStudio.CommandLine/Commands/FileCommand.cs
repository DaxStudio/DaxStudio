using Serilog;
using Spectre.Console.Cli;
using System.ComponentModel;
using Spectre.Console;
using DaxStudio.Core.Interfaces;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.Core.Model;
using DaxStudio.Core.ResultsTargets;
using System;
using System.IO;
using DaxStudio.Interfaces.Enums;
using DaxStudio.CommandLine.UIStubs;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.CommandLine.Commands
{
    internal class FileCommand : AsyncCommand<FileCommand.Settings>
    {
        internal class Settings : CommandSettingsFileBase,IQueryTextProvider
        {

            [CommandOption("-f|--file <file>")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            [Description("A DAX query to be executed")]
            public string Query { get; set; }

            [CommandOption("-t|--fileType")]
            [Description("Specifies the format of the file")]
            public TextFileType FileType { get; set; }

            public string EditorText => Query;

            public string QueryText => Query;

            [CommandOption("-m|--parameter <PARAMETER=VALUE>")]
            public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();

            private List<AdomdParameter> _parameters = new List<AdomdParameter>();
            public List<AdomdParameter> ParameterCollection { get 
                {
                    if (_parameters.Count == 0 && Parameters.Count > 0)
                    {
                        foreach (var p in Parameters)
                        {
                            Log.Information("Setting parameter {name} to {value}", p.Key, p.Value);
                            // TODO - should we try to parse the value to see if it is an int or double or datetime?
                            _parameters.Add(new AdomdParameter(p.Key, p.Value));
                        }
                    }
                    return _parameters;
                } 
            } 
            public QueryInfo QueryInfo { get => new QueryInfo(Query, null); set => throw new NotImplementedException(); }


        }

        protected override ValidationResult Validate(CommandContext context, Settings settings)
        {
            
            if (string.IsNullOrWhiteSpace(settings.OutputFile)) return ValidationResult.Error("You must specify an Out option");
            var result = base.Validate(context, settings);
            return result;
        }

        

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            
            Log.Information("Starting File command");

            if (settings.File != null && settings.Query == null)
            {
                settings.Query = File.ReadAllText(settings.File);
            }

            QueryRunner runner = new QueryRunner(settings);
            var target = new ResultsTargetTextFile();

            if (settings.FileType == TextFileType.Unknown)
            {
                var fi = new FileInfo(settings.OutputFile);

                switch (fi.Extension.ToLower())
                {
                    case ".csv":
                        settings.FileType = TextFileType.UTF8CSV;
                        break;
                    case ".txt":
                        settings.FileType = TextFileType.TAB;
                        break;
                    case ".json":
                        settings.FileType = TextFileType.JSON;
                        break;
                    case ".parquet":
                        settings.FileType = TextFileType.PARQUET;
                        break;
                    default:
                        settings.FileType = (TextFileType)runner.Options.DefaultTextFileType;
                        break;
                }
            }

            // export to csv
            await AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Star)
                .SpinnerStyle(Style.Parse("green bold"))
                .StartAsync("Exporting to file...", async ctx =>
                {
                    //AnsiConsole.MarkupLine("[green]Done![/]");

                    runner.Options.CmdLineTextFileType = settings.FileType;
                    await target.OutputResultsAsync(runner, settings, settings.OutputFile).ConfigureAwait(true);
                });

            Log.Information("Finished FILE command");

            // Run any comment-script assertions found in the query. The assertion (test-runner)
            // commands are only produced by the new grammar-based pre-processor, so force it on for
            // this separate parse without affecting the file-export path above.
            try
            {
                runner.Options.UseNewPreprocessor = true;
                var queryInfo = new QueryInfo(settings.Query, new EventAggregator(), runner.Options);
                var batches = queryInfo.ScriptBatches;

                bool HasAsserts(ScriptBatch b) =>
                    b.Commands.Any(c => c is AssertRowcountCommand || c is AssertTableCommand || c is AssertCommand);

                var assertBatches = batches?.Where(HasAsserts).ToList() ?? new List<ScriptBatch>();
                if (assertBatches.Count == 0)
                {
                    return 0;
                }

                var results = new List<TestResult>();
                var warnedPerf = false;

                foreach (var batch in assertBatches)
                {
                    var testName = batch.Commands.OfType<TestCommand>().FirstOrDefault()?.TestName;

                    DataTable dt = null;
                    if (!string.IsNullOrWhiteSpace(batch.QueryText))
                    {
                        using (var reader = runner.ExecuteDataReaderQuery(batch.QueryText, settings.ParameterCollection))
                        {
                            dt = new DataTable();
                            dt.Load(reader);
                        }
                    }
                    var rowCount = dt?.Rows.Count ?? 0;

                    foreach (var cmd in batch.Commands.OfType<AssertRowcountCommand>())
                    {
                        results.Add(AssertionEngine.EvaluateRowCount(cmd, rowCount, testName));
                    }
                    foreach (var cmd in batch.Commands.OfType<AssertTableCommand>())
                    {
                        results.Add(AssertionEngine.EvaluateTable(cmd, dt, testName));
                    }
                    foreach (var cmd in batch.Commands.OfType<AssertCommand>())
                    {
                        if (!warnedPerf)
                        {
                            Log.Warning("Performance assertions are not yet supported in dscmd and will be reported as errors");
                            warnedPerf = true;
                        }
                        results.Add(AssertionEngine.EvaluatePerformance(cmd, new Dictionary<PerformanceProperty, double>(), testName));
                    }
                }

                var passed = results.Count(r => r.Outcome == TestOutcome.Passed);
                var failed = results.Count(r => r.Outcome == TestOutcome.Failed);
                var errored = results.Count(r => r.Outcome == TestOutcome.Error);

                var table = new Table().Title("[bold]Test Results[/]");
                table.AddColumn("Test");
                table.AddColumn("Assertion");
                table.AddColumn("Expected");
                table.AddColumn("Actual");
                table.AddColumn("Result");

                foreach (var r in results)
                {
                    string resultCell;
                    switch (r.Outcome)
                    {
                        case TestOutcome.Passed:
                            resultCell = "[green]Passed[/]";
                            break;
                        case TestOutcome.Failed:
                            resultCell = "[red]Failed[/]";
                            break;
                        default:
                            resultCell = "[yellow]Error[/]";
                            break;
                    }

                    table.AddRow(
                        Markup.Escape(r.TestName ?? string.Empty),
                        Markup.Escape(r.Description ?? string.Empty),
                        Markup.Escape(r.Expected ?? string.Empty),
                        Markup.Escape(r.Actual ?? string.Empty),
                        resultCell);
                }

                AnsiConsole.Write(table);
                AnsiConsole.MarkupLine($"[bold]{passed} passed, {failed} failed, {errored} errors[/]");
                Log.Information("Test results: {passed} passed, {failed} failed, {errored} errors", passed, failed, errored);

                return (failed == 0 && errored == 0) ? 0 : 1;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Unexpected error while evaluating comment-script assertions", nameof(FileCommand), nameof(ExecuteAsync));
                return 2;
            }
        }
    }

}
