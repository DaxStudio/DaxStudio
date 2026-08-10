using System.Collections.Generic;
using System.Linq;
using System.Text;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ScriptBatch
    {
        public List<ScriptCommand> Commands { get; } = new List<ScriptCommand>();
        public StringBuilder Output { get;  } = new StringBuilder();

        /// <summary>
        /// The executable DAX for this batch: the batch's section of the original input with any
        /// comment-script (<c>--&gt;</c>) command lines removed and whitespace/formatting preserved.
        /// (<see cref="Output"/> cannot be used for this because whitespace is lexed on a hidden
        /// channel and would be stripped, producing invalid DAX.)
        /// </summary>
        public string QueryText { get; set; } = string.Empty;

        /// <summary>
        /// True when this batch's DAX is consumed as the target of an analysis rather than executed.
        /// <c>--&gt; SHOW DEPENDENCIES</c> and <c>--&gt; SHOW DIAGRAM</c> parse the batch's query to work
        /// out which objects to report on and deliberately never send it to the server; every other
        /// <c>SHOW</c> variant ignores the DAX and leaves it to run normally.
        /// </summary>
        /// <remarks>
        /// This is the single source of truth for that rule: it is used both by the batch-execution loop
        /// (to decide what to skip) and by the <c>PREVIOUS</c> resolver (to decide which batch produces
        /// results and timings worth comparing against). Keeping one definition means the parser and the
        /// runtime cannot drift apart about which batches actually run.
        /// </remarks>
        public bool ConsumesQueryAsAnalysisTarget =>
            Commands.OfType<ShowCommand>()
                .Any(s => s.ShowType == ShowType.Dependencies || s.ShowType == ShowType.Diagram);

        /// <summary>
        /// True when this batch sends DAX to the server - it has a query and does not consume that query
        /// as an analysis target (see <see cref="ConsumesQueryAsAnalysisTarget"/>). Only such a batch
        /// produces the result set and Server Timings a baseline comparison needs.
        /// </summary>
        public bool RunsItsQuery =>
            !string.IsNullOrWhiteSpace(QueryText) && !ConsumesQueryAsAnalysisTarget;
    }
}
