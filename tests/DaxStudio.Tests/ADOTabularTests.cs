using System;
using System.Data;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

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
        /*
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
        */

        [TestMethod]
        public void TestADOTabularCSDLVisitor()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\csdl.xml");
            var tabs = new ADOTabularTableCollection(c,m);
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(4, tabs.Count);
            Assert.AreEqual(8, tabs["Sales"].Columns.Count());
        }

        [TestMethod]
        public void TestADOTabularGetDatabaseID()
        {
            //ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            //MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            //ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\discover_xml_metadata.xml");
            //var tabs = new ADOTabularTableCollection(c, m);
            //v.GenerateTablesFromXmlReader(tabs, xr);
            var dbs = DiscoverXmlParser.Databases(xr);

            Assert.AreEqual("AdventureWorksID", dbs["AdventureWorks"]);
            //Assert.AreEqual(8, tabs["Sales"].Columns.Count());
        }

        [TestMethod]
        public void TestADOTabularGetServerMode()
        {
            //ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            //MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            //ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\discover_xml_metadata.xml");
            //var tabs = new ADOTabularTableCollection(c, m);
            //v.GenerateTablesFromXmlReader(tabs, xr);
            var props = DiscoverXmlParser.ServerProperties(xr);

            Assert.AreEqual("Tabular", props["ServerMode"]);
            //Assert.AreEqual(8, tabs["Sales"].Columns.Count());
        }

        [TestMethod]
        public void TestCSDLColumnTranslations()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Reseller"];
            var cmpyCol = cmpyTab.Columns["Reseller Name"];

            Assert.AreEqual("Reseller Cap", cmpyTab.Caption, "Table Name is translated");
            Assert.AreEqual("Reseller Name Cap", cmpyCol.Caption, "Column Name is translated");
            
        }


        [TestMethod]
        public void TestCSDLTablesWithSpaces()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Sales Quota"];
            

            Assert.AreEqual("Sales Quota", cmpyTab.Caption, "Table Name is translated");

        }

        [TestMethod]
        public void TestADOTabularCSDLVisitorHierarchies()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\AdvWrks.xml");
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            var tabs = new ADOTabularTableCollection(c,m);
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(15, tabs.Count);
            Assert.AreEqual(24, tabs["Sales Territory"].Columns.Count());
            Assert.AreEqual(1, tabs["Sales Territory"].Columns.Where((t) => t.ColumnType == ADOTabularColumnType.Hierarchy).Count());
            var h = (ADOTabularHierarchy) (tabs["Sales Territory"].Columns.Where((t) => t.ColumnType == ADOTabularColumnType.Hierarchy).First());
            Assert.AreEqual(3, h.Levels.Count);
            Assert.AreEqual("Group", h.Levels[0].LevelName);
            Assert.AreEqual("Country", h.Levels[1].LevelName);
            Assert.AreEqual("Region", h.Levels[2].LevelName);
        }

        [TestMethod]
        public void TestADOTabularCSDLVisitorKPI()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader("..\\..\\AdvWrks.xml");
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            var tabs = new ADOTabularTableCollection(c, m);
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(15, tabs.Count);
            Assert.AreEqual(24, tabs["Sales Territory"].Columns.Count());
            Assert.AreEqual(1, tabs["Sales Territory"].Columns.Where((t) => t.ColumnType == ADOTabularColumnType.Hierarchy).Count());
            var k = tabs["Sales Territory"].Columns["Total Current Quarter Sales Performance"] as ADOTabularKpi;
            Assert.AreEqual("Total Current Quarter Sales Performance", k.Caption);
            Assert.AreEqual("_Total Current Quarter Sales Performance Goal", k.Goal.Caption);
            Assert.AreEqual("_Total Current Quarter Sales Performance Status", k.Status.Caption);
        }

        [TestMethod]
        public void TestDatabaseParser()
        {
            ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            var dd = c.Databases.GetDatabaseDictionary();
            Assert.AreEqual(4, dd.Count, "has 2 databases");
        }

    }
}
