using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class OpenCommand : ScriptCommand
    {
        public OpenCommand(string fileName)
        {
            FileName = fileName;
        }

        public string FileName { get; }
    }
}
