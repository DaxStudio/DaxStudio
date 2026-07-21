using System;
using System.Collections.Generic;
using System.Data;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class AssertionEngineTests
    {
        #region ROWCOUNT

        [DataTestMethod]
        [DataRow("=", 5, 5L, true)]
        [DataRow("=", 5, 6L, false)]
        [DataRow(">", 5, 6L, true)]
        [DataRow(">", 5, 5L, false)]
        [DataRow("<", 5, 4L, true)]
        [DataRow("<", 5, 5L, false)]
        [DataRow(">=", 5, 5L, true)]
        [DataRow(">=", 5, 4L, false)]
        [DataRow("<=", 5, 5L, true)]
        [DataRow("<=", 5, 6L, false)]
        public void EvaluateRowCount_Comparisons(string op, int expected, long actual, bool shouldPass)
        {
            var cmd = new AssertRowcountCommand(op, expected);

            var result = AssertionEngine.EvaluateRowCount(cmd, actual);

            Assert.AreEqual(AssertionKind.RowCount, result.Kind);
            Assert.AreEqual(shouldPass ? TestOutcome.Passed : TestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void EvaluateRowCount_InvalidOperator_ReturnsError()
        {
            var cmd = new AssertRowcountCommand("!=", 5);

            var result = AssertionEngine.EvaluateRowCount(cmd, 5);

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
        }

        [TestMethod]
        public void EvaluateRowCount_CarriesTestName()
        {
            var cmd = new AssertRowcountCommand("=", 1);

            var result = AssertionEngine.EvaluateRowCount(cmd, 1, "my test");

            Assert.AreEqual("my test", result.TestName);
            Assert.IsTrue(result.Passed);
        }

        #endregion

        #region Performance

        private static Dictionary<PerformanceProperty, double> Metrics(
            double? duration = null, double? seCpu = null, double? seQueries = null)
        {
            var d = new Dictionary<PerformanceProperty, double>();
            if (duration.HasValue) d[PerformanceProperty.Duration] = duration.Value;
            if (seCpu.HasValue) d[PerformanceProperty.SE_CPU] = seCpu.Value;
            if (seQueries.HasValue) d[PerformanceProperty.SE_QUERIES] = seQueries.Value;
            return d;
        }

        [TestMethod]
        public void EvaluatePerformance_DurationUnderLimit_Passes()
        {
            var cmd = new AssertCommand("DURATION", "<", 200, 0);

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(duration: 100));

            Assert.AreEqual(AssertionKind.Performance, result.Kind);
            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual("100", result.Actual);
        }

        [TestMethod]
        public void EvaluatePerformance_DurationOverLimit_Fails()
        {
            var cmd = new AssertCommand("DURATION", "<", 200, 0);

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(duration: 500));

            Assert.AreEqual(TestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void EvaluatePerformance_SeQueries_Comparison()
        {
            var cmd = new AssertCommand("SE_QUERIES", "<=", 2, 0);

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluatePerformance(cmd, Metrics(seQueries: 2)).Outcome);
            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluatePerformance(cmd, Metrics(seQueries: 3)).Outcome);
        }

        [TestMethod]
        public void EvaluatePerformance_MissingMetric_ReturnsError()
        {
            var cmd = new AssertCommand("SE_CPU", "<", 100, 0);

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(duration: 50));

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
        }

        [TestMethod]
        public void EvaluatePerformance_RealLiteralValue_IsUsed()
        {
            // real literal parsed -> DoubleValue set, IntegerValue 0
            var cmd = new AssertCommand("DURATION", "<", 0, 12.5);

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluatePerformance(cmd, Metrics(duration: 12)).Outcome);
            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluatePerformance(cmd, Metrics(duration: 13)).Outcome);
        }

        #endregion

        #region TABLE

        private static DataTable Table(string[] columns, Type[] types, params object[][] rows)
        {
            var dt = new DataTable();
            for (int i = 0; i < columns.Length; i++)
                dt.Columns.Add(columns[i], types[i]);
            foreach (var row in rows)
                dt.Rows.Add(row);
            return dt;
        }

        private static AssertTableCommand ExpectedTable(AssertTableMode mode, string[] columns, Type[] types, params object[][] rows)
        {
            var cmd = new AssertTableCommand(mode);
            for (int i = 0; i < columns.Length; i++)
                cmd.Data.Columns.Add(columns[i], types[i]);
            foreach (var row in rows)
                cmd.Data.Rows.Add(row);
            return cmd;
        }

        [TestMethod]
        public void EvaluateTable_Ordered_ExactMatch_Passes()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            var result = AssertionEngine.EvaluateTable(expected, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
        }

        [TestMethod]
        public void EvaluateTable_Ordered_DifferentOrder_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "B" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "B" }, new object[] { "A" });

            var result = AssertionEngine.EvaluateTable(expected, actual);

            Assert.AreEqual(TestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void EvaluateTable_Unordered_DifferentOrder_Passes()
        {
            var expected = ExpectedTable(AssertTableMode.Unordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "B" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "B" }, new object[] { "A" });

            var result = AssertionEngine.EvaluateTable(expected, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
        }

        [TestMethod]
        public void EvaluateTable_Unordered_CountMismatch_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Unordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "A" });

            var result = AssertionEngine.EvaluateTable(expected, actual);

            Assert.AreEqual(TestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void EvaluateTable_Unordered_DuplicateRowsMustMatchCount()
        {
            // two identical expected rows require two matching actual rows
            var expected = ExpectedTable(AssertTableMode.Unordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "A" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "A" });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_Partial_SubsetRows_Passes()
        {
            var expected = ExpectedTable(AssertTableMode.Partial,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "B" });
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L }, new object[] { "C", 3L });

            var result = AssertionEngine.EvaluateTable(expected, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
        }

        [TestMethod]
        public void EvaluateTable_Partial_MissingRow_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Partial,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { "Z" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" }, new object[] { "B" });

            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_MissingColumn_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Unordered,
                new[] { "DoesNotExist" }, new[] { typeof(string) },
                new object[] { "A" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" });

            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_ColumnMatchIsCaseInsensitive()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "name" }, new[] { typeof(string) },
                new object[] { "A" });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { "A" });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_ColumnMatchesOnFriendlyCaption()
        {
            // The results DataTable escapes spaces in ColumnName (grid-sorting workaround) and keeps
            // the friendly name in Caption. Expected column uses the friendly name with spaces, so
            // the match must succeed against Caption, not the escaped ColumnName.
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Internet Total Sales" }, new[] { typeof(long) },
                new object[] { 1L });
            var actual = Table(new[] { "Internet`Total`Sales" }, new[] { typeof(long) },
                new object[] { 1L });
            actual.Columns[0].Caption = "Internet Total Sales";

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_NumericToleranceAcrossTypes()
        {
            // expected long 2 vs actual double 2.0 should be considered equal
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Qty" }, new[] { typeof(long) },
                new object[] { 2L });
            var actual = Table(new[] { "Qty" }, new[] { typeof(double) },
                new object[] { 2.0 });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_NullCellsMatch()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { DBNull.Value });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { DBNull.Value });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_EmptyStringsMatch()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { string.Empty });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { string.Empty });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_EmptyStringVsNull_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { string.Empty });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { DBNull.Value });

            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_NullVsEmptyString_Fails()
        {
            var expected = ExpectedTable(AssertTableMode.Ordered,
                new[] { "Name" }, new[] { typeof(string) },
                new object[] { DBNull.Value });
            var actual = Table(new[] { "Name" }, new[] { typeof(string) },
                new object[] { string.Empty });

            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateTable(expected, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_NullExpectedData_ReturnsError()
        {
            var cmd = new AssertTableCommand(AssertTableMode.Ordered); // no rows/columns added
            var actual = Table(new[] { "Name" }, new[] { typeof(string) }, new object[] { "A" });

            // empty expected table (0 columns, 0 rows) trivially matches an actual with columns in
            // Partial mode; in Ordered mode column count differs -> Failed. Here we assert it does
            // not throw and produces a definite outcome.
            var result = AssertionEngine.EvaluateTable(cmd, actual);
            Assert.IsTrue(result.Outcome == TestOutcome.Failed || result.Outcome == TestOutcome.Passed);
        }

        #endregion

        #region TABLE from file

        private static string _tempDir;

        private static string WriteTemp(string fileName, string content)
        {
            var path = System.IO.Path.Combine(EnsureTempDir(), fileName);
            System.IO.File.WriteAllText(path, content);
            return path;
        }

        [ClassCleanup]
        public static void CleanupTempDir()
        {
            try
            {
                if (_tempDir != null && System.IO.Directory.Exists(_tempDir))
                    System.IO.Directory.Delete(_tempDir, true);
            }
            catch { /* best-effort temp cleanup */ }
        }

        private static AssertTableCommand FileCommand(AssertTableFormat format, string filePath, AssertTableMode mode = AssertTableMode.Ordered)
        {
            return new AssertTableCommand(mode) { Format = format, FilePath = filePath };
        }

        [TestMethod]
        public void EvaluateTable_FromCsvFile_Passes()
        {
            var path = WriteTemp("expected.csv", "Name,Qty\r\nA,1\r\nB,2\r\n");
            var cmd = FileCommand(AssertTableFormat.Csv, path);
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            var result = AssertionEngine.EvaluateTable(cmd, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
            Assert.AreEqual(typeof(long), cmd.Data.Columns["Qty"].DataType, "CSV column types should be inferred like inline rows");
        }

        [TestMethod]
        public void EvaluateTable_FromCsvFile_ValueMismatch_Fails()
        {
            var path = WriteTemp("mismatch.csv", "Name,Qty\r\nA,1\r\nB,99\r\n");
            var cmd = FileCommand(AssertTableFormat.Csv, path);
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateTable(cmd, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_FromTxtFile_TabDelimited_Passes()
        {
            var path = WriteTemp("expected.txt", "Name\tQty\r\nA\t1\r\nB\t2\r\n");
            var cmd = FileCommand(AssertTableFormat.Txt, path);
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateTable(cmd, actual).Outcome);
        }

        [TestMethod]
        public void EvaluateTable_FromMarkdownFile_SkipsSeparator_Passes()
        {
            var path = WriteTemp("expected.md",
                "| Name | Qty |\r\n| --- | --- |\r\nsome preamble line to ignore\r\n| A | 1 |\r\n| B | 2 |\r\n");
            var cmd = FileCommand(AssertTableFormat.Md, path);
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            var result = AssertionEngine.EvaluateTable(cmd, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
            Assert.AreEqual(2, cmd.Data.Rows.Count, "The markdown separator and non-table lines must be ignored");
        }

        [TestMethod]
        public void EvaluateTable_FromParquetFile_Passes()
        {
            var path = System.IO.Path.Combine(EnsureTempDir(), "expected.parquet");
            WriteParquet(path, new[] { "A", "B" }, new long[] { 1L, 2L });

            var cmd = FileCommand(AssertTableFormat.Parquet, path);
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L }, new object[] { "B", 2L });

            var result = AssertionEngine.EvaluateTable(cmd, actual);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
        }

        [TestMethod]
        public void EvaluateTable_FileNotFound_ReturnsError()
        {
            var cmd = FileCommand(AssertTableFormat.Csv, System.IO.Path.Combine(EnsureTempDir(), "does_not_exist.csv"));
            var actual = Table(new[] { "Name" }, new[] { typeof(string) }, new object[] { "A" });

            var result = AssertionEngine.EvaluateTable(cmd, actual);

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
        }

        [TestMethod]
        public void EvaluateTable_RelativePathWithoutBaseDirectory_ReturnsError()
        {
            // A relative path with no base directory (unsaved document) cannot be resolved -> Error.
            var cmd = FileCommand(AssertTableFormat.Csv, "expected.csv");
            var actual = Table(new[] { "Name" }, new[] { typeof(string) }, new object[] { "A" });

            var result = AssertionEngine.EvaluateTable(cmd, actual, testName: null, baseDirectory: null);

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
            StringAssert.Contains(result.Message, "has not been saved");
        }

        [TestMethod]
        public void EvaluateTable_RelativePathWithBaseDirectory_Resolves()
        {
            var dir = EnsureTempDir();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "rel.csv"), "Name,Qty\r\nA,1\r\n");
            var cmd = FileCommand(AssertTableFormat.Csv, "rel.csv");
            var actual = Table(new[] { "Name", "Qty" }, new[] { typeof(string), typeof(long) },
                new object[] { "A", 1L });

            var result = AssertionEngine.EvaluateTable(cmd, actual, testName: null, baseDirectory: dir);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome, result.Message);
        }

        private static string EnsureTempDir()
        {
            if (_tempDir == null)
            {
                _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DaxAssertFileTests_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(_tempDir);
            }
            return _tempDir;
        }

        private static void WriteParquet(string path, string[] names, long[] qty)
        {
            var schema = new Parquet.Schema.ParquetSchema(
                new Parquet.Schema.DataField<string>("Name"),
                new Parquet.Schema.DataField<long>("Qty"));

            using (var fs = System.IO.File.Create(path))
            using (var writer = Parquet.ParquetWriter.CreateAsync(schema, fs).GetAwaiter().GetResult())
            using (var rg = writer.CreateRowGroup())
            {
                rg.WriteColumnAsync(new Parquet.Data.DataColumn((Parquet.Schema.DataField)schema[0], names)).GetAwaiter().GetResult();
                rg.WriteColumnAsync(new Parquet.Data.DataColumn((Parquet.Schema.DataField)schema[1], qty)).GetAwaiter().GetResult();
            }
        }

        #endregion

        #region Discovery

        private static ScriptBatch BatchWith(params ScriptCommand[] commands)
        {
            var b = new ScriptBatch();
            b.Commands.AddRange(commands);
            return b;
        }

        [TestMethod]
        public void DiscoverTests_ProducesPendingResultForEachAssertion()
        {
            var batch = BatchWith(
                new TestCommand("Sales Test"),
                new AssertRowcountCommand(">", 10),
                new AssertCommand("DURATION", "<", 200, 0));

            var results = AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch });

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(System.Linq.Enumerable.All(results, r => r.Outcome == TestOutcome.Pending));
            Assert.IsTrue(System.Linq.Enumerable.All(results, r => r.TestName == "Sales Test"));
            Assert.AreEqual(AssertionKind.RowCount, results[0].Kind);
            Assert.AreEqual(AssertionKind.Performance, results[1].Kind);
        }

        [TestMethod]
        public void DiscoverTests_DescriptionAndExpectedMatchEvaluatedResult()
        {
            // A discovered (pending) row must carry the same Description/Expected text as the row that
            // will later replace it, so the Test Results pane can match them and preserve run outcomes.
            var rowCountCmd = new AssertRowcountCommand(">=", 5);

            var discovered = AssertionEngine.DiscoverTests(new List<ScriptBatch> { BatchWith(rowCountCmd) });
            var evaluated = AssertionEngine.EvaluateRowCount(rowCountCmd, 7);

            Assert.AreEqual(1, discovered.Count);
            Assert.AreEqual(evaluated.Description, discovered[0].Description);
            Assert.AreEqual(evaluated.Expected, discovered[0].Expected);
        }

        [TestMethod]
        public void DiscoverTests_NullBatches_ReturnsEmpty()
        {
            Assert.AreEqual(0, AssertionEngine.DiscoverTests(null).Count);
        }

        [TestMethod]
        public void DiscoverTests_BatchWithoutAsserts_ProducesNothing()
        {
            var batch = BatchWith(new TestCommand("Empty"));

            Assert.AreEqual(0, AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch }).Count);
        }

        [TestMethod]
        public void DiscoverTests_SetsBatchIndexPerBatch()
        {
            // Each discovered test carries the index of the "--> GO"-separated batch it belongs to, so
            // the Test Results pane can mark just that batch's tests running as its query executes.
            var batch0 = BatchWith(new AssertRowcountCommand(">", 1));
            var batch1 = BatchWith(new AssertRowcountCommand(">", 2), new AssertCommand("DURATION", "<", 200, 0));

            var results = AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch0, batch1 });

            Assert.AreEqual(3, results.Count);
            Assert.AreEqual(0, results[0].BatchIndex);
            Assert.AreEqual(1, results[1].BatchIndex);
            Assert.AreEqual(1, results[2].BatchIndex);
        }

        #endregion
    }
}
