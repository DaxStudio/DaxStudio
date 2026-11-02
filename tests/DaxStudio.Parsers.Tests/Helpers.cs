using Antlr4.Runtime;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using System.Collections.Generic;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    internal static class Helpers
    {
        internal static PreProcessorParser.DocumentContext ConfigureLexerAndParser(string input, ref List<Error> errors)
        {
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var errorListener = new PreProcessorErrorListener(ref errors);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new BufferedTokenStream(lexer);
            //var tokens = lexer.GetAllTokens();
            //lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();
            return tree;
        }


        //internal static  CommentScriptIntellisenseParser.DocumentContext ConfigureLexerAndParser2(string input, ref List<Error> errors)
        //{
        //    ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
        //    var errorListener = new PreProcessorErrorListener(ref errors);
        //    PreProcessorLexer lexer = new PreProcessorLexer(chars);
        //    lexer.RemoveErrorListeners();
        //    lexer.AddErrorListener(errorListener);
        //    ITokenStream stream = new BufferedTokenStream(lexer);
        //    //var tokens = lexer.GetAllTokens();
        //    //lexer.Reset();
        //    PreProcessorParser parser = new PreProcessorParser(stream);
        //    parser.RemoveErrorListeners();
        //    parser.AddErrorListener(errorListener);
        //    var tree = parser.document();
        //    return tree;
        //}

    }
}
