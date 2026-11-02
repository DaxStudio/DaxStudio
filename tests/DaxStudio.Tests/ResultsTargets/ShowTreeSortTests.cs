using System.Collections.Generic;
using System.Linq;
using DaxStudio.Core.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ResultsTargets
{
    /// <summary>
    /// Unit tests for the recursive Object-column sort behind the SHOW tree-grid
    /// (<see cref="ShowTreeResultTab.SortNodesRecursive"/>).
    /// </summary>
    [TestClass]
    public class ShowTreeSortTests
    {
        // Semantic model
        // └ Tables
        //    ├ Beta   { Columns { Zeb, Amy } }
        //    └ Alpha  { Columns { Yan, Bob } }
        private static ShowTreeNode BuildTree()
        {
            ShowTreeNode Table(string name, params string[] columns)
            {
                var cols = columns.Select(c => new ShowTreeNode(c, "COLUMN", name)).ToList();
                var columnsFolder = new ShowTreeNode("Columns", string.Empty, null, null, isFolder: true);
                foreach (var c in cols) columnsFolder.Children.Add(c);
                var table = new ShowTreeNode(name, "TABLE");
                table.Children.Add(columnsFolder);
                return table;
            }

            var tablesFolder = new ShowTreeNode("Tables", string.Empty, null, null, isFolder: true);
            tablesFolder.Children.Add(Table("Beta", "Zeb", "Amy"));
            tablesFolder.Children.Add(Table("Alpha", "Yan", "Bob"));

            var root = new ShowTreeNode("Semantic model", "MODEL");
            root.Children.Add(tablesFolder);
            return root;
        }

        private static List<string> ColumnNames(ShowTreeNode table)
            => table.Children.Single(f => f.Name == "Columns").Children.Select(c => c.Name).ToList();

        [TestMethod]
        public void SortNodesRecursive_Ascending_SortsEveryLevel()
        {
            var root = BuildTree();

            ShowTreeResultTab.SortNodesRecursive(new List<ShowTreeNode> { root }, descending: false);

            var tables = root.Children.Single(f => f.Name == "Tables").Children;
            CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, tables.Select(t => t.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Bob", "Yan" }, ColumnNames(tables[0]).ToArray(), "Alpha's columns sorted ascending");
            CollectionAssert.AreEqual(new[] { "Amy", "Zeb" }, ColumnNames(tables[1]).ToArray(), "Beta's columns sorted ascending");
        }

        [TestMethod]
        public void SortNodesRecursive_Descending_SortsEveryLevel()
        {
            var root = BuildTree();

            ShowTreeResultTab.SortNodesRecursive(new List<ShowTreeNode> { root }, descending: true);

            var tables = root.Children.Single(f => f.Name == "Tables").Children;
            CollectionAssert.AreEqual(new[] { "Beta", "Alpha" }, tables.Select(t => t.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "Zeb", "Amy" }, ColumnNames(tables[0]).ToArray(), "Beta's columns sorted descending");
            CollectionAssert.AreEqual(new[] { "Yan", "Bob" }, ColumnNames(tables[1]).ToArray(), "Alpha's columns sorted descending");
        }

        [TestMethod]
        public void SortNodesRecursive_PreservesHierarchyMembership()
        {
            var root = BuildTree();

            ShowTreeResultTab.SortNodesRecursive(new List<ShowTreeNode> { root }, descending: false);

            // Same set of tables and each table still owns exactly its two columns (nothing flattened / lost).
            var tables = root.Children.Single(f => f.Name == "Tables").Children;
            Assert.AreEqual(2, tables.Count);
            CollectionAssert.AreEquivalent(new[] { "Bob", "Yan" }, ColumnNames(tables.Single(t => t.Name == "Alpha")).ToArray());
            CollectionAssert.AreEquivalent(new[] { "Amy", "Zeb" }, ColumnNames(tables.Single(t => t.Name == "Beta")).ToArray());
        }

        [TestMethod]
        public void SortNodesRecursive_HandlesEmptyAndNull()
        {
            ShowTreeResultTab.SortNodesRecursive(null, false);
            ShowTreeResultTab.SortNodesRecursive(new List<ShowTreeNode>(), true);
        }
    }
}
