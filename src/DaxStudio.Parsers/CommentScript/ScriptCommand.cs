using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public abstract class ScriptCommand
    {
        /// <summary>1-based source line of the comment-script command (0 when unknown).</summary>
        public int Line { get; set; }
    }
}
