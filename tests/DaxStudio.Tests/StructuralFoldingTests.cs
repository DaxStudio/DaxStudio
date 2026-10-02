using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class StructuralFoldingTests
    {
        private static ICSharpCode.AvalonEdit.Document.TextDocument Doc(params string[] lines)
        {
            return new ICSharpCode.AvalonEdit.Document.TextDocument(string.Join(Environment.NewLine, lines));
        }

        [TestMethod]
        public void SingleLineConstructIsNotFolded()
        {
            var doc = Doc("EVALUATE ROW(\"a\", 1)");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(0, foldings.Count, "A single line query should produce no folds");
        }

        [TestMethod]
        public void MultiLineFunctionCallIsFolded()
        {
            var doc = Doc(
                "EVALUATE",
                "FILTER(",
                "    'Table',",
                "    'Table'[x] > 1",
                ")");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.IsTrue(foldings.Count >= 1, "Expected at least one fold for the multi-line FILTER call");
            // no fold should collapse to nothing / be single line
            Assert.IsTrue(foldings.All(f => f.EndOffset > f.StartOffset));
        }

        [TestMethod]
        public void FoldingsAreSortedByStartOffset()
        {
            var doc = Doc(
                "DEFINE",
                "    MEASURE 'Sales'[Total] =",
                "        SUMX(",
                "            Sales,",
                "            Sales[Amount]",
                "        )",
                "EVALUATE",
                "    ROW(\"x\", [Total])");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.IsTrue(foldings.Count >= 3, $"Expected folds for DEFINE, MEASURE and SUMX; got {foldings.Count}");
            for (int i = 1; i < foldings.Count; i++)
            {
                Assert.IsTrue(foldings[i].StartOffset >= foldings[i - 1].StartOffset, "Foldings must be sorted by start offset");
            }
        }

        [TestMethod]
        public void VarReturnBlockIsFolded()
        {
            var doc = Doc(
                "EVALUATE",
                "ROW(",
                "    \"v\",",
                "    VAR x = 1",
                "    VAR y = 2",
                "    RETURN x + y",
                ")");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.IsTrue(foldings.Count >= 1, "Expected folds including the VAR/RETURN block");
        }

        [TestMethod]
        public void ConsecutiveAssertTableLinesFoldStartingAtHeaderLine()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "--> ASSERT TABLE",
                "-->> | A | B |",
                "-->> | 1 | 2 |",
                "-->> | 3 | 4 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count, "The run of -->> lines should fold into a single region");
            Assert.AreEqual(" [A, B] (2x2)", foldings[0].Name);
            // fold should start at the end of the "--> ASSERT TABLE" header line (line index 1)
            var headerLine = doc.GetLineByNumber(2);
            Assert.AreEqual(headerLine.EndOffset, foldings[0].StartOffset, "Fold should start on the ASSERT TABLE header line");
        }

        [TestMethod]
        public void AssertTableRunWithoutHeaderFoldsFromFirstContinuationLine()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "-->> | A | B |",
                "-->> | 1 | 2 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(" [A, B] (1x2)", foldings[0].Name);
            var firstRowLine = doc.GetLineByNumber(2);
            Assert.AreEqual(firstRowLine.EndOffset, foldings[0].StartOffset, "Without a header the fold starts on the first -->> line");
        }

        [TestMethod]
        public void AssertTableTitleExcludesSeparatorRow()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "--> ASSERT TABLE",
                "-->> | A | B |",
                "-->> |---|---|",
                "-->> | 1 | 2 |",
                "-->> | 3 | 4 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(" [A, B] (2x2)", foldings[0].Name, "The |---| separator row must not be counted as data");
        }

        [TestMethod]
        public void AssertTableTitleExcludesLeadingTypeRow()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "--> ASSERT TABLE",
                "-->> | A | B |",
                "-->> | STRING | INT64 |",
                "-->> | x | 1 |",
                "-->> | y | 2 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(" [A, B] (2x2)", foldings[0].Name, "The leading DAX type row must not be counted as data");
        }

        [TestMethod]
        public void AssertTableTitleTruncatesColumnListAfterFour()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "--> ASSERT TABLE",
                "-->> | A | B | C | D | E | F | G |",
                "-->> | 1 | 2 | 3 | 4 | 5 | 6 | 7 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(" [A, B, C, D, \u2026] (1x7)", foldings[0].Name,
                "Column list should truncate after 4 columns while the total column count stays 7");
        }

        [TestMethod]
        public void SingleAssertTableLineIsNotFolded()
        {
            var doc = Doc(
                "EVALUATE 'T'",
                "-->> | A | B |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(0, foldings.Count, "A single -->> line has nothing to collapse");
        }

        [TestMethod]
        public void IncompleteDaxDoesNotThrowAndStillFolds()
        {
            var doc = Doc(
                "EVALUATE",
                "FILTER(",
                "    'Table',");
            // Should not throw thanks to parser error recovery
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.IsNotNull(foldings);
        }

        [TestMethod]
        public void GoSeparatedDefineQueryProducesStructuralFolds()
        {
            var doc = Doc(
                "DEFINE",
                "    MEASURE T[First] =",
                "        1",
                "EVALUATE",
                "    { [First] }",
                "--> GO",
                "DEFINE",
                "    MEASURE T[Second] =",
                "        2",
                "EVALUATE",
                "    SUMMARIZECOLUMNS(",
                "        \"Value\", [Second]",
                "    )");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            var secondDefineLine = doc.GetLineByNumber(7);
            var secondMeasureLine = doc.GetLineByNumber(8);
            var secondEvaluateLine = doc.GetLineByNumber(10);

            Assert.IsTrue(foldings.Any(f => f.StartOffset == secondDefineLine.EndOffset),
                "The second DEFINE should start a fold");
            Assert.IsTrue(foldings.Any(f => f.StartOffset == secondMeasureLine.EndOffset),
                "The second batch's measure should start a fold");
            Assert.IsTrue(foldings.Any(f => f.StartOffset == secondEvaluateLine.EndOffset),
                "The second batch's EVALUATE should start a fold");
        }

        [TestMethod]
        public void ConsecutiveCommentScriptCommandsFoldUnderFirstCommand()
        {
            var doc = Doc(
                "--> TEST \"Demo\"",
                "--> CLEARCACHE",
                "--> TRACE QUERYPLAN");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(doc.GetLineByNumber(1).EndOffset, foldings[0].StartOffset);
            Assert.AreEqual(doc.GetLineByNumber(3).EndOffset, foldings[0].EndOffset);
            Assert.AreEqual(" \u2026 2 more commands", foldings[0].Name);
        }

        [TestMethod]
        public void TwoCommentScriptCommandsUseSingularHiddenCount()
        {
            var doc = Doc(
                "--> TEST \"Demo\"",
                "--> CLEARCACHE");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(1, foldings.Count);
            Assert.AreEqual(" \u2026 1 more command", foldings[0].Name);
        }

        [TestMethod]
        public void SingleCommentScriptCommandIsNotFolded()
        {
            var doc = Doc("--> TEST \"Demo\"");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(0, foldings.Count);
        }

        [TestMethod]
        public void NonCommandLinesBreakCommentScriptCommandRuns()
        {
            var doc = Doc(
                "--> TEST \"One\"",
                "",
                "--> CLEARCACHE",
                "-- regular comment",
                "--> TRACE QUERYPLAN",
                "--> UNKNOWN value",
                "--> RESULTS OFF",
                "EVALUATE ROW(\"Value\", 1)",
                "--> SHOW TIMING");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(0, foldings.Count);
        }

        [TestMethod]
        public void GoRemainsVisibleAndStartsNewCommandRun()
        {
            var doc = Doc(
                "--> TEST \"Demo\"",
                "--> CLEARCACHE",
                "--> GO",
                "--> TRACE QUERYPLAN",
                "--> RESULTS OFF",
                "EVALUATE ROW(\"Value\", 1)");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            var commandFoldings = foldings.Where(f => f.Name?.Contains("more command") == true).ToList();
            Assert.AreEqual(2, commandFoldings.Count);

            Assert.AreEqual(doc.GetLineByNumber(1).EndOffset, commandFoldings[0].StartOffset);
            Assert.AreEqual(doc.GetLineByNumber(2).EndOffset, commandFoldings[0].EndOffset);
            Assert.IsTrue(commandFoldings[0].EndOffset < doc.GetLineByNumber(3).Offset,
                "GO must not be hidden in the preceding command fold");

            Assert.AreEqual(doc.GetLineByNumber(3).EndOffset, commandFoldings[1].StartOffset);
            Assert.AreEqual(doc.GetLineByNumber(5).EndOffset, commandFoldings[1].EndOffset);
            Assert.AreEqual(" \u2026 2 more commands", commandFoldings[1].Name);
        }

        [TestMethod]
        public void CommentCommandFoldContainsNestedAssertTableFold()
        {
            var doc = Doc(
                "--> TEST \"Demo\"",
                "--> ASSERT TABLE",
                "-->> | A | B |",
                "-->> | 1 | 2 |",
                "-->> | 3 | 4 |");
            var foldings = new StructuralFoldingStrategy().CreateNewFoldings(doc).ToList();

            Assert.AreEqual(2, foldings.Count);
            var commandFold = foldings.Single(f => f.Name == " \u2026 1 more command");
            var tableFold = foldings.Single(f => f.Name == " [A, B] (2x2)");

            Assert.AreEqual(doc.GetLineByNumber(1).EndOffset, commandFold.StartOffset);
            Assert.AreEqual(doc.GetLineByNumber(5).EndOffset, commandFold.EndOffset);
            Assert.AreEqual(doc.GetLineByNumber(2).EndOffset, tableFold.StartOffset);
            Assert.AreEqual(commandFold.EndOffset, tableFold.EndOffset);
            Assert.IsTrue(commandFold.StartOffset < tableFold.StartOffset,
                "The ASSERT TABLE row fold must be nested inside the command block fold");
        }

        [TestMethod]
        public void PaddedFoldingGeneratorHandlesCommandAndDaxFolds()
        {
            var document = new ICSharpCode.AvalonEdit.Document.TextDocument("01234567890123456789");
            var manager = new ICSharpCode.AvalonEdit.Folding.FoldingManager(document);
            var commandFold = manager.CreateFolding(1, 5);
            commandFold.Title = " \u2026 1 more command";
            commandFold.IsFolded = true;
            var daxFold = manager.CreateFolding(8, 12);
            daxFold.Title = "...";
            daxFold.IsFolded = true;

            var generator = new PaddedFoldingElementGenerator(manager);

            Assert.AreEqual(1, generator.GetFirstInterestedOffset(0));
            Assert.IsNotNull(generator.ConstructElement(commandFold.StartOffset));
            Assert.IsNotNull(generator.ConstructElement(daxFold.StartOffset));
        }
    }
}
