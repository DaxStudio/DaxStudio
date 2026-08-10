using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class AssertRowcountCommand : ScriptCommand
    {
        public AssertRowcountCommand(string comparison, int value)
        {
            Comparison = comparison;
            Value = value;
        }

        /// <summary>
        /// Creates a row-count assertion whose expected value comes from a previously captured
        /// baseline (<c>--&gt; ASSERT ROWCOUNT = BASELINE "v1"</c>) rather than a literal.
        /// </summary>
        public AssertRowcountCommand(string comparison, BaselineReference baseline)
        {
            Comparison = comparison;
            Baseline = baseline;
        }

        public string Comparison { get; }
        public int Value { get; }

        /// <summary>
        /// The baseline supplying the expected row count, or <c>null</c> when the assertion compares
        /// against the literal in <see cref="Value"/>.
        /// </summary>
        /// <remarks>
        /// Settable only so the post-parse resolver can replace an unresolved <c>PREVIOUS</c> reference.
        /// </remarks>
        public BaselineReference Baseline { get; set; }
    }
}
