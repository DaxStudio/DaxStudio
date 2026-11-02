namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// Represents a <c>--&gt; RESULTS ON|OFF</c> comment-script command which controls whether the
    /// query result grid is displayed for the run. When placed in a batch it explicitly overrides
    /// the default result-grid visibility (which otherwise defaults to OFF when the script contains
    /// any ASSERT commands, and ON when it does not).
    /// </summary>
    public class ResultsCommand : ScriptCommand
    {
        public ResultsCommand(bool enabled)
        {
            Enabled = enabled;
        }

        /// <summary>True for <c>--&gt; RESULTS ON</c>, false for <c>--&gt; RESULTS OFF</c>.</summary>
        public bool Enabled { get; }
    }
}
