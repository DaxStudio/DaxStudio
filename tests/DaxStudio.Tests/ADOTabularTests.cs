using System;
using System.Data;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using DaxStudio.UI.Extensions;

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
        private static string ConnectionString { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext) {
            ConnectionString = @"Data Source=localhost\tab12;";
            //ConnectionString = @"Data Source=.\sql2014tb";
        }
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
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test","Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl.xml");
            var tabs = new ADOTabularTableCollection(c,m);
            
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(4, tabs.Count);
            Assert.AreEqual(8, tabs["Sales"].Columns.Count());
            Assert.AreEqual(0, tabs["Sales"].Columns[2].DistinctValueCount);
        }

        [TestMethod]
        public void TestADOTabularLargeCSDLVisitor()
        {
            ADOTabularConnection connection = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL visitor = new MetaDataVisitorCSDL(connection);
            ADOTabularModel model = new ADOTabularModel(connection, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\mtm_csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, model);

            visitor.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(110, tabs.Count);
            Assert.AreEqual(190, tabs["Orders"].Columns.Where(c => c.ColumnType == ADOTabularColumnType.Column).Count());
            Assert.AreEqual(347, tabs["Orders"].Columns.Where(c => c.ColumnType == ADOTabularColumnType.Measure).Count());
        }

        [TestMethod]
        public void TestPowerBICSDLVisitor()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(13, tabs.Count, "Wrong number of tables in database");
            Assert.AreEqual(2, tabs["ProductCategory"].Columns.Count(), "Wrong Column Count in ProductCategory");
            Assert.AreEqual(8, tabs["ProductCategory"].Columns["ProductCategory"].DistinctValueCount);
        }

        [TestMethod]
        public void TestADOTabularGetDatabaseID()
        {
            //ADOTabularConnection c = new ADOTabularConnection("Data Source=localhost", AdomdType.AnalysisServices);
            //MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            //ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\discover_xml_metadata.xml");
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
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\discover_xml_metadata.xml");
            //var tabs = new ADOTabularTableCollection(c, m);
            //v.GenerateTablesFromXmlReader(tabs, xr);
            var props = DiscoverXmlParser.ServerProperties(xr);

            Assert.AreEqual("Tabular", props["ServerMode"]);
            //Assert.AreEqual(8, tabs["Sales"].Columns.Count());
        }

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestCSDLColumnTranslations()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test","Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);
            
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Reseller"];
            var cmpyCol = cmpyTab.Columns["Reseller Name"];

            Assert.AreEqual("Reseller Cap", cmpyTab.Caption, "Table Name is translated");
            Assert.AreEqual("Reseller Name Cap", cmpyCol.Caption, "Column Name is translated");
            
        }

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestCSDLTablesWithSpaces()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test","Test Caption", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Sales Quota"];
            

            Assert.AreEqual("Sales Quota", cmpyTab.Caption, "Table Name is translated");

        }

        [TestMethod]
        public void TestInvalidCSDLKPIs()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString + ";Initial Catalog=AW Internet Sales Tabular Model 2014", AdomdType.AnalysisServices);
            c.Open();
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Caption", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\aw_internetsales_2014_csdl.xml");
            var tabs = new ADOTabularTableCollection(c, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Internet Sales"];


            Assert.AreEqual("Internet Sales", cmpyTab.Caption, "Table Name is translated");

        }

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestADOTabularCSDLVisitorHierarchies()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrks.xml");
            ADOTabularModel m = new ADOTabularModel(c, "Test","Test", "Test Description", "");
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

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestADOTabularCSDLVisitorKPI()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(c);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrks.xml");
            ADOTabularModel m = new ADOTabularModel(c, "Test", "Test Caption","Test Description", "");
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
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var dd = c.Databases.GetDatabaseDictionary(c.SPID);
            Assert.AreEqual(c.Databases.Count, dd.Count, "has 2 databases");
        }


        [TestMethod]
        public void TestADOTabularCSDLVisitorMeasures()
        {
            var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);            
            var v = new MetaDataVisitorCSDL(c);
            var m = new ADOTabularModel(c, "AdventureWorks", "AdventureWorks", "Test AdventureWorks", "");            
            var tabs = new ADOTabularTableCollection(c, m);

            foreach (var table in tabs)
            {
                var measures = table.Measures;


            }
        }

        [TestMethod]
        public void TestADOTabularCSDLVisitorKeywords()
        {
            var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var v = new MetaDataVisitorCSDL(c);

            var kw = c.Keywords;

            Assert.AreEqual(true, kw.Count > 5, "More than 5 keywords found");

        }


        [TestMethod]
        public void TestColumnRenaming()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var dt = new DataTable();
            dt.Columns.Add("table1[Column1]");
            dt.Columns.Add("table2[Column1]");
            dt.Columns.Add("table1[Column2]");
            dt.Columns.Add("table2[Column3]");
            dt.Columns.Add("table2[Column 4]");
            dt.Columns.Add("table2[Column, 5]");
            dt.Columns.Add("[[Measures] (test)]");
            dt.FixColumnNaming( "evaluate 'blah'");
            Assert.AreEqual("table1[Column1]", dt.Columns[0].ColumnName );
            Assert.AreEqual("table2[Column1]",dt.Columns[1].ColumnName );
            Assert.AreEqual("Column2", dt.Columns[2].ColumnName);
            Assert.AreEqual("Column3", dt.Columns[3].ColumnName);
            Assert.AreEqual("Column`4", dt.Columns[4].ColumnName,"spaces must be replaced with backticks");
            Assert.AreEqual("Column``5", dt.Columns[5].ColumnName, "commas must be replaced with backticks");
            Assert.AreEqual("[Measures] (test)", dt.Columns[6].Caption);
        }

        [TestMethod]
        public void TestMDXColumnRenaming()
        {
            ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var dt = new DataTable();
            dt.Columns.Add("[blah].[blah]");
            dt.Columns.Add("[Measures].[Test]");
            dt.FixColumnNaming( "SELECT [blah].[blah].[blah] on 0 from [Cube]");
            Assert.AreEqual("[blah].[blah]", dt.Columns[0].ColumnName);
            Assert.AreEqual("Test", dt.Columns[1].ColumnName);
            
        }
    }
}
