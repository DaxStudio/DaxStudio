using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.PreProcessor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.Tests.CommentScript
{
    /// <summary>
    /// Parser tests for the <c>--&gt; BASELINE</c> command and the <c>BASELINE</c> operand that may
    /// replace the literal in an <c>--&gt; ASSERT</c> command.
    /// </summary>
    [TestClass]
    public class BaselineCommandTests
    {
        private static List<ScriptBatch> Parse(string input)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var parser = new PreProcessorParser(stream);
            var tree = parser.document();

            var batches = new List<ScriptBatch>();
            var listener = new PreProcessorListener(new Dictionary<string, List<string>>(), batches);
            new ParseTreeWalker().Walk(listener, tree);
            return batches;
        }

        // A two-batch script: the first captures a baseline, the second asserts against it.
        private static string BaselineScript(string assertLine, string baselineName = "\"v1\"")
        {
            return $"--> BASELINE {baselineName}\n" +
                   "EVALUATE { 1 }\n" +
                   "--> GO\n" +
                   assertLine + "\n" +
                   "EVALUATE { 1 }\n";
        }

        [TestMethod]
        public void UnnamedBaselineIsCaptured()
        {
            var batches = Parse("--> BASELINE\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<BaselineCommand>().Single();
            Assert.AreEqual(BaselineReference.DefaultName, cmd.Name);
            Assert.IsTrue(cmd.IsDefault);
            Assert.AreEqual(1, cmd.Runs);
        }

        [TestMethod]
        public void NamedBaselineIsCaptured()
        {
            var batches = Parse("--> BASELINE \"original\"\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<BaselineCommand>().Single();
            Assert.AreEqual("original", cmd.Name);
            Assert.IsFalse(cmd.IsDefault);
        }

        [TestMethod]
        public void BaselineNameMayBeAnUnquotedIdentifier()
        {
            var batches = Parse("--> BASELINE original\nEVALUATE { 1 }\n");

            Assert.AreEqual("original", batches[0].Commands.OfType<BaselineCommand>().Single().Name);
        }

        [TestMethod]
        public void PerformanceAssertAcceptsABaselineOperand()
        {
            var batches = Parse(BaselineScript("--> ASSERT DURATION <= BASELINE \"v1\""));

            var cmd = batches[1].Commands.OfType<AssertCommand>().Single();
            Assert.AreEqual(PerformanceProperty.Duration, cmd.Property);
            Assert.AreEqual("<=", cmd.Comparison);
            Assert.IsNotNull(cmd.Baseline);
            Assert.AreEqual("v1", cmd.Baseline.Name);
            Assert.AreEqual(1.0, cmd.Baseline.Factor);
        }

        [TestMethod]
        public void PerformanceAssertAcceptsABaselineFactor()
        {
            var batches = Parse(BaselineScript("--> ASSERT DURATION <= BASELINE \"v1\" * 1.1"));

            var cmd = batches[1].Commands.OfType<AssertCommand>().Single();
            Assert.AreEqual(1.1, cmd.Baseline.Factor);
        }

        [TestMethod]
        public void PerformanceAssertAcceptsAnIntegerBaselineFactor()
        {
            var batches = Parse(BaselineScript("--> ASSERT SE_QUERIES <= BASELINE \"v1\" * 2"));

            var cmd = batches[1].Commands.OfType<AssertCommand>().Single();
            Assert.AreEqual(PerformanceProperty.SE_QUERIES, cmd.Property);
            Assert.AreEqual(2.0, cmd.Baseline.Factor);
        }

        [TestMethod]
        public void PerformanceAssertStillAcceptsALiteral()
        {
            var batches = Parse("--> ASSERT DURATION < 500\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<AssertCommand>().Single();
            Assert.IsNull(cmd.Baseline);
            Assert.AreEqual(500, cmd.IntegerValue);
        }

        [TestMethod]
        public void PerformanceAssertStillAcceptsARealLiteral()
        {
            var batches = Parse("--> ASSERT DURATION < 1.5\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<AssertCommand>().Single();
            Assert.IsNull(cmd.Baseline);
            Assert.AreEqual(1.5, cmd.DoubleValue);
        }

        [TestMethod]
        public void RowcountAssertAcceptsABaselineOperand()
        {
            var batches = Parse(BaselineScript("--> ASSERT ROWCOUNT = BASELINE \"v1\""));

            var cmd = batches[1].Commands.OfType<AssertRowcountCommand>().Single();
            Assert.AreEqual("=", cmd.Comparison);
            Assert.AreEqual("v1", cmd.Baseline.Name);
        }

        [TestMethod]
        public void RowcountAssertStillAcceptsALiteral()
        {
            var batches = Parse("--> ASSERT ROWCOUNT >= 10\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<AssertRowcountCommand>().Single();
            Assert.IsNull(cmd.Baseline);
            Assert.AreEqual(10, cmd.Value);
        }

        [TestMethod]
        public void AssertTableAcceptsABaselineOperand()
        {
            var batches = Parse(BaselineScript("--> ASSERT TABLE BASELINE \"v1\""));

            var cmd = batches[1].Commands.OfType<AssertTableCommand>().Single();
            Assert.AreEqual(AssertTableMode.Ordered, cmd.Mode);
            Assert.AreEqual("v1", cmd.Baseline.Name);
            Assert.IsTrue(cmd.HasTableDefinition);
        }

        [TestMethod]
        public void AssertTableBaselineKeepsItsModeModifier()
        {
            var batches = Parse(BaselineScript("--> ASSERT TABLE UNORDERED BASELINE \"v1\""));

            var cmd = batches[1].Commands.OfType<AssertTableCommand>().Single();
            Assert.AreEqual(AssertTableMode.Unordered, cmd.Mode);
            Assert.AreEqual("v1", cmd.Baseline.Name);
        }

        [TestMethod]
        public void AssertTableWithoutABaselineIsUnaffected()
        {
            var batches = Parse("--> ASSERT TABLE CSV \"expected.csv\"\nEVALUATE { 1 }\n");

            var cmd = batches[0].Commands.OfType<AssertTableCommand>().Single();
            Assert.IsNull(cmd.Baseline);
            Assert.AreEqual(AssertTableFormat.Csv, cmd.Format);
            Assert.AreEqual("expected.csv", cmd.FilePath);
        }

        [TestMethod]
        public void UnnamedBaselineCanBeReferencedWithoutAName()
        {
            var batches = Parse(BaselineScript("--> ASSERT DURATION <= BASELINE", baselineName: string.Empty));

            var cmd = batches[1].Commands.OfType<AssertCommand>().Single();
            Assert.IsTrue(cmd.Baseline.IsDefault);
        }

        [TestMethod]
        public void ReferencingAnUndefinedBaselineThrows()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> ASSERT DURATION <= BASELINE \"nope\"\nEVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void ForwardReferencingABaselineThrows()
        {
            // The baseline is captured in a LATER batch, so it can never hold data when the
            // assertion runs - batches execute in order.
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> ASSERT DURATION <= BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n" +
                      "--> GO\n" +
                      "--> BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void DuplicateBaselineNameThrows()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n" +
                      "--> GO\n" +
                      "--> BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void DuplicateUnnamedBaselineThrows()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> BASELINE\n" +
                      "EVALUATE { 1 }\n" +
                      "--> GO\n" +
                      "--> BASELINE\n" +
                      "EVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void AssertingAgainstTheBaselineDefinedInTheSameBatchThrows()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> BASELINE \"v1\"\n" +
                      "--> ASSERT DURATION <= BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void ZeroBaselineFactorThrows()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse(BaselineScript("--> ASSERT DURATION <= BASELINE \"v1\" * 0")));
        }

        [TestMethod]
        public void RunsGreaterThanOneThrowsUntilImplemented()
        {
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> BASELINE \"v1\" RUNS 5\nEVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void RunsOneIsAccepted()
        {
            var batches = Parse("--> BASELINE \"v1\" RUNS 1\nEVALUATE { 1 }\n");

            Assert.AreEqual(1, batches[0].Commands.OfType<BaselineCommand>().Single().Runs);
        }

        [TestMethod]
        public void CombiningInlineRowsWithABaselineThrows()
        {
            // The expected table would come from the baseline, so the inline rows would be silently
            // ignored - that contradiction is a hard error.
            Assert.Throws<CommentScriptCommandException>(() =>
                Parse("--> BASELINE \"v1\"\n" +
                      "EVALUATE { 1 }\n" +
                      "--> GO\n" +
                      "--> ASSERT TABLE BASELINE \"v1\"\n" +
                      "-->> | Color |\n" +
                      "-->> | Red   |\n" +
                      "EVALUATE { 1 }\n"));
        }

        [TestMethod]
        public void NewKeywordsDoNotBreakUnquotedConnectAndUseValues()
        {
            // CS_BASELINE / CS_RUNS / CS_STAR were added to the comment-script lexer mode. The
            // 'unquoted_value' rule used by CONNECT/USE accepts any token except a newline, so a
            // database or server name that happens to contain one of the new keywords must still parse.
            var batches = Parse("--> CONNECT SERVER localhost\\baseline\n" +
                                "--> USE BASELINE\n" +
                                "EVALUATE { 1 }\n");

            Assert.AreEqual("localhost\\baseline", batches[0].Commands.OfType<ConnectCommand>().Single().ConnectionName);
            Assert.AreEqual("BASELINE", batches[0].Commands.OfType<UseCommand>().Single().DatabaseName);
        }

        [TestMethod]
        public void NewKeywordsDoNotBreakMultiWordDatabaseNames()
        {
            var batches = Parse("--> USE Baseline Runs Report\nEVALUATE { 1 }\n");

            Assert.AreEqual("Baseline Runs Report", batches[0].Commands.OfType<UseCommand>().Single().DatabaseName);
        }

        [TestMethod]
        public void MalformedCommandsAreReportedAsErrorsNotCrashes()
        {
            // ANTLR error-recovers over missing tokens by inserting "<missing ...>" placeholders. These
            // must surface as command errors - an unhandled FormatException / NullReferenceException
            // would abort the tree walk and silently drop EVERY command in the script.
            foreach (var script in new[]
            {
                "--> BASELINE RUNS\nEVALUATE { 1 }\n",
                "--> BASELINE \"v1\" RUNS\nEVALUATE { 1 }\n",
                "--> ASSERT DURATION <=\nEVALUATE { 1 }\n",
                "--> ASSERT ROWCOUNT >=\nEVALUATE { 1 }\n",
            })
            {
                var result = AntlrPreProcessor.Parse(script);
                Assert.IsNotEmpty(result.CommandErrors, $"expected a command error for: {script}");
            }
        }

        [TestMethod]
        public void MalformedBaselineFactorIsReportedAsAnError()
        {
            var result = AntlrPreProcessor.Parse(
                "--> BASELINE \"v1\"\n" +
                "EVALUATE { 1 }\n" +
                "--> GO\n" +
                "--> ASSERT DURATION <= BASELINE \"v1\" *\n" +
                "EVALUATE { 1 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
        }

        [TestMethod]
        public void BaselineBatchKeepsItsQueryText()
        {
            // QueryText is assigned by AntlrPreProcessor (not the raw tree walk), so go through the
            // full pre-processor to verify the "--> BASELINE" line is stripped from the executable DAX.
            var result = AntlrPreProcessor.Parse("--> BASELINE \"v1\"\nEVALUATE { 1 }\n--> GO\nEVALUATE { 2 }\n");

            Assert.IsEmpty(result.CommandErrors);
            StringAssert.Contains(result.Batches[0].QueryText, "EVALUATE { 1 }");
            Assert.DoesNotContain("BASELINE", result.Batches[0].QueryText);
            StringAssert.Contains(result.Batches[1].QueryText, "EVALUATE { 2 }");
        }

        [TestMethod]
        public void BaselineScriptParsesWithoutCommandErrors()
        {
            var result = AntlrPreProcessor.Parse(
                "--> TEST \"Sales optimisation\"\n" +
                "--> BASELINE \"original\"\n" +
                "--> CLEARCACHE\n" +
                "EVALUATE { 1 }\n" +
                "--> GO\n" +
                "--> CLEARCACHE\n" +
                "--> ASSERT TABLE BASELINE \"original\"\n" +
                "--> ASSERT DURATION <= BASELINE \"original\" * 1.1\n" +
                "--> ASSERT SE_QUERIES <= BASELINE \"original\"\n" +
                "EVALUATE { 1 }\n");

            Assert.IsEmpty(result.CommandErrors);
            Assert.HasCount(1, result.Batches[0].Commands.OfType<BaselineCommand>().ToList());
            Assert.HasCount(2, result.Batches[1].Commands.OfType<AssertCommand>().ToList());
        }

        [TestMethod]
        public void UndefinedBaselineIsReportedAsACommandError()
        {
            // The pre-processor catches CommentScriptCommandException and surfaces it as a command
            // error rather than letting it escape, so the user sees a clear message in the editor.
            var result = AntlrPreProcessor.Parse("--> ASSERT DURATION <= BASELINE \"nope\"\nEVALUATE { 1 }\n");

            Assert.IsNotEmpty(result.CommandErrors);
            StringAssert.Contains(result.CommandErrors[0].Msg, "nope");
        }

        [TestMethod]
        public void BaselineComposesWithTestAndClearCache()
        {
            var batches = Parse("--> TEST \"Sales optimisation\"\n" +
                                "--> BASELINE \"v1\"\n" +
                                "--> CLEARCACHE\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> CLEARCACHE\n" +
                                "--> ASSERT TABLE BASELINE \"v1\"\n" +
                                "--> ASSERT DURATION <= BASELINE \"v1\" * 1.1\n" +
                                "EVALUATE { 1 }\n");

            Assert.AreEqual("Sales optimisation", batches[0].Commands.OfType<TestCommand>().Single().TestName);
            Assert.HasCount(1, batches[0].Commands.OfType<BaselineCommand>().ToList());
            Assert.HasCount(1, batches[0].Commands.OfType<ClearCacheCommand>().ToList());
            Assert.HasCount(1, batches[1].Commands.OfType<AssertTableCommand>().ToList());
            Assert.HasCount(1, batches[1].Commands.OfType<AssertCommand>().ToList());
        }
    }
}
