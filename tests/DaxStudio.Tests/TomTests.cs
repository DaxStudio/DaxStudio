using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Interfaces;
using DaxStudio.Tests.Utils;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TomTests
    {
        private static DataSet keywordDataSet;
        private static DataSet measureDataSetEmpty;
        private static IADOTabularConnection connection;
        private static ADOTabularDatabase mockDatabase;
        private static DataSet dmvDataSet;
        private static DataSet functionDataSet;
        private static DataSet measureDataSet_MD;
        private static DataSet measureDataSet_TM;
        private static DataSet tablesDataSet;
        private static DataSet cubesDataSet;

        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            //ConnectionString = @"Data Source=localhost\tab17;";
            //ConnectionString = @"Data Source=.\sql2014tb";
            keywordDataSet = new DataSet();
            keywordDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Keyword> {
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

            tablesDataSet = new DataSet();
            tablesDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<TableTM> {
                    new TableTM { ID = 1, Name = "Reseller Sales"}
                })
            );

            measureDataSetEmpty = new DataSet();
            measureDataSetEmpty.Tables.Add(DmvHelpers.ListToTable(new List<MeasureMD>()));

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
        public void MyTestInitialize()
        {
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
            mockConn.SetupGet(x => x.DynamicManagementViews).Returns(new ADOTabularDynamicManagementViewCollection(mockConn.Object));
            mockConn.SetupGet(x => x.IsAdminConnection).Returns(true);
            connection = mockConn.Object;
        }


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


        [TestMethod]
        public void TestPowerBITomModel_CSDL_2_0()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\powerbi-csdl.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);
            
            Assert.IsNotNull(m.TOMModel);
            Assert.AreEqual(13, m.TOMModel.Tables.Count,"Table Counts are equal");
            Assert.AreEqual(tabs["ProductCategory"].Columns.Count , m.TOMModel.Tables["ProductCategory"].Columns.Count,"ProductCategory column counts are equal");
            Assert.AreEqual(tabs["Sales"].Relationships.Count, m.TOMModel.Relationships.Count(r => r.FromTable.Name == "Sales"), "Sales table relationships are equal");
            Assert.AreEqual(2, m.TOMModel.Tables["Sales"].Measures.Count, "Sales table measure counts are equal");

        }


        [TestMethod]
        public void TestPowerBITomModel_CSDL_2_5()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);

            //IADOTabularConnection c = new Mock<IADOTabularConnection>().Object;
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
            ADOTabularModel m = new ADOTabularModel(connection, db, "Test", "Test", "Test Description", "");
            System.Xml.XmlReader xr = new System.Xml.XmlTextReader(@"..\..\data\csdl_2_5.xml");
            var tabs = new ADOTabularTableCollection(connection, m);

            v.GenerateTablesFromXmlReader(tabs, xr);

            Assert.IsNotNull(m.TOMModel);
            Assert.AreEqual(tabs.Count, m.TOMModel.Tables.Count, "Table Counts are equal");
            Assert.AreEqual(tabs["Product"].Columns.Count, m.TOMModel.Tables["Product"].Columns.Count, "Product column counts are equal");
            Assert.AreEqual(tabs["Sales"].Relationships.Count, m.TOMModel.Relationships.Count(r => r.FromTable.Name == "Sales"), "Sales table relationships are equal");
            Assert.AreEqual(2, m.TOMModel.Tables["Sales"].Measures.Count, "Sales table measure counts are equal");

            Assert.AreEqual(8, m.TOMModel.Relationships.Count, "Total Relationships" );
            Assert.AreEqual(4,m.TOMModel.Relationships.Count(r => r.FromTable.Name == "Sales"), "4 relationships FROM sales table");
            Assert.AreEqual(2, m.TOMModel.Relationships.Count(r => r.FromTable.Name == "Bugets"), "2 relationships FROM budgets table");
            Assert.AreEqual(1, m.TOMModel.Relationships.Count(r => !r.IsActive), "There should be 1 inactive relationship");
            Assert.AreEqual(3, m.TOMModel.Relationships.Count(r => r.CrossFilteringBehavior == CrossFilteringBehavior.BothDirections), "3 Bi-Di relationships");
            Assert.AreEqual(1, m.TOMModel.Relationships.Count(r => r.CrossFilteringBehavior == CrossFilteringBehavior.BothDirections && !r.IsActive), "1 inactive Bi-Di relationships");
        }
    }
}
