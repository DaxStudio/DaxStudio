using System;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class MetricsCommand : ScriptCommand
    {
        public MetricsCommand(MetricsAction action, string fileName = null)
        {
            Action = action;
            FileName = fileName;
        }

        public MetricsAction Action { get; }
        public string FileName { get; }
    }
}
