using ADOTabular;
using DaxStudio.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Data;
using MeasureMD = DaxStudio.Tests.Utils.MeasureMD;
using DaxStudio.UI.Model;
using DaxStudio.Interfaces;
using Caliburn.Micro;
using System.IO;
using System.Linq;
using DaxStudio.UI.Interfaces;
using ADOTabular.Interfaces;
using ADOTabular.AdomdClientWrappers;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TreeViewDisplayFolderTests
    {
        private IADOTabularConnection connection;
        private static DataSet keywordDataSet;
        private static DataSet functionDataSet;
        private static DataSet measureDataSet;
        private static DataSet measureDataSetEmpty;
        private static DataSet cubesDataSet;
        private static ADOTabularDatabase mockDatabase;
        private static IGlobalOptions mockOptions;
        private static DataSet csdlMetaDataRowset;
        private static DataSet emptyDataSet;


        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            mockOptions = new Mock<IGlobalOptions>().Object;
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

            measureDataSet = new DataSet();
            measureDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<MeasureMD> {
                    new MeasureMD { MEASURE_NAME = "MyMeasure", MEASURE_CAPTION="MyMeasure",DESCRIPTION="My Description",EXPRESSION="1"}
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
            var csdl = string.Join("\n", File.ReadAllLines(@"..\..\data\FoldersCSDL.xml"));
            csdlMetaDataRowset = new DataSet();
            csdlMetaDataRowset.Tables.Add(
                DmvHelpers.ListToTable(new List<CSDL_METADATA> {
                        new CSDL_METADATA {Metadata = csdl}
                    })
                );
            emptyDataSet = new DataSet();
            emptyDataSet.Tables.Add(new DataTable());
        }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //

        private ADOTabularDatabase GetTestDB()
        {
            return new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
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


        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void MyTestInitialize()
        {
            var mockConn = new Mock<IADOTabularConnection>();
            var columnCollection = new Dictionary<string, ADOTabularColumn>();

            mockConn.SetupGet(x => x.Columns).Returns(columnCollection);
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false)).Returns(keywordDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false)).Returns(functionDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_CUBES", It.IsAny<AdomdRestrictionCollection>())).Returns(cubesDataSet);
            mockConn.Setup(x => x.ShowHiddenObjects).Returns(true);
            var mockDb = new Mock<ADOTabularDatabase>(mockConn.Object, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*");
            mockDatabase = mockDb.Object;
            mockConn.SetupGet(x => x.Database).Returns(mockDatabase);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res)),
                false))
                .Returns(measureDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res)),
                false))
                .Returns(measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res))
                ))
                .Returns(measureDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res))
                ))
                .Returns(measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "DISCOVER_CSDL_METADATA",
                It.IsAny<AdomdRestrictionCollection>()
                ))
                .Returns(csdlMetaDataRowset);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_HIERARCHIES",
                It.IsAny<AdomdRestrictionCollection>()
                ))
                .Returns(emptyDataSet);
            mockConn.Setup(x => x.ServerVersion).Returns("15.0.0");
            mockConn.SetupGet(x => x.Visitor).Returns(new MetaDataVisitorCSDL(mockConn.Object));

            mockConn.SetupGet(x => x.Keywords).Returns(new ADOTabularKeywordCollection(mockConn.Object));
            mockConn.SetupGet(x => x.AllFunctions).Returns(new List<string>());

            connection = mockConn.Object;
        }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestOnlyDisplayFolders()
        {
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            var mockEventAggregator = new Mock<IEventAggregator>().Object;
            var mockMetadata = new Mock<IMetadataPane>().Object;
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator,mockMetadata);
            Assert.AreEqual(2, tt.Count, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Sales");

            Assert.IsNotNull(tbl, "Could not find 'Sales' table");
 
            var folder = (TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Amount Folder");
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual(folder.Name,"Amount Folder");

            TreeViewColumn col = folder.Children.FirstOrDefault(x => x.Name == "Amount") as TreeViewColumn;
            Assert.IsInstanceOfType(col, typeof(TreeViewColumn));
            Assert.AreEqual(MetadataImages.Column, col.MetadataImage);
            

        }


        [TestMethod]
        public void TestSecondDisplayFolderWithoutCaption()
        {
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            var mockEventAggregator = new Mock<IEventAggregator>().Object;
            var mockMetadata = new Mock<IMetadataPane>().Object;
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator, mockMetadata);
            Assert.AreEqual(2, tt.Count, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Sales");

            Assert.IsNotNull(tbl, "Could not find 'Sales' table");

            var folder = ((TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Price"));
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual(folder.Name, "Price");

            TreeViewColumn col = folder.Children.FirstOrDefault(x => x.Name == "Price") as TreeViewColumn;
            Assert.IsInstanceOfType(col, typeof(TreeViewColumn));
            Assert.AreEqual(MetadataImages.Column, col.MetadataImage);


        }

        [TestMethod]
        public void TestSecondDisplayFolderWithHierarchy()
        {
            MetaDataVisitorCSDL v = new MetaDataVisitorCSDL(connection);
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(connection,db, "Test", "Test", "Test Description", "");
            var mockEventAggregator = new Mock<IEventAggregator>().Object;
            var mockMetadata = new Mock<IMetadataPane>().Object;
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator, mockMetadata);
            Assert.AreEqual(2, tt.Count, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Calendar");

            Assert.IsNotNull(tbl, "Could not find 'Date' table");

            var folder = ((TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Dates"));
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual(folder.Name, "Dates");

            TreeViewColumn col = folder.Children.FirstOrDefault(x => x.Name == "Date_Hierarchy1") as TreeViewColumn;
            Assert.IsInstanceOfType(col, typeof(TreeViewColumn));
            Assert.AreEqual(MetadataImages.Hierarchy, col.MetadataImage);


        }

    }
}
