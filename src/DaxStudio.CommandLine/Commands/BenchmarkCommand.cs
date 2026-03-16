using Caliburn.Micro;
using DaxStudio.CommandLine.UIStubs;
using DaxStudio.CommandLine.ViewModel;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.QueryTrace.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.AnalysisServices.AdomdClient;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.Commands
{
    internal class BenchmarkCommand : AsyncCommand<BenchmarkCommand.Settings>
    {
        internal class Settings : CommandSettingsFileBase
        {
            [CommandOption("-f|--file <file>")]
            [Description("A text file containing a DAX query to be executed")]
            public string File { get; set; }

            [CommandOption("-q|--query <query>")]
            [Description("A DAX query to be executed")]
            public string Query { get; set; }

            [CommandOption("--cold <cold>")]
            [Description("Number of cold cache (cache cleared) iterations")]
            [DefaultValue(5)]
            public int ColdRuns { get; set; }

            [CommandOption("--warm <warm>")]
            [Description("Number of warm cache iterations")]
            [DefaultValue(5)]
            public int WarmRuns { get; set; }

            [CommandOption("--silent")]
            [Description("Suppress console output (only write CSV)")]
            [DefaultValue(false)]
            public bool Silent { get; set; }

            [CommandOption("--role <role>")]
            [Description("RLS role to test (adds Roles= to the query connection string)")]
            public string Role { get; set; }

            [CommandOption("--effective-user <upn>")]
            [Description("User to impersonate for RLS testing (adds EffectiveUserName= to the query connection string)")]
            public string EffectiveUser { get; set; }
        }

        public IEventAggregator EventAggregator { get; }
        public IGlobalOptions Options { get; }

        public BenchmarkCommand(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            EventAggregator = eventAggregator;
            Options = options;
        }

        public override ValidationResult Validate(CommandContext context, Settings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.OutputFile))
                return ValidationResult.Error("You must specify an output file path");
            if (string.IsNullOrWhiteSpace(settings.File) && string.IsNullOrWhiteSpace(settings.Query))
                return ValidationResult.Error("You must specify either a --file or --query option");
            if (!string.IsNullOrWhiteSpace(settings.File) && !string.IsNullOrWhiteSpace(settings.Query))
                return ValidationResult.Error("You cannot specify both --file and --query");
            if (settings.ColdRuns < 0)
                return ValidationResult.Error("--cold must be >= 0");
            if (settings.WarmRuns < 0)
                return ValidationResult.Error("--warm must be >= 0");
            if (settings.ColdRuns == 0 && settings.WarmRuns == 0)
                return ValidationResult.Error("You must run at least one cold or warm iteration");
            return base.Validate(context, settings);
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            Log.Information("Starting Benchmark command");
            bool silent = settings.Silent;

            // Install a SynchronizationContext so that Caliburn.Micro's
            // PublishOnUIThreadAsync works in the CLI (no WPF dispatcher).
            // Without this, ServerTimingsEvent may not be delivered to our handler.
            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            // Read query
            string daxQuery = settings.Query;
            if (!string.IsNullOrWhiteSpace(settings.File))
            {
                if (!System.IO.File.Exists(settings.File))
                {
                    if (!silent) AnsiConsole.MarkupLine($"[red]Error:[/] Query file not found: {Markup.Escape(settings.File)}");
                    return 1;
                }
                daxQuery = System.IO.File.ReadAllText(settings.File);
            }

            if (string.IsNullOrWhiteSpace(daxQuery))
            {
                if (!silent) AnsiConsole.MarkupLine("[red]Error:[/] Query is empty");
                return 1;
            }

            // Connect — same pattern as CustomTraceCommand (lines 98-110)
            // Roles/EffectiveUserName are added to the query connection for RLS testing.
            // ClearCache requires admin privileges, so it uses the base connection
            // (without role impersonation) via a separate ConnectionManager.
            string baseConnectionString = settings.FullConnectionString;
            string queryConnectionString = baseConnectionString;
            bool hasImpersonation = false;
            if (!string.IsNullOrWhiteSpace(settings.Role))
            {
                queryConnectionString += $";Roles={settings.Role}";
                hasImpersonation = true;
            }
            if (!string.IsNullOrWhiteSpace(settings.EffectiveUser))
            {
                queryConnectionString += $";EffectiveUserName={settings.EffectiveUser}";
                hasImpersonation = true;
            }

            var connMgr = new ConnectionManager(EventAggregator);
            var connEvent = new UIStubs.ConnectEvent()
            {
                ConnectionString = queryConnectionString,
                ApplicationName = "DAX Studio Command Line",
                DatabaseName = settings.Database,
                PowerBIFileName = settings.PowerBIFileName ?? ""
            };

            try
            {
                if (!silent) AnsiConsole.MarkupLine("[yellow]Connecting...[/]");
                connMgr.Connect(connEvent);
                connMgr.SelectedModel = connMgr.Database.Models.BaseModel;
                connMgr.SelectedModelName = connMgr.SelectedModel.Name;
                if (!silent) AnsiConsole.MarkupLine($"[green]Connected[/] to [bold]{Markup.Escape(connMgr.ServerName)}[/]");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Connection failed");
                if (!silent) AnsiConsole.MarkupLine($"[red]Connection failed:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }

            // Set up trace watcher — same pattern as CustomTraceCommand (lines 112-120)
            var metadataPane = new CmdLineMetadataPane();
            var doc = new CmdLineDocument(connMgr, metadataPane);

            var serverTimes = new CmdServerTimesViewModel(
                EventAggregator, new ServerTimingDetailsViewModel(), Options, null);
            serverTimes.Document = doc;

            // Subscribe to QueryTraceCompletedEvent — fired by ProcessAllEvents()
            // after ProcessResults() completes. This is the same event-driven
            // approach the UI uses (BenchmarkViewModel subscribes to ServerTimingsEvent).
            var timingReady = new ManualResetEventSlim(false);
            EventAggregator.SubscribeOnPublishedThread(
                new TraceCompletedHandler(serverTimes, () => timingReady.Set()));

            // Start trace
            if (!silent) AnsiConsole.MarkupLine("[yellow]Starting server trace...[/]");
            serverTimes.IsChecked = true;

            // Wait for trace to become active
            int waitMs = 0;
            while (serverTimes.TraceStatus != QueryTraceStatus.Started && waitMs < 30000)
            {
                Thread.Sleep(500);
                waitMs += 500;
            }

            bool traceActive = serverTimes.TraceStatus == QueryTraceStatus.Started;
            if (!silent)
            {
                if (traceActive)
                    AnsiConsole.MarkupLine("[green]Server trace active[/] — capturing FE/SE timings");
                else
                    AnsiConsole.MarkupLine("[yellow]Trace unavailable[/] — wall-clock timing only");
            }

            // For cache clearing with RLS impersonation: use a separate admin
            // connection without Roles/EffectiveUserName, since ClearCache
            // requires server admin privileges that the impersonated role lacks.
            ConnectionManager adminConnMgr = null;
            if (hasImpersonation && settings.ColdRuns > 0)
            {
                try
                {
                    adminConnMgr = new ConnectionManager(new Caliburn.Micro.EventAggregator());
                    var adminEvent = new UIStubs.ConnectEvent()
                    {
                        ConnectionString = baseConnectionString,
                        ApplicationName = "DAX Studio Command Line (admin)",
                        DatabaseName = settings.Database,
                        PowerBIFileName = settings.PowerBIFileName ?? ""
                    };
                    adminConnMgr.Connect(adminEvent);
                    adminConnMgr.SelectedModel = adminConnMgr.Database.Models.BaseModel;
                    if (!silent) AnsiConsole.MarkupLine("[dim]Admin connection for cache clear established[/]");
                }
                catch (Exception ex)
                {
                    Log.Warning("Could not create admin connection for cache clear: {message}", ex.Message);
                    adminConnMgr = null;
                }
            }

            // Run benchmark
            int totalRuns = settings.ColdRuns + settings.WarmRuns;
            var details = new List<BenchmarkResult>();
            int sequence = 0;

            for (int i = 0; i < settings.ColdRuns; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                sequence++;

                try
                {
                    var clearConn = adminConnMgr ?? connMgr;
                    clearConn.Database.ClearCache();
                }
                catch (Exception ex) { Log.Warning("Cache clear failed: {message}", ex.Message); }

                timingReady.Reset();
                var r = ExecuteTimedQuery(connMgr, daxQuery, sequence, "Cold",
                    traceActive, serverTimes, timingReady);
                details.Add(r);

                if (!silent)
                    AnsiConsole.MarkupLine($"  Cold {i + 1}/{settings.ColdRuns}: [bold]{r.TotalDurationMs}ms[/] " +
                        $"(FE={r.FormulaEngineDurationMs} SE={r.StorageEngineDurationMs} " +
                        $"SEq={r.StorageEngineQueryCount} rows={r.RowCount})");
            }

            for (int i = 0; i < settings.WarmRuns; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;
                sequence++;

                timingReady.Reset();
                var r = ExecuteTimedQuery(connMgr, daxQuery, sequence, "Warm",
                    traceActive, serverTimes, timingReady);
                details.Add(r);

                if (!silent)
                    AnsiConsole.MarkupLine($"  Warm {i + 1}/{settings.WarmRuns}: [bold]{r.TotalDurationMs}ms[/] " +
                        $"(FE={r.FormulaEngineDurationMs} SE={r.StorageEngineDurationMs} " +
                        $"SEq={r.StorageEngineQueryCount} rows={r.RowCount})");
            }

            // Stop trace and close
            try { await serverTimes.StopTraceAsync(); } catch { }
            connMgr.Close();
            adminConnMgr?.Close();

            if (details.Count == 0)
            {
                if (!silent) AnsiConsole.MarkupLine("[yellow]No benchmark results collected[/]");
                return 1;
            }

            var summary = CalculateSummary(details);
            if (!silent) PrintSummaryTable(summary, traceActive);
            WriteCsvOutput(settings.OutputFile, details, summary);
            if (!silent) AnsiConsole.MarkupLine($"\n[green]Results written to:[/] {Markup.Escape(settings.OutputFile)}");

            Log.Information("Finished Benchmark command");
            return 0;
        }

        private static BenchmarkResult ExecuteTimedQuery(
            ConnectionManager connMgr, string daxQuery, int sequence, string cacheType,
            bool traceActive, CmdServerTimesViewModel serverTimes,
            ManualResetEventSlim timingReady)
        {
            // Reset trace state so we get fresh timings for this run
            if (traceActive) serverTimes.OnReset();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long rowCount = 0;

            try
            {
                using (var reader = connMgr.ExecuteReader(daxQuery, new List<AdomdParameter>()))
                {
                    do
                    {
                        while (reader.Read()) { rowCount++; }
                    } while (reader.NextResult());
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                return new BenchmarkResult
                {
                    Sequence = sequence, CacheType = cacheType,
                    TotalDurationMs = sw.ElapsedMilliseconds, Error = ex.Message
                };
            }
            sw.Stop();

            var result = new BenchmarkResult
            {
                Sequence = sequence, CacheType = cacheType,
                TotalDurationMs = sw.ElapsedMilliseconds, RowCount = rowCount
            };

            // Wait for ProcessResults() to complete via the event-driven signal.
            // QueryTraceCompletedEvent is published by ProcessAllEvents() after
            // ProcessResults() finishes — same signal the UI uses.
            if (traceActive)
            {
                int timeoutMs = Math.Max(15000, (int)sw.ElapsedMilliseconds * 3);
                if (timingReady.Wait(timeoutMs))
                {
                    result.TotalDurationMs = serverTimes.TotalDuration;
                    result.FormulaEngineDurationMs = serverTimes.FormulaEngineDuration;
                    result.StorageEngineDurationMs = serverTimes.StorageEngineDuration;
                    result.StorageEngineQueryCount = serverTimes.StorageEngineQueryCount;
                    result.StorageEngineCpuMs = serverTimes.StorageEngineCpu;
                    result.TotalCpuMs = serverTimes.TotalCpuDuration;
                    result.VertipaqCacheMatches = serverTimes.VertipaqCacheMatches;
                }
            }

            return result;
        }

        #region Summary & Output

        private static List<BenchmarkSummaryRow> CalculateSummary(List<BenchmarkResult> details)
        {
            var summary = new List<BenchmarkSummaryRow>();
            foreach (var group in details.Where(d => d.Error == null).GroupBy(d => d.CacheType))
            {
                var rows = group.ToArray();
                AddStat(summary, group.Key, "Average", rows, Enumerable.Average);
                AddStat(summary, group.Key, "StdDev", rows, StdDev);
                AddStat(summary, group.Key, "Min", rows, Enumerable.Min);
                AddStat(summary, group.Key, "Max", rows, Enumerable.Max);
            }
            return summary;
        }

        private static void AddStat(List<BenchmarkSummaryRow> summary, string cache, string stat,
            BenchmarkResult[] rows, Func<IEnumerable<double>, double> agg)
        {
            summary.Add(new BenchmarkSummaryRow
            {
                CacheType = cache, Statistic = stat,
                TotalDurationMs = agg(rows.Select(r => (double)r.TotalDurationMs)),
                FormulaEngineDurationMs = agg(rows.Select(r => (double)r.FormulaEngineDurationMs)),
                StorageEngineDurationMs = agg(rows.Select(r => (double)r.StorageEngineDurationMs)),
                StorageEngineQueryCount = agg(rows.Select(r => (double)r.StorageEngineQueryCount)),
                VertipaqCacheMatches = agg(rows.Select(r => (double)r.VertipaqCacheMatches)),
                RowCount = agg(rows.Select(r => (double)r.RowCount))
            });
        }

        private static double StdDev(IEnumerable<double> values)
        {
            var arr = values.ToArray();
            if (arr.Length <= 1) return 0;
            double avg = arr.Average();
            return Math.Sqrt(arr.Select(v => Math.Pow(v - avg, 2)).Sum() / (arr.Length - 1));
        }

        private static void PrintSummaryTable(List<BenchmarkSummaryRow> summary, bool hasTrace)
        {
            AnsiConsole.WriteLine();
            var table = new Table().Title("[bold]Benchmark Summary[/]");
            table.AddColumn("Cache");
            table.AddColumn("Statistic");
            table.AddColumn(new TableColumn("Total (ms)").RightAligned());
            if (hasTrace)
            {
                table.AddColumn(new TableColumn("FE (ms)").RightAligned());
                table.AddColumn(new TableColumn("SE (ms)").RightAligned());
                table.AddColumn(new TableColumn("SE Queries").RightAligned());
                table.AddColumn(new TableColumn("SE Cache").RightAligned());
            }
            table.AddColumn(new TableColumn("Rows").RightAligned());

            foreach (var row in summary)
            {
                if (hasTrace)
                    table.AddRow(row.CacheType, row.Statistic,
                        N(row.TotalDurationMs), N(row.FormulaEngineDurationMs),
                        N(row.StorageEngineDurationMs), N(row.StorageEngineQueryCount),
                        N(row.VertipaqCacheMatches), N(row.RowCount));
                else
                    table.AddRow(row.CacheType, row.Statistic,
                        N(row.TotalDurationMs), N(row.RowCount));
            }
            AnsiConsole.Write(table);
        }

        private static string N(double v) => v.ToString("N0", CultureInfo.InvariantCulture);

        private static void WriteCsvOutput(string outputFile, List<BenchmarkResult> details,
            List<BenchmarkSummaryRow> summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sequence,Cache,TotalDuration_ms,FormulaEngineDuration_ms," +
                "StorageEngineDuration_ms,StorageEngineQueryCount,StorageEngineCpu_ms," +
                "TotalCpu_ms,VertipaqCacheMatches,RowCount,Error");

            foreach (var r in details)
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(r.Sequence.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.CacheType),
                    Csv(r.TotalDurationMs.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.FormulaEngineDurationMs.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.StorageEngineDurationMs.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.StorageEngineQueryCount.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.StorageEngineCpuMs.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.TotalCpuMs.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.VertipaqCacheMatches.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.RowCount.ToString(CultureInfo.InvariantCulture)),
                    Csv(r.Error ?? string.Empty)
                }));

            sb.AppendLine().AppendLine("# Summary");
            sb.AppendLine("Cache,Statistic,TotalDuration_ms,FormulaEngineDuration_ms," +
                "StorageEngineDuration_ms,StorageEngineQueryCount,VertipaqCacheMatches,RowCount");

            foreach (var r in summary)
                sb.AppendLine(string.Join(",", new[]
                {
                    Csv(r.CacheType),
                    Csv(r.Statistic),
                    Csv(r.TotalDurationMs.ToString("F1", CultureInfo.InvariantCulture)),
                    Csv(r.FormulaEngineDurationMs.ToString("F1", CultureInfo.InvariantCulture)),
                    Csv(r.StorageEngineDurationMs.ToString("F1", CultureInfo.InvariantCulture)),
                    Csv(r.StorageEngineQueryCount.ToString("F1", CultureInfo.InvariantCulture)),
                    Csv(r.VertipaqCacheMatches.ToString("F1", CultureInfo.InvariantCulture)),
                    Csv(r.RowCount.ToString("F1", CultureInfo.InvariantCulture))
                }));

            var dir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);
        }

        private static string Csv(string value)
        {
            if (value == null) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        #endregion

        #region Data Classes

        internal class BenchmarkResult
        {
            public int Sequence { get; set; }
            public string CacheType { get; set; }
            public long TotalDurationMs { get; set; }
            public long FormulaEngineDurationMs { get; set; }
            public long StorageEngineDurationMs { get; set; }
            public long StorageEngineQueryCount { get; set; }
            public long StorageEngineCpuMs { get; set; }
            public long TotalCpuMs { get; set; }
            public int VertipaqCacheMatches { get; set; }
            public long RowCount { get; set; }
            public string Error { get; set; }
        }

        internal class BenchmarkSummaryRow
        {
            public string CacheType { get; set; }
            public string Statistic { get; set; }
            public double TotalDurationMs { get; set; }
            public double FormulaEngineDurationMs { get; set; }
            public double StorageEngineDurationMs { get; set; }
            public double StorageEngineQueryCount { get; set; }
            public double VertipaqCacheMatches { get; set; }
            public double RowCount { get; set; }
        }

        #endregion

        /// <summary>
        /// Handles QueryTraceCompletedEvent from the event aggregator.
        /// Fired by TraceWatcherBaseViewModel.ProcessAllEvents() after
        /// ProcessResults() completes — signals that IServerTimes properties
        /// on ServerTimesViewModel are now populated with fresh data.
        /// </summary>
        private class TraceCompletedHandler : IHandle<UI.Events.QueryTraceCompletedEvent>
        {
            private readonly ITraceWatcher _traceWatcher;
            private readonly System.Action _callback;
            public TraceCompletedHandler(ITraceWatcher traceWatcher, System.Action callback)
            {
                _traceWatcher = traceWatcher;
                _callback = callback;
            }
            public Task HandleAsync(UI.Events.QueryTraceCompletedEvent message, CancellationToken cancellationToken)
            {
                if (!ReferenceEquals(message.Trace, _traceWatcher)) return Task.CompletedTask;
                _callback();
                return Task.CompletedTask;
            }
        }
    }
}
