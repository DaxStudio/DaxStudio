using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class UseCommand : ScriptCommand
    {
        public UseCommand(string database)
        {
            DatabaseName = database;
        }
        public string DatabaseName {get;}
    }
}
