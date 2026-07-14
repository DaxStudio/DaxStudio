using System.Linq;
using Caliburn.Micro;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class NewPreprocessorTests
    {
        private IEventAggregator _eventAggregator;

        [TestInitialize]
        public void Init()
        {
            _eventAggregator = Substitute.For<IEventAggregator>();
        }

        private static IGlobalOptions NewParserOptions()
        {
            var options = Substitute.For<IGlobalOptions>();
            options.UseNewPreprocessor.Returns(true);
            return options;
        }

        private const string ParamBlock = @"<Parameters xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns=""urn:schemas-microsoft-com:xml-analysis"">
  <Parameter>
    <Name>param</Name>
    <Value xsi:type=""xsd:string"">hello</Value>
  </Parameter></Parameters>";

        [DataTestMethod]
        [DataRow("EVALUATE\nFILTER( 'table', 'table'[col] = @param )", 1)]
        [DataRow("EVALUATE\nFILTER( 'table', 'table'[email] = \"abc @gmail.com\" || 'table'[col] = @param )", 1)]
        [DataRow("EVALUATE\nADDCOLUMNS( { \"Hello\" }, \"@test\", 42 )", 0)]
        [DataRow("EVALUATE\nFILTER( 't@ble', 't@ble'[em@il] = \"x@y.com\" || 'table'[col] = @param )", 1)]
        public void ParamDiscoveryMatchesRegexParser(string query, int expectedParamCount)
        {
            var classic = new QueryInfo(query, _eventAggregator);
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            Assert.AreEqual(expectedParamCount, classic.Parameters.Count, "regex path param count");
            Assert.AreEqual(classic.Parameters.Count, antlr.Parameters.Count, "new path should discover the same @params as the regex path");
            Assert.AreEqual(classic.NeedsParameterValues, antlr.NeedsParameterValues, "NeedsParameterValues should match");
        }

        [TestMethod]
        public void XmlParameterBlockHonoredWithNewParser()
        {
            var query = "EVALUATE\nFILTER( 'table', 'table'[col] = @param )\n" + ParamBlock;

            var classic = new QueryInfo(query, _eventAggregator);
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            Assert.IsFalse(antlr.NeedsParameterValues, "supplied <Parameters> block should satisfy the value");
            Assert.AreEqual(
                classic.QueryWithMergedParameters.NormalizeNewline(),
                antlr.QueryWithMergedParameters.NormalizeNewline(),
                "merged query should be identical to the regex path");
            StringAssert.Contains(antlr.QueryWithMergedParameters, "\"hello\"");
        }

        [TestMethod]
        public void BareParamStillNeedsValuesWithNewParser()
        {
            var query = "EVALUATE\nFILTER( 'table', 'table'[col] = @param )";
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            Assert.AreEqual(1, antlr.Parameters.Count);
            Assert.IsTrue(antlr.NeedsParameterValues, "a bare @param with no supplied value must still prompt");
        }

        [TestMethod]
        public void ParameterCommandIsExecuted()
        {
            var query = "--> PARAMETER @param = \"world\"\nEVALUATE\nFILTER( 'table', 'table'[col] = @param )";
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            Assert.IsFalse(antlr.NeedsParameterValues, "the --> PARAMETER command supplies the value");
            var merged = antlr.QueryWithMergedParameters;
            StringAssert.Contains(merged, "\"world\"");
            Assert.IsFalse(merged.Contains("@param"), "@param should have been substituted");
            Assert.IsFalse(merged.Contains("-->"), "the comment-script command line should be stripped from the executable query");
        }

        [TestMethod]
        public void ScriptBatchesPopulatedForBothPaths()
        {
            var query = "EVALUATE\nROW( \"x\", 1 )";

            var classic = new QueryInfo(query, _eventAggregator);
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            Assert.IsTrue(classic.ScriptBatches.Count >= 1, "regex path should expose at least one batch");
            Assert.IsTrue(antlr.ScriptBatches.Count >= 1, "new path should expose at least one batch");
        }

        [TestMethod]
        public void ParameterCommandProducesParameterCommandInBatch()
        {
            var query = "--> PARAMETER @param = \"world\"\nEVALUATE\nROW( \"x\", @param )";
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            var paramCommands = antlr.ScriptBatches.SelectMany(b => b.Commands).OfType<ParameterCommand>().ToList();
            Assert.IsTrue(paramCommands.Any(c => c.ParameterName == "@param"), "batch should contain the parsed PARAMETER command");
        }

        [TestMethod]
        public void GoSeparatorProducesTwoBatchesEachWithExecutableText()
        {
            var query = "EVALUATE\nROW( \"a\", 1 )\n--> GO\nEVALUATE\nROW( \"b\", 2 )";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(query);

            Assert.IsFalse(result.HasErrors, "a --> GO separated script should parse cleanly");
            Assert.AreEqual(2, result.Batches.Count, "each --> GO section should be its own batch");
            StringAssert.Contains(result.Batches[0].QueryText, "\"a\"");
            Assert.IsFalse(result.Batches[0].QueryText.Contains("\"b\""), "batch 1 should not contain batch 2 text");
            StringAssert.Contains(result.Batches[1].QueryText, "\"b\"");
            Assert.IsFalse(result.Batches[1].QueryText.Contains("\"a\""), "batch 2 should not contain batch 1 text");
            // whitespace/formatting preserved (the space inside ROW( "a", 1 ) survives)
            StringAssert.Contains(result.Batches[0].QueryText, "ROW( \"a\", 1 )");
            Assert.IsFalse(result.Batches[0].QueryText.Contains("-->"), "the GO line must be stripped from executable text");
        }

        [TestMethod]
        public void TerminalGoLeavesTrailingBatchWithNoExecutableText()
        {
            var query = "EVALUATE\nROW( \"a\", 1 )\n--> GO";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(query);

            Assert.IsFalse(result.HasErrors);
            StringAssert.Contains(result.Batches[0].QueryText, "\"a\"");
            // any batch created by the terminal GO must not contain executable DAX
            for (int i = 1; i < result.Batches.Count; i++)
                Assert.IsTrue(string.IsNullOrWhiteSpace(result.Batches[i].QueryText),
                    "a batch created by a terminal --> GO should have no executable text");
        }

        [TestMethod]
        public void NoGoSingleBatchQueryTextMatchesProcessedText()
        {
            var query = "EVALUATE\nROW( \"x\", 1 )";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(query);

            Assert.AreEqual(1, result.Batches.Count);
            Assert.AreEqual(result.ProcessedText.NormalizeNewline(), result.Batches[0].QueryText.NormalizeNewline(),
                "with no GO the single batch text should equal the whole processed text");
        }

        [TestMethod]
        public void CommandLinesStrippedWithinEachBatchSegment()
        {
            var query = "--> CLEAR CACHE\nEVALUATE\nROW( \"a\", 1 )\n--> GO\n--> CLEAR CACHE\nEVALUATE\nROW( \"b\", 2 )";
            var result = DaxStudio.Parsers.PreProcessor.AntlrPreProcessor.Parse(query);

            Assert.AreEqual(2, result.Batches.Count);
            Assert.IsFalse(result.Batches[0].QueryText.Contains("-->"), "command lines stripped in batch 1");
            Assert.IsFalse(result.Batches[1].QueryText.Contains("-->"), "command lines stripped in batch 2");
            StringAssert.Contains(result.Batches[0].QueryText, "\"a\"");
            StringAssert.Contains(result.Batches[1].QueryText, "\"b\"");
        }

        [TestMethod]
        public void ClassicPathBatchExposesExecutableQueryText()
        {
            var query = "EVALUATE\nROW( \"x\", 1 )";
            var classic = new QueryInfo(query, _eventAggregator);

            Assert.AreEqual(1, classic.ScriptBatches.Count);
            StringAssert.Contains(classic.ScriptBatches[0].QueryText, "EVALUATE");
        }

        [TestMethod]
        public void HistoryTextKeepsCommentScriptCommandsButProcessedQueryStripsThem()
        {
            var query = "--> PARAMETER @param = \"world\"\nEVALUATE\nFILTER( 'table', 'table'[col] = @param )";
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            StringAssert.Contains(antlr.HistoryText, "--> PARAMETER", "history should retain the comment-script command");
            Assert.IsFalse(antlr.ProcessedQuery.Contains("-->"), "the executable query must have the command line stripped");
        }

        [TestMethod]
        public void HistoryTextEqualsProcessedQueryOnClassicPath()
        {
            var query = "EVALUATE\nROW( \"x\", 1 )";
            var classic = new QueryInfo(query, _eventAggregator);

            Assert.AreEqual(classic.ProcessedQuery, classic.HistoryText,
                "on the classic path history text should be identical to the processed query");
        }

        [TestMethod]
        public void HistoryTextExcludesXmlParametersBlock()
        {
            var query = "--> PARAMETER @param = \"world\"\nEVALUATE\nFILTER( 'table', 'table'[col] = @param )\n" + ParamBlock;
            var antlr = new QueryInfo(query, _eventAggregator, NewParserOptions());

            StringAssert.Contains(antlr.HistoryText, "--> PARAMETER", "history keeps the comment-script command");
            Assert.IsFalse(antlr.HistoryText.Contains("<Parameters"), "the <Parameters> XML block should be excluded from history");
        }
    }
}
