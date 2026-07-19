using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DaxStudio.Parsers.Tests.CommentScript
{
    [TestClass]
    public class TableAssertionFormatterTests
    {
        private static DataTable MakeTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("Color", typeof(string));
            dt.Columns.Add("Count", typeof(long));
            dt.Columns.Add("Amount", typeof(double));
            dt.Columns.Add("Price", typeof(decimal));
            dt.Columns.Add("Active", typeof(bool));
            dt.Columns.Add("Created", typeof(DateTime));

            dt.Rows.Add("Red", 5L, 1.5d, 9.99m, true, new DateTime(2020, 1, 2));
            dt.Rows.Add("Blue", 3L, 2.25d, 12.50m, false, new DateTime(2021, 3, 4, 5, 6, 7));
            return dt;
        }

        // Parses a generated ASSERT TABLE block (which starts with "--> ASSERT TABLE") back into
        // the AssertTableCommand so we can verify the block round-trips correctly.
        private static AssertTableCommand ParseBlock(string block)
        {
            var input = block + Environment.NewLine + "EVALUATE { 1 }" + Environment.NewLine;
            var errors = new List<Error>();
            var tree = Helpers.ConfigureLexerAndParser(input, ref errors);
            Assert.IsNull(tree.exception, "parse exception");
            Assert.IsEmpty(errors, "Generated block should parse with no errors: " + block);

            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(new Dictionary<string, List<string>>(), batch);
            new ParseTreeWalker().Walk(listener, tree);

            return batch[0].Commands.OfType<AssertTableCommand>().Single();
        }

        [TestMethod]
        public void FormatDataTable_IncludesHeaderLineAndTypeRow()
        {
            var dt = new DataTable();
            dt.Columns.Add("Color", typeof(string));
            dt.Columns.Add("Count", typeof(long));
            dt.Rows.Add("Red", 5L);

            var block = TableAssertionFormatter.FormatDataTable(dt);
            var lines = block.Replace("\r\n", "\n").Split('\n');

            Assert.AreEqual("--> ASSERT TABLE", lines[0]);
            Assert.AreEqual("-->> | Color  | Count |", lines[1]);
            Assert.AreEqual("-->> | STRING | INT64 |", lines[2]);
            Assert.AreEqual("-->> | Red    |     5 |", lines[3]);
        }

        [TestMethod]
        public void FormatDataTable_OmitHeaderLine()
        {
            var dt = new DataTable();
            dt.Columns.Add("A", typeof(long));
            dt.Rows.Add(1L);

            var block = TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: false);
            Assert.IsFalse(block.Contains("--> ASSERT TABLE"));
            Assert.IsTrue(block.StartsWith("-->> | A"));
        }

        [TestMethod]
        public void FormatDataTable_OmitTypeRow()
        {
            var dt = new DataTable();
            dt.Columns.Add("A", typeof(long));
            dt.Rows.Add(1L);

            var block = TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: false, includeTypeRow: false);
            var lines = block.Replace("\r\n", "\n").Split('\n');
            Assert.AreEqual("-->> | A |", lines[0]);
            Assert.AreEqual("-->> | 1 |", lines[1]);
        }

        [TestMethod]
        public void FormatDataTable_NullsBecomeEmptyCells()
        {
            var dt = new DataTable();
            dt.Columns.Add("A", typeof(string));
            dt.Columns.Add("B", typeof(long));
            var r = dt.NewRow();
            r["A"] = DBNull.Value;
            r["B"] = DBNull.Value;
            dt.Rows.Add(r);

            var block = TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: false, includeTypeRow: false);
            var lines = block.Replace("\r\n", "\n").Split('\n');
            Assert.AreEqual("-->> |   |   |", lines[1]);
        }

        [TestMethod]
        public void FormatDataTable_SanitizesPipeAndNewlineInCell()
        {
            var dt = new DataTable();
            dt.Columns.Add("A", typeof(string));
            dt.Rows.Add("has|pipe\nand newline");

            var block = TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: false, includeTypeRow: false);
            Assert.IsFalse(block.Contains("has|pipe"), "pipe should be replaced");
            var lines = block.Replace("\r\n", "\n").Split('\n');
            // one header row + one data row (no stray newline splitting the data row)
            Assert.AreEqual(2, lines.Length);
            Assert.IsTrue(lines[1].Contains("has/pipe and newline"));
        }

        [TestMethod]
        public void FormatDataTable_DateTimeFormats()
        {
            var dt = new DataTable();
            dt.Columns.Add("D", typeof(DateTime));
            dt.Rows.Add(new DateTime(2020, 1, 2));               // midnight -> date only
            dt.Rows.Add(new DateTime(2020, 1, 2, 3, 4, 5));      // with time

            var block = TableAssertionFormatter.FormatDataTable(dt, includeHeaderLine: false, includeTypeRow: true);
            // cells are padded for alignment, so compare on the trimmed cell values
            var cells = block.Replace("\r\n", "\n").Split('\n')
                .Select(l => l.Substring(l.IndexOf('|') + 1).Trim().Trim('|').Trim())
                .ToList();
            Assert.AreEqual("DATETIME", cells[1]);
            Assert.AreEqual("2020-01-02", cells[2]);
            Assert.AreEqual("2020-01-02 03:04:05", cells[3]);
        }

        [TestMethod]
        public void FormatTabDelimited_InfersTypes()
        {
            var text = "Color\tCount\nRed\t5\nBlue\t3";
            var block = TableAssertionFormatter.FormatTabDelimited(text);
            var lines = block.Replace("\r\n", "\n").Split('\n');
            Assert.AreEqual("--> ASSERT TABLE", lines[0]);
            Assert.AreEqual("-->> | Color  | Count |", lines[1]);
            Assert.AreEqual("-->> | STRING | INT64 |", lines[2]);
            Assert.AreEqual("-->> | Red    |     5 |", lines[3]);
            Assert.AreEqual("-->> | Blue   |     3 |", lines[4]);
        }

        [TestMethod]
        public void FormatTabDelimited_HandlesCrLfAndTrailingBlankLines()
        {
            var text = "A\tB\r\n1\t2\r\n\r\n";
            var block = TableAssertionFormatter.FormatTabDelimited(text, includeHeaderLine: false, includeTypeRow: false);
            var lines = block.Replace("\r\n", "\n").Split('\n');
            Assert.AreEqual(2, lines.Length);
            Assert.AreEqual("-->> | A | B |", lines[0]);
            Assert.AreEqual("-->> | 1 | 2 |", lines[1]);
        }

        [TestMethod]
        public void LooksLikeTabDelimited()
        {
            Assert.IsTrue(TableAssertionFormatter.LooksLikeTabDelimited("a\tb"));
            Assert.IsFalse(TableAssertionFormatter.LooksLikeTabDelimited("a b c"));
            Assert.IsFalse(TableAssertionFormatter.LooksLikeTabDelimited(""));
            Assert.IsFalse(TableAssertionFormatter.LooksLikeTabDelimited(null));
        }

        [TestMethod]
        public void FormatDataTable_RoundTripsThroughParser()
        {
            var dt = MakeTable();
            var block = TableAssertionFormatter.FormatDataTable(dt);

            var cmd = ParseBlock(block);

            Assert.AreEqual(dt.Columns.Count, cmd.Data.Columns.Count, "column count");
            for (int c = 0; c < dt.Columns.Count; c++)
            {
                Assert.AreEqual(dt.Columns[c].ColumnName, cmd.Data.Columns[c].ColumnName, "column name " + c);
                Assert.AreEqual(dt.Columns[c].DataType, cmd.Data.Columns[c].DataType, "column type " + dt.Columns[c].ColumnName);
            }

            Assert.AreEqual(dt.Rows.Count, cmd.Data.Rows.Count, "row count");
            for (int r = 0; r < dt.Rows.Count; r++)
            {
                for (int c = 0; c < dt.Columns.Count; c++)
                {
                    var expected = dt.Rows[r][c];
                    var actual = cmd.Data.Rows[r][c];
                    if (dt.Columns[c].DataType == typeof(DateTime))
                    {
                        // formatter drops sub-second precision for values with a time component
                        var e = (DateTime)expected;
                        var a = (DateTime)actual;
                        Assert.AreEqual(e.Year, a.Year);
                        Assert.AreEqual(e.Month, a.Month);
                        Assert.AreEqual(e.Day, a.Day);
                        Assert.AreEqual(e.Hour, a.Hour);
                        Assert.AreEqual(e.Minute, a.Minute);
                        Assert.AreEqual(e.Second, a.Second);
                    }
                    else
                    {
                        Assert.AreEqual(expected, actual, $"cell [{r},{c}] ({dt.Columns[c].ColumnName})");
                    }
                }
            }
        }

        [TestMethod]
        public void FormatDataTable_AlignsColumnsAndRightAlignsNumbers()
        {
            var dt = new DataTable();
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("Amount", typeof(long));
            dt.Rows.Add("A", 5L);
            dt.Rows.Add("LongerName", 12345L);

            var block = TableAssertionFormatter.FormatDataTable(dt);
            var lines = block.Replace("\r\n", "\n").Split('\n');

            var contLines = lines.Where(l => l.StartsWith("-->>")).ToList();
            var len = contLines[0].Length;
            foreach (var l in contLines)
                Assert.AreEqual(len, l.Length, "line lengths should match for alignment: '" + l + "'");

            Assert.AreEqual("-->> | A          |      5 |", contLines[2]);
            Assert.AreEqual("-->> | LongerName |  12345 |", contLines[3]);
        }
    }
}
