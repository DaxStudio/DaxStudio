using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DaxStudio.CommandLine.Commands;
using DaxStudio.Core.Assertions;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.PreProcessor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.CommandLine.Tests
{
    /// <summary>
    /// Tests for the dscmd comment-script assertion runner. These cover the parts that do not need a
    /// live Analysis Services connection: which execution strategy a script requires, how a batch's
    /// assertions are evaluated (including baselines), and the failure paths that must report an error
    /// rather than silently pass.
    /// </summary>
    [TestClass]
    public class FileCommandAssertionTests
    {
        private static IReadOnlyList<ScriptBatch> Parse(string script)
            => AntlrPreProcessor.Parse(script).Batches;

        private static DataTable Table(params (string Color, long Count)[] rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("Color", typeof(string));
            dt.Columns.Add("ProductCount", typeof(long));
            foreach (var r in rows) dt.Rows.Add(r.Color, r.Count);
            return dt;
        }

        private static IReadOnlyDictionary<PerformanceProperty, double> Metrics(double duration, double seQueries = 0)
            => new Dictionary<PerformanceProperty, double>
            {
                [PerformanceProperty.Duration] = duration,
                [PerformanceProperty.SE_QUERIES] = seQueries,
            };

        #region Strategy selection

        [TestMethod]
        public void ResultOnlyScriptUsesTheSimplePath()
        {
            var batches = Parse("--> ASSERT ROWCOUNT >= 1\nEVALUATE { 1 }\n");

            Assert.IsFalse(FileCommand.RequiresSequencedRun(batches, out var needsTrace));
            Assert.IsFalse(needsTrace);
        }

        [TestMethod]
        public void PerformanceAssertionRequiresTheTrace()
        {
            var batches = Parse("--> ASSERT DURATION < 500\nEVALUATE { 1 }\n");

            Assert.IsTrue(FileCommand.RequiresSequencedRun(batches, out var needsTrace));
            Assert.IsTrue(needsTrace);
        }

        [TestMethod]
        public void BaselineRequiresSequencingButNotNecessarilyTheTrace()
        {
            // A results-only baseline comparison needs ordered execution so the capture happens first,
            // but nothing reads the metrics, so starting the trace would be wasted work.
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT TABLE BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n");

            Assert.IsTrue(FileCommand.RequiresSequencedRun(batches, out var needsTrace));
            Assert.IsFalse(needsTrace);
        }

        [TestMethod]
        public void BaselineWithAPerformanceAssertionRequiresBoth()
        {
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT DURATION <= BASELINE \"v1\" * 1.1\n" +
                                "EVALUATE { 1 }\n");

            Assert.IsTrue(FileCommand.RequiresSequencedRun(batches, out var needsTrace));
            Assert.IsTrue(needsTrace);
        }

        [TestMethod]
        public void PreviousRequiresBothViaItsSynthesisedCapture()
        {
            var batches = Parse("EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT DURATION <= PREVIOUS\n" +
                                "EVALUATE { 2 }\n");

            Assert.IsTrue(FileCommand.RequiresSequencedRun(batches, out var needsTrace));
            Assert.IsTrue(needsTrace);
            Assert.IsTrue(FileCommand.BatchIsBaseline(batches[0]), "the previous batch is captured");
            Assert.IsTrue(FileCommand.BatchHasAsserts(batches[1]));
        }

        [TestMethod]
        public void RequiresSequencedRun_HandlesNullAndEmpty()
        {
            Assert.IsFalse(FileCommand.RequiresSequencedRun(null, out _));
            Assert.IsFalse(FileCommand.RequiresSequencedRun(new List<ScriptBatch>(), out _));
        }

        #endregion

        #region Metrics

        [TestMethod]
        public void NoTraceYieldsNoMetricsSoPerformanceAssertionsError()
        {
            // The critical property: without a trace a performance assertion must ERROR, never pass.
            var metrics = FileCommand.BuildPerformanceMetrics(null);
            Assert.AreEqual(0, metrics.Count);

            var batch = Parse("--> ASSERT DURATION < 500\nEVALUATE { 1 }\n")[0];
            var results = FileCommand.EvaluateBatchAssertions(batch, Table(("Red", 5)), metrics, null, null);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TestOutcome.Error, results[0].Outcome);
        }

        #endregion

        #region Evaluation

        [TestMethod]
        public void EvaluatesLiteralAssertionsAgainstTheBatchResults()
        {
            var batch = Parse("--> TEST \"my test\"\n" +
                              "--> ASSERT ROWCOUNT = 2\n" +
                              "--> ASSERT DURATION < 500\n" +
                              "EVALUATE { 1 }\n")[0];

            var results = FileCommand.EvaluateBatchAssertions(
                batch, Table(("Red", 5), ("Blue", 3)), Metrics(400), null, null);

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.Outcome == TestOutcome.Passed));
            Assert.IsTrue(results.All(r => r.TestName == "my test"));
        }

        [TestMethod]
        public void CapturedBaselineIsUsedByALaterBatch()
        {
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT TABLE      BASELINE \"v1\"\n" +
                                "--> ASSERT DURATION   <= BASELINE \"v1\" * 1.1\n" +
                                "--> ASSERT SE_QUERIES <= BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n");

            var store = new BaselineStore();
            FileCommand.CaptureBatchBaselines(batches[0], Table(("Red", 5)), Metrics(1000, 6), store);

            // Candidate: identical results, 20% faster, fewer SE queries.
            var results = FileCommand.EvaluateBatchAssertions(
                batches[1], Table(("Red", 5)), Metrics(800, 2), store, null);

            Assert.AreEqual(3, results.Count);
            CollectionAssert.AreEqual(
                new[] { TestOutcome.Passed, TestOutcome.Passed, TestOutcome.Passed },
                results.Select(r => r.Outcome).ToArray());
            StringAssert.Contains(results[1].Actual, "-20%");
        }

        [TestMethod]
        public void ARegressionAgainstTheBaselineFails()
        {
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT DURATION <= BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n");

            var store = new BaselineStore();
            FileCommand.CaptureBatchBaselines(batches[0], Table(("Red", 5)), Metrics(1000), store);

            var results = FileCommand.EvaluateBatchAssertions(
                batches[1], Table(("Red", 5)), Metrics(1500), store, null);

            Assert.AreEqual(TestOutcome.Failed, results[0].Outcome);
        }

        [TestMethod]
        public void AMissingBaselineErrorsRatherThanPasses()
        {
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT DURATION <= BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n");

            // Nothing captured - e.g. the baseline batch's query failed.
            var results = FileCommand.EvaluateBatchAssertions(
                batches[1], Table(("Red", 5)), Metrics(800), new BaselineStore(), null);

            Assert.AreEqual(TestOutcome.Error, results[0].Outcome);
        }

        #endregion

        #region Failure handling

        [TestMethod]
        public void ABatchWithAssertionsButNoQueryReportsAnError()
        {
            // A trailing "--> GO" followed by assert lines is an ordinary authoring outcome: it produces
            // a batch with assertions and no query. Those assertions must still be reported - otherwise
            // the run summarises as "0 passed, 0 failed, 0 errors" and exits 0.
            var batches = Parse("EVALUATE { 1 }\n--> GO\n--> ASSERT DURATION < 500\n");

            Assert.AreEqual(2, batches.Count);
            Assert.IsFalse(batches[1].RunsItsQuery);
            Assert.IsTrue(FileCommand.BatchHasAsserts(batches[1]));

            var results = FileCommand.BatchNotEvaluated(batches[1]);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TestOutcome.Error, results[0].Outcome);
            StringAssert.Contains(results[0].Message, "did not run a query");
        }

        [TestMethod]
        public void ARowcountAssertionInAQuerylessBatchDoesNotSilentlyPass()
        {
            // Guards the specific false green: evaluating against a null table would make
            // "ASSERT ROWCOUNT = 0" pass even though nothing ran.
            var batches = Parse("EVALUATE { 1 }\n--> GO\n--> ASSERT ROWCOUNT = 0\n");

            var results = FileCommand.BatchNotEvaluated(batches[1]);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(TestOutcome.Error, results[0].Outcome);
        }

        [TestMethod]
        public void EveryAssertionInABatchIsReportedWhenNotEvaluated()
        {
            var batches = Parse("EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> TEST \"my test\"\n" +
                                "--> ASSERT ROWCOUNT = 1\n" +
                                "--> ASSERT TABLE CSV \"expected.csv\"\n" +
                                "--> ASSERT DURATION < 500\n");

            var results = FileCommand.BatchNotEvaluated(batches[1]);

            Assert.AreEqual(3, results.Count);
            Assert.IsTrue(results.All(r => r.Outcome == TestOutcome.Error));
            Assert.IsTrue(results.All(r => r.TestName == "my test"));
        }

        [TestMethod]
        public void AFailedQueryReportsAnErrorPerAssertion()        {
            // Without this, a failed query would leave a null table and an ASSERT ROWCOUNT = 0 would pass.
            var batch = Parse("--> TEST \"my test\"\n" +
                              "--> ASSERT ROWCOUNT = 0\n" +
                              "--> ASSERT DURATION < 500\n" +
                              "EVALUATE { 1 }\n")[0];

            var results = FileCommand.BatchQueryFailed(batch, new InvalidOperationException("boom"));

            Assert.AreEqual(2, results.Count);
            Assert.IsTrue(results.All(r => r.Outcome == TestOutcome.Error));
            Assert.IsTrue(results.All(r => r.Message == "boom"));
            Assert.IsTrue(results.All(r => r.TestName == "my test"));
        }

        [TestMethod]
        public void ABaselineCapturedWithoutMetricsErrorsOnAPerformanceAssertion()
        {
            var batches = Parse("--> BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n" +
                                "--> GO\n" +
                                "--> ASSERT TABLE    BASELINE \"v1\"\n" +
                                "--> ASSERT DURATION <= BASELINE \"v1\"\n" +
                                "EVALUATE { 1 }\n");

            // Trace never started, so the capture has results but no metrics.
            var store = new BaselineStore();
            FileCommand.CaptureBatchBaselines(batches[0], Table(("Red", 5)), FileCommand.BuildPerformanceMetrics(null), store);

            var results = FileCommand.EvaluateBatchAssertions(
                batches[1], Table(("Red", 5)), FileCommand.BuildPerformanceMetrics(null), store, null);

            // The table comparison still works; only the performance assertion errors.
            Assert.AreEqual(TestOutcome.Passed, results[0].Outcome);
            Assert.AreEqual(TestOutcome.Error, results[1].Outcome);
        }

        #endregion
    }
}
