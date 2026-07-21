using Caliburn.Micro;
using DaxStudio.Core.Assertions;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Packaging;
using System.Linq;
using System.Windows.Input;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class TestResultsPaneViewModel : ToolWindowBase, ISaveState
    {
        private readonly BindableCollection<TestResult> _results;
        private readonly IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public TestResultsPaneViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _results = new BindableCollection<TestResult>();
            // Hidden until a batch containing a test is actually run (see DocumentViewModel
            // .ProcessCommentScriptPostQueryCommandsAsync), so users who never write tests never
            // see the pane. It can then be closed/hidden like the trace windows (CanHide below).
            IsVisible = false;
        }

        public IObservableCollection<TestResult> Results => _results;

        public override string Title => "Test Results";
        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "testresults";

        // Allow the pane to be closed/hidden from its title-bar 'x', mirroring the trace windows.
        public override bool CanHide => true;

        public void Clear()
        {
            _results.Clear();
            RefreshSummary();
        }

        public void AddResults(IEnumerable<TestResult> results)
        {
            if (results == null) return;
            _results.AddRange(results);
            RefreshSummary();
        }

        /// <summary>
        /// Replaces the pane contents with the set of tests discovered in the editor text (each in a
        /// greyed-out "pending" state) - but only when that set actually differs from what is already
        /// shown. The comparison ignores the outcome, so real Passed/Failed results from a run are
        /// preserved while the user edits unrelated parts of the query, and are only reset back to
        /// pending once the assertions themselves change (added, removed, or their expected value
        /// edited). Returns true when the pane was updated.
        /// </summary>
        public bool TryUpdateDiscoveredTests(IReadOnlyList<TestResult> discovered)
        {
            discovered = discovered ?? new List<TestResult>();
            if (Signature(discovered) == Signature(_results)) return false;

            _results.Clear();
            _results.AddRange(discovered);
            RefreshSummary();
            return true;
        }

        /// <summary>
        /// Unconditionally replaces the pane contents with the supplied discovered tests in their
        /// pending (clock icon) state. Called at the start of a run so any Passed/Failed/Error results
        /// from a previous run are reset back to pending before the run transitions them to running and
        /// then to their final outcome. Unlike <see cref="TryUpdateDiscoveredTests"/> this always
        /// replaces (it ignores the signature optimisation) so a re-run of the same script still resets.
        /// </summary>
        public void SetPendingForRun(IReadOnlyList<TestResult> discovered)
        {
            _results.Clear();
            if (discovered != null) _results.AddRange(discovered);
            RefreshSummary();
        }

        /// <summary>
        /// Marks every test currently shown in the pane as running (with a "running" icon) while the
        /// query that produces its data executes. Called after <see cref="SetPendingForRun"/> at the
        /// start of a run; the final Passed/Failed/Error outcomes replace these once the query
        /// completes. The bindings in the view are OneTime, so a Refresh is raised to regenerate the
        /// rows and re-read the new outcome.
        /// </summary>
        public void MarkAllRunning()
        {
            foreach (var r in _results)
                r.Outcome = TestOutcome.Running;
            _results.Refresh();
            RefreshSummary();
        }

        /// <summary>
        /// Transitions the tests belonging to a single script batch (sections separated by
        /// <c>--&gt; GO</c>) from pending to a "running" state as that batch's query starts executing.
        /// Batches run sequentially, so this is called once per batch during a run. Only pending tests
        /// are advanced, so a test that has already produced a final outcome is left untouched.
        /// </summary>
        public void MarkBatchRunning(int batchIndex)
        {
            var changed = false;
            foreach (var r in _results.Where(r => r.BatchIndex == batchIndex && r.Outcome == TestOutcome.Pending))
            {
                r.Outcome = TestOutcome.Running;
                changed = true;
            }
            if (!changed) return;
            _results.Refresh();
            RefreshSummary();
        }

        /// <summary>
        /// Replaces the pending/running rows of a single script batch (sections separated by
        /// <c>--&gt; GO</c>) with their evaluated results, in place, leaving every other batch's rows -
        /// and their order - untouched. Used by the per-batch execution path so a completed batch shows
        /// its Passed/Failed/Error outcome while later batches remain pending (clock icon). The supplied
        /// results are stamped with <paramref name="batchIndex"/> so subsequent subset operations still
        /// match them. Must be called on the UI thread.
        /// </summary>
        public void SetBatchResults(int batchIndex, IReadOnlyList<TestResult> batchResults)
        {
            batchResults = batchResults ?? new List<TestResult>();
            foreach (var r in batchResults) r.BatchIndex = batchIndex;

            var rebuilt = new List<TestResult>(_results.Count);
            bool inserted = false;
            foreach (var r in _results)
            {
                if (r.BatchIndex == batchIndex)
                {
                    // Drop the batch's existing (pending/running) rows, splicing the evaluated
                    // results in at the position of the first one so ordering is preserved.
                    if (!inserted) { rebuilt.AddRange(batchResults); inserted = true; }
                    continue;
                }
                rebuilt.Add(r);
            }
            if (!inserted) rebuilt.AddRange(batchResults);

            _results.Clear();
            _results.AddRange(rebuilt);
            RefreshSummary();
        }

        /// <summary>
        /// Turns any tests still shown as running into an errored state with the supplied message.
        /// Used when a run is aborted (e.g. the query throws) before the assertions could be evaluated,
        /// so the pane does not leave tests stuck showing the "running" icon.
        /// </summary>
        public void MarkRunningAsError(string message)
        {
            var changed = false;
            foreach (var r in _results.Where(r => r.Outcome == TestOutcome.Running))
            {
                r.Outcome = TestOutcome.Error;
                r.Message = message;
                changed = true;
            }
            if (!changed) return;
            _results.Refresh();
            RefreshSummary();
        }

        // A stable identity for a set of tests based on the assertion definitions (test name + kind +
        // description + expected value) and NOT their outcome, so a run's results and the pending rows
        // discovered from the same script text compare as equal.
        private static string Signature(IEnumerable<TestResult> results)
        {
            return string.Join("\u0001", results.Select(r => $"{r.TestName}\u0002{r.Kind}\u0002{r.Description}\u0002{r.Expected}"));
        }

        private void RefreshSummary()
        {
            NotifyOfPropertyChange(nameof(TotalCount));
            NotifyOfPropertyChange(nameof(PassedCount));
            NotifyOfPropertyChange(nameof(FailedCount));
            NotifyOfPropertyChange(nameof(ErrorCount));
            NotifyOfPropertyChange(nameof(PendingCount));
            NotifyOfPropertyChange(nameof(RunningCount));
            NotifyOfPropertyChange(nameof(AllPassed));
            NotifyOfPropertyChange(nameof(HasFailures));
            NotifyOfPropertyChange(nameof(SummaryText));
        }

        public int TotalCount => _results.Count;
        public int PassedCount => _results.Count(r => r.Outcome == TestOutcome.Passed);
        public int FailedCount => _results.Count(r => r.Outcome == TestOutcome.Failed);
        public int ErrorCount => _results.Count(r => r.Outcome == TestOutcome.Error);
        public int PendingCount => _results.Count(r => r.Outcome == TestOutcome.Pending);
        public int RunningCount => _results.Count(r => r.Outcome == TestOutcome.Running);
        public bool AllPassed => TotalCount > 0 && FailedCount == 0 && ErrorCount == 0 && PendingCount == 0 && RunningCount == 0;

        /// <summary>True when at least one test has failed or errored (used to colour the summary red).</summary>
        public bool HasFailures => FailedCount > 0 || ErrorCount > 0;

        public string SummaryText
        {
            get
            {
                if (TotalCount == 0) return string.Empty;
                // While the query runs every test is marked running, so show a simple "running" summary.
                if (RunningCount == TotalCount) return $"Running {TotalCount} test{(TotalCount == 1 ? "" : "s")}...";
                // Before a run every discovered test is pending, so show a simple "pending" summary.
                if (PendingCount == TotalCount) return $"{TotalCount} test{(TotalCount == 1 ? "" : "s")} pending";

                var summary = $"{PassedCount} passed, {FailedCount} failed, {ErrorCount} errors";
                if (RunningCount > 0) summary += $", {RunningCount} running";
                if (PendingCount > 0) summary += $", {PendingCount} pending";
                return summary;
            }
        }

        private ICommand _gotoLocation;
        public ICommand GotoLocation
        {
            get
            {
                if (_gotoLocation == null)
                {
                    _gotoLocation = new Utils.RelayCommand(
                        param =>
                        {
                            var result = param as TestResult;
                            if (result == null || result.Line <= 0) return;
                            _eventAggregator.PublishAsync(new NavigateToLocationEvent(result.Line, 1));
                        },
                        param =>
                        {
                            var result = param as TestResult;
                            return result != null && result.Line > 0;
                        });
                }
                return _gotoLocation;
            }
        }

        #region ISaveState
        // The Test Results pane holds transient per-run output that is not persisted with the
        // document, so the ISaveState members are intentionally no-ops (mirroring the lightest
        // existing pane implementation).
        public void Save(string filename) { }
        public void Load(string filename) { }
        public string GetJson() { return string.Empty; }
        public void LoadJson(string json) { }
        public void SavePackage(Package package) { }
        public void LoadPackage(Package package) { }
        #endregion
    }
}
