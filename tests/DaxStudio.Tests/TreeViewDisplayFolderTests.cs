using ADOTabular;
using DaxStudio.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
using NSubstitute;

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
            mockOptions = Substitute.For<IGlobalOptions>();
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
            var csdl = string.Join("\n", File.ReadAllLines($@"{Constants.TestDataPath}\FoldersCSDL.xml"));
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
            return new ADOTabularDatabase(connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*", "Test Description");
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
            var mockConn = Substitute.For<IADOTabularConnection>();
            var columnCollection = new Dictionary<string, ADOTabularColumn>();

            mockConn.Columns.Returns(columnCollection);
            mockConn.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false).Returns(keywordDataSet);
            mockConn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false).Returns(functionDataSet);
            mockConn.GetSchemaDataSet("MDSCHEMA_CUBES", Arg.Any<AdomdRestrictionCollection>()).Returns(cubesDataSet);
            mockConn.ShowHiddenObjects.Returns(true);
            var mockDb = Substitute.For<ADOTabularDatabase>(mockConn, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*", "Test Description");
            mockDatabase = mockDb;
            mockConn.Database.Returns(mockDatabase);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res)),
                false)
                .Returns(measureDataSet);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res)),
                false)
                .Returns(measureDataSetEmpty);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res))
                )
                .Returns(measureDataSet);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                Arg.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res))
                )
                .Returns(measureDataSetEmpty);
            mockConn.GetSchemaDataSet(
                "DISCOVER_CSDL_METADATA",
                Arg.Any<AdomdRestrictionCollection>()
                )
                .Returns(csdlMetaDataRowset);
            mockConn.GetSchemaDataSet(
                "MDSCHEMA_HIERARCHIES",
                Arg.Any<AdomdRestrictionCollection>()
                )
                .Returns(emptyDataSet);
            mockConn.ServerVersion.Returns("15.0.0");
            var visitor = new MetaDataVisitorCSDL(mockConn);
            mockConn.Visitor.Returns(visitor);
            var keywords = new ADOTabularKeywordCollection(mockConn);
            mockConn.Keywords.Returns(keywords);
            var emptyList = new List<string>();
            mockConn.AllFunctions.Returns(emptyList);

            connection = mockConn;
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
            var mockEventAggregator = Substitute.For<IEventAggregator>();
            var mockMetadata = Substitute.For<IMetadataPane>();
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator,mockMetadata);
            Assert.HasCount(2, tt, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Sales");

            Assert.IsNotNull(tbl, "Could not find 'Sales' table");
 
            var folder = (TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Amount Folder");
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual("Amount Folder", folder.Name);

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
            var mockEventAggregator = Substitute.For<IEventAggregator>();
            var mockMetadata = Substitute.For<IMetadataPane>();
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator, mockMetadata);
            Assert.HasCount(2, tt, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Sales");

            Assert.IsNotNull(tbl, "Could not find 'Sales' table");

            var folder = ((TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Price"));
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual("Price", folder.Name);

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
            var mockEventAggregator = Substitute.For<IEventAggregator>();
            var mockMetadata = Substitute.For<IMetadataPane>();
            var tt = m.TreeViewTables(mockOptions, mockEventAggregator, mockMetadata);
            Assert.HasCount(2, tt, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Calendar");

            Assert.IsNotNull(tbl, "Could not find 'Date' table");

            var folder = ((TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "Dates"));
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual("Dates", folder.Name);

            TreeViewColumn col = folder.Children.FirstOrDefault(x => x.Name == "Date_Hierarchy1") as TreeViewColumn;
            Assert.IsInstanceOfType(col, typeof(TreeViewColumn));
            Assert.AreEqual(MetadataImages.Hierarchy, col.MetadataImage);


        }

    }
}
