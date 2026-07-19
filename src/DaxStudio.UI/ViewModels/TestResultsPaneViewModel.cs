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
            NotifyOfPropertyChange(nameof(AllPassed));
            NotifyOfPropertyChange(nameof(HasFailures));
            NotifyOfPropertyChange(nameof(SummaryText));
        }

        public int TotalCount => _results.Count;
        public int PassedCount => _results.Count(r => r.Outcome == TestOutcome.Passed);
        public int FailedCount => _results.Count(r => r.Outcome == TestOutcome.Failed);
        public int ErrorCount => _results.Count(r => r.Outcome == TestOutcome.Error);
        public int PendingCount => _results.Count(r => r.Outcome == TestOutcome.Pending);
        public bool AllPassed => TotalCount > 0 && FailedCount == 0 && ErrorCount == 0 && PendingCount == 0;

        /// <summary>True when at least one test has failed or errored (used to colour the summary red).</summary>
        public bool HasFailures => FailedCount > 0 || ErrorCount > 0;

        public string SummaryText
        {
            get
            {
                if (TotalCount == 0) return string.Empty;
                // Before a run every discovered test is pending, so show a simple "pending" summary.
                if (PendingCount == TotalCount) return $"{TotalCount} test{(TotalCount == 1 ? "" : "s")} pending";

                var summary = $"{PassedCount} passed, {FailedCount} failed, {ErrorCount} errors";
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
