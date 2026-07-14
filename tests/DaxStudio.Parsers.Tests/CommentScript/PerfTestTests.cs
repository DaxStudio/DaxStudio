using Antlr4.Runtime.Tree;
using Antlr4.Runtime;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class PerfTestTests
    {
        [TestMethod]
        public void BasicPerformanceTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> TEST PERFORMANCE \"Query 1\"\n" +
                "--> ASSERT DURATION < 20\n" +
                "EVALUATE { 1 }\n" +
                "";

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            //var tokens = lexer.GetAllTokens();
            //lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tree = parser.document();

            Assert.AreEqual(2, tree.ChildCount,"Tree Child Count");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(2, batch[0].Commands);

            var testCmd = batch[0].Commands[0] as TestCommand;
            Assert.AreEqual("PERFORMANCE", testCmd.TestType);
            Assert.AreEqual("Query 1", testCmd.TestName);

            var assertCmd = batch[0].Commands[1] as AssertCommand;
            Assert.AreEqual(PerformanceProperty.Duration, assertCmd.Property);
            Assert.AreEqual("<", assertCmd.Comparison);
            Assert.AreEqual(20, assertCmd.IntegerValue);
        }

        [TestMethod]
        public void AllAssertionPerformanceTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> TEST PERFORMANCE \"Query 1\"\n" +
                "--> ASSERT DURATION < 20\n" +
                "--> ASSERT SE_CPU > 10\n" +
                "--> ASSERT SE_QUERIES = 1\n" +
                "EVALUATE { 1 }\n" +
                "";
            List<Error> errors = new List<Error>();
            var errorListener = new PreProcessorErrorListener(ref errors);
            
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
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

            Assert.AreEqual(2, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(4, batch[0].Commands, "Incorrect Command Count");

            var testCmd = batch[0].Commands[0] as TestCommand;
            Assert.AreEqual("PERFORMANCE", testCmd.TestType);
            Assert.AreEqual("Query 1", testCmd.TestName);

            var assertCmd = batch[0].Commands[1] as AssertCommand;
            Assert.AreEqual(PerformanceProperty.Duration, assertCmd.Property);
            Assert.AreEqual("<", assertCmd.Comparison);
            Assert.AreEqual(20, assertCmd.IntegerValue);

            var assertCmd2 = batch[0].Commands[2] as AssertCommand;
            Assert.AreEqual(PerformanceProperty.SE_CPU, assertCmd2.Property);
            Assert.AreEqual(">", assertCmd2.Comparison);
            Assert.AreEqual(10, assertCmd2.IntegerValue);

            var assertCmd3 = batch[0].Commands[3] as AssertCommand;
            Assert.AreEqual(PerformanceProperty.SE_QUERIES, assertCmd3.Property);
            Assert.AreEqual("=", assertCmd3.Comparison);
            Assert.AreEqual(1, assertCmd3.IntegerValue);
        }
    }
}
