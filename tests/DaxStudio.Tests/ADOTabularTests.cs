using System;
using System.Data;
using System.Linq;
using ADOTabular;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.UI.Extensions;
using Moq;
using DaxStudio.Tests.Utils;
using MeasureMD = DaxStudio.Tests.Utils.MeasureMD;
using MeasureTM = DaxStudio.Tests.Utils.MeasureTM;
using System.Collections.Generic;
using DaxStudio.UI.Model;
using Caliburn.Micro;
using ADOTabular.Interfaces;
using ADOTabular.AdomdClientWrappers;

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
        //private static string ConnectionString { get; set; }
        private IADOTabularConnection connection;
        private static DataSet keywordDataSet;
        private static DataSet functionDataSet;
        private static DataSet dmvDataSet;
        private static DataSet measureDataSet_MD;
        private static DataSet measureDataSet_TM;
        private static DataSet tablesDataSet;
        private static DataSet measureDataSetEmpty;
        private static DataSet cubesDataSet;
        private static ADOTabularDatabase mockDatabase;
        //private Dictionary<string, ADOTabularColumn> columnCollection;

        private bool IsResellerSalesMeasureGroup(AdomdRestrictionCollection res)
        {
            foreach (AdomdRestriction r in res)
            {
                if (r.Name == "MEASUREGROUP_NAME" && r.Value.ToString() == "Reseller Sales")
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsResellerSalesTable(AdomdRestrictionCollection res)
        {
            foreach (AdomdRestriction r in res)
            {
                if (r.Name == "TableID" && r.Value.ToString() == "1")
                {
                    return true;
                }
            }
            return false;
        }

        private ADOTabularDatabase GetTestDB()
        {
            return new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext) {
            //ConnectionString = @"Data Source=localhost\tab17;";
            //ConnectionString = @"Data Source=.\sql2014tb";
            keywordDataSet = new DataSet();
            keywordDataSet.Tables.Add(
                DmvHelpers.ListToTable( new List<Keyword> {
                    new Keyword { KEYWORD = "TABLE" },
                    new Keyword { KEYWORD = "EVALUATE"}
                })
            );

            functionDataSet = new DataSet();
            functionDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Function> {
                    new Function { FUNCTION_NAME = "FILTER", ORIGIN=4 },
                    new Function { FUNCTION_NAME = "CALCULATE", ORIGIN=3}
                })
            );

            dmvDataSet = new DataSet();
            dmvDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Dmv> {
                    new Dmv { SchemaName = "TMSCHEMA_MEASURES" },
                })
            );

            measureDataSet_MD = new DataSet();
            measureDataSet_MD.Tables.Add(
                DmvHelpers.ListToTable(new List<MeasureMD> {
                    new MeasureMD { MEASURE_NAME = "MyMeasure", MEASURE_CAPTION="MyMeasure",DESCRIPTION="My Description",EXPRESSION="1"} 
                })
            );

            measureDataSet_TM = new DataSet();
            measureDataSet_TM.Tables.Add(
                DmvHelpers.ListToTable(new List<MeasureTM> {
                    new MeasureTM { TableID = 1, Name = "MyMeasure", Description="My Description",Expression="1"}
                })
            );

            tablesDataSet= new DataSet();
            tablesDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<TableTM> {
                    new TableTM { ID = 1, Name = "Reseller Sales"}
                })
            );

            measureDataSetEmpty = new DataSet();
            measureDataSetEmpty.Tables.Add( DmvHelpers.ListToTable(new List<MeasureMD>() ) );

            cubesDataSet = new DataSet();
            cubesDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Cube>{
                    new Cube {CUBE_NAME = "Adventure Works", CUBE_CAPTION="Adventure Works", BASE_CUBE_NAME="", DESCRIPTION="Mock Cube"}
                })
            );

        }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void MyTestInitialize() {
            var mockConn = new Mock<IADOTabularConnection>();
            var columnCollection = new Dictionary<string, ADOTabularColumn>();
            
            mockConn.SetupGet(x => x.Columns).Returns(columnCollection);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false)).Returns(keywordDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false)).Returns(functionDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS")).Returns(dmvDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_CUBES", It.IsAny<AdomdRestrictionCollection>())).Returns(cubesDataSet);
            mockConn.Setup(x => x.ShowHiddenObjects).Returns(true);
            var mockDb = new Mock<ADOTabularDatabase>(mockConn.Object, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*");
            mockDatabase = mockDb.Object;
            mockConn.SetupGet(x => x.Database).Returns(mockDatabase);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES", 
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res)), 
                false))
                .Returns(measureDataSet_MD);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res)),
                false))
                .Returns(measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "TMSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesTable(res))
                ))
                .Returns(measureDataSet_TM);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "TMSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesTable(res))
                ))
                .Returns(measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet("TMSCHEMA_TABLES", It.IsAny<AdomdRestrictionCollection>())).Returns(tablesDataSet);
            mockConn.Setup(x => x.ServerVersion).Returns("15.0.0");
            mockConn.SetupGet(x => x.Visitor).Returns(new MetaDataVisitorCSDL(mockConn.Object));

            mockConn.SetupGet(x => x.Keywords).Returns(new ADOTabularKeywordCollection(mockConn.Object));
            mockConn.SetupGet(x => x.AllFunctions).Returns(new List<string>());
            mockConn.SetupGet(x => x.DynamicManagementViews).Returns( new ADOTabularDynamicManagementViewCollection(mockConn.Object));
            mockConn.SetupGet(x => x.IsAdminConnection).Returns(true);
            connection = mockConn.Object;
        }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion


        
        [TestMethod]
        public void TestADOTabularCSDLVisitor()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test","Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);
            
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(4, tabs.Count);
            Assert.AreEqual(8, tabs["Sales"].Columns.Count()); // excludes internal rowcount column
            Assert.AreEqual(0, tabs["Sales"].Columns[2].DistinctValues);

            // Check TOM objects

            Assert.AreEqual(4, m.TOMModel.Tables.Count, "Count of tables in TOM Model");
            Assert.AreEqual(4, m.TOMModel.Tables["Sales"].Columns.Count, "Count of columns in TOM Model"); // includes external rowcount column
            Assert.AreEqual(5, m.TOMModel.Tables["Sales"].Measures.Count, "Count of measures in TOM Model");
        }

        [TestMethod]
        public void TestADOTabularCSDLVisitorMeasureDescriptions()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            var measure = tabs["Sales"].Columns["Sector Sales"];

            Assert.AreEqual("Sector Sales Description", measure.Description);
            
        }

        [TestMethod]
        public void TestADOTabularCSDLVisitTwice()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            using (System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl.xml"))
            {
                var tabs = new ADOTabularTableCollection(connection, m);

                v.GenerateTablesFromXmlReader(tabs, xr);

                Assert.AreEqual(4, tabs.Count);
                Assert.AreEqual(8, tabs["Sales"].Columns.Count());
                Assert.AreEqual(0, tabs["Sales"].Columns[2].DistinctValues);
            }

            m = new ADOTabularModel(connection,db, "Test2", "Test2", "Test2 Description", "");
            using (System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl.xml"))
            {
                var tabs = new ADOTabularTableCollection(connection, m);

                v.GenerateTablesFromXmlReader(tabs, xr);

                Assert.AreEqual(4, tabs.Count);
                Assert.AreEqual(8, tabs["Sales"].Columns.Count());
                Assert.AreEqual(0, tabs["Sales"].Columns[2].DistinctValues);
            }
        }

        [TestMethod]
        public void TestADOTabularLargeCSDLVisitor()
        {
            //ADOTabularConnection connection = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL visitor = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel model = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\mtm_csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, model);

            visitor.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(110, tabs.Count);
            Assert.AreEqual(190, tabs["Orders"].Columns.Where(c => c.ObjectType == ADOTabularObjectType.Column).Count());
            Assert.AreEqual(347, tabs["Orders"].Columns.Where(c => c.ObjectType == ADOTabularObjectType.Measure).Count());
        }

        [TestMethod]
        public void TestPowerBICSDLVisitor()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(13, tabs.Count, "Wrong number of tables in database");
            Assert.AreEqual(2, tabs["ProductCategory"].Columns.Count(), "Wrong Column Count in ProductCategory");
            Assert.AreEqual(8, tabs["ProductCategory"].Columns["ProductCategory"].DistinctValues);
        }

        [TestMethod]
        public void TestPowerBIVariationsVisitor()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var promoTable = tabs["Promotion"];
            Assert.IsNotNull(promoTable);
            Assert.AreEqual(0, promoTable.Columns["PromotionName"].Variations.Count);
            Assert.AreEqual(1, promoTable.Columns["StartDate"].Variations.Count);

        }

        [TestMethod]
        public void TestPowerBIOrderByVisitor()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var localDateTable = tabs["LocalDateTable_697ceb23-7c16-46b1-a1ed-0100727de4c7"];
            Assert.IsNotNull(localDateTable);
            Assert.AreEqual(localDateTable.Columns["MonthNo"], localDateTable.Columns["Month"].OrderBy);
            Assert.AreEqual(localDateTable.Columns["QuarterNo"], localDateTable.Columns["Quarter"].OrderBy);
            Assert.IsNull(localDateTable.Columns["QuarterNo"].OrderBy);
            Assert.IsNull( localDateTable.Columns["Year"].OrderBy);

        }

        [TestMethod]
        public void TestPowerBIDatabaseCulture()
        {
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            Assert.AreEqual(string.Empty, db.Culture);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual("en-US", db.Culture);
            
        }


        [TestMethod]
        public void TestPowerBIModelCapabilities()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(true, m.Capabilities.DAXFunctions.SubstituteWithIndex);
            Assert.AreEqual(true, m.Capabilities.DAXFunctions.SummarizeColumns);
            Assert.AreEqual(true, m.Capabilities.DAXFunctions.TreatAs);
            Assert.AreEqual(true, m.Capabilities.Variables);
            Assert.AreEqual(true, m.Capabilities.TableConstructor);
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

        [TestMethod]
        public void TestCSDLDisplayFolders()
        {
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrksFoldersCsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Internet Sales"];
            var cmpyCol = cmpyTab.Columns["Internet Current Quarter Sales"];

            var cmpyCol2 = cmpyTab.Columns["Internet Current Quarter Margin"];

            Assert.AreEqual("Internet Sales", cmpyTab.Caption, "Table Name is correct");
            Assert.AreEqual("QTD Folder", cmpyTab.FolderItems[0].Name);
            Assert.AreEqual(8, ((IADOTabularFolderReference)cmpyTab.FolderItems[0]).FolderItems.Count);

        }


        [TestMethod]
        public void TestCSDLHierarchInDisplayFolders()
        {

            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\HierInFolder.csdl");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var tabDate = tabs["Date"];
            //var cmpyCol = tabDate.FolderItems.Columns["Internet Current Quarter Sales"];

            //var cmpyCol2 = cmpyTab.Columns["Internet Current Quarter Margin"];

            Assert.AreEqual(1, tabDate.FolderItems.Count, "Table Name is correct");
            Assert.AreEqual("Calendar Folder", tabDate.FolderItems[0].Name);
            Assert.IsInstanceOfType(tabDate.Columns["Calendar"], typeof(ADOTabularHierarchy));
            Assert.AreEqual(true, tabDate.Columns["Calendar"].IsInDisplayFolder);
            //Assert.AreEqual(8, ((IADOTabularFolderReference)cmpyTab.FolderItems[0]).FolderItems.Count);

        }

        [TestMethod]
        public void TestCSDLNestedDisplayFolders()
        {

            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\NestedFoldersCsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Table1"];
            var cmpyCol = cmpyTab.Columns["Value"];

            /* test folder structure should be:
             
             Folder 1
               |- Measure_2
               +- Folder 2
                  +- Folder 3
                     +- Measure

             */

            Assert.AreEqual("Table1", cmpyTab.Caption, "Table Name is correct");
            Assert.AreEqual(1, cmpyTab.FolderItems.Count);

            var folder1 = ((IADOTabularFolderReference)cmpyTab.FolderItems[0]);
            Assert.AreEqual("Folder 1", folder1.Name);

            Assert.AreEqual(2, folder1.FolderItems.Count);
            Assert.AreEqual("Measure_2", folder1.FolderItems[0].InternalReference);
            var folder2 = ((IADOTabularFolderReference)folder1.FolderItems[1]);
            Assert.AreEqual("Folder 2", folder2.Name);

            var folder3 = ((IADOTabularFolderReference)folder2.FolderItems[0]);
            Assert.AreEqual("Folder 3", folder3.Name);
            Assert.AreEqual("Measure", folder3.FolderItems[0].InternalReference);

        }

        [TestMethod]
        public void TestCSDLNestedMultipleDisplayFolders()
        {

            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\NestedMultipleFoldersCsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Table1"];
            

            /* test folder structure should be:
             
             Folder 1
               |- Measure
             Folder 2
               +- Folder 3
                  +- Measure

             */

            Assert.AreEqual("Table1", cmpyTab.Caption, "Table Name is correct");
            Assert.AreEqual(2, cmpyTab.FolderItems.Count);

            var folder1 = ((IADOTabularFolderReference)cmpyTab.FolderItems[0]);
            Assert.AreEqual("Folder 1", folder1.Name);

            Assert.AreEqual(1, folder1.FolderItems.Count);
            Assert.AreEqual("Measure", folder1.FolderItems[0].InternalReference);
            var folder2 = ((IADOTabularFolderReference)cmpyTab.FolderItems[1]);
            Assert.AreEqual("Folder 2", folder2.Name);

            var folder3 = ((IADOTabularFolderReference)folder2.FolderItems[0]);
            Assert.AreEqual("Folder 3", folder3.Name);
            Assert.AreEqual("Measure", folder3.FolderItems[0].InternalReference);

        }


        // We need CSDL v2.5 (or higher) to get the full relationship detail. Prior to that version we do not 
        // have information on which columns are involved in the relationship
        [TestMethod, Ignore]
        public void TestCSDLRelationships()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\RelationshipCsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(6, tabs.Count, "Table count is correct");
            Assert.AreEqual(1, tabs["Customer"].Relationships.Count, "Customer Table has incorrect relationship count");
            Assert.AreEqual(0, tabs["Accounts"].Relationships.Count, "Accounts Table has incorrect relationship count");
            Assert.AreEqual(1, tabs["Customer Geography"].Relationships.Count, "Customer Geography Table has incorrect relationship count");
            Assert.AreEqual("Both", tabs["Customer Geography"].Relationships[0].CrossFilterDirection, "Customer Geography Table has a Both crossfilter relationship");
            Assert.AreEqual(1, tabs["Geography Population"].Relationships.Count, "Geography Table has incorrect relationship count");
            Assert.AreEqual(2, tabs["Customer Accounts"].Relationships.Count, "Customer Accounts Table has incorrect relationship count");
            Assert.AreEqual("Both", tabs["Customer Accounts"].Relationships[0].CrossFilterDirection, "Customer Accounts Table has a Both crossfilter on relationship 0");
            Assert.AreEqual("", tabs["Customer Accounts"].Relationships[1].CrossFilterDirection, "Customer Accounts Table does not have a Both crossfilter on relationship 1");

            var tabCust = tabs["Customer"];
            var relCustToCustGeog = tabCust.Relationships[0];

            //var col = tabCust.Columns.GetByPropertyRef("Customer_Geography_ID2");

            Assert.AreEqual("", relCustToCustGeog.CrossFilterDirection);
            Assert.AreEqual("Customer_Geography_ID2", relCustToCustGeog.FromColumn, "Incorrect from column");
            Assert.AreEqual("*", relCustToCustGeog.FromColumnMultiplicity);
            Assert.AreEqual("Customer_Geography_ID", relCustToCustGeog.ToColumn,"Incorrect to column");
            Assert.AreEqual("0..1", relCustToCustGeog.ToColumnMultiplicity );

        }


        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestCSDLColumnTranslations()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test","Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);
            
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
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test","Test Caption", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Sales Quota"];
            

            Assert.AreEqual("Sales Quota", cmpyTab.Caption, "Table Name is translated");

        }

        [TestMethod]
        public void TestCSDLMarkAsDateTable()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test Caption", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\advwrkscsdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Date"];


            Assert.AreEqual(true, cmpyTab.IsDateTable, "'Date' table is marked as date table");
            Assert.AreEqual(false, tabs["Customer"].IsDateTable, "'Date' table is marked as date table");

        }


        [Ignore,TestMethod]
        public void TestInvalidCSDLKPIs()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString + ";Initial Catalog=AW Internet Sales Tabular Model 2014", AdomdType.AnalysisServices);
            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            //c.Open();
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test Caption", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\aw_internetsales_2014_csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);
            var cmpyTab = tabs["Internet Sales"];


            Assert.AreEqual("Internet Sales", cmpyTab.Caption, "Table Name is translated");

        }

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestADOTabularCSDLVisitorHierarchies()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrks.xml");
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test","Test", "Test Description", "");
            var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(15, tabs.Count);
            Assert.AreEqual(24, tabs["Sales Territory"].Columns.Count());
            Assert.AreEqual(1, tabs["Sales Territory"].Columns.Where((t) => t.ObjectType == ADOTabularObjectType.Hierarchy).Count());
            var h = (ADOTabularHierarchy) (tabs["Sales Territory"].Columns.Where((t) => t.ObjectType == ADOTabularObjectType.Hierarchy).First());
            Assert.AreEqual(false, h.IsVisible);
            Assert.AreEqual(3, h.Levels.Count);
            Assert.AreEqual("Group", h.Levels[0].LevelName);
            Assert.AreEqual("Country", h.Levels[1].LevelName);
            Assert.AreEqual("Region", h.Levels[2].LevelName);
        }

        //TODO - need to fix the tests to mock out MDSCHEMA_HIERARCHIES
        [TestMethod]
        public void TestADOTabularCSDLVisitorKPI()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrks.xml");
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test Caption","Test Description", "");
            var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.AreEqual(15, tabs.Count);
            Assert.AreEqual(24, tabs["Sales Territory"].Columns.Count());
            Assert.AreEqual(1, tabs["Sales Territory"].Columns.Where((t) => t.ObjectType == ADOTabularObjectType.Hierarchy).Count());
            var k = tabs["Sales Territory"].Columns["Total Current Quarter Sales Performance"] as ADOTabularKpi;
            Assert.AreEqual("Total Current Quarter Sales Performance", k.Caption);
            Assert.AreEqual("_Total Current Quarter Sales Performance Goal", k.Goal.Caption);
            Assert.AreEqual("_Total Current Quarter Sales Performance Status", k.Status.Caption);
        }

        // todo - how to test parser without live connection...
        //[TestMethod, Ignore]
        //public void TestDatabaseParser()
        //{
        //    ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
        //    var dd = c.Databases.GetDatabaseDictionary(c.SPID);
        //    Assert.AreEqual(c.Databases.Count, dd.Count, "has 2 databases");
        //}


        [TestMethod]
        public void TestADOTabularCSDLVisitorMeasures()
        {
            //var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);            
            
            var v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            var m = new ADOTabularModel(connection,db, "AdventureWorks", "AdventureWorks", "Test AdventureWorks", "");            
            var tabs = new ADOTabularTableCollection(connection, m);

            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\AdvWrks.xml");
            //ADOTabularModel m = new ADOTabularModel(connection, "Test", "Test Caption", "Test Description", "");
            //var tabs = new ADOTabularTableCollection(connection, m);
            v.GenerateTablesFromXmlReader(tabs, xr);


            //foreach (var table in tabs)
            //{
            //    var measures = table.Measures;
            //}

            Assert.AreEqual(1, tabs["Reseller Sales"].Measures.Count,"There should be 1 measure populated by the mocks");
        }

        [TestMethod]
        public void TestADOTabularCSDLVisitorKeywords()
        {
            //var c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            
            var v = new MetaDataVisitorCSDL(connection);

            var kw = connection.Keywords;

            Assert.AreEqual(true, kw.Count == keywordDataSet.Tables[0].Rows.Count, "More than 5 keywords found");

        }


        [TestMethod]
        public void TestColumnRenaming()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
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
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var dt = new DataTable();
            dt.Columns.Add("[blah].[blah]");
            dt.Columns.Add("[Measures].[Test]");
            dt.FixColumnNaming( "SELECT [blah].[blah].[blah] on 0 from [Cube]");
            Assert.AreEqual("[blah].[blah]", dt.Columns[0].ColumnName);
            Assert.AreEqual("Test", dt.Columns[1].ColumnName);
            
        }

        [TestMethod]
        public void CreatePowerPivotConnection()
        {
            var mockEventAgg = new Mock<IEventAggregator>().Object;
            var ppvt = new ProxyPowerPivot(mockEventAgg, 9000);
            var cnn = ppvt.GetPowerPivotConnection("Application Name=Dax Studio Test", "");
            Assert.AreEqual("Data Source=http://localhost:9000/xmla;Application Name=Dax Studio Test;Show Hidden Cubes=true", cnn.ConnectionString);
        }

        [TestMethod]
        public void CreatePowerPivotConnectionWithFileName()
        {
            var mockEventAgg = new Mock<IEventAggregator>().Object;
            var ppvt = new ProxyPowerPivot(mockEventAgg, 9000);
            var cnn = ppvt.GetPowerPivotConnection("Application Name=Dax Studio Test", "Workstation ID=\"c:\\test folder\\blah's folder\\test's crazy ;=-` file.xlsx\";");
            Assert.AreEqual("Data Source=http://localhost:9000/xmla;Application Name=Dax Studio Test;Workstation ID=\"c:\\test folder\\blah's folder\\test's crazy ;=-` file.xlsx\";Show Hidden Cubes=true", cnn.ConnectionString);
        }
    }
}
