using System;
using System.Data;
using System.Linq;
using ADOTabular;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DaxStudio.UI.Extensions;
using DaxStudio.Tests.Utils;
using MeasureMD = DaxStudio.Tests.Utils.MeasureMD;
using MeasureTM = DaxStudio.Tests.Utils.MeasureTM;
using System.Collections.Generic;
using DaxStudio.UI.Model;
using Caliburn.Micro;
using ADOTabular.Interfaces;
using ADOTabular.AdomdClientWrappers;
using System.IO;
using NSubstitute;

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

        private static bool IsResellerSalesMeasureGroup(AdomdRestrictionCollection res)
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

        private static bool IsResellerSalesTable(AdomdRestrictionCollection res)
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
            var mockConn = Substitute.For<IADOTabularConnection>();
            var columnCollection = new Dictionary<string, ADOTabularColumn>();
            
            mockConn.Columns.Returns(columnCollection);
            mockConn.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false).Returns(keywordDataSet);
            mockConn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false).Returns(functionDataSet);
            mockConn.GetSchemaDataSet("DISCOVER_SCHEMA_ROWSETS").Returns(dmvDataSet);
            mockConn.GetSchemaDataSet("MDSCHEMA_CUBES", Arg.Any<AdomdRestrictionCollection>()).Returns(cubesDataSet);
            mockConn.ShowHiddenObjects.Returns(true);
            var mockDb = Substitute.For<ADOTabularDatabase>(mockConn, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*", "Test Description");
            mockDatabase = mockDb;
            mockConn.Database.Returns(mockDatabase);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES", 
                Arg.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res)), 
                false)
                .Returns(measureDataSet_MD);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res)),
                false)
                .Returns(measureDataSetEmpty);
            mockConn.GetSchemaDataSet(
                "TMSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => IsResellerSalesTable(res))
                )
                .Returns(measureDataSet_TM);
            mockConn.GetSchemaDataSet(
                "TMSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => !IsResellerSalesTable(res))
                )
                .Returns(measureDataSetEmpty);
            mockConn.GetSchemaDataSet("TMSCHEMA_TABLES", Arg.Any<AdomdRestrictionCollection>()).Returns(tablesDataSet);
            mockConn.ServerVersion.Returns("15.0.0");
            var visitor = new MetaDataVisitorCSDL(mockConn);
            mockConn.Visitor.Returns(visitor);
            var keywords = new ADOTabularKeywordCollection(mockConn);
            mockConn.Keywords.Returns(keywords);
            mockConn.AllFunctions.Returns(new List<string>());
            mockConn.DynamicManagementViews.Returns( new ADOTabularDynamicManagementViewCollection(mockConn));
            mockConn.IsAdminConnection.Returns(true);

            var csdlString = File.ReadAllText(csdlFile);
            var csdlDataSet = new DataSet();
            var csdlDataTable = new DataTable();
            csdlDataTable.Columns.Add("metadata", typeof(string));
            csdlDataTable.Rows.Add(csdlString);
            csdlDataSet.Tables.Add(csdlDataTable);
            mockConn.GetSchemaDataSet("DISCOVER_CSDL_METADATA", Arg.Any<AdomdRestrictionCollection>()).Returns(csdlDataSet);

            var emptyDataset = new DataSet();
            var emptyDataTable = new DataTable();
            emptyDataset.Tables.Add(emptyDataTable);
            mockConn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", Arg.Any<AdomdRestrictionCollection>()).Returns(emptyDataset);

            return mockConn;
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
            Assert.AreEqual(41, tabs["Customer"].Columns.Count); // excludes internal rowcount column
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
