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
    }
}
