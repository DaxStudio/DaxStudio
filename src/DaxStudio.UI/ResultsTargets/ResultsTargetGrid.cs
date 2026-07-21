using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using Serilog;
using DaxStudio.UI.Extensions;
using System.Data;
using System.Linq;
using DaxStudio.Common.Extensions;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetGrid: PropertyChangedBase, IResultsTarget 
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;



        [ImportingConstructor]
        public ResultsTargetGrid(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _options = options;
        }

        #region Standard Properties
        public string Name => "Results Table";
        public string Group => "Standard";
        public int DisplayOrder => 10;
        public bool IsDefault => true;
        public bool IsAvailable => true;
        public string Message => "Query results will be displayed in a data grid";
        public OutputTarget Icon => OutputTarget.Grid;
        public string ImageResource => "results_tableDrawingImage";
        public string Tooltip => "Displays the Query results in a data grid";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        // This is the core method that handles the output of the results
        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {
            // Read the AutoFormat option from the options singleton
            bool autoFormat = _options.ResultAutoFormat;
            string autoDateFormat = _options.DefaultDateAutoFormat;
            await Task.Run(async () =>
                {
                    // When any batch contains a "--> SHOW" command the results become a heterogeneous,
                    // interspersed set of tabs (query grids and SHOW trees in batch execution order).
                    // Otherwise we keep the classic single/multi data-batch path byte-for-byte unchanged.
                    if (AnyShowCommand(textProvider))
                    {
                        await RunInterspersedBatches(runner, textProvider, autoFormat, autoDateFormat);
                    }
                    else
                    {
                        await RunDataBatches(runner, textProvider, autoFormat, autoDateFormat);
                    }
                });
        }

        // The classic path: no "--> SHOW" commands anywhere. Runs each executable batch and appends its
        // result tables into a single combined DataSet, exactly as before.
        private async Task RunDataBatches(IQueryRunner runner, IQueryTextProvider textProvider, bool autoFormat, string autoDateFormat)
        {
            long durationMs = 0;
            int queryCnt = 1;

            var sw = Stopwatch.StartNew();

            // Clear any existing results
            runner.ResultsDataSet = new DataSet();
            runner.SetResultsMessage("Waiting for query results", OutputTarget.Grid);
            runner.RowCount = 0;

            // When the pre-processor produced more than one executable batch (script sections
            // separated by "--> GO") run each in turn and append its result tables. Otherwise
            // this is a single batch equal to the whole processed query text (unchanged path).
            // Batches that contain only comment-script commands (e.g. "--> CONNECT" with no
            // DAX after it) have no executable query text and are excluded here.
            var batches = GetExecutableBatches(textProvider);

            if (batches.Count == 0)
            {
                // The script contained only comment-script commands (no DAX to run). Those
                // commands (e.g. "--> CONNECT") were already processed, so report success
                // instead of sending an empty query to the server.
                sw.Stop();
                runner.RowCount = 0;
                runner.SetResultsMessage("Command(s) completed successfully - no query to run", OutputTarget.Grid);
                runner.OutputMessage("Command(s) completed successfully - no query to run", sw.ElapsedMilliseconds);
                return;
            }

            var isSessionsDmv = batches.Any(b => b.QueryText.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase));
            var combined = new DataSet();
            int tableIdx = 1;
            bool anyReader = false;

            foreach (var (batchIndex, dq) in batches)
            {
                // Comment-script commands that change the document state (e.g. "--> CONNECT")
                // are dispatched in DocumentViewModel.RunQueryInternalAsync before we get here.
                // Future per-batch commands (CLEAR CACHE, TRACE, USE, ...) would hook in here.
                // Let the runner arm any per-batch assertion state (e.g. reset the Server Timings
                // trace) BEFORE the query runs so this batch's metrics are captured in isolation.
                runner.PrepareBatchAssertions(batchIndex);

                // Signal that this batch's query is starting so the Test Results pane can mark just
                // this batch's tests as running (batches run sequentially).
                _ = _eventAggregator.PublishAsync(new DaxStudio.Core.Events.QueryBatchStartedEvent(batchIndex));

                var batchIsSessionsDmv = dq.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase);
                var batchTables = new System.Collections.Generic.List<System.Data.DataTable>();
                using (var dataReader = runner.ExecuteDataReaderQuery(dq, textProvider.ParameterCollection))
                {
                    if (dataReader != null)
                    {
                        anyReader = true;
                        Log.Verbose("Start Processing Grid DataReader (Elapsed: {elapsed})", sw.ElapsedMilliseconds);
                        var batchDataSet = dataReader.ConvertToDataSet(autoFormat, batchIsSessionsDmv, autoDateFormat, runner.Connection);
                        // Capture the batch's tables before AppendTables moves them into the combined set,
                        // so this batch's assertions can be evaluated against them.
                        batchTables.AddRange(batchDataSet.Tables.Cast<System.Data.DataTable>());
                        AppendTables(combined, batchDataSet, ref tableIdx);
                        Log.Verbose("End Processing Grid DataReader (Elapsed: {elapsed})", sw.ElapsedMilliseconds);
                    }
                }

                // Evaluate just this batch's assertions (waiting for / capturing this batch's Server
                // Timings slice) before the next batch starts, so a completed batch's tests show their
                // outcome while later batches remain pending. A no-op when the script has no asserts.
                await runner.ProcessBatchAssertionsAsync(batchIndex, batchTables);
            }

            sw.Stop();
            durationMs = sw.ElapsedMilliseconds;

            if (anyReader)
            {
                // add extended properties to DataSet
                combined.ExtendedProperties.Add("QueryText", textProvider.QueryText);
                combined.ExtendedProperties.Add("IsDiscoverSessions", isSessionsDmv);

                // Assign the fully populated DataSet so the results pane is notified and
                // binds the grid (assigning the property is what raises the change event).
                runner.ResultsDataSet = combined;

                var rowCnt = combined.Tables.Count > 0 ? combined.Tables[0].Rows.Count : 0;
                foreach (DataTable tbl in combined.Tables)
                {
                    runner.OutputMessage(
                        string.Format("Query {2} Completed ({0:N0} row{1} returned)", tbl.Rows.Count,
                                        tbl.Rows.Count == 1 ? "" : "s", queryCnt));
                    queryCnt++;
                }
                runner.RowCount = rowCnt;
                // activate the result only when Counters are not selected...
                runner.ActivateResults();
                runner.OutputMessage("Query Batch Completed", durationMs);
            }
            else
                runner.OutputError("Query Batch Completed with errors listed above (you may need to scroll up)", durationMs);
        }

        // The interspersed path: at least one batch is a "--> SHOW" command. Iterates the parsed
        // batches in order and builds an ordered list of tab descriptors - a SHOW tree-grid for each
        // SHOW batch and a query-result grid for each executable DAX batch - then hands the ordered
        // list to the runner so the tabs appear interspersed in execution order.
        private async Task RunInterspersedBatches(IQueryRunner runner, IQueryTextProvider textProvider, bool autoFormat, string autoDateFormat)
        {
            var sw = Stopwatch.StartNew();

            runner.SetResultsMessage("Waiting for query results", OutputTarget.Grid);
            runner.RowCount = 0;

            var batches = textProvider.QueryInfo?.ScriptBatches;
            var tabs = new System.Collections.Generic.List<ResultTabDescriptor>();
            int tableIdx = 1;
            int queryCnt = 1;
            bool anyError = false;

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];
                var showCommands = batch.Commands.OfType<ShowCommand>().ToList();
                if (showCommands.Count > 0)
                {
                    // A batch can contain several "--> SHOW" commands (e.g. SHOW MAX_UPDATED then
                    // SHOW LAST_UPDATED). Build a tree-grid tab for each, in command order. Errors and
                    // empty results are reported and simply add no tab.
                    if (TryHandleShowBatch(runner, textProvider, batch, out var showDescriptors))
                    {
                        tabs.AddRange(showDescriptors);
                    }

                    // SHOW DEPENDENCIES consumes the batch DAX as its analysis target, so that query is
                    // not run separately. Every other SHOW variant ignores the DAX, so any query in the
                    // same batch should still run and produce its own result tab(s) after the SHOW tab(s).
                    if (showCommands.Any(sc => sc.ShowType == ShowType.Dependencies)) continue;
                }

                var dax = batch.QueryText;
                if (string.IsNullOrWhiteSpace(dax)) continue; // comment-only batch (e.g. "--> CONNECT")

                // Let the runner arm any per-batch assertion state before the query runs.
                runner.PrepareBatchAssertions(batchIndex);

                // Signal that this batch's query is starting so the Test Results pane can mark just
                // this batch's tests as running (batches run sequentially).
                _ = _eventAggregator.PublishAsync(new DaxStudio.Core.Events.QueryBatchStartedEvent(batchIndex));

                var batchIsSessionsDmv = dax.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase);
                var batchTables = new System.Collections.Generic.List<DataTable>();
                using (var dataReader = runner.ExecuteDataReaderQuery(dax, textProvider.ParameterCollection))
                {
                    if (dataReader != null)
                    {
                        var batchDataSet = dataReader.ConvertToDataSet(autoFormat, batchIsSessionsDmv, autoDateFormat, runner.Connection);
                        foreach (var tbl in batchDataSet.Tables.Cast<DataTable>().ToList())
                        {
                            batchDataSet.Tables.Remove(tbl);
                            batchTables.Add(tbl);
                            tbl.TableName = tableIdx.ToString();
                            tableIdx++;
                            tabs.Add(ResultTabDescriptor.ForTable(tbl));
                            runner.OutputMessage(
                                string.Format("Query {2} Completed ({0:N0} row{1} returned)", tbl.Rows.Count,
                                                tbl.Rows.Count == 1 ? "" : "s", queryCnt));
                            queryCnt++;
                        }
                    }
                    else
                    {
                        anyError = true;
                    }
                }

                // Evaluate just this batch's assertions before the next batch starts (no-op without asserts).
                await runner.ProcessBatchAssertionsAsync(batchIndex, batchTables);
            }

            sw.Stop();
            var durationMs = sw.ElapsedMilliseconds;

            runner.SetResultTabs(tabs);

            // report the row-count of the first data-grid tab (SHOW tabs have no row data)
            var firstDataTab = tabs.FirstOrDefault(t => !t.IsShowTree);
            runner.RowCount = firstDataTab?.Table?.Rows.Count ?? 0;
            runner.ActivateResults();

            if (anyError)
                runner.OutputError("Query Batch Completed with errors listed above (you may need to scroll up)", durationMs);
            else
                runner.OutputMessage("Query Batch Completed", durationMs);
        }

        // Returns true if any parsed batch contains a "--> SHOW" command.
        private static bool AnyShowCommand(IQueryTextProvider textProvider)
        {
            var batches = textProvider.QueryInfo?.ScriptBatches;
            if (batches == null) return false;
            return batches.Any(b => b.Commands.OfType<ShowCommand>().Any());
        }

        // Builds the SHOW tree-grid descriptors for every "--> SHOW" command in a batch. Returns true
        // when the batch contained at least one SHOW command (handled), regardless of whether any tab was
        // produced: successful commands add a descriptor to 'descriptors' (in command order); commands
        // that error or return no items report the error/warning and add nothing. Returns false (with an
        // empty list) when the batch has no SHOW command.
        internal static bool TryHandleShowBatch(IQueryRunner runner, IQueryTextProvider textProvider, ScriptBatch batch, out System.Collections.Generic.List<ResultTabDescriptor> descriptors)
        {
            descriptors = new System.Collections.Generic.List<ResultTabDescriptor>();
            var showCommands = batch?.Commands.OfType<ShowCommand>().ToList();
            if (showCommands == null || showCommands.Count == 0) return false;

            foreach (var showCommand in showCommands)
            {
                if (TryBuildShowTab(runner, textProvider, batch, showCommand, out var descriptor) && descriptor != null)
                {
                    descriptors.Add(descriptor);
                }
            }

            return true;
        }

        // Builds the tree-grid descriptor for a single "--> SHOW" command. On success 'descriptor' is the
        // tree-grid tab; on error/empty it is null and the error or warning has already been reported.
        private static bool TryBuildShowTab(IQueryRunner runner, IQueryTextProvider textProvider, ScriptBatch batch, ShowCommand showCommand, out ResultTabDescriptor descriptor)
        {
            descriptor = null;
            if (showCommand == null) return false;

            try
            {
                System.Collections.Generic.List<ShowTreeNode> roots;
                switch (showCommand.ShowType)
                {
                    case ShowType.Dependencies:
                        var query = string.IsNullOrWhiteSpace(batch.QueryText)
                            ? textProvider.QueryText
                            : batch.QueryText;
                        if (string.IsNullOrWhiteSpace(query))
                        {
                            runner.OutputError("--> SHOW DEPENDENCIES requires a DAX query to analyze");
                            return true;
                        }
                        roots = runner.Connection.BuildQueryDependencyTree(query);
                        break;
                    case ShowType.LastUpdated:
                    case ShowType.MaxUpdated:
                        // The timestamp trees are built purely from the connection metadata (no DAX),
                        // so they share the same code path used by the metadata-pane context menu.
                        return TryBuildTimestampShowTab(runner, showCommand.ShowType, out descriptor);
                    default:
                        runner.OutputError($"Unknown SHOW command type: {showCommand.ShowType}");
                        return true;
                }

                if (roots == null || roots.Count == 0)
                {
                    runner.OutputWarning($"--> SHOW {showCommand.ShowType} returned no items");
                    return true;
                }

                descriptor = ResultTabDescriptor.ForShowTree(roots, showCommand.ShowType);
                runner.OutputMessage($"--> SHOW {showCommand.ShowType} completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} error handling SHOW command", nameof(ResultsTargetGrid), nameof(TryBuildShowTab));
                runner.OutputError($"Error running --> SHOW {showCommand.ShowType}: {ex.Message}");
            }

            return true;
        }

        // Builds a metadata timestamp tree-grid tab (SHOW LAST_UPDATED / SHOW MAX_UPDATED) directly
        // from the connection. Shared by the "--> SHOW" command dispatcher and the metadata-pane
        // database context menu so both produce byte-identical results, messages and error handling.
        // Returns true when the request was handled (a produced descriptor, an empty-result warning or
        // an error message); descriptor is non-null only on success.
        internal static bool TryBuildTimestampShowTab(IQueryRunner runner, ShowType showType, out ResultTabDescriptor descriptor)
        {
            descriptor = null;
            try
            {
                System.Collections.Generic.List<ShowTreeNode> roots;
                switch (showType)
                {
                    case ShowType.LastUpdated:
                        roots = runner.Connection.BuildMetadataTimestampTree(false);
                        break;
                    case ShowType.MaxUpdated:
                        roots = runner.Connection.BuildMetadataTimestampTree(true);
                        break;
                    default:
                        runner.OutputError($"Unknown SHOW command type: {showType}");
                        return true;
                }

                if (roots == null || roots.Count == 0)
                {
                    runner.OutputWarning($"--> SHOW {showType} returned no items");
                    return true;
                }

                descriptor = ResultTabDescriptor.ForShowTree(roots, showType);
                runner.OutputMessage($"--> SHOW {showType} completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} error handling SHOW command", nameof(ResultsTargetGrid), nameof(TryBuildTimestampShowTab));
                runner.OutputError($"Error running --> SHOW {showType}: {ex.Message}");
            }

            return true;
        }

        // Runs a SHOW LAST_UPDATED / SHOW MAX_UPDATED request outside the query pipeline (e.g. from the
        // metadata-pane database context menu) and displays the resulting timestamp tree as the single
        // result tab, exactly as running the equivalent "--> SHOW" command on its own would.
        internal static void RunMetadataTimestampShow(IQueryRunner runner, ShowType showType)
        {
            if (TryBuildTimestampShowTab(runner, showType, out var descriptor) && descriptor != null)
            {
                runner.SetResultTabs(new System.Collections.Generic.List<ResultTabDescriptor> { descriptor });
                runner.ActivateResults();
            }
        }

        // Returns the executable query text for each batch to run, paired with the zero-based index of
        // the script batch it came from (so callers can map a running batch back to its assertions).
        // When the pre-processor produced multiple non-empty batches (sections separated by "--> GO")
        // each is returned in order. Otherwise a single element equal to the whole processed query text
        // is returned (batch index 0) so the classic / single-batch path is byte-identical to the
        // previous behaviour. Batches with no executable DAX (only comment-script commands such as
        // "--> CONNECT") are excluded, so the returned list can be empty when the script contained
        // commands but no query.
        private static System.Collections.Generic.List<(int BatchIndex, string QueryText)> GetExecutableBatches(IQueryTextProvider textProvider)
        {
            var batches = textProvider.QueryInfo?.ScriptBatches;
            if (batches != null && batches.Count > 1)
            {
                var list = batches.Select((b, idx) => (BatchIndex: idx, QueryText: b.QueryText))
                                  .Where(t => !string.IsNullOrWhiteSpace(t.QueryText))
                                  .ToList();
                if (list.Count > 1) return list;
            }

            // Single-batch / classic path: run the whole processed query text, but only if it
            // actually contains executable DAX. A batch that is only comment-script commands has
            // no query text and must not be sent to the server.
            var queryText = textProvider.QueryText;
            return string.IsNullOrWhiteSpace(queryText)
                ? new System.Collections.Generic.List<(int, string)>()
                : new System.Collections.Generic.List<(int, string)> { (0, queryText) };
        }

        // Moves the tables from a single batch's result DataSet into the accumulating DataSet,
        // renaming them to a running sequential index so table names stay unique across batches
        // (this matches the naming a single multi-result query would produce).
        private static void AppendTables(DataSet target, DataSet source, ref int tableIdx)
        {
            foreach (var tbl in source.Tables.Cast<DataTable>().ToList())
            {
                source.Tables.Remove(tbl);
                tbl.TableName = tableIdx.ToString();
                tableIdx++;
                target.Tables.Add(tbl);
            }
        }

    }

}
