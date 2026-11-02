using System;

namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// Thrown when a comment-script ("--&gt;") command is recognised but is malformed or invalid
    /// (e.g. a USE with no database name, a TRACE with an unknown trace type, or a CONNECT with the
    /// wrong number of arguments). Unlike a general parser error - which the pre-processor treats as a
    /// soft failure and silently falls back to the classic regex path - a command error is a mistake
    /// the user made in an explicit "--&gt;" command, so it is surfaced with this helpful message
    /// (and the line/column of the offending command) rather than being silently swallowed.
    /// </summary>
    public class CommentScriptCommandException : Exception
    {
        /// <summary>The 1-based line of the offending command (0 when unknown).</summary>
        public int Line { get; }

        /// <summary>The 0-based character position within the line (0 when unknown).</summary>
        public int Column { get; }

        public CommentScriptCommandException(string message) : this(message, 0, 0) { }

        public CommentScriptCommandException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }
    }
}
