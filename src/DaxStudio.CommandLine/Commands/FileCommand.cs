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
using DaxStudio.Core;
using DaxStudio.Core.Connections;
using DaxStudio.Core.Events;
using DaxStudio.Core.Trace;
using DaxStudio.CommandLine.ViewModel;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;
using System.IO.Packaging;
using System.Text;

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

                // Expand any "$(...)" script-variable / built-in references in command arguments (e.g.
                // an "--> ASSERT TABLE CSV" file path) before the commands are used. A fresh expander
                // is created per file so variables do not leak across files in a folder run.
                ScriptVariableExpander.ExpandBatches(batches);

                // "--> SHOW DIAGRAM|METRICS|DELTA" open a tool window in the UI; dscmd has no UI so these
                // are no-ops here. Log an informational message so scripts shared between the UI and dscmd
                // are clear about what was skipped.
                var uiOnlyShowCommands = batches?
                    .SelectMany(b => b.Commands)
                    .OfType<ShowCommand>()
                    .Where(c => c.ShowType == ShowType.Diagram
                             || c.ShowType == ShowType.Metrics
                             || c.ShowType == ShowType.Delta)
                    .ToList() ?? new List<ShowCommand>();
                foreach (var showCmd in uiOnlyShowCommands)
                {
                    Log.Information("--> SHOW {showType} is not supported in dscmd (no UI to open the {showType} view) and was ignored", showCmd.ShowType.ToString().ToUpperInvariant(), showCmd.ShowType.ToString().ToUpperInvariant());
                }

                // "--> EXPORT METRICS <file>" writes a .vpax file for the connected model. Unlike the SHOW
                // panes this works headlessly, so dscmd performs the export directly.
                var exportCommands = batches?
                    .SelectMany(b => b.Commands)
                    .OfType<ExportCommand>()
                    .Where(c => c.Target == ExportTarget.Metrics)
                    .ToList() ?? new List<ExportCommand>();
                foreach (var export in exportCommands)
                {
                    ExecuteExportMetricsCommand(settings, export);
                }

                // "--> SAVEAS <path>" snapshots the query (and, for a .daxx package, the captured
                // server timings) to a separate file. Handled before the assert-only early return
                // below because a script may contain SAVEAS without any assertions.
                var saveAsCommands = batches?
                    .SelectMany(b => b.Commands)
                    .OfType<SaveAsCommand>()
                    .ToList() ?? new List<SaveAsCommand>();
                if (saveAsCommands.Count > 0)
                {
                    await ExecuteSaveAsCommandsAsync(runner, settings, batches, saveAsCommands, cancellationToken);
                }

                bool HasAsserts(ScriptBatch b) =>
                    b.Commands.Any(c => c is AssertRowcountCommand || c is AssertTableCommand || c is AssertCommand);

                var assertBatches = batches?.Where(HasAsserts).ToList() ?? new List<ScriptBatch>();
                if (assertBatches.Count == 0)
                {
                    return 0;
                }

                var results = new List<TestResult>();
                var warnedPerf = false;
                var assertBaseDir = !string.IsNullOrEmpty(settings.File)
                    ? Path.GetDirectoryName(Path.GetFullPath(settings.File))
                    : null;

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
                        results.Add(AssertionEngine.EvaluateTable(cmd, dt, testName, assertBaseDir));
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

        private static bool IsDaxxPath(string path)
            => !string.IsNullOrEmpty(path) && path.EndsWith(".daxx", StringComparison.OrdinalIgnoreCase);

        // Executes a "--> EXPORT METRICS <file>" command by generating a .vpax for the connected model,
        // reusing the same ModelAnalyzer.ExportVPAX path as the dedicated "vpax" dscmd command.
        private static void ExecuteExportMetricsCommand(Settings settings, ExportCommand export)
        {
            var path = export.FileName;
            if (string.IsNullOrWhiteSpace(path))
            {
                Log.Warning("--> EXPORT METRICS was ignored because no file path was supplied");
                AnsiConsole.MarkupLine("[yellow]EXPORT METRICS:[/] no file path supplied - skipped");
                return;
            }

            try
            {
                EnsureSaveAsDirectory(path);
                var appVersion = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
                var connStr = settings.FullConnectionString;
                var statsColumnBatchSize = Dax.Model.Extractor.StatExtractor.DefaultColumnBatchSize;

                Log.Information("--> EXPORT METRICS exporting VPAX to {path}", path);
                DaxStudio.Core.Vpax.ModelAnalyzer.ExportVPAX(
                    connStr, path, string.Empty, string.Empty,
                    true, "DAX Studio Command Line", appVersion, true, "Model",
                    false, Dax.Metadata.DirectLakeExtractionMode.ResidentOnly, statsColumnBatchSize);

                AnsiConsole.MarkupLine($"[green]EXPORT METRICS:[/] saved {Markup.Escape(path)}");
                Log.Information("--> EXPORT METRICS wrote VPAX to {path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "--> EXPORT METRICS failed for {path}", path);
                AnsiConsole.MarkupLine($"[red]EXPORT METRICS failed for {Markup.Escape(path)}:[/] {Markup.Escape(ex.Message)}");
            }
        }

        // Executes all "--> SAVEAS <path>" commands. Non-package targets get the query text; a .daxx
        // target gets a package with the query text and (when the script enables a Server Timings
        // trace) the captured server-timing data. Errors are logged and reported but do not abort.
        private async Task ExecuteSaveAsCommandsAsync(
            QueryRunner runner, Settings settings, IReadOnlyList<ScriptBatch> batches,
            List<SaveAsCommand> saveAsCommands, CancellationToken cancellationToken)
        {
            // The full script text (including comment-script directives) is saved, matching the
            // interactive SAVEAS which persists the whole editor buffer.
            var scriptText = settings.Query ?? string.Empty;

            var daxxTargets = saveAsCommands.Where(c => IsDaxxPath(c.FileName)).ToList();
            foreach (var cmd in saveAsCommands.Where(c => !IsDaxxPath(c.FileName)))
                WriteSaveAsQueryText(cmd.FileName, scriptText);

            if (daxxTargets.Count == 0) return;

            var wantsTimings = batches
                .SelectMany(b => b.Commands)
                .OfType<TraceCommand>()
                .Any(t => t.TraceType == TraceType.ServerTimings && t.Enabled);

            CmdServerTimesViewModel serverTimes = null;
            if (wantsTimings)
                serverTimes = await CaptureServerTimingsAsync(runner, settings, batches, cancellationToken);

            foreach (var cmd in daxxTargets)
                WriteSaveAsPackage(cmd.FileName, scriptText, serverTimes);
        }

        // Runs the script's queries under a fresh Server Timings trace and returns the populated
        // trace model so its data can be embedded in a .daxx package. Returns null (and logs a
        // warning) if a connection or the trace cannot be established.
        private async Task<CmdServerTimesViewModel> CaptureServerTimingsAsync(
            QueryRunner runner, Settings settings, IReadOnlyList<ScriptBatch> batches, CancellationToken cancellationToken)
        {
            var eventAggregator = new EventAggregator();
            var connMgr = new ConnectionManager(eventAggregator);
            try
            {
                connMgr.Connect(new UIStubs.ConnectEvent()
                {
                    ConnectionString = settings.FullConnectionString,
                    ApplicationName = "DAX Studio Command Line",
                    DatabaseName = settings.Database,
                    PowerBIFileName = settings.PowerBIFileName ?? ""
                });
                connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                connMgr.SelectedModelName = connMgr.SelectedModel.Name;
            }
            catch (Exception ex)
            {
                Log.Warning("SAVEAS: could not connect to capture server timings: {message}", ex.Message);
                try { connMgr.Close(); } catch { }
                return null;
            }

            var doc = new CmdLineDocument(connMgr, new CmdLineMetadataPane());
            var serverTimes = new CmdServerTimesViewModel(
                eventAggregator, new ServerTimingDetailsViewModel(), runner.Options, null);
            serverTimes.Document = doc;

            var timingReady = new ManualResetEventSlim(false);
            eventAggregator.SubscribeOnPublishedThread(
                new SaveAsTraceCompletedHandler(serverTimes, () => timingReady.Set()));

            serverTimes.IsChecked = true;

            int waitMs = 0;
            while (serverTimes.TraceStatus != QueryTraceStatus.Started && waitMs < 30000)
            {
                Thread.Sleep(500);
                waitMs += 500;
            }
            if (serverTimes.TraceStatus != QueryTraceStatus.Started)
            {
                Log.Warning("SAVEAS: server timings trace did not start; package will not include timings");
                try { connMgr.Close(); } catch { }
                return null;
            }

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested) break;
                if (string.IsNullOrWhiteSpace(batch.QueryText)) continue;

                timingReady.Reset();
                try
                {
                    using (var reader = connMgr.ExecuteReader(batch.QueryText, new List<AdomdParameter>()))
                    {
                        do { while (reader.Read()) { } } while (reader.NextResult());
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("SAVEAS: query failed while capturing server timings: {message}", ex.Message);
                }
                timingReady.Wait(15000);
            }

            try { await serverTimes.StopTraceAsync(); } catch { }
            try { connMgr.Close(); } catch { }
            return serverTimes;
        }

        private static void WriteSaveAsQueryText(string path, string queryText)
        {
            try
            {
                EnsureSaveAsDirectory(path);
                File.WriteAllText(path, queryText ?? string.Empty, new UTF8Encoding(false));
                AnsiConsole.MarkupLine($"[green]SAVEAS:[/] saved {Markup.Escape(path)}");
                Log.Information("SAVEAS wrote query text to {path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SAVEAS failed for {path}", path);
                AnsiConsole.MarkupLine($"[red]SAVEAS failed for {Markup.Escape(path)}:[/] {Markup.Escape(ex.Message)}");
            }
        }

        private static void WriteSaveAsPackage(string path, string queryText, CmdServerTimesViewModel serverTimes)
        {
            try
            {
                EnsureSaveAsDirectory(path);
                using (var package = Package.Open(path, FileMode.Create))
                {
                    var uriDax = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.Query, UriKind.Relative));
                    using (var tw = new StreamWriter(
                        package.CreatePart(uriDax, "text/plain", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
                    {
                        tw.Write(queryText ?? string.Empty);
                    }

                    if (serverTimes != null && serverTimes.CanExport)
                        serverTimes.SavePackage(package);
                }
                AnsiConsole.MarkupLine($"[green]SAVEAS:[/] saved package {Markup.Escape(path)}");
                Log.Information("SAVEAS wrote package to {path}", path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "SAVEAS failed for {path}", path);
                AnsiConsole.MarkupLine($"[red]SAVEAS failed for {Markup.Escape(path)}:[/] {Markup.Escape(ex.Message)}");
            }
        }

        private static void EnsureSaveAsDirectory(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        // Signals when the Server Timings trace has finished aggregating for the current query,
        // mirroring BenchmarkCommand's handler.
        private class SaveAsTraceCompletedHandler : IHandle<QueryTraceCompletedEvent>
        {
            private readonly ITraceWatcher _traceWatcher;
            private readonly System.Action _callback;
            public SaveAsTraceCompletedHandler(ITraceWatcher traceWatcher, System.Action callback)
            {
                _traceWatcher = traceWatcher;
                _callback = callback;
            }
            public Task HandleAsync(QueryTraceCompletedEvent message, CancellationToken cancellationToken)
            {
                if (!ReferenceEquals(message.Trace, _traceWatcher)) return Task.CompletedTask;
                _callback();
                return Task.CompletedTask;
            }
        }
    }

}
