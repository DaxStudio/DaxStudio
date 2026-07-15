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
            await Task.Run(() =>
                {
                    long durationMs = 0;
                    int queryCnt = 1;

                    var sw = Stopwatch.StartNew();

                    // A "--> SHOW" command produces its own tree-grid output in place of the normal
                    // results grid. Handle it before clearing/resetting the results grid so the
                    // "Waiting for query results" placeholder is never shown for a SHOW command.
                    if (TryHandleShowCommand(runner, textProvider))
                    {
                        return;
                    }

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

                    var isSessionsDmv = batches.Any(b => b.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase));
                    var combined = new DataSet();
                    int tableIdx = 1;
                    bool anyReader = false;

                    foreach (var dq in batches)
                    {
                        // Comment-script commands that change the document state (e.g. "--> CONNECT")
                        // are dispatched in DocumentViewModel.RunQueryInternalAsync before we get here.
                        // Future per-batch commands (CLEAR CACHE, TRACE, USE, ...) would hook in here.
                        var batchIsSessionsDmv = dq.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase);
                        using (var dataReader = runner.ExecuteDataReaderQuery(dq, textProvider.ParameterCollection))
                        {
                            if (dataReader != null)
                            {
                                anyReader = true;
                                Log.Verbose("Start Processing Grid DataReader (Elapsed: {elapsed})", sw.ElapsedMilliseconds);
                                var batchDataSet = dataReader.ConvertToDataSet(autoFormat, batchIsSessionsDmv, autoDateFormat, runner.Connection);
                                AppendTables(combined, batchDataSet, ref tableIdx);
                                Log.Verbose("End Processing Grid DataReader (Elapsed: {elapsed})", sw.ElapsedMilliseconds);
                            }
                        }
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

                });
        }

        // Detects a "--> SHOW" command in any parsed batch and, if found, builds the appropriate
        // tree (query dependencies, or model metadata timestamps) and pushes it into the Results
        // pane instead of running the batch queries. Returns true when a SHOW command was handled.
        internal static bool TryHandleShowCommand(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            var batches = textProvider.QueryInfo?.ScriptBatches;
            if (batches == null || batches.Count == 0) return false;

            ScriptBatch showBatch = null;
            ShowCommand showCommand = null;
            foreach (var batch in batches)
            {
                var cmd = batch.Commands.OfType<ShowCommand>().FirstOrDefault();
                if (cmd != null)
                {
                    showBatch = batch;
                    showCommand = cmd;
                    break;
                }
            }

            if (showCommand == null) return false;

            try
            {
                System.Collections.Generic.List<ShowTreeNode> roots;
                switch (showCommand.ShowType)
                {
                    case ShowType.Dependencies:
                        var query = string.IsNullOrWhiteSpace(showBatch.QueryText)
                            ? textProvider.QueryText
                            : showBatch.QueryText;
                        if (string.IsNullOrWhiteSpace(query))
                        {
                            runner.OutputError("--> SHOW DEPENDENCIES requires a DAX query to analyze");
                            return true;
                        }
                        roots = runner.Connection.BuildQueryDependencyTree(query);
                        break;
                    case ShowType.LastUpdated:
                        roots = runner.Connection.BuildMetadataTimestampTree(false);
                        break;
                    case ShowType.MaxUpdated:
                        roots = runner.Connection.BuildMetadataTimestampTree(true);
                        break;
                    default:
                        runner.OutputError($"Unknown SHOW command type: {showCommand.ShowType}");
                        return true;
                }

                if (roots == null || roots.Count == 0)
                {
                    runner.OutputWarning($"--> SHOW {showCommand.ShowType} returned no items");
                    runner.SetResultsMessage($"SHOW {showCommand.ShowType} returned no items", OutputTarget.Grid);
                    return true;
                }

                runner.DisplayShowTree(roots, showCommand.ShowType);
                runner.OutputMessage($"--> SHOW {showCommand.ShowType} completed");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} error handling SHOW command", nameof(ResultsTargetGrid), nameof(TryHandleShowCommand));
                runner.OutputError($"Error running --> SHOW {showCommand.ShowType}: {ex.Message}");
            }

            return true;
        }

        // Returns the executable query text for each batch to run. When the pre-processor produced
        // multiple non-empty batches (sections separated by "--> GO") each is returned in order.
        // Otherwise a single element equal to the whole processed query text is returned so the
        // classic / single-batch path is byte-identical to the previous behaviour. Batches with no
        // executable DAX (only comment-script commands such as "--> CONNECT") are excluded, so the
        // returned list can be empty when the script contained commands but no query.
        private static System.Collections.Generic.List<string> GetExecutableBatches(IQueryTextProvider textProvider)
        {
            var batches = textProvider.QueryInfo?.ScriptBatches;
            if (batches != null && batches.Count > 1)
            {
                var list = batches.Select(b => b.QueryText)
                                  .Where(t => !string.IsNullOrWhiteSpace(t))
                                  .ToList();
                if (list.Count > 1) return list;
            }

            // Single-batch / classic path: run the whole processed query text, but only if it
            // actually contains executable DAX. A batch that is only comment-script commands has
            // no query text and must not be sent to the server.
            var queryText = textProvider.QueryText;
            return string.IsNullOrWhiteSpace(queryText)
                ? new System.Collections.Generic.List<string>()
                : new System.Collections.Generic.List<string> { queryText };
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
