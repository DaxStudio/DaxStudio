using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using DaxStudio.Core.Assertions;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

using TestResult = DaxStudio.Core.Assertions.TestResult;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Verifies the "pending" test-discovery behaviour of the <see cref="TestResultsPaneViewModel"/>:
    /// discovered tests are shown greyed-out until a run replaces them, and real run outcomes are only
    /// reset back to pending when the assertion definitions themselves change.
    /// </summary>
    [TestClass]
    public class TestResultsPaneDiscoveryTests
    {
        [TestInitialize]
        public void Init()
        {
            // The BindableCollection updates route through Caliburn.Micro's PlatformProvider; force the
            // default provider so they run synchronously without a WPF Dispatcher.
            PlatformProvider.Current = new DefaultPlatformProvider();
        }

        private static TestResultsPaneViewModel BuildViewModel()
        {
            return new TestResultsPaneViewModel(Substitute.For<IEventAggregator>());
        }

        private static TestResult Pending(string test, AssertionKind kind, string desc, string expected)
        {
            return new TestResult { TestName = test, Kind = kind, Description = desc, Expected = expected, Outcome = TestOutcome.Pending };
        }

        [TestMethod]
        public void NewPane_IsHiddenAndCanHide()
        {
            var vm = BuildViewModel();

            // Hidden until a batch containing a test is run; closeable like the trace windows.
            Assert.IsFalse(vm.IsVisible, "pane should start hidden");
            Assert.IsTrue(vm.CanHide, "pane should be closeable/hideable");
        }

        [TestMethod]
        public void TryUpdate_PopulatesPendingTests()
        {
            var vm = BuildViewModel();

            // Discovery populates content but must NOT reveal the pane (only a run does that).
            var changed = vm.TryUpdateDiscoveredTests(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
            });

            Assert.IsTrue(changed);
            Assert.IsFalse(vm.IsVisible, "discovery should not reveal the pane");
            Assert.AreEqual(1, vm.TotalCount);
            Assert.AreEqual(1, vm.PendingCount);
            Assert.IsFalse(vm.AllPassed);
            Assert.AreEqual("1 test pending", vm.SummaryText);
        }

        [TestMethod]
        public void TryUpdate_SameSignature_DoesNotReplaceExistingResults()
        {
            var vm = BuildViewModel();

            // Simulate a completed run: a real Passed result is in the pane.
            vm.AddResults(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Actual = "5", Outcome = TestOutcome.Passed },
            });

            // Re-discovering the same assertion (same signature) must NOT wipe the real result.
            var changed = vm.TryUpdateDiscoveredTests(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
            });

            Assert.IsFalse(changed);
            Assert.AreEqual(TestOutcome.Passed, vm.Results.Single().Outcome);
            Assert.AreEqual(1, vm.PassedCount);
            Assert.IsTrue(vm.AllPassed);
        }

        [TestMethod]
        public void TryUpdate_ChangedExpectedValue_ResetsToPending()
        {
            var vm = BuildViewModel();
            vm.AddResults(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Actual = "5", Outcome = TestOutcome.Passed },
            });

            // The threshold changed (> 1 -> > 10): a different signature, so the pane resets to pending.
            var changed = vm.TryUpdateDiscoveredTests(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 10", "> 10"),
            });

            Assert.IsTrue(changed);
            Assert.AreEqual(TestOutcome.Pending, vm.Results.Single().Outcome);
        }

        [TestMethod]
        public void TryUpdate_Null_ClearsPane()
        {
            var vm = BuildViewModel();
            vm.TryUpdateDiscoveredTests(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
            });

            var changed = vm.TryUpdateDiscoveredTests(null);

            Assert.IsTrue(changed);
            Assert.AreEqual(0, vm.TotalCount);
            Assert.AreEqual(string.Empty, vm.SummaryText);
        }

        [TestMethod]
        public void Summary_MixedPendingAndPassed_ShowsBoth()
        {
            var vm = BuildViewModel();
            vm.AddResults(new List<TestResult>
            {
                new TestResult { Outcome = TestOutcome.Passed, Description = "a", Expected = "a" },
                new TestResult { Outcome = TestOutcome.Pending, Description = "b", Expected = "b" },
            });

            Assert.AreEqual(1, vm.PassedCount);
            Assert.AreEqual(1, vm.PendingCount);
            Assert.IsFalse(vm.AllPassed);
            Assert.AreEqual("1 passed, 0 failed, 0 errors, 1 pending", vm.SummaryText);
        }

        [TestMethod]
        public void SetPendingForRun_ReplacesPreviousResultsWithPending()
        {
            var vm = BuildViewModel();

            // A previous run left a real Passed result in the pane.
            vm.AddResults(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Actual = "5", Outcome = TestOutcome.Passed },
            });

            // Starting a re-run of the same script (same signature) must still reset it to pending,
            // unlike TryUpdateDiscoveredTests which preserves the prior result.
            vm.SetPendingForRun(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
            });

            Assert.AreEqual(1, vm.TotalCount);
            Assert.AreEqual(TestOutcome.Pending, vm.Results.Single().Outcome);
            Assert.AreEqual(1, vm.PendingCount);
        }

        [TestMethod]
        public void MarkAllRunning_TransitionsPendingToRunning()
        {
            var vm = BuildViewModel();
            vm.SetPendingForRun(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
                Pending("t2", AssertionKind.Table, "TABLE", "2 rows"),
            });

            vm.MarkAllRunning();

            Assert.AreEqual(2, vm.RunningCount);
            Assert.AreEqual(0, vm.PendingCount);
            Assert.IsFalse(vm.AllPassed, "tests still running are not all passed");
            Assert.AreEqual("Running 2 tests...", vm.SummaryText);
        }

        [TestMethod]
        public void MarkBatchRunning_OnlyAdvancesThatBatchsPendingTests()
        {
            var vm = BuildViewModel();
            vm.SetPendingForRun(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Outcome = TestOutcome.Pending, BatchIndex = 0 },
                new TestResult { TestName = "t2", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 2", Expected = "> 2", Outcome = TestOutcome.Pending, BatchIndex = 1 },
            });

            // Batch 0 starts running - only its test transitions; batch 1 stays pending.
            vm.MarkBatchRunning(0);

            var t1 = vm.Results.Single(r => r.TestName == "t1");
            var t2 = vm.Results.Single(r => r.TestName == "t2");
            Assert.AreEqual(TestOutcome.Running, t1.Outcome);
            Assert.AreEqual(TestOutcome.Pending, t2.Outcome);
            Assert.AreEqual(1, vm.RunningCount);
            Assert.AreEqual(1, vm.PendingCount);

            // Then batch 1 starts running.
            vm.MarkBatchRunning(1);
            Assert.AreEqual(TestOutcome.Running, t2.Outcome);
            Assert.AreEqual(2, vm.RunningCount);
        }

        [TestMethod]
        public void SetBatchResults_ReplacesOnlyThatBatchAndLeavesOthersPending()
        {
            var vm = BuildViewModel();
            vm.SetPendingForRun(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.Table, Description = "TABLE Ordered", Expected = "8 rows", Outcome = TestOutcome.Pending, BatchIndex = 0 },
                new TestResult { TestName = "t2", Kind = AssertionKind.RowCount, Description = "SE_QUERIES <= 1", Expected = "<= 1", Outcome = TestOutcome.Pending, BatchIndex = 1 },
                new TestResult { TestName = "t3", Kind = AssertionKind.Table, Description = "TABLE Ordered", Expected = "4 rows", Outcome = TestOutcome.Pending, BatchIndex = 1 },
            });

            // Batch 0 completes: only its row becomes a final outcome; batch 1 stays pending.
            vm.SetBatchResults(0, new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.Table, Description = "TABLE Ordered", Expected = "8 rows", Actual = "8 rows", Outcome = TestOutcome.Passed },
            });

            Assert.AreEqual(3, vm.Results.Count, "The batch's row count is unchanged");
            Assert.AreEqual(TestOutcome.Passed, vm.Results.Single(r => r.TestName == "t1").Outcome);
            Assert.AreEqual(TestOutcome.Pending, vm.Results.Single(r => r.TestName == "t2").Outcome);
            Assert.AreEqual(TestOutcome.Pending, vm.Results.Single(r => r.TestName == "t3").Outcome);
            Assert.AreEqual(1, vm.PassedCount);
            Assert.AreEqual(2, vm.PendingCount);

            // Order is preserved: batch 0's result stays first.
            Assert.AreEqual("t1", vm.Results.First().TestName);
        }

        [TestMethod]
        public void SetBatchResults_StampsBatchIndexAndSupportsMultipleRowsPerBatch()
        {
            var vm = BuildViewModel();
            vm.SetPendingForRun(new List<TestResult>
            {
                new TestResult { TestName = "b0", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Outcome = TestOutcome.Pending, BatchIndex = 0 },
                new TestResult { TestName = "b1a", Kind = AssertionKind.RowCount, Description = "SE_QUERIES <= 1", Expected = "<= 1", Outcome = TestOutcome.Pending, BatchIndex = 1 },
                new TestResult { TestName = "b1b", Kind = AssertionKind.Table, Description = "TABLE Ordered", Expected = "4 rows", Outcome = TestOutcome.Pending, BatchIndex = 1 },
            });

            // Batch 1 (two assertions) completes - results supplied without a BatchIndex are stamped.
            vm.SetBatchResults(1, new List<TestResult>
            {
                new TestResult { TestName = "b1a", Kind = AssertionKind.RowCount, Description = "SE_QUERIES <= 1", Expected = "<= 1", Outcome = TestOutcome.Passed },
                new TestResult { TestName = "b1b", Kind = AssertionKind.Table, Description = "TABLE Ordered", Expected = "4 rows", Outcome = TestOutcome.Failed },
            });

            Assert.AreEqual(3, vm.Results.Count);
            Assert.AreEqual(TestOutcome.Pending, vm.Results.Single(r => r.TestName == "b0").Outcome);
            Assert.AreEqual(TestOutcome.Passed, vm.Results.Single(r => r.TestName == "b1a").Outcome);
            Assert.AreEqual(TestOutcome.Failed, vm.Results.Single(r => r.TestName == "b1b").Outcome);
            // Both new rows carry the batch index so a later subset update still matches them.
            Assert.IsTrue(vm.Results.Where(r => r.TestName.StartsWith("b1")).All(r => r.BatchIndex == 1));
            Assert.AreEqual(1, vm.PassedCount);
            Assert.AreEqual(1, vm.FailedCount);
            Assert.AreEqual(1, vm.PendingCount);
        }

        [TestMethod]
        public void MarkRunningAsError_ErrorsOnlyRunningTests()
        {
            var vm = BuildViewModel();
            vm.SetPendingForRun(new List<TestResult>
            {
                Pending("t1", AssertionKind.RowCount, "ROWCOUNT > 1", "> 1"),
            });
            vm.MarkAllRunning();

            vm.MarkRunningAsError("aborted");

            var result = vm.Results.Single();
            Assert.AreEqual(TestOutcome.Error, result.Outcome);
            Assert.AreEqual("aborted", result.Message);
            Assert.AreEqual(0, vm.RunningCount);
            Assert.AreEqual(1, vm.ErrorCount);
        }

        [TestMethod]
        public void MarkRunningAsError_LeavesFinalOutcomesUntouched()
        {
            var vm = BuildViewModel();

            // A completed run: real outcomes already recorded, nothing left running.
            vm.AddResults(new List<TestResult>
            {
                new TestResult { TestName = "t1", Kind = AssertionKind.RowCount, Description = "ROWCOUNT > 1", Expected = "> 1", Actual = "5", Outcome = TestOutcome.Passed },
            });

            vm.MarkRunningAsError("aborted");

            Assert.AreEqual(TestOutcome.Passed, vm.Results.Single().Outcome, "a finished test must not be errored");
            Assert.IsTrue(vm.AllPassed);
        }
    }
}
