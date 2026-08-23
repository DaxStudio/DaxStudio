using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
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
    public class MDXTests
    {
        [TestMethod]
        public void SimpleMDXReferenceTest()
        {
            var input = @"SELECT {[Measures].[Total Sales]} ON 0,
[Product].[Color].Members * [Product Category].[Product Category].Members ON 1
FROM [Adventure Works]
WHERE [Customer].[Country].[Australia]";
            

            List<Error> errors = new List<Error>();

            var errorListener = new PreProcessorErrorListener(ref errors);
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            var lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            ITokenStream stream = new CommonTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            var parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();

            Assert.AreEqual(2, tree.ChildCount);
            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "Error count should be 0");

            var output = new StringBuilder();
            var listener = new PreFormatListener(output, true);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);
        }
    }
}
