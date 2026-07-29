using System;
using System.Collections.Generic;
using System.Data;
using Caliburn.Micro;
using DaxStudio.Core.Model;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Verifies that the SHOW tree output on the <see cref="QueryResultsPaneViewModel"/> survives a
    /// GetJson/LoadJson round-trip (the mechanism used to persist SHOW results into the .daxx package
    /// via ISaveState).
    /// </summary>
    [TestClass]
    public class QueryResultsPaneSaveStateTests
    {
        [TestInitialize]
        public void Init()
        {
            // DisplayShowTree (and the BindableCollection it mutates) route their work through
            // Caliburn.Micro.Execute.OnUIThread / PlatformProvider.Current. In a headless unit-test
            // there is no WPF Dispatcher, so we force the default platform provider which runs the
            // supplied actions synchronously and inline on the calling thread.
            PlatformProvider.Current = new DefaultPlatformProvider();
        }

        private static QueryResultsPaneViewModel BuildViewModel()
        {
            var eventAggregator = Substitute.For<IEventAggregator>();
            var host = Substitute.For<IDaxStudioHost>();
            var options = Substitute.For<IGlobalOptions>();
            options.ResultFontSizePx.Returns(12d);
            return new QueryResultsPaneViewModel(eventAggregator, host, options);
        }

        [TestMethod]
        public void DependenciesTreeRoundTripsThroughJson()
        {
            // Arrange - a TABLE root with a MEASURE child that carries an expression
            var column = new ShowTreeNode("Sales Amount", "MEASURE", "Sales") { Expression = "SUM ( Sales[Amount] )" };
            var table = new ShowTreeNode("Sales", "TABLE");
            table.Children.Add(column);

            var source = BuildViewModel();
            source.AddShowTreeTab(new List<ShowTreeNode> { table }, ShowType.Dependencies);

            // Act - serialize then deserialize into a fresh view-model
            var json = source.GetJson();
            Assert.IsFalse(string.IsNullOrWhiteSpace(json), "GetJson should produce content when a SHOW tab is present");

            var target = BuildViewModel();
            target.LoadJson(json);

            // Assert - structure matches the original
            Assert.AreEqual(1, target.ResultTabs.Count, "Loading should recreate a single SHOW tab");
            var loadedTab = target.ResultTabs[0] as ShowTreeResultTab;
            Assert.IsNotNull(loadedTab, "The loaded tab should be a SHOW tree tab");
            Assert.AreEqual(1, loadedTab.ShowTreeRoots.Count, "Should have a single root node");

            var loadedTable = loadedTab.ShowTreeRoots[0];
            Assert.AreEqual("Sales", loadedTable.Name);
            Assert.AreEqual("TABLE", loadedTable.ObjectType);
            Assert.IsNull(loadedTable.TableName);
            Assert.AreEqual(1, loadedTable.Children.Count, "The table should have a single child column");

            var loadedColumn = loadedTable.Children[0];
            Assert.AreEqual("Sales Amount", loadedColumn.Name);
            Assert.AreEqual("MEASURE", loadedColumn.ObjectType);
            Assert.AreEqual("Sales", loadedColumn.TableName);
            Assert.AreEqual("SUM ( Sales[Amount] )", loadedColumn.Expression, "The Expression should round-trip through JSON");

            // Dependencies => Expression column shown, no timestamp column
            Assert.IsTrue(loadedTab.ShowTreeExpressionColumn, "Dependencies should show the Expression column");

            // Dependencies => no timestamp column, "Dependencies" title
            Assert.IsFalse(loadedTab.ShowTreeTimestampColumn, "Dependencies should not show the timestamp column");
            Assert.AreEqual("Dependencies", loadedTab.Title);

            var sourceTab = source.ResultTabs[0] as ShowTreeResultTab;
            Assert.AreEqual(sourceTab.ShowTreeTimestampColumn, loadedTab.ShowTreeTimestampColumn);
            Assert.AreEqual(sourceTab.Title, loadedTab.Title);
        }

        [TestMethod]
        public void LastUpdatedTreeRoundTripsTimestampAndTitle()
        {
            // Arrange - a TABLE root carrying a last-modified timestamp
            var lastModified = new DateTime(2024, 3, 14, 9, 26, 53, DateTimeKind.Utc);
            var table = new ShowTreeNode("Product", "TABLE", null, lastModified);
            var partition = new ShowTreeNode("Product-2024", "PARTITION", "Product", lastModified);
            table.Children.Add(partition);

            var source = BuildViewModel();
            source.AddShowTreeTab(new List<ShowTreeNode> { table }, ShowType.LastUpdated);

            // Act
            var json = source.GetJson();
            var target = BuildViewModel();
            target.LoadJson(json);

            // Assert - structure
            Assert.AreEqual(1, target.ResultTabs.Count);
            var loadedTab = target.ResultTabs[0] as ShowTreeResultTab;
            Assert.IsNotNull(loadedTab);
            Assert.AreEqual(1, loadedTab.ShowTreeRoots.Count);

            var loadedTable = loadedTab.ShowTreeRoots[0];
            Assert.AreEqual("Product", loadedTable.Name);
            Assert.AreEqual("TABLE", loadedTable.ObjectType);
            Assert.AreEqual(1, loadedTable.Children.Count);
            Assert.IsTrue(loadedTable.LastModifiedUtc.HasValue, "The last-modified timestamp should round-trip");
            Assert.AreEqual(lastModified, loadedTable.LastModifiedUtc.Value.ToUniversalTime());

            var loadedPartition = loadedTable.Children[0];
            Assert.AreEqual("Product-2024", loadedPartition.Name);
            Assert.AreEqual("PARTITION", loadedPartition.ObjectType);
            Assert.AreEqual("Product", loadedPartition.TableName);
            Assert.IsTrue(loadedPartition.LastModifiedUtc.HasValue);
            Assert.AreEqual(lastModified, loadedPartition.LastModifiedUtc.Value.ToUniversalTime());

            // LastUpdated => timestamp column visible with the "Last Updated" title
            Assert.IsTrue(loadedTab.ShowTreeTimestampColumn, "LastUpdated should show the timestamp column");
            Assert.AreEqual("Last Updated", loadedTab.Title);
        }

        [TestMethod]
        public void MultipleShowTabsRoundTripPreservingIndices()
        {
            // Arrange - two SHOW tabs interspersed with data tabs (data tabs are never saved)
            var depRoot = new ShowTreeNode("Sales", "TABLE");
            var updRoot = new ShowTreeNode("Product", "TABLE", null, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            var source = BuildViewModel();
            var tabs = new List<DaxStudio.Core.Model.ResultTabDescriptor>
            {
                DaxStudio.Core.Model.ResultTabDescriptor.ForTable(new DataTable("1")),                 // index 0
                DaxStudio.Core.Model.ResultTabDescriptor.ForShowTree(new List<ShowTreeNode> { depRoot }, ShowType.Dependencies), // index 1
                DaxStudio.Core.Model.ResultTabDescriptor.ForTable(new DataTable("2")),                 // index 2
                DaxStudio.Core.Model.ResultTabDescriptor.ForShowTree(new List<ShowTreeNode> { updRoot }, ShowType.MaxUpdated),   // index 3
            };
            source.SetResultTabs(tabs);
            Assert.AreEqual(4, source.ResultTabs.Count, "All four tabs should be present in the source");

            // Act
            var json = source.GetJson();
            var target = BuildViewModel();
            target.LoadJson(json);

            // Assert - only the two SHOW tabs are persisted, in their saved relative order
            Assert.AreEqual(2, target.ResultTabs.Count, "Only the SHOW tabs are persisted (data grids are never saved)");
            var first = target.ResultTabs[0] as ShowTreeResultTab;
            var second = target.ResultTabs[1] as ShowTreeResultTab;
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(ShowType.Dependencies, first.ShowType, "The lower-indexed SHOW tab should load first");
            Assert.AreEqual(ShowType.MaxUpdated, second.ShowType, "The higher-indexed SHOW tab should load second");
        }

        [TestMethod]
        public void InterspersedTabsPreserveExecutionOrder()
        {
            // query, SHOW, query => 3 tabs in that order
            var source = BuildViewModel();
            var tabs = new List<DaxStudio.Core.Model.ResultTabDescriptor>
            {
                DaxStudio.Core.Model.ResultTabDescriptor.ForTable(new DataTable("1")),
                DaxStudio.Core.Model.ResultTabDescriptor.ForShowTree(new List<ShowTreeNode> { new ShowTreeNode("Sales", "TABLE") }, ShowType.Dependencies),
                DaxStudio.Core.Model.ResultTabDescriptor.ForTable(new DataTable("2")),
            };

            source.SetResultTabs(tabs);

            Assert.AreEqual(3, source.ResultTabs.Count);
            Assert.IsInstanceOfType(source.ResultTabs[0], typeof(DataTableResultTab));
            Assert.IsInstanceOfType(source.ResultTabs[1], typeof(ShowTreeResultTab));
            Assert.IsInstanceOfType(source.ResultTabs[2], typeof(DataTableResultTab));
        }

        [TestMethod]
        public void OldSingleObjectFormatIsLoadedForBackwardCompatibility()
        {
            // The original schema serialized a single { Roots, ShowType } object (not an array).
            var legacyJson =
                "{ \"Roots\": [ { \"Name\": \"Sales\", \"ObjectType\": \"TABLE\", \"Children\": [] } ], \"ShowType\": 0 }";

            var target = BuildViewModel();
            target.LoadJson(legacyJson);

            Assert.AreEqual(1, target.ResultTabs.Count, "The legacy single-object schema should load as one SHOW tab");
            var tab = target.ResultTabs[0] as ShowTreeResultTab;
            Assert.IsNotNull(tab);
            Assert.AreEqual(1, tab.ShowTreeRoots.Count);
            Assert.AreEqual("Sales", tab.ShowTreeRoots[0].Name);
            Assert.AreEqual(ShowType.Dependencies, tab.ShowType);
        }

        [TestMethod]
        public void LoadJsonWithEmptyContentIsIgnored()
        {
            var target = BuildViewModel();

            target.LoadJson(null);
            target.LoadJson(string.Empty);
            target.LoadJson("   ");

            Assert.AreEqual(0, target.ResultTabs.Count, "Empty/blank JSON should not create any tabs");
        }
    }
}
