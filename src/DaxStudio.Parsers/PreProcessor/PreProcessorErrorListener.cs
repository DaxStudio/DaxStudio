using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    public class Error
    {
        public string Stack { get; set; } = "";
        public string Symbol { get; set; } = "";
        public string Msg { get; set; } = "";
        public string Exception { get; set; } = "";
    }

    public class PreProcessorErrorListener : BaseErrorListener, IAntlrErrorListener<int>
    {
        public List<Error> Errors { get; private set; }

        public PreProcessorErrorListener(ref List<Error> errors)
        {
            Errors = errors;
        }
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Error error = new Error();

            if (recognizer.GetType() == typeof(PreProcessorParser))
                error.Stack = (recognizer as PreProcessorParser).GetRuleInvocationStackAsString();

            error.Msg = $"{msg} at {line}:{charPositionInLine}";
            System.Diagnostics.Debug.WriteLine($"ANTLR ERROR: {error.Msg}");

            if (offendingSymbol != null)
                error.Symbol = offendingSymbol.Text;

            if (e != null)
                error.Exception = e.GetType().ToString();

            this.Errors.Add(error);
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            SyntaxError(output, recognizer, new CommonToken(offendingSymbol), line, charPositionInLine, msg, e);
        }
    }
}
