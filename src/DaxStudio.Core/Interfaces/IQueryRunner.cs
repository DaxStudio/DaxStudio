using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.Core.Interfaces
{
    public interface IQueryRunner
    {
        string QueryText { get; }
        Task<DataTable> ExecuteDataTableQueryAsync(string daxQuery);
        AdomdDataReader ExecuteDataReaderQuery(string daxQuery, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList);
        DataSet ResultsDataSet { get; set; }
        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void OutputMessage(OutputMessage message);
        void OutputWarning(string warning);
        void OutputError(string errorMessage);
        void OutputError(string errorMessage, double duration);
        void ActivateResults();
        void ActivateOutput();
        //bool IsOutputActive { get; }
        void QueryCompleted();
        void QueryCompleted(bool isCancelled);
        void QueryFailed(string errorMessage);
        IDaxStudioHost Host { get; }
        string SelectedWorksheet { get; set; }
        string ConnectionStringWithInitialCatalog { get; }
        bool ConnectedToPowerPivot { get; }

        void SetResultsMessage(string message, OutputTarget icon);
        void SetResultsMessage(string message, OutputTarget icon, string fileName);
        IStatusBarMessage NewStatusBarMessage(string message);
        int RowCount { get; set; }

        IGlobalOptions Options { get; }
        //ADOTabular.ADOTabularConnection Connection { get; }
        DaxStudio.Core.Connections.ConnectionManager Connection { get; }
        void OutputQueryError(string errorMessage);
        void ClearQueryError();
        void ClearQueryResults();

        /// <summary>
        /// Populates the Results pane with an ordered, interspersed set of tabs - a mix of query-result
        /// data grids and the tree-grids produced by Comment Script <c>--&gt; SHOW</c> commands
        /// (DEPENDENCIES / LAST_UPDATED / MAX_UPDATED) - preserving batch execution order.
        /// </summary>
        void SetResultTabs(IList<DaxStudio.Core.Model.ResultTabDescriptor> tabs);

        /// <summary>
        /// Called synchronously by the results-target batch loop immediately BEFORE a batch's query is
        /// executed (batches are separated by <c>--&gt; GO</c> and run sequentially). Lets the runner
        /// arm any per-batch state - e.g. resetting the Server Timings trace and arming a completion
        /// signal - so that batch's performance metrics can be captured in isolation. A no-op when the
        /// script contains no test assertions.
        /// </summary>
        void PrepareBatchAssertions(int batchIndex);

        /// <summary>
        /// Called (and awaited) by the results-target batch loop immediately BEFORE a batch's query is
        /// executed, ahead of <see cref="PrepareBatchAssertions"/>. Runs the comment-script commands that
        /// must take effect per batch rather than once per script - currently just
        /// <c>--&gt; CLEARCACHE</c>, so that a script comparing a baseline batch to a candidate batch can
        /// give each batch the same cold-cache starting point. Batch 0's commands are already handled by
        /// the whole-script pre-query pass, so this is a no-op for the first batch.
        /// </summary>
        Task ProcessBatchPreQueryCommandsAsync(int batchIndex);

        /// <summary>
        /// Waits at a delayed <c>--&gt; GO</c> boundary. Implementations should cancel the wait when
        /// the active query run is cancelled.
        /// </summary>
        Task WaitForBatchDelayAsync(int milliseconds);

        /// <summary>
        /// Called (and awaited) by the results-target batch loop immediately AFTER a batch's query has
        /// produced its result tables, before the next batch starts. Evaluates just this batch's
        /// assertions - waiting for and capturing this batch's Server Timings slice for any performance
        /// assertions - and updates the Test Results pane for this batch only, so a completed batch
        /// shows its outcome while later batches remain pending. A no-op when the script contains no
        /// test assertions.
        /// </summary>
        Task ProcessBatchAssertionsAsync(int batchIndex, IReadOnlyList<System.Data.DataTable> batchTables);
    }
}
