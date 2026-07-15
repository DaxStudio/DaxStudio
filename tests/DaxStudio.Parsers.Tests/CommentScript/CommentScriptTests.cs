using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class CommentScriptTests
    {


        [TestMethod]
        public void ConnectCommandTest()
        {
            // --> USE ""Adventure Works""
            var input = @"--> CONNECT SERVER localhost\tab19
";
         
            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tree = parser.document();

            Assert.AreEqual(2, tree.ChildCount,"Tree Child Count");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters,batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
        }

        [TestMethod]
        public void ConnectAndQueryTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> CONNECT SERVER localhost\\tab19\n" +
                "--> USE \"Adventure Works\"\n" +
                "EVALUATE { 1 }\n" +
                "";

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            //var tokens = lexer.GetAllTokens();
            //lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            var tree = parser.document();

            Assert.AreEqual(2, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(2, batch[0].Commands);
            
            var connCmd = batch[0].Commands[0] as ConnectCommand;
            Assert.AreEqual(ConnectionType.SERVER, connCmd.ConnectionType);
            Assert.AreEqual("localhost\\tab19", connCmd.ConnectionName);

            var useCmd = batch[0].Commands[1] as UseCommand;
            Assert.AreEqual("Adventure Works", useCmd.DatabaseName);
        }

        [TestMethod]
        //[ExpectedException(typeof(ArgumentException))]
        
        public void InvalidConnectTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> CONNECT XX localhost\\tab19\n" +
                "--> USE \"Adventure Works\"\n" +
                "EVALUATE { 1 }\n" +
                "";

            List<Error> errors = new List<Error>();
            var errorListener = new PreProcessorErrorListener(ref errors);

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);

            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();

            Assert.HasCount(2, errors);

            Assert.AreEqual(2, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            Assert.ThrowsExactly<ArgumentException>(
                () => walker.Walk(listener, tree)
            );

            //Assert.HasCount(2, batch[0].Commands);

            //var connCmd = batch[0].Commands[0] as ConnectCommand;
            //Assert.AreEqual(ConnectionType.SERVER, connCmd.ConnectionType);
            //Assert.AreEqual("localhost\\tab19", connCmd.ConnectionName);

            //var useCmd = batch[0].Commands[1] as UseCommand;
            //Assert.AreEqual("Adventure Works", useCmd.DatabaseName);
        }

        [TestMethod]
        public void InvalidCommandTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> WRONG\n" +
                "EVALUATE { 1 }\n" +
                "";

            List<Error> errors = new List<Error>();
            var errorListener = new PreProcessorErrorListener(ref errors);

            ICharStream chars = new DAXCharStream(input);  // CharStreams.fromString(input);
            PreProcessorLexer lexer = new PreProcessorLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);

            ITokenStream stream = new BufferedTokenStream(lexer);
            var tokens = lexer.GetAllTokens();
            lexer.Reset();
            PreProcessorParser parser = new PreProcessorParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            var tree = parser.document();

            Assert.HasCount(1, errors);
            Assert.StartsWith("mismatched input 'WRONG' expecting", errors[0].Msg);
            Assert.AreEqual(2, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.IsEmpty(batch[0].Commands);


            Assert.AreEqual("EVALUATE { 1 }\n", batch[0].Output.ToString());
        }

        [TestMethod]
        public void ParameterAndQueryTest()
        {
            // --> USE ""Adventure Works""
            var input = "// leading comment\n" +
                "--> PARAMETER @Color = Red\n" +
                "EVALUATE { @Color }\n" +
                "";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(3, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);

            var paramCmd = batch[0].Commands[0] as ParameterCommand;
            Assert.IsNotNull(paramCmd);
            Assert.AreEqual("@Color", paramCmd.ParameterName);
            Assert.AreEqual("Red", paramCmd.Value);
        }


        [TestMethod]
        public void PartialQuerySelectedTest()
        {
            // --> USE ""Adventure Works""
            var input = "EVALUATE { @Co";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(2, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.IsEmpty(batch[0].Commands);

            
            Assert.AreEqual(input, batch[0].Output.ToString(), "input should not be modified");
        }

        [TestMethod]
        public void BlankCommandTest()
        {
            // --> USE ""Adventure Works""
            var input = "--> ";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(2, tree.ChildCount,"Tree Count");
            Assert.IsNull(tree.exception);

            // A lone "--> " reports a single error: a command keyword is expected. The DAX query is
            // now optional after a command (so a commands-only block like "--> SHOW LAST_UPDATED" is
            // valid), therefore no separate "missing query" error is raised.
            Assert.HasCount(1, errors);

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.IsEmpty(batch[0].Commands, "Batch 0 Command Count");
            
        }

        [TestMethod]
        public void ParameterArrayAndQueryTest()
        {
            // --> USE ""Adventure Works""
            var input = @"// leading comment
--> PARAMETER @Color = {""Red"",""Blue""}
DEFINE VAR blah = 1
EVALUATE { @Color }
-- trailing comment";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(3, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);

            var paramCmd = batch[0].Commands[0] as ParameterCommand;
            Assert.IsNotNull(paramCmd);
            Assert.AreEqual("@Color", paramCmd.ParameterName);
            Assert.IsTrue(paramCmd.Value is List<string>);
            var list = paramCmd.Value as List<string>;
            Assert.HasCount(2, list);
            Assert.AreEqual("Red", list[0]);
            Assert.AreEqual("Blue", list[1]);

            var expected = @"// leading comment
--> PARAMETER @Color = {""Red"",""Blue""}
DEFINE VAR blah = 1
EVALUATE { @Color }
-- trailing comment";
            Assert.AreEqual(expected, batch[0].Output.ToString());
        }

        [TestMethod]
        public void ParameterArrayWithUnquotedValuesAndQueryTest()
        {
            // --> USE ""Adventure Works""
            var input = @"// leading comment
--> PARAMETER @Color = { ""Red"" , Blue}
DEFINE VAR blah = 1
EVALUATE { @Color }
-- trailing comment";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.AreEqual(3, tree.ChildCount, "Tree Child Count");
            Assert.IsNull(tree.exception);

            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var output = new StringBuilder();
            var daxParameters = new List<string>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);

            var paramCmd = batch[0].Commands[0] as ParameterCommand;
            Assert.IsNotNull(paramCmd);
            Assert.AreEqual("@Color", paramCmd.ParameterName);
            Assert.IsTrue(paramCmd.Value is List<string>);
            var list = paramCmd.Value as List<string>;
            Assert.HasCount(2, list);
            Assert.AreEqual("Red", list[0]);
            Assert.AreEqual("Blue", list[1]);

            var expected = @"// leading comment
--> PARAMETER @Color = {""Red"",Blue}
DEFINE VAR blah = 1
EVALUATE { @Color }
-- trailing comment";
            Assert.AreEqual(expected, batch[0].Output.ToString());
        }

        [TestMethod]
        public void ConnectPbixWithFullPathTest()
        {
            var input = "--> CONNECT PBIX \"C:\\reports\\Sales.pbix\"\nEVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var connCmd = batch[0].Commands[0] as ConnectCommand;
            Assert.IsNotNull(connCmd);
            Assert.AreEqual(ConnectionType.PBIX, connCmd.ConnectionType);
            Assert.IsTrue(connCmd.IsFilePath, "IsFilePath should be true for a full .pbix path");
            Assert.AreEqual("C:\\reports\\Sales.pbix", connCmd.FilePath);
            Assert.AreEqual("Sales", connCmd.InstanceName);
        }

        [TestMethod]
        public void ConnectPbixWithBareNameTest()
        {
            var input = "--> CONNECT PBIX SalesReport\nEVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var connCmd = batch[0].Commands[0] as ConnectCommand;
            Assert.IsNotNull(connCmd);
            Assert.AreEqual(ConnectionType.PBIX, connCmd.ConnectionType);
            Assert.IsFalse(connCmd.IsFilePath, "IsFilePath should be false for a bare instance name");
            Assert.IsNull(connCmd.FilePath);
            Assert.AreEqual("SalesReport", connCmd.InstanceName);
        }

        [TestMethod]
        public void ConnectServerIsNotAFilePathTest()
        {
            var input = "--> CONNECT SERVER localhost\\tab19\nEVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            var connCmd = batch[0].Commands[0] as ConnectCommand;
            Assert.IsNotNull(connCmd);
            Assert.AreEqual(ConnectionType.SERVER, connCmd.ConnectionType);
            Assert.IsFalse(connCmd.IsFilePath, "SERVER connections are never file paths");
            Assert.IsNull(connCmd.FilePath);
            Assert.AreEqual("localhost\\tab19", connCmd.InstanceName);
        }

        [TestMethod]
        public void ClearCacheCommandTest()
        {
            var input = "--> CLEARCACHE\nEVALUATE { 1 }\n";

            List<Error> errors = new List<Error>();
            PreProcessorParser.DocumentContext tree = Helpers.ConfigureLexerAndParser(input, ref errors);

            Assert.IsNull(tree.exception);
            Assert.IsEmpty(errors, "The errors list should be empty");

            Dictionary<string, List<string>> arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            Assert.HasCount(1, batch[0].Commands);
            Assert.IsInstanceOfType<ClearCacheCommand>(batch[0].Commands[0]);
        }

    }
}
