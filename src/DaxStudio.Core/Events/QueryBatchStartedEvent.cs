namespace DaxStudio.Core.Events
{
    /// <summary>
    /// Raised by the results target just before it executes one script batch (sections separated by
    /// <c>--&gt; GO</c> run sequentially). The Test Results pane uses it to transition just that batch's
    /// tests from pending to a "running" state while the batch's query executes.
    /// </summary>
    public class QueryBatchStartedEvent
    {
        public QueryBatchStartedEvent(int batchIndex)
        {
            BatchIndex = batchIndex;
        }

        /// <summary>The zero-based index of the script batch that is about to run.</summary>
        public int BatchIndex { get; }
    }
}
