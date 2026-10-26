using Antlr4.Runtime;
using Serilog;
using System.IO;

namespace DaxStudio.Parsers.StorageEngine
{
    /// <summary>
    /// Logs xmSQL diagnostics without changing ANTLR's partial-parse recovery behavior.
    /// Shared by formatting, analysis, and fingerprinting; DirectQuery is unaffected.
    /// </summary>
    internal sealed class XmSqlErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        internal static readonly XmSqlErrorListener Instance = new XmSqlErrorListener();

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            LogError("lexer", line, charPositionInLine, msg, e);
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            LogError("parser", line, charPositionInLine, msg, e);
        }

        private static void LogError(string stage, int line, int charPositionInLine, string message, RecognitionException exception)
        {
            // Match the common logging convention without adding a dependency on DaxStudio.Common.
            // Report both line and column as one-based. ANTLR diagnostics may include token text;
            // do not additionally log the entire query.
            Log.Debug(exception, "{class} {method} {message}: {Stage} error at line {Line}, column {Column}: {Diagnostic}",
                nameof(XmSqlErrorListener), nameof(SyntaxError), "xmSQL parsing",
                stage, line, charPositionInLine + 1, message);
        }
    }
}
