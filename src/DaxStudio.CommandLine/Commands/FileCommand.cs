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

                var assertBatches = batches?.Where(BatchHasAsserts).ToList() ?? new List<ScriptBatch>();
                if (assertBatches.Count == 0)
                {
                    return 0;
                }

                var assertBaseDir = !string.IsNullOrEmpty(settings.File)
                    ? Path.GetDirectoryName(Path.GetFullPath(settings.File))
                    : null;

                var results = await EvaluateAssertionsAsync(
                    runner, settings, batches, assertBaseDir, cancellationToken);

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

        #region Assertions

        internal static bool BatchHasAsserts(ScriptBatch b) =>
            b.Commands.Any(c => c is AssertRowcountCommand || c is AssertTableCommand || c is AssertCommand);

        internal static bool BatchIsBaseline(ScriptBatch b) => b.Commands.OfType<BaselineCommand>().Any();

        /// <summary>
        /// True when the script must be run <b>in order on a single traced connection</b> rather than by
        /// the cheap "run just the asserting batches" path: it captures a baseline (so batch order and
        /// snapshotting matter) and/or asserts on a performance metric (so each batch needs its own
        /// Server Timings slice).
        /// </summary>
        internal static bool RequiresSequencedRun(IReadOnlyList<ScriptBatch> batches, out bool needsTrace)
        {
            var allCommands = batches?.SelectMany(b => b.Commands).ToList() ?? new List<ScriptCommand>();
            var hasBaselines = allCommands.OfType<BaselineCommand>().Any();

            // Only a performance assertion consumes the captured metrics, so that alone decides whether
            // the (comparatively expensive) trace is worth starting.
            needsTrace = allCommands.OfType<AssertCommand>().Any();

            return hasBaselines || needsTrace;
        }

        /// <summary>
        /// Runs the script's comment-script assertions and returns one <see cref="TestResult"/> per
        /// assertion.
        /// </summary>
        /// <remarks>
        /// Two execution strategies, chosen by what the script actually needs:
        /// <list type="bullet">
        /// <item><b>Simple</b> - no performance assertions and no baselines. Only the batches that carry
        /// assertions are run, on the shared query runner. This is the cheap path and is what nearly
        /// every result-only script takes.</item>
        /// <item><b>Sequenced</b> - the script has <c>--&gt; BASELINE</c> (so batches must run in order and
        /// be snapshotted) and/or performance assertions (so each batch needs its own isolated Server
        /// Timings slice). Runs on a dedicated traced connection, mirroring what the UI does.</item>
        /// </list>
        /// </remarks>
        private async Task<List<TestResult>> EvaluateAssertionsAsync(
            QueryRunner runner, Settings settings, IReadOnlyList<ScriptBatch> batches,
            string assertBaseDir, CancellationToken cancellationToken)
        {
            if (!RequiresSequencedRun(batches, out var needsTrace))
                return EvaluateAssertionsSimple(runner, settings, batches, assertBaseDir);

            return await EvaluateAssertionsSequencedAsync(
                runner, settings, batches, assertBaseDir, needsTrace, cancellationToken);
        }

        /// <summary>
        /// The cheap path: run each asserting batch on the shared runner and evaluate. No trace, no
        /// baseline store - neither is reachable here, because a baseline reference can only be parsed
        /// when a <c>--&gt; BASELINE</c> capture exists somewhere in the script.
        /// </summary>
        private static List<TestResult> EvaluateAssertionsSimple(
            QueryRunner runner, Settings settings, IReadOnlyList<ScriptBatch> batches, string assertBaseDir)
        {
            var results = new List<TestResult>();

            foreach (var batch in batches.Where(BatchHasAsserts))
            {
                // A batch with no query has nothing to assert about - report it rather than evaluating
                // against a null table, where "ASSERT ROWCOUNT = 0" would otherwise pass.
                if (!batch.RunsItsQuery)
                {
                    results.AddRange(BatchNotEvaluated(batch));
                    continue;
                }

                DataTable dt;
                using (var reader = runner.ExecuteDataReaderQuery(batch.QueryText, settings.ParameterCollection))
                {
                    dt = new DataTable();
                    dt.Load(reader);
                }

                results.AddRange(EvaluateBatchAssertions(batch, dt, NoMetrics, null, assertBaseDir));
            }

            return results;
        }

        /// <summary>
        /// Runs every batch that executes a query, in script order, on a dedicated connection so that
        /// <c>--&gt; BASELINE</c> captures happen before the batches that compare against them, and each
        /// batch gets its own Server Timings slice.
        /// </summary>
        /// <remarks>
        /// The trace is only started when the script has a performance assertion, since that is the only
        /// thing that consumes the metrics - a baseline captured without them is never read. The queries
        /// must run on the traced connection itself because the trace filters for its own session.
        /// </remarks>
        private async Task<List<TestResult>> EvaluateAssertionsSequencedAsync(
            QueryRunner runner, Settings settings, IReadOnlyList<ScriptBatch> batches, string assertBaseDir,
            bool startTrace, CancellationToken cancellationToken)
        {
            var results = new List<TestResult>();
            var baselines = new BaselineStore();

            var eventAggregator = new EventAggregator();
            var connMgr = new ConnectionManager(eventAggregator);
            try
            {
                var connectEvent = new UIStubs.ConnectEvent()
                {
                    ConnectionString = settings.FullConnectionString,
                    ApplicationName = "DAX Studio Command Line",
                    DatabaseName = settings.Database,
                    PowerBIFileName = settings.PowerBIFileName ?? ""
                };

                // The assertion run needs its own connection (the trace filters for its own session), so
                // it must acquire an access token the same way QueryRunner does - otherwise a
                // token-authenticated model (Power BI / Fabric) would fail to connect here even though
                // the rest of the command works.
                if (Helpers.AccessTokenHelper.IsAccessTokenNeeded(settings.FullConnectionString))
                    connectEvent.AccessToken = Helpers.AccessTokenHelper.GetAccessToken(settings.FullConnectionString, settings);

                connMgr.Connect(connectEvent);
                connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                connMgr.SelectedModelName = connMgr.SelectedModel.Name;
            }
            catch (Exception ex)
            {
                Log.Error("Assertions: could not connect to run the script: {message}", ex.Message);
                try { connMgr.Close(); } catch { }
                throw;
            }

            CmdServerTimesViewModel serverTimes = null;
            ManualResetEventSlim timingReady = null;
            var traceActive = false;

            // Held for the whole run: Caliburn's EventAggregator keeps subscribers via a WeakReference,
            // so an unrooted handler would be collected part-way through the batch loop, the
            // QueryTraceCompletedEvent would stop being delivered, and every later batch would stall
            // until its wait timed out. See the GC.KeepAlive in the finally below.
            TraceCompletedHandler traceCompletedHandler = null;

            try
            {
                if (startTrace)
                {
                    var doc = new CmdLineDocument(connMgr, new CmdLineMetadataPane());
                    serverTimes = new CmdServerTimesViewModel(
                        eventAggregator, new ServerTimingDetailsViewModel(), runner.Options, null);
                    serverTimes.Document = doc;

                    timingReady = new ManualResetEventSlim(false);
                    traceCompletedHandler = new TraceCompletedHandler(serverTimes, () => timingReady.Set());
                    eventAggregator.SubscribeOnPublishedThread(traceCompletedHandler);

                    serverTimes.IsChecked = true;

                    var waitMs = 0;
                    while (serverTimes.TraceStatus != QueryTraceStatus.Started && waitMs < 30000)
                    {
                        Thread.Sleep(500);
                        waitMs += 500;
                    }

                    traceActive = serverTimes.TraceStatus == QueryTraceStatus.Started;
                    if (!traceActive)
                    {
                        // Not fatal: the assertions still run, and each performance assertion reports a
                        // clear "metric not captured" error rather than silently passing.
                        Log.Warning("Assertions: the Server Timings trace did not start; performance assertions will report their metrics as not captured");
                    }
                }

                var evaluated = new HashSet<int>();

                for (int i = 0; i < batches.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    var batch = batches[i];

                    // "--> CLEARCACHE" applies per batch so a baseline batch and the batch comparing
                    // against it can start from the same cold cache. Checked BEFORE the RunsItsQuery
                    // skip below, because a CLEARCACHE may sit in its own comment-only batch - skipping
                    // it there would leave the next batch running warm and bias its timings.
                    if (batch.Commands.OfType<ClearCacheCommand>().Any())
                    {
                        try
                        {
                            connMgr.ClearCache();
                        }
                        catch (Exception ex)
                        {
                            Log.Warning("--> CLEARCACHE failed: {message}", ex.Message);
                        }
                    }

                    // Skip batches that never send DAX to the server (comment-only batches, and the
                    // SHOW variants that consume the query as an analysis target). Any assertions they
                    // carry are reported as errors by the sweep after the loop.
                    if (!batch.RunsItsQuery) continue;

                    if (traceActive)
                    {
                        serverTimes.OnReset();
                        timingReady.Reset();
                    }

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    DataTable dt = null;
                    try
                    {
                        using (var reader = connMgr.ExecuteReader(batch.QueryText, settings.ParameterCollection))
                        {
                            dt = new DataTable();
                            dt.Load(reader);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Assertions: query failed: {message}", ex.Message);
                        results.AddRange(BatchQueryFailed(batch, ex));
                        evaluated.Add(i);
                        continue;
                    }
                    finally
                    {
                        sw.Stop();
                    }

                    // Wait for this batch's trace slice to finish aggregating before reading the metrics,
                    // exactly as the UI and the benchmark do.
                    //
                    // The metrics are read ONLY when that wait succeeds. Mid-query the model already
                    // reports HasData (TotalDuration is updated in real time) while StorageEngineCpu and
                    // StorageEngineQueryCount are still 0, because those are only assigned once
                    // ProcessResults has aggregated. Reading it early would hand the engine a dictionary
                    // of zeros, and "ASSERT SE_QUERIES <= n" would pass on data that was never captured.
                    var timingsCaptured = false;
                    if (traceActive)
                    {
                        var timeoutMs = Math.Max(15000, (int)sw.ElapsedMilliseconds * 3);
                        timingsCaptured = timingReady.Wait(timeoutMs);
                        if (!timingsCaptured)
                            Log.Warning("Assertions: timed out waiting for the Server Timings trace; this batch's performance assertions will report their metrics as not captured");
                    }

                    var metrics = BuildPerformanceMetrics(timingsCaptured ? serverTimes : null);

                    if (BatchIsBaseline(batch))
                        CaptureBatchBaselines(batch, dt, metrics, baselines);

                    if (BatchHasAsserts(batch))
                    {
                        results.AddRange(EvaluateBatchAssertions(batch, dt, metrics, baselines, assertBaseDir));
                        evaluated.Add(i);
                    }
                }

                // Every assertion in the script must produce exactly one result, otherwise a script whose
                // assertions were all skipped would print "0 passed, 0 failed, 0 errors" and exit 0.
                // Anything not reached above (a batch with no query to assert against, or a run cut short
                // by cancellation) is reported as an error.
                for (int i = 0; i < batches.Count; i++)
                {
                    if (!BatchHasAsserts(batches[i]) || evaluated.Contains(i)) continue;
                    results.AddRange(BatchNotEvaluated(batches[i]));
                }
            }
            finally
            {
                if (serverTimes != null)
                {
                    try { await serverTimes.StopTraceAsync(); } catch { }
                }
                // Keeps the weakly-referenced trace handler alive for the whole run (see its declaration).
                GC.KeepAlive(traceCompletedHandler);
                timingReady?.Dispose();
                try { connMgr.Close(); } catch { }
            }

            return results;
        }

        private static readonly IReadOnlyDictionary<PerformanceProperty, double> NoMetrics =
            new Dictionary<PerformanceProperty, double>();

        /// <summary>
        /// Builds the metric dictionary the assertion engine consumes. Returns an empty dictionary when
        /// the trace is inactive or captured nothing, which the engine turns into a clear
        /// "metric not captured" error rather than a silent pass. Kept identical to the UI's
        /// <c>DocumentViewModel.BuildPerformanceMetrics</c> so both hosts assert on the same values.
        /// </summary>
        internal static IReadOnlyDictionary<PerformanceProperty, double> BuildPerformanceMetrics(CmdServerTimesViewModel serverTimes)
        {
            var metrics = new Dictionary<PerformanceProperty, double>();
            if (serverTimes != null && serverTimes.HasData)
            {
                metrics[PerformanceProperty.Duration] = serverTimes.TotalDuration;
                metrics[PerformanceProperty.SE_CPU] = serverTimes.StorageEngineCpu;
                metrics[PerformanceProperty.SE_QUERIES] = serverTimes.StorageEngineQueryCount;
            }
            return metrics;
        }

        /// <summary>Snapshots a "--&gt; BASELINE" batch's results and metrics for later batches.</summary>
        internal static void CaptureBatchBaselines(
            ScriptBatch batch, DataTable dt,
            IReadOnlyDictionary<PerformanceProperty, double> metrics, BaselineStore baselines)
        {
            foreach (var cmd in batch.Commands.OfType<BaselineCommand>())
            {
                baselines.Capture(cmd.Name, dt, metrics, cmd.Runs);
                var described = cmd.IsSynthesised
                    ? " (previous batch)"
                    : cmd.IsDefault ? string.Empty : $" \"{cmd.Name}\"";
                Log.Information("--> BASELINE{described} captured: {rows} row(s)", described, dt?.Rows.Count ?? 0);
            }
        }

        /// <summary>
        /// Evaluates every assertion in a batch. The order (row count, table, then performance) matches
        /// the UI so the two hosts report the same rows in the same sequence.
        /// </summary>
        internal static List<TestResult> EvaluateBatchAssertions(
            ScriptBatch batch, DataTable dt,
            IReadOnlyDictionary<PerformanceProperty, double> metrics,
            BaselineStore baselines, string assertBaseDir)
        {
            var results = new List<TestResult>();
            var testName = batch.Commands.OfType<TestCommand>().FirstOrDefault()?.TestName;
            var rowCount = dt?.Rows.Count ?? 0;

            foreach (var cmd in batch.Commands.OfType<AssertRowcountCommand>())
                results.Add(AssertionEngine.EvaluateRowCount(cmd, rowCount, testName, baselines));

            foreach (var cmd in batch.Commands.OfType<AssertTableCommand>())
                results.Add(AssertionEngine.EvaluateTable(cmd, dt, testName, assertBaseDir, baselines));

            foreach (var cmd in batch.Commands.OfType<AssertCommand>())
                results.Add(AssertionEngine.EvaluatePerformance(cmd, metrics, testName, baselines));

            return results;
        }

        /// <summary>
        /// Turns a failed batch query into an error result per assertion, so a query that could not run
        /// is reported as a test error rather than silently producing no rows (which an
        /// <c>ASSERT ROWCOUNT = 0</c> would otherwise pass).
        /// </summary>
        internal static List<TestResult> BatchQueryFailed(ScriptBatch batch, Exception ex)
            => AssertionErrors(batch, "query failed", ex.Message);

        /// <summary>
        /// Turns a batch whose assertions were never evaluated into an error per assertion. A batch that
        /// runs no query has nothing to assert against, and a run cut short by cancellation never reached
        /// its later batches - in both cases the assertions must still be reported, or the run would
        /// summarise as "0 passed, 0 failed, 0 errors" and exit successfully.
        /// </summary>
        internal static List<TestResult> BatchNotEvaluated(ScriptBatch batch)
            => AssertionErrors(batch, "not evaluated",
                "This batch did not run a query, so there was nothing to assert against. Add a query to the batch, or remove the assertion.");

        private static List<TestResult> AssertionErrors(ScriptBatch batch, string description, string message)
        {
            var testName = batch.Commands.OfType<TestCommand>().FirstOrDefault()?.TestName;
            var results = new List<TestResult>();

            foreach (var cmd in batch.Commands.Where(c =>
                c is AssertRowcountCommand || c is AssertTableCommand || c is AssertCommand))
            {
                results.Add(new TestResult
                {
                    TestName = testName,
                    Outcome = TestOutcome.Error,
                    Description = description,
                    Expected = string.Empty,
                    Actual = "n/a",
                    Message = message,
                    Line = cmd.Line,
                });
            }

            return results;
        }

        #endregion

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
            // Rooted for the whole capture: Caliburn's EventAggregator holds subscribers via a
            // WeakReference, so an unrooted handler can be collected part-way through the loop below,
            // after which the QueryTraceCompletedEvent stops being delivered and every remaining batch
            // stalls for the full timeout. See the GC.KeepAlive before the return.
            var traceCompletedHandler = new TraceCompletedHandler(serverTimes, () => timingReady.Set());
            eventAggregator.SubscribeOnPublishedThread(traceCompletedHandler);

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
                GC.KeepAlive(traceCompletedHandler);
                timingReady.Dispose();
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
            // Keeps the weakly-referenced trace handler alive for the whole capture (see its declaration).
            GC.KeepAlive(traceCompletedHandler);
            timingReady.Dispose();
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
        // mirroring BenchmarkCommand's handler. Shared by the SAVEAS capture and the assertion run.
        private class TraceCompletedHandler : IHandle<QueryTraceCompletedEvent>
        {
            private readonly ITraceWatcher _traceWatcher;
            private readonly System.Action _callback;
            public TraceCompletedHandler(ITraceWatcher traceWatcher, System.Action callback)
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
