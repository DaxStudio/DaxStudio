using System;
using System.Collections.Generic;
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
            // Arrange - a TABLE root with a COLUMN child
            var column = new ShowTreeNode("Sales Amount", "COLUMN", "Sales");
            var table = new ShowTreeNode("Sales", "TABLE");
            table.Children.Add(column);

            var source = BuildViewModel();
            source.DisplayShowTree(new List<ShowTreeNode> { table }, ShowType.Dependencies);

            // Act - serialize then deserialize into a fresh view-model
            var json = source.GetJson();
            Assert.IsFalse(string.IsNullOrWhiteSpace(json), "GetJson should produce content when a tree is displayed");

            var target = BuildViewModel();
            target.LoadJson(json);

            // Assert - structure matches the original
            Assert.IsTrue(target.IsShowTreeVisible, "Loading a tree should make it visible");
            Assert.AreEqual(1, target.ShowTreeRoots.Count, "Should have a single root node");

            var loadedTable = target.ShowTreeRoots[0];
            Assert.AreEqual("Sales", loadedTable.Name);
            Assert.AreEqual("TABLE", loadedTable.ObjectType);
            Assert.IsNull(loadedTable.TableName);
            Assert.AreEqual(1, loadedTable.Children.Count, "The table should have a single child column");

            var loadedColumn = loadedTable.Children[0];
            Assert.AreEqual("Sales Amount", loadedColumn.Name);
            Assert.AreEqual("COLUMN", loadedColumn.ObjectType);
            Assert.AreEqual("Sales", loadedColumn.TableName);

            // Dependencies => no timestamp column, "Dependencies" title
            Assert.IsFalse(target.ShowTreeTimestampColumn, "Dependencies should not show the timestamp column");
            Assert.AreEqual("Dependencies", target.ShowTreeTitle);
            Assert.AreEqual(source.ShowTreeTimestampColumn, target.ShowTreeTimestampColumn);
            Assert.AreEqual(source.ShowTreeTitle, target.ShowTreeTitle);
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
            source.DisplayShowTree(new List<ShowTreeNode> { table }, ShowType.LastUpdated);

            // Act
            var json = source.GetJson();
            var target = BuildViewModel();
            target.LoadJson(json);

            // Assert - structure
            Assert.IsTrue(target.IsShowTreeVisible);
            Assert.AreEqual(1, target.ShowTreeRoots.Count);

            var loadedTable = target.ShowTreeRoots[0];
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
            Assert.IsTrue(target.ShowTreeTimestampColumn, "LastUpdated should show the timestamp column");
            Assert.AreEqual("Last Updated", target.ShowTreeTitle);
            Assert.AreEqual(source.ShowTreeTimestampColumn, target.ShowTreeTimestampColumn);
            Assert.AreEqual(source.ShowTreeTitle, target.ShowTreeTitle);
        }

        [TestMethod]
        public void LoadJsonWithEmptyContentIsIgnored()
        {
            var target = BuildViewModel();

            target.LoadJson(null);
            target.LoadJson(string.Empty);
            target.LoadJson("   ");

            Assert.IsFalse(target.IsShowTreeVisible, "Empty/blank JSON should not show a tree");
            Assert.AreEqual(0, target.ShowTreeRoots.Count);
        }
    }
}
