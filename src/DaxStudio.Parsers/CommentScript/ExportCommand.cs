using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ExportCommand : ScriptCommand
    {
        public ExportCommand(ExportTarget target, string fileName = null)
        {
            Target = target;
            FileName = fileName;
        }

        public ExportTarget Target { get; }
        public string FileName { get; set; }
    }
}
