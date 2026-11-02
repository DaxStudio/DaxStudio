using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// A "--&gt; SAVEAS &lt;filename&gt;" comment-script command that saves the current document to the
    /// given path after the query has run. A ".daxx" path saves a full package (query plus trace /
    /// results state such as server timings); any other extension (e.g. ".dax") saves just the query
    /// text. <see cref="FileName"/> is settable so <c>$(...)</c> references in the path can be expanded
    /// at run time by <see cref="ScriptVariableExpander"/>.
    /// </summary>
    public class SaveAsCommand : ScriptCommand
    {
        public SaveAsCommand(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; set; }
    }
}
