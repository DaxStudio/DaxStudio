using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Custom ANTLR4 error strategy for intellisense parsing.
    /// Recovers gracefully from partial/incomplete input by consuming tokens
    /// until a synchronization point is found, rather than throwing exceptions.
    /// </summary>
    public class DaxIntellisenseErrorStrategy : DefaultErrorStrategy
    {
        public List<string> Errors { get; } = new List<string>();

        protected override void ReportNoViableAlternative(Parser recognizer, NoViableAltException e)
        {
            Errors.Add($"line {e.OffendingToken.Line}:{e.OffendingToken.Column} no viable alternative at '{e.OffendingToken.Text}'");
        }

        protected override void ReportInputMismatch(Parser recognizer, InputMismatchException e)
        {
            Errors.Add($"line {e.OffendingToken.Line}:{e.OffendingToken.Column} mismatched input '{e.OffendingToken.Text}'");
        }

        protected override void ReportUnwantedToken(Parser recognizer)
        {
            if (InErrorRecoveryMode(recognizer)) return;
            BeginErrorCondition(recognizer);
            var t = recognizer.CurrentToken;
            Errors.Add($"line {t.Line}:{t.Column} extraneous input '{t.Text}'");
        }

        protected override void ReportMissingToken(Parser recognizer)
        {
            if (InErrorRecoveryMode(recognizer)) return;
            BeginErrorCondition(recognizer);
            var t = recognizer.CurrentToken;
            Errors.Add($"line {t.Line}:{t.Column} missing token before '{t.Text}'");
        }

        public override void Recover(Parser recognizer, RecognitionException e)
        {
            // Single-token deletion/insertion recovery — let the default handle it,
            // but don't throw so parsing can continue for the intellisense tree.
            try
            {
                base.Recover(recognizer, e);
            }
            catch (Exception)
            {
                // Swallow — we want partial parse trees for intellisense
            }
        }

        public override IToken RecoverInline(Parser recognizer)
        {
            try
            {
                return base.RecoverInline(recognizer);
            }
            catch (InputMismatchException)
            {
                // Return the current token to keep parsing going
                return recognizer.CurrentToken;
            }
        }
    }

    /// <summary>
    /// Silent error listener that collects errors without writing to stderr.
    /// </summary>
    public class DaxIntellisenseErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public List<string> Errors { get; } = new List<string>();

        public override void SyntaxError(
            TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add($"line {line}:{charPositionInLine} {msg}");
        }

        public void SyntaxError(
            TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Errors.Add($"line {line}:{charPositionInLine} {msg}");
        }
    }
}
