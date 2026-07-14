using System.Collections.Generic;
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
    }
}
