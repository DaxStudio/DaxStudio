using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class QueryCommand
    {
        public QueryCommand(string text) {
            Text = text;
        }

        public string Text { get; }
    }
}
