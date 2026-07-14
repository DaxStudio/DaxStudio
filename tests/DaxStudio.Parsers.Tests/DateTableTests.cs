using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class DateTableTests
    {
        [TestMethod]
        public void DateTableTest()
        {
            var input = DaxDateTable.DateTable;
            List<Error> errors = new List<Error>();
            var errorListener = new PreProcessorErrorListener(ref errors);

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);

            ITokenStream stream = new CommonTokenStream(lexer);
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.block();

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batches);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.IsEmpty(errors);
        }
    }
}
