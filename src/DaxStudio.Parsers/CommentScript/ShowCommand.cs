using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ShowCommand : ScriptCommand
    {
        public ShowCommand(ShowType showType)
        {
            ShowType = showType;
        }

        public ShowType ShowType { get; }
    }
}
