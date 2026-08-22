using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Caliburn.Micro;
using DaxStudio.Common.Enums;
using DaxStudio.Core.Events;
using DaxStudio.Core.Trace;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TraceWatcherBusyStateTests
    {
        // A minimal concrete trace watcher so that the base class behaviour can be tested without
        // needing a live connection or any of the WPF views.
        private class TestTraceWatcher : TraceWatcherBaseModel
        {
            public TestTraceWatcher(IEventAggregator eventAggregator, IGlobalOptions globalOptions, IWindowManager windowManager)
                : base(eventAggregator, globalOptions, windowManager)
            { }

            public int OnResetCallCount { get; private set; }

            protected override List<DaxStudioTraceEventClass> GetMonitoredEvents() => new List<DaxStudioTraceEventClass>();
            protected override void ProcessResults() { }
            protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent) => true;
            public override void OnReset() { OnResetCallCount++; }
            public override void ClearAll() { }
            public override void CopyAll() { }
            public override void CopyEventContent() { }
            public override void CopyResults() { }
            public override void ExportTraceDetails(string filePath) { }
            public override string Title => nameof(TestTraceWatcher);
            public override string TraceSuffix => "test";
            public override string ToolTipText => string.Empty;
            public override string ContentId => nameof(TestTraceWatcher);
            public override int SortOrder => 1;
            public override bool FilterForCurrentSession => true;
            public override string ImageResource => string.Empty;
            public override string KeyTip => "T";
        }

        private static TestTraceWatcher CreateCheckedWatcher()
        {
            var watcher = new TestTraceWatcher(
                Substitute.For<IEventAggregator>(),
                Substitute.For<IGlobalOptions>(),
                Substitute.For<IWindowManager>());

            // set the backing field directly so that we do not kick off a real trace
            var isChecked = typeof(TraceWatcherBaseModel).GetField("_isChecked", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(isChecked, "the _isChecked field could not be found");
            isChecked.SetValue(watcher, true);

            return watcher;
        }

        [TestMethod]
        public void QueryStartedShowsTheQueryRunningBusyMessage()
        {
            var watcher = CreateCheckedWatcher();

            _ = watcher.HandleAsync(new QueryStartedEvent(), CancellationToken.None);

            // Reset() clears IsBusy/BusyMessage, so if it runs after the busy state is set the
            // "Query Running..." overlay is wiped before it is ever displayed
            Assert.IsTrue(watcher.IsBusy, "the trace watcher should be busy while the query is running");
            Assert.AreEqual("Query Running...", watcher.BusyMessage);
        }

        [TestMethod]
        public void QueryStartedStillResetsThePreviousResults()
        {
            var watcher = CreateCheckedWatcher();
            watcher.Events.Enqueue(new DaxStudioTraceEventArgs());
            watcher.ErrorMessage = "a previous error";

            _ = watcher.HandleAsync(new QueryStartedEvent(), CancellationToken.None);

            Assert.AreEqual(1, watcher.OnResetCallCount, "the watcher should still be reset when a new query starts");
            Assert.AreEqual(0, watcher.Events.Count, "events from the previous query should be discarded");
            Assert.AreEqual(string.Empty, watcher.ErrorMessage, "the error from the previous query should be cleared");
        }

        [TestMethod]
        public void QueryStartedIsIgnoredWhenTheTraceIsNotChecked()
        {
            var watcher = new TestTraceWatcher(
                Substitute.For<IEventAggregator>(),
                Substitute.For<IGlobalOptions>(),
                Substitute.For<IWindowManager>());

            _ = watcher.HandleAsync(new QueryStartedEvent(), CancellationToken.None);

            Assert.IsFalse(watcher.IsBusy, "an inactive trace watcher should not show a busy message");
        }
    }
}
