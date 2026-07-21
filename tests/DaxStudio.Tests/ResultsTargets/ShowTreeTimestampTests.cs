using System;
using System.Collections.Generic;
using System.Linq;
using DaxStudio.Core.Connections;
using DaxStudio.Core.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ResultsTargets
{
    /// <summary>
    /// Unit tests for the pure tree-shaping helpers behind SHOW LAST_UPDATED / MAX_UPDATED
    /// (rollup of Max Update / Days Since Change and the MAX_UPDATED prune).
    /// </summary>
    [TestClass]
    public class ShowTreeTimestampTests
    {
        private static readonly DateTime Now = new DateTime(2026, 01, 20, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day1 = Now.AddDays(-19);
        private static readonly DateTime Day2 = Now.AddDays(-18);
        private static readonly DateTime Day3 = Now.AddDays(-17);
        private static readonly DateTime Day5 = Now.AddDays(-5);

        // Builds:  Semantic model
        //          └ Tables
        //             └ Sales (Day1)
        //                ├ Columns { A(Day5), B(Day2) }
        //                └ Measures { M1(Day3) }
        private static ShowTreeNode BuildSampleTree()
        {
            var colA = new ShowTreeNode("A", "COLUMN", "Sales", Day5);
            var colB = new ShowTreeNode("B", "COLUMN", "Sales", Day2);
            var m1 = new ShowTreeNode("M1", "MEASURE", "Sales", Day3);

            var table = new ShowTreeNode("Sales", "TABLE", null, Day1);
            table.Children.Add(ConnectionManager.MakeFolder("Columns", new List<ShowTreeNode> { colA, colB }));
            table.Children.Add(ConnectionManager.MakeFolder("Measures", new List<ShowTreeNode> { m1 }));

            var tablesFolder = new ShowTreeNode("Tables", string.Empty, null, null, isFolder: true);
            tablesFolder.Children.Add(table);

            var root = new ShowTreeNode("Semantic model", "MODEL", null, null);
            root.Children.Add(tablesFolder);
            return root;
        }

        [TestMethod]
        public void PreferStructure_UsesStructureWhenPresent()
        {
            Assert.AreEqual(Day2, ConnectionManager.PreferStructure(Day5, Day2), "StructureModifiedTime should win even when older");
            Assert.AreEqual(Day5, ConnectionManager.PreferStructure(Day5, null), "Falls back to ModifiedTime when no StructureModifiedTime");
            Assert.IsNull(ConnectionManager.PreferStructure(null, null));
        }

        [TestMethod]
        public void MakeFolder_SortsChildrenAndFlagsFolder()
        {
            var folder = ConnectionManager.MakeFolder("Columns", new List<ShowTreeNode>
            {
                new ShowTreeNode("Zeta", "COLUMN", "Sales", Day1),
                new ShowTreeNode("Alpha", "COLUMN", "Sales", Day1),
            });

            Assert.IsTrue(folder.IsFolder);
            Assert.AreEqual("Columns (2)", folder.DisplayName);
            Assert.AreEqual("Alpha", folder.Children[0].Name);
            Assert.AreEqual("Zeta", folder.Children[1].Name);
        }

        [TestMethod]
        public void ComputeRollups_RollsUpMaxUpdateAndDaysSinceChange()
        {
            var root = BuildSampleTree();

            ConnectionManager.ComputeRollups(root, Now);

            // Root has no own timestamp but rolls up to the newest descendant (Day5)
            Assert.AreEqual(Day5, root.MaxUpdateUtc);
            Assert.AreEqual(5, root.DaysSinceChange);

            var table = root.Children[0].Children[0];
            Assert.AreEqual("Sales", table.Name);
            Assert.AreEqual(Day5, table.MaxUpdateUtc, "Table rolls up its newest column even though its own timestamp is older");
            Assert.AreEqual(5, table.DaysSinceChange);

            var colA = table.Children.Single(c => c.Name == "Columns").Children.Single(c => c.Name == "A");
            Assert.AreEqual(Day5, colA.MaxUpdateUtc, "A carries the newest change in its Columns folder, so its own date is surfaced");
            Assert.AreEqual(5, colA.DaysSinceChange, "Days Since Change falls back to the leaf's own timestamp");

            var colB = table.Children.Single(c => c.Name == "Columns").Children.Single(c => c.Name == "B");
            Assert.IsNull(colB.MaxUpdateUtc, "B is not the newest column, so its Max Update stays blank");

            var m1 = table.Children.Single(c => c.Name == "Measures").Children.Single(c => c.Name == "M1");
            Assert.AreEqual(Day3, m1.MaxUpdateUtc, "M1 is the sole (hence newest) measure, so its own date is surfaced");
        }

        [TestMethod]
        public void ComputeRollups_SurfacesContainerMaxOnLeafItems()
        {
            // Columns folder with a tie for the newest change (A and C both Day5, B older).
            var colA = new ShowTreeNode("A", "COLUMN", "Sales", Day5);
            var colB = new ShowTreeNode("B", "COLUMN", "Sales", Day2);
            var colC = new ShowTreeNode("C", "COLUMN", "Sales", Day5);

            var table = new ShowTreeNode("Sales", "TABLE", null, Day1);
            table.Children.Add(ConnectionManager.MakeFolder("Columns", new List<ShowTreeNode> { colA, colB, colC }));

            var tablesFolder = new ShowTreeNode("Tables", string.Empty, null, null, isFolder: true);
            tablesFolder.Children.Add(table);
            var root = new ShowTreeNode("Semantic model", "MODEL", null, null);
            root.Children.Add(tablesFolder);

            ConnectionManager.ComputeRollups(root, Now);

            var columnsFolder = table.Children.Single(c => c.Name == "Columns");
            Assert.AreEqual(Day5, colA.MaxUpdateUtc, "Tied newest column shows its date");
            Assert.AreEqual(Day5, colC.MaxUpdateUtc, "All ties for the container max show their date");
            Assert.IsNull(colB.MaxUpdateUtc, "Older column stays blank");
            Assert.AreEqual(Day5, columnsFolder.MaxUpdateUtc, "Folder keeps its descendant rollup");
            Assert.AreEqual(Day5, table.MaxUpdateUtc, "Table keeps its descendant rollup");
        }

        [TestMethod]
        public void PruneToMax_KeepsOnlyMaxObjectAndAncestors()
        {
            var root = BuildSampleTree();
            ConnectionManager.ComputeRollups(root, Now);

            DateTime? max = null;
            ConnectionManager.CollectMaxObjectTimestamp(root, ref max);
            Assert.AreEqual(Day5, max);

            var keep = ConnectionManager.PruneToMax(root, max.Value);
            Assert.IsTrue(keep);

            var tablesFolder = root.Children.Single();
            Assert.AreEqual("Tables (1)", tablesFolder.DisplayName);

            var table = tablesFolder.Children.Single();
            Assert.AreEqual("Sales", table.Name);

            // Measures folder (only M1 @ Day3) is pruned away; Columns keeps only A @ Day5.
            Assert.AreEqual(1, table.Children.Count);
            var columns = table.Children.Single();
            Assert.AreEqual("Columns (1)", columns.DisplayName, "Folder count reflects the pruned children");
            Assert.AreEqual("A", columns.Children.Single().Name);
        }

        [TestMethod]
        public void IsRealObject_ExcludesFoldersAndModelRoot()
        {
            Assert.IsFalse(ConnectionManager.IsRealObject(new ShowTreeNode("Semantic model", "MODEL")));
            Assert.IsFalse(ConnectionManager.IsRealObject(new ShowTreeNode("Measures", string.Empty, null, null, isFolder: true)));
            Assert.IsTrue(ConnectionManager.IsRealObject(new ShowTreeNode("Sales", "TABLE", null, Day1)));
            Assert.IsTrue(ConnectionManager.IsRealObject(new ShowTreeNode("A", "COLUMN", "Sales", Day5)));
        }
    }
}
