using System;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class TraceCommand : ScriptCommand
    {
        public TraceCommand(string traceType, bool enabled)
        {
            try
            {
                TraceType = (TraceType)System.Enum.Parse(typeof(TraceType), traceType, true);
            }
            catch
            {
                throw new ArgumentException($"Unable to process TRACE command '{traceType}' is not a valid TraceType");
            }

            Enabled = enabled;
        }

        public TraceType TraceType { get; }
        public bool Enabled { get; }
    }
}
