using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.Core.Assertions
{
    /// <summary>The kind of assertion that produced a <see cref="TestResult"/>.</summary>
    public enum AssertionKind
    {
        RowCount,
        Performance,
        Table,
    }

    /// <summary>The outcome of evaluating a single comment-script assertion.</summary>
    public enum TestOutcome
    {
        Passed,
        Failed,
        Error,

        /// <summary>
        /// The assertion has been discovered in the editor text but has not been run yet. Shown in the
        /// Test Results pane in a greyed-out "pending" state (with a clock icon) until a query run
        /// produces a real Passed/Failed/Error outcome.
        /// </summary>
        Pending,
    }

    /// <summary>
    /// The UI-independent result of evaluating a single comment-script assertion
    /// (<c>--&gt; ASSERT</c>, <c>--&gt; ASSERT ROWCOUNT</c> or <c>--&gt; ASSERT TABLE</c>).
    /// Produced by <see cref="AssertionEngine"/> so both the DAX Studio UI (Test Results pane)
    /// and the <c>dscmd</c> CLI (console summary + exit code) can render the same data.
    /// </summary>
    public class TestResult
    {
        /// <summary>
        /// The name from the enclosing <c>--&gt; TEST PERFORMANCE "name"</c> header, when present;
        /// otherwise <c>null</c>. Assertions in the same batch share this name.
        /// </summary>
        public string TestName { get; set; }

        /// <summary>The kind of assertion (row count, performance or table).</summary>
        public AssertionKind Kind { get; set; }

        /// <summary>A short human-readable description of the assertion, e.g. <c>ROWCOUNT &gt;= 10</c>.</summary>
        public string Description { get; set; }

        /// <summary>The expected value/condition as text (for display).</summary>
        public string Expected { get; set; }

        /// <summary>The actual value observed as text (for display).</summary>
        public string Actual { get; set; }

        /// <summary>Whether the assertion passed, failed, or errored while being evaluated.</summary>
        public TestOutcome Outcome { get; set; }

        /// <summary>
        /// A message describing the failure or error (empty when the assertion passed).
        /// </summary>
        public string Message { get; set; }

        /// <summary>The 1-based source line of the assertion command (0 when unknown).</summary>
        public int Line { get; set; }

        public bool Passed => Outcome == TestOutcome.Passed;
    }
}
