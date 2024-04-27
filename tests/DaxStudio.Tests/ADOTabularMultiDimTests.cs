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
using System.IO;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ADOTabularMultiDimTests
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }
        //private static string ConnectionString { get; set; }
        //private IADOTabularConnection connection;
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

        //private ADOTabularDatabase GetTestDB()
        //{
        //    return new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
        //}

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
        
        public IADOTabularConnection MockConnection(string csdlFile) {
            var mockConn = new Mock<IADOTabularConnection>();
            var columnCollection = new Dictionary<string, ADOTabularColumn>();
            
            mockConn.SetupGet(x => x.Columns).Returns(columnCollection);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false)).Returns(keywordDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false)).Returns(functionDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS")).Returns(dmvDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_CUBES", It.IsAny<AdomdRestrictionCollection>())).Returns(cubesDataSet);
            mockConn.Setup(x => x.ShowHiddenObjects).Returns(true);
            var mockDb = new Mock<ADOTabularDatabase>(mockConn.Object, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*", "Test Description");
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

            var csdlString = File.ReadAllText(csdlFile);
            var csdlDataSet = new DataSet();
            var csdlDataTable = new DataTable();
            csdlDataTable.Columns.Add("metadata", typeof(string));
            csdlDataTable.Rows.Add(csdlString);
            csdlDataSet.Tables.Add(csdlDataTable);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_CSDL_METADATA", It.IsAny<AdomdRestrictionCollection>())).Returns(csdlDataSet);

            var emptyDataset = new DataSet();
            var emptyDataTable = new DataTable();
            emptyDataset.Tables.Add(emptyDataTable);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", It.IsAny<AdomdRestrictionCollection>())).Returns(emptyDataset);

            return mockConn.Object;
        }

        #endregion


        
        [TestMethod]
        public void TestADOTabularMultiDimCSDLVisitor()
        {
            //ADOTabularConnection c = new ADOTabularConnection(ConnectionString, AdomdType.AnalysisServices);
            var conn = MockConnection($@"{Constants.TestDataPath}\multidim_csdl.xml");
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(conn);
            ADOTabularDatabase db = new ADOTabularDatabase(conn, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*","Test Description");
            ADOTabularModel m = new ADOTabularModel(conn,db, "Test","Test", "Test Description", "");
            var tabs = new ADOTabularTableCollection(conn, m);

            Assert.AreEqual(33, tabs.Count);
            Assert.AreEqual(41, tabs["Customer"].Columns.Count()); // excludes internal rowcount column
            Assert.AreEqual(0, tabs["Customer"].Columns[2].DistinctValues);

            // Check TOM objects

            Assert.AreEqual(33, m.TOMModel.Tables.Count, "Count of tables in TOM Model");
            Assert.AreEqual(40, m.TOMModel.Tables["Customer"].Columns.Count, "Count of columns in TOM Model"); // includes external rowcount column
            
            // check if ADOTabular collection contains TOM columns
            foreach(var c in m.TOMModel.Tables["Customer"].Columns)
            {
                Assert.IsTrue(tabs["Customer"].Columns.ContainsKey(c.Name), $"ADOTabular Column: {c.Name} is null");
            }

            // check if TOM Columns contains all ADOTabular columns
            foreach (var c in tabs["Customer"].Columns)
            {
                if (c.ObjectType == ADOTabularObjectType.Hierarchy) continue;
                Assert.IsTrue(m.TOMModel.Tables["Customer"].Columns.ContainsName(c.Name), $"TOM Column: {c.Name} is null");
            }

            Assert.AreEqual(64, m.TOMModel.Tables["Internet Sales"].Measures.Count, "Count of measures in TOM Model");
        }

    }
}
