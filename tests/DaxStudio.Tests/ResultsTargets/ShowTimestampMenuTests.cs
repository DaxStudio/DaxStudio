using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ResultsTargets;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;

namespace DaxStudio.Tests.ResultsTargets
{
    /// <summary>
    /// Tests for the metadata-pane database context menu entry points (Show Last Updated /
    /// Show Max Updated) which reuse the same timestamp-tree builder as the "--> SHOW" commands.
    /// The connection is a concrete ConnectionManager that cannot be substituted, so these tests
    /// exercise the routing and error/empty handling (mirroring ShowCommandDispatchTests).
    /// </summary>
    [TestClass]
    public class ShowTimestampMenuTests
    {
        [TestMethod]
        public void TryBuildTimestampShowTab_LastUpdated_WithoutConnection_ReportsErrorAndNoDescriptor()
        {
            var runner = Substitute.For<IQueryRunner>();
            // runner.Connection is null (default), so BuildMetadataTimestampTree throws - which the
            // helper must catch and surface as an error rather than letting it propagate.
            var handled = ResultsTargetGrid.TryBuildTimestampShowTab(runner, ShowType.LastUpdated, out var descriptor);

            Assert.IsTrue(handled, "The request should be treated as handled even when it errors");
            Assert.IsNull(descriptor, "No tab descriptor should be produced when the build fails");
            runner.Received().OutputError(Arg.Is<string>(s => s.Contains("LastUpdated")));
        }

        [TestMethod]
        public void TryBuildTimestampShowTab_MaxUpdated_WithoutConnection_ReportsErrorAndNoDescriptor()
        {
            var runner = Substitute.For<IQueryRunner>();

            var handled = ResultsTargetGrid.TryBuildTimestampShowTab(runner, ShowType.MaxUpdated, out var descriptor);

            Assert.IsTrue(handled);
            Assert.IsNull(descriptor);
            runner.Received().OutputError(Arg.Is<string>(s => s.Contains("MaxUpdated")));
        }

        [TestMethod]
        public void TryBuildTimestampShowTab_NonTimestampType_ReportsUnknownType()
        {
            var runner = Substitute.For<IQueryRunner>();

            // Dependencies is not a timestamp show type, so the timestamp-specific helper should
            // reject it as an unknown type rather than attempting to build a tree.
            var handled = ResultsTargetGrid.TryBuildTimestampShowTab(runner, ShowType.Dependencies, out var descriptor);

            Assert.IsTrue(handled);
            Assert.IsNull(descriptor);
            runner.Received().OutputError(Arg.Is<string>(s => s.Contains("Unknown SHOW command type")));
        }

        [TestMethod]
        public void RunMetadataTimestampShow_WithoutConnection_DoesNotSetResultTabs()
        {
            var runner = Substitute.For<IQueryRunner>();

            ResultsTargetGrid.RunMetadataTimestampShow(runner, ShowType.LastUpdated);

            // The build fails (no connection) so no descriptor is produced and the results pane must
            // not be replaced with an empty tab set.
            runner.DidNotReceive().SetResultTabs(Arg.Any<IList<ResultTabDescriptor>>());
            runner.DidNotReceive().ActivateResults();
            runner.Received().OutputError(Arg.Any<string>());
        }
    }
}
