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
using ADOTabular.Interfaces;
using ADOTabular.AdomdClientWrappers;

namespace DaxStudio.Tests
{
    [TestClass]
    public class TreeViewTests
    {
        private IADOTabularConnection _connection;
        private static DataSet _keywordDataSet;
        private static DataSet _functionDataSet;
        private static DataSet _measureDataSet;
        private static DataSet _measureDataSetEmpty;
        private static DataSet _cubesDataSet;
        private static ADOTabularDatabase _mockDatabase;
        private static IGlobalOptions _mockOptions;
        private static DataSet _csdlMetaDataRowset;
        private static DataSet _emptyDataSet;


        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext)
        {
            _mockOptions = new Mock<IGlobalOptions>().Object;
            //ConnectionString = @"Data Source=localhost\tab17;";
            //ConnectionString = @"Data Source=.\sql2014tb";
            _keywordDataSet = new DataSet();
            _keywordDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Keyword> {
                    new Keyword { KEYWORD = "TABLE" },
                    new Keyword { KEYWORD = "EVALUATE"}
                })
            );

            _functionDataSet = new DataSet();
            _functionDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Function> {
                    new Function { FUNCTION_NAME = "FILTER", ORIGIN=4 },
                    new Function { FUNCTION_NAME = "CALCULATE", ORIGIN=3}
                })
            );

            _measureDataSet = new DataSet();
            _measureDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<MeasureMD> {
                    new MeasureMD { MEASURE_NAME = "MyMeasure", MEASURE_CAPTION="MyMeasure",DESCRIPTION="My Description",EXPRESSION="1"}
                })
            );

            _measureDataSetEmpty = new DataSet();
            _measureDataSetEmpty.Tables.Add(DmvHelpers.ListToTable(new List<MeasureMD>()));

            _cubesDataSet = new DataSet();
            _cubesDataSet.Tables.Add(
                DmvHelpers.ListToTable(new List<Cube>{
                    new Cube {CUBE_NAME = "Adventure Works", CUBE_CAPTION="Adventure Works", BASE_CUBE_NAME="", DESCRIPTION="Mock Cube"}
                })
            );
            var csdl = string.Join("\n", File.ReadAllLines(@"..\..\data\AdvWrksFoldersCSDL.xml"));
            _csdlMetaDataRowset = new DataSet();
            _csdlMetaDataRowset.Tables.Add(
                DmvHelpers.ListToTable(new List<CSDL_METADATA> {
                        new CSDL_METADATA {Metadata = csdl}
                    })
                );
            _emptyDataSet = new DataSet();
            _emptyDataSet.Tables.Add(new DataTable());
        }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //

        private ADOTabularDatabase GetTestDB()
        {
            return new ADOTabularDatabase(_connection, "Test", "Test", DateTime.Parse("2019-09-01 09:00:00"), "1200", "*");
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
            mockConn.Setup(x => x.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false)).Returns(_keywordDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false)).Returns(_functionDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet("MDSCHEMA_CUBES", It.IsAny<AdomdRestrictionCollection>())).Returns(_cubesDataSet);
            mockConn.Setup(x => x.ShowHiddenObjects).Returns(true);
            var mockDb = new Mock<ADOTabularDatabase>(mockConn.Object, "Adventure Works", "Adventure Works", new DateTime(2017, 7, 20), "1400", "*");
            _mockDatabase = mockDb.Object;
            mockConn.SetupGet(x => x.Database).Returns(_mockDatabase);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res)),
                false))
                .Returns(_measureDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res)),
                false))
                .Returns(_measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => IsResellerSalesMeasureGroup(res))
                ))
                .Returns(_measureDataSet);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_MEASURES",
                It.Is<AdomdRestrictionCollection>(res => !IsResellerSalesMeasureGroup(res))
                ))
                .Returns(_measureDataSetEmpty);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "DISCOVER_CSDL_METADATA",
                It.IsAny<AdomdRestrictionCollection>()
                ))
                .Returns(_csdlMetaDataRowset);
            mockConn.Setup(x => x.GetSchemaDataSet(
                "MDSCHEMA_HIERARCHIES",
                It.IsAny<AdomdRestrictionCollection>()
                ))
                .Returns(_emptyDataSet);
            mockConn.Setup(x => x.ServerVersion).Returns("15.0.0");
            mockConn.SetupGet(x => x.Visitor).Returns(new MetaDataVisitorCSDL(mockConn.Object));

            mockConn.SetupGet(x => x.Keywords).Returns(new ADOTabularKeywordCollection(mockConn.Object));
            mockConn.SetupGet(x => x.AllFunctions).Returns(new List<string>());

            _connection = mockConn.Object;
        }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestDisplayFolders()
        {
            
            ADOTabularDatabase db = GetTestDB();
            ADOTabularModel m = new ADOTabularModel(_connection,db, "Test", "Test", "Test Description", "");
            var mockEventAggregator = new Mock<IEventAggregator>().Object;
            var mockMetadata = new Mock<IMetadataPane>().Object;
            var tt = m.TreeViewTables(_mockOptions, mockEventAggregator,mockMetadata);
            Assert.AreEqual(7, tt.Count, "Correct Table Count");


            var tbl = tt.FirstOrDefault(x => x.Name == "Internet Sales");

            Assert.IsNotNull(tbl, "Could not find 'Internet Sales' table");
 
            var folder = ((TreeViewColumn)tbl.Children.FirstOrDefault(x => ((TreeViewColumn)x).Name == "QTD Folder"));
            Assert.IsNotNull(folder, "Folder Object not found");
            Assert.AreEqual(folder.Name,"QTD Folder");

            TreeViewColumn col = folder.Children.FirstOrDefault(x => x.Name == "Internet Current Quarter Margin") as TreeViewColumn;
            Assert.IsInstanceOfType(col, typeof(TreeViewColumn));
            if (col != null) Assert.AreEqual(MetadataImages.Measure, col.MetadataImage);
        }


    }
}
