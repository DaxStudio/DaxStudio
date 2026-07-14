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

        public string Comparison { get; }
        public int Value { get; }
    }
}
