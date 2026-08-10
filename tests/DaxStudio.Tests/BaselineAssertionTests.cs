using System.Collections.Generic;
using System.Data;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Tests for baseline-relative assertions - <c>--&gt; ASSERT ... BASELINE ["name"] [* factor]</c>
    /// evaluated against a <see cref="BaselineStore"/> populated by a <c>--&gt; BASELINE</c> batch.
    /// </summary>
    [TestClass]
    public class BaselineAssertionTests
    {
        private const string BaselineName = "v1";

        private static BaselineStore StoreWithMetrics(params (PerformanceProperty Property, double Value)[] metrics)
        {
            var dict = new Dictionary<PerformanceProperty, double>();
            foreach (var m in metrics) dict[m.Property] = m.Value;

            var store = new BaselineStore();
            store.Capture(BaselineName, null, dict);
            return store;
        }

        private static DataTable Table(params (string Color, long Count)[] rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("Color", typeof(string));
            dt.Columns.Add("ProductCount", typeof(long));
            foreach (var r in rows) dt.Rows.Add(r.Color, r.Count);
            return dt;
        }

        private static IReadOnlyDictionary<PerformanceProperty, double> Metrics(PerformanceProperty property, double value)
            => new Dictionary<PerformanceProperty, double> { [property] = value };

        #region BaselineStore

        [TestMethod]
        public void Store_CopiesTheResultTable()
        {
            var source = Table(("Red", 5));
            var store = new BaselineStore();
            store.Capture(BaselineName, source, null);

            // Mutating the live table after capture must not change the snapshot.
            source.Rows.Add("Blue", 3);

            Assert.IsTrue(store.TryGet(BaselineName, out var captured));
            Assert.AreEqual(1, captured.Results.Rows.Count);
            Assert.AreEqual(1, captured.RowCount);
        }

        [TestMethod]
        public void Store_CopiesTheMetricDictionary()
        {
            var metrics = new Dictionary<PerformanceProperty, double> { [PerformanceProperty.Duration] = 100 };
            var store = new BaselineStore();
            store.Capture(BaselineName, null, metrics);

            metrics[PerformanceProperty.Duration] = 999;

            store.TryGet(BaselineName, out var captured);
            Assert.AreEqual(100d, captured.Metrics[PerformanceProperty.Duration]);
        }

        [TestMethod]
        public void Store_LooksUpNamesCaseInsensitively()
        {
            var store = new BaselineStore();
            store.Capture("Original", null, null);

            Assert.IsTrue(store.TryGet("ORIGINAL", out _));
        }

        [TestMethod]
        public void Store_UnnamedBaselineUsesTheDefaultName()
        {
            var store = new BaselineStore();
            store.Capture(null, null, null);

            Assert.IsTrue(store.TryGet(BaselineReference.DefaultName, out _));
            Assert.IsTrue(store.TryGet(null, out _));
        }

        [TestMethod]
        public void Store_ClearRemovesEverything()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, null, null);
            store.Clear();

            Assert.AreEqual(0, store.Count);
            Assert.IsFalse(store.TryGet(BaselineName, out _));
        }

        #endregion

        #region Performance

        [DataTestMethod]
        [DataRow("<=", 1000d, 900d, true)]   // faster than the baseline
        [DataRow("<=", 1000d, 1000d, true)]  // identical
        [DataRow("<=", 1000d, 1100d, false)] // slower
        [DataRow("<", 1000d, 999d, true)]
        [DataRow(">=", 1000d, 1000d, true)]
        [DataRow("=", 1000d, 1000d, true)]
        public void Performance_ComparesAgainstTheCapturedBaseline(string op, double baseline, double actual, bool shouldPass)
        {
            var cmd = new AssertCommand("DURATION", op, new BaselineReference(BaselineName));
            var store = StoreWithMetrics((PerformanceProperty.Duration, baseline));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, actual), null, store);

            Assert.AreEqual(shouldPass ? TestOutcome.Passed : TestOutcome.Failed, result.Outcome);
        }

        [TestMethod]
        public void Performance_FactorAboveOneAllowsARegression()
        {
            // "<= BASELINE * 1.1" tolerates the candidate being up to 10% slower.
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName, 1.1));
            var store = StoreWithMetrics((PerformanceProperty.Duration, 1000d));

            Assert.AreEqual(TestOutcome.Passed,
                AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 1100), null, store).Outcome);
            Assert.AreEqual(TestOutcome.Failed,
                AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 1101), null, store).Outcome);
        }

        [TestMethod]
        public void Performance_FactorBelowOneDemandsAnImprovement()
        {
            // "<= BASELINE * 0.9" requires the candidate to be at least 10% faster.
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName, 0.9));
            var store = StoreWithMetrics((PerformanceProperty.Duration, 1000d));

            Assert.AreEqual(TestOutcome.Passed,
                AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 900), null, store).Outcome);
            // Merely equalling the baseline is not a 10% improvement.
            Assert.AreEqual(TestOutcome.Failed,
                AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 1000), null, store).Outcome);
        }

        [TestMethod]
        public void Performance_ResolvesTheExpectedValueForDisplay()
        {
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName, 1.1));
            var store = StoreWithMetrics((PerformanceProperty.Duration, 1000d));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 800), null, store);

            Assert.AreEqual("<= 1100", result.Expected);
            StringAssert.Contains(result.Description, "BASELINE \"v1\" * 1.1");
        }

        [TestMethod]
        public void Performance_ReportsThePercentageDeltaFromTheBaseline()
        {
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName));
            var store = StoreWithMetrics((PerformanceProperty.Duration, 1000d));

            var improved = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 790), null, store);
            Assert.AreEqual("790 (-21%)", improved.Actual);

            var regressed = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 1250), null, store);
            Assert.AreEqual("1250 (+25%)", regressed.Actual);
        }

        [TestMethod]
        public void Performance_MissingBaselineIsAnError()
        {
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 100), null, new BaselineStore());

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
            StringAssert.Contains(result.Message, "'v1'");
        }

        [TestMethod]
        public void Performance_NullStoreIsAnError()
        {
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 100));

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
        }

        [TestMethod]
        public void Performance_BaselineWithoutTheMetricIsAnError()
        {
            // The baseline was captured while the Server Timings trace was not running.
            var cmd = new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName));
            var store = StoreWithMetrics((PerformanceProperty.SE_CPU, 10d));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 100), null, store);

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
            StringAssert.Contains(result.Message, "Server Timings");
        }

        [TestMethod]
        public void Performance_LiteralOperandIsUnaffectedByTheStore()
        {
            var cmd = new AssertCommand("DURATION", "<", 500, 0.0);
            var store = StoreWithMetrics((PerformanceProperty.Duration, 1d));

            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 400), null, store);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual("< 500", result.Expected);
            Assert.AreEqual("400", result.Actual);
        }

        #endregion

        #region ROWCOUNT

        [TestMethod]
        public void RowCount_ComparesAgainstTheCapturedBaseline()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, Table(("Red", 5), ("Blue", 3)), null);

            var cmd = new AssertRowcountCommand("=", new BaselineReference(BaselineName));

            Assert.AreEqual(TestOutcome.Passed, AssertionEngine.EvaluateRowCount(cmd, 2, null, store).Outcome);
            Assert.AreEqual(TestOutcome.Failed, AssertionEngine.EvaluateRowCount(cmd, 3, null, store).Outcome);
        }

        [TestMethod]
        public void RowCount_MissingBaselineIsAnError()
        {
            var cmd = new AssertRowcountCommand("=", new BaselineReference(BaselineName));

            Assert.AreEqual(TestOutcome.Error, AssertionEngine.EvaluateRowCount(cmd, 2, null, new BaselineStore()).Outcome);
        }

        #endregion

        #region TABLE

        [TestMethod]
        public void Table_MatchingResultsPass()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, Table(("Red", 5), ("Blue", 3)), null);

            var cmd = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = new BaselineReference(BaselineName) };

            var result = AssertionEngine.EvaluateTable(cmd, Table(("Red", 5), ("Blue", 3)), null, null, store);

            Assert.AreEqual(TestOutcome.Passed, result.Outcome);
            Assert.AreEqual("2 row(s)", result.Expected);
        }

        [TestMethod]
        public void Table_DifferentValuesFail()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, Table(("Red", 5)), null);

            var cmd = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = new BaselineReference(BaselineName) };

            Assert.AreEqual(TestOutcome.Failed,
                AssertionEngine.EvaluateTable(cmd, Table(("Red", 6)), null, null, store).Outcome);
        }

        [TestMethod]
        public void Table_OrderedModeIsTheDefaultAndIsOrderSensitive()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, Table(("Red", 5), ("Blue", 3)), null);

            var ordered = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = new BaselineReference(BaselineName) };
            Assert.AreEqual(TestOutcome.Failed,
                AssertionEngine.EvaluateTable(ordered, Table(("Blue", 3), ("Red", 5)), null, null, store).Outcome);

            var unordered = new AssertTableCommand(AssertTableMode.Unordered) { Baseline = new BaselineReference(BaselineName) };
            Assert.AreEqual(TestOutcome.Passed,
                AssertionEngine.EvaluateTable(unordered, Table(("Blue", 3), ("Red", 5)), null, null, store).Outcome);
        }

        [TestMethod]
        public void Table_PartialModeAllowsExtraRows()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, Table(("Red", 5)), null);

            var cmd = new AssertTableCommand(AssertTableMode.Partial) { Baseline = new BaselineReference(BaselineName) };

            Assert.AreEqual(TestOutcome.Passed,
                AssertionEngine.EvaluateTable(cmd, Table(("Red", 5), ("Blue", 3)), null, null, store).Outcome);
        }

        [TestMethod]
        public void Table_MissingBaselineIsAnError()
        {
            var cmd = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = new BaselineReference(BaselineName) };

            var result = AssertionEngine.EvaluateTable(cmd, Table(("Red", 5)), null, null, new BaselineStore());

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
        }

        [TestMethod]
        public void Table_BaselineWithoutAResultTableIsAnError()
        {
            var store = new BaselineStore();
            store.Capture(BaselineName, null, null);

            var cmd = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = new BaselineReference(BaselineName) };

            var result = AssertionEngine.EvaluateTable(cmd, Table(("Red", 5)), null, null, store);

            Assert.AreEqual(TestOutcome.Error, result.Outcome);
            StringAssert.Contains(result.Message, "no result table");
        }

        [TestMethod]
        public void Table_BaselineReferenceCountsAsATableDefinition()
        {
            var cmd = new AssertTableCommand(AssertTableMode.Ordered);
            Assert.IsFalse(cmd.HasTableDefinition);

            cmd.Baseline = new BaselineReference(BaselineName);
            Assert.IsTrue(cmd.HasTableDefinition);
        }

        #endregion

        #region Discovery

        [TestMethod]
        public void DiscoverTests_ShowsTheBaselineExpressionWhilePending()        {
            var batch = new ScriptBatch();
            batch.Commands.Add(new AssertCommand("DURATION", "<=", new BaselineReference(BaselineName, 1.1)) { Line = 7 });

            var discovered = AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch });

            Assert.AreEqual(1, discovered.Count);
            Assert.AreEqual(TestOutcome.Pending, discovered[0].Outcome);
            // PerformanceProperty.Duration renders with its enum casing, as it does for a literal operand.
            Assert.AreEqual("Duration <= BASELINE \"v1\" * 1.1", discovered[0].Description);
            Assert.AreEqual("<= BASELINE \"v1\" * 1.1", discovered[0].Expected);
        }

        [TestMethod]
        public void DiscoverTests_UnnamedBaselineRendersWithoutAName()
        {
            var batch = new ScriptBatch();
            batch.Commands.Add(new AssertCommand("SE_QUERIES", "<=", new BaselineReference()));

            var discovered = AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch });

            Assert.AreEqual("SE_QUERIES <= BASELINE", discovered[0].Description);
        }

        #endregion

        #region PREVIOUS

        // A resolved PREVIOUS reference is an ordinary baseline reference pointing at a generated name,
        // so it must evaluate exactly like a named baseline while still DISPLAYING as "PREVIOUS".
        // (The unresolved -> resolved transition itself is covered by PreviousReferenceTests.)
        private static BaselineReference ResolvedPrevious(string targetName, double factor = 1.0)
            => new BaselineReference(targetName, factor, isPrevious: true);

        [TestMethod]
        public void Previous_EvaluatesIdenticallyToANamedBaseline()
        {
            const string generated = "(previous:0)";
            var store = new BaselineStore();
            store.Capture(generated, null, new Dictionary<PerformanceProperty, double> { [PerformanceProperty.Duration] = 1000 });

            var viaPrevious = new AssertCommand("DURATION", "<=", ResolvedPrevious(generated));
            var viaName = new AssertCommand("DURATION", "<=", new BaselineReference(generated));

            var a = AssertionEngine.EvaluatePerformance(viaPrevious, Metrics(PerformanceProperty.Duration, 800), null, store);
            var b = AssertionEngine.EvaluatePerformance(viaName, Metrics(PerformanceProperty.Duration, 800), null, store);

            Assert.AreEqual(TestOutcome.Passed, a.Outcome);
            Assert.AreEqual(b.Outcome, a.Outcome);
            Assert.AreEqual(b.Expected, a.Expected);
            Assert.AreEqual(b.Actual, a.Actual);
        }

        [TestMethod]
        public void Previous_DisplaysAsPreviousNotTheGeneratedName()
        {
            var store = new BaselineStore();
            store.Capture("(previous:0)", null, new Dictionary<PerformanceProperty, double> { [PerformanceProperty.Duration] = 1000 });

            var cmd = new AssertCommand("DURATION", "<=", ResolvedPrevious("(previous:0)", 0.9));
            var result = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 800), null, store);

            Assert.AreEqual("Duration <= PREVIOUS * 0.9", result.Description);
            Assert.DoesNotContain("previous:0", result.Description);
        }

        [TestMethod]
        public void Previous_RendersAsPreviousWhilePending()
        {
            var batch = new ScriptBatch();
            batch.Commands.Add(new AssertCommand("DURATION", "<=", ResolvedPrevious("(previous:0)")));
            batch.Commands.Add(new AssertTableCommand(AssertTableMode.Unordered) { Baseline = ResolvedPrevious("(previous:0)") });

            var discovered = AssertionEngine.DiscoverTests(new List<ScriptBatch> { batch });

            Assert.AreEqual("TABLE Unordered vs PREVIOUS", discovered[0].Description);
            Assert.AreEqual("Duration <= PREVIOUS", discovered[1].Description);
            Assert.AreEqual("<= PREVIOUS", discovered[1].Expected);
        }

        [TestMethod]
        public void Previous_TableComparisonUsesTheCapturedResults()
        {
            var store = new BaselineStore();
            store.Capture("(previous:0)", Table(("Red", 5), ("Blue", 3)), null);

            var cmd = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = ResolvedPrevious("(previous:0)") };

            Assert.AreEqual(TestOutcome.Passed,
                AssertionEngine.EvaluateTable(cmd, Table(("Red", 5), ("Blue", 3)), null, null, store).Outcome);
            Assert.AreEqual(TestOutcome.Failed,
                AssertionEngine.EvaluateTable(cmd, Table(("Red", 5), ("Blue", 4)), null, null, store).Outcome);
        }

        [TestMethod]
        public void Previous_ErrorMessagesDoNotLeakTheGeneratedName()
        {
            // The resolver names a PREVIOUS target "(previous:0)". That is an implementation detail and
            // must never reach the user, who never wrote a "--> BASELINE" command at all.
            var cmd = new AssertCommand("DURATION", "<=", ResolvedPrevious("(previous:0)"));

            var missing = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 100), null, new BaselineStore());
            Assert.AreEqual(TestOutcome.Error, missing.Outcome);
            Assert.DoesNotContain("previous:0", missing.Message);
            StringAssert.Contains(missing.Message, "previous batch");

            // Same for a baseline that was captured but has no metrics.
            var store = new BaselineStore();
            store.Capture("(previous:0)", null, null);
            var noMetrics = AssertionEngine.EvaluatePerformance(cmd, Metrics(PerformanceProperty.Duration, 100), null, store);
            Assert.AreEqual(TestOutcome.Error, noMetrics.Outcome);
            Assert.DoesNotContain("previous:0", noMetrics.Message);

            // ...and for a missing result table.
            var table = new AssertTableCommand(AssertTableMode.Ordered) { Baseline = ResolvedPrevious("(previous:0)") };
            var noTable = AssertionEngine.EvaluateTable(table, Table(("Red", 5)), null, null, store);
            Assert.AreEqual(TestOutcome.Error, noTable.Outcome);
            Assert.DoesNotContain("previous:0", noTable.Message);
        }

        [TestMethod]
        public void Previous_UnresolvedReferenceIsFlagged()
        {
            var unresolved = new BaselineReference(BaselineReference.PreviousName, 1.0, isPrevious: true);

            Assert.IsTrue(unresolved.IsUnresolvedPrevious);
            Assert.IsTrue(unresolved.IsPrevious);

            // A resolved one keeps IsPrevious (for display) but is no longer flagged as unresolved.
            var resolved = ResolvedPrevious("(previous:2)");
            Assert.IsFalse(resolved.IsUnresolvedPrevious);
            Assert.IsTrue(resolved.IsPrevious);
        }

        #endregion
    }
}
