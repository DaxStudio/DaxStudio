using System;
using System.Data;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace DaxStudio.Tests
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ADOTabularTests
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestMethod1()
        {
            var conn =
                new ADOTabularConnection("Data Source=localhost;Initial Catalog=AdventureWorks Tabular Model SQL 2012",
                                                    AdomdType.AnalysisServices, ADOTabularMetadataDiscovery.Csdl);

            Assert.IsTrue(conn.Database.Models[0].Tables.Any(), "No tables found");
            conn.Close();
        }

        [TestMethod]
        public void FindAllDateColumns()
        {
            var conn =
                   new ADOTabularConnection("Data Source=localhost;Initial Catalog=AdventureWorks Tabular Model SQL 2012",
                                                       AdomdType.AnalysisServices, ADOTabularMetadataDiscovery.Csdl);
            DataTable final = null;
            foreach (var t in conn.Database.Models.First().Tables)
            {
                foreach (var c in t.Columns)
                {
                    if (c.DataType == typeof(DateTime))
                    {
                        var qry = string.Format("evaluate row(\"TableName\", \"{0}\",\"ColumnName\",\"{1}\", \"MaxDate\", max({2}))",c.Table.Caption,c.Caption ,c.DaxName);
                        var dt = conn.ExecuteDaxQueryDataTable(qry);
                        if (final == null)
                            final = dt.Clone();
                        else
                        {
                            final.Merge(dt);
                        }
                    }
                }
            }
            Assert.AreEqual(0,final.Rows.Count,"incorrect row count");
        }
    }
}
