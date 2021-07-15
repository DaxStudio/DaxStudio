using System;
using System.Collections.Generic;
using ADOTabular;
using ADOTabular.Interfaces;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Mocks;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class QueryBuilderTests
    {
        private IModelCapabilities modelCaps;
        private Mock<IDAXFunctions> mockFuncs;
        private IGlobalOptions mockOptions;
        private IEventAggregator mockEventAggregator = new MockEventAggregator();

        [TestInitialize]
        public void TestSetup()
        {
            // Setup the modelCaps variable
            var mockModelCaps = new Mock<IModelCapabilities>();
            mockFuncs = new Mock<IDAXFunctions>();
            mockFuncs.Setup(f => f.TreatAs).Returns(true);
            mockModelCaps.Setup(c => c.DAXFunctions).Returns(mockFuncs.Object);
            modelCaps = mockModelCaps.Object;
            mockOptions = new Mock<IGlobalOptions>().Object;
        }

        [TestMethod]
        public void TestColumnsOnlyQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);

            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }


        [TestMethod]
        public void TestColumnsAndFiltersQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "M"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "Red"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""M""}, 'Customer'[Gender] )),
    KEEPFILTERS( TREATAS( {""Red""}, 'Product'[Color] ))
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestColumnsIsNotFilterQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.IsNot, FilterValue = "M"});


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[Gender] ), 'Customer'[Gender] <> ""M"" ))
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestColumnsAndMeasureAndFiltersQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "M"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "Red"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""M""}, 'Customer'[Gender] )),
    KEEPFILTERS( TREATAS( {""Red""}, 'Product'[Color] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestNumericFilterQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();
            List<QueryBuilderColumn> orderBy = new List<QueryBuilderColumn>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number of Children", "'Customer'[Number of Children]", typeof(int),
                        ADOTabularObjectType.Column), modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "2"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {2}, 'Customer'[Number of Children] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestNumericFilterTypesQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 1", "'Customer'[Number1]", typeof(int), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.GreaterThan, FilterValue = "1"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 2", "'Customer'[Number2]", typeof(int), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.GreaterThanOrEqual, FilterValue = "2"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 3", "'Customer'[Number3]", typeof(int), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.LessThan, FilterValue = "3"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 4", "'Customer'[Number4]", typeof(int), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.LessThanOrEqual, FilterValue = "4"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[Number1] ), 'Customer'[Number1] > 1 )),
    KEEPFILTERS( FILTER( ALL( 'Customer'[Number2] ), 'Customer'[Number2] >= 2 )),
    KEEPFILTERS( FILTER( ALL( 'Customer'[Number3] ), 'Customer'[Number3] < 3 )),
    KEEPFILTERS( FILTER( ALL( 'Customer'[Number4] ), 'Customer'[Number4] <= 4 )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestStringFilterTypesQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Contains, FilterValue = "ABC"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.DoesNotContain, FilterValue = "DEF"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 3", "'Customer'[String3]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.StartsWith, FilterValue = "GHI"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 4", "'Customer'[String4]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.DoesNotStartWith, FilterValue = "JKL"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[String1] ), SEARCH( ""ABC"", 'Customer'[String1], 1, 0 ) >= 1 )),
    KEEPFILTERS( FILTER( ALL( 'Customer'[String2] ), NOT( SEARCH( ""DEF"", 'Customer'[String2], 1, 0 ) >= 1 ))),
    KEEPFILTERS( FILTER( ALL( 'Customer'[String3] ), SEARCH( ""GHI"", 'Customer'[String3], 1, 0 ) = 1 )),
    KEEPFILTERS( FILTER( ALL( 'Customer'[String4] ), NOT( SEARCH( ""JKL"", 'Customer'[String4], 1, 0 ) = 1 ))),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestBetweenNumbersFilterTypesQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 1", "'Customer'[Number1]", typeof(long), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Between, FilterValue = "2", FilterValue2 = "5"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[Number1] ), 'Customer'[Number1] >= 2 && 'Customer'[Number1] <= 5 )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestBlankFilterTypesQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.IsBlank, FilterValue = ""});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.IsNotBlank, FilterValue = ""});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[String1] ), ISBLANK( 'Customer'[String1] ))),
    KEEPFILTERS( FILTER( ALL( 'Customer'[String2] ), NOT( ISBLANK( 'Customer'[String2] )))),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestDateFilterQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                        MockColumn.CreateADOTabularColumn("Date 1", "'Customer'[Birth Date]", typeof(DateTime),
                            ADOTabularObjectType.Column), modelCaps, mockEventAggregator)
                    {FilterType = FilterType.Is, FilterValue = "2019-11-24"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {DATE(2019,11,24)}, 'Customer'[Birth Date] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        //[ExpectedException(typeof(ArgumentException))]
        public void TestInvalidDateFilterQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                        MockColumn.CreateADOTabularColumn("Date 1", "'Customer'[Birth Date]", typeof(DateTime),
                            ADOTabularObjectType.Column), modelCaps, mockEventAggregator)
                    {FilterType = FilterType.Is, FilterValue = "24/24/2019"});

            ExceptionAssert.Throws<ArgumentException>(
                () => QueryBuilder.BuildQuery(modelCaps, cols, filters,false),
                "Unable to parse the value '24/24/2019' as a DateTime value");


        }

        [TestMethod]
        public void TestOnlyMeasuresQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));
            cols.Add(
                MockColumn.Create("Total Freight", "[Total Freight]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                        MockColumn.CreateADOTabularColumn("Date 1", "'Customer'[Birth Date]", typeof(DateTime),
                            ADOTabularObjectType.Column), modelCaps, mockEventAggregator)
                    {FilterType = FilterType.Is, FilterValue = "2019-11-24"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
CALCULATETABLE(
    ROW(
    ""Total Sales"", [Total Sales],
    ""Total Freight"", [Total Freight]
    ),
    KEEPFILTERS( TREATAS( {DATE(2019,11,24)}, 'Customer'[Birth Date] ))
)
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestMeasureOverrideQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            var meas = MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure);
            meas.MeasureExpression = "123";
            var tab = new Mock<IADOTabularObject>();
            tab.SetupGet(t => t.DaxName).Returns("'Internet Sales'");
            meas.SelectedTable = tab.Object;
            cols.Add(meas);

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number of Children", "'Customer'[Number of Children]", typeof(int),
                        ADOTabularObjectType.Column), modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "2"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
DEFINE
MEASURE 'Internet Sales'[Total Sales] = 123
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {2}, 'Customer'[Number of Children] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }


        [TestMethod]
        public void TestCustomMeasureQuery()
        {

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            var meas = MockColumn.Create("Test Measure", null, typeof(double), ADOTabularObjectType.Measure, false);
            meas.MeasureExpression = "123";

            var tab = new Mock<IADOTabularObject>();
            tab.SetupGet(t => t.DaxName).Returns("'Internet Sales'");
            meas.SelectedTable = tab.Object;
            cols.Add(meas);

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number of Children", "'Customer'[Number of Children]", typeof(int),
                        ADOTabularObjectType.Column), modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "2"});

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
DEFINE
MEASURE 'Internet Sales'[Test Measure] = 123
EVALUATE
SUMMARIZECOLUMNS(
    'Product'[Color],
    KEEPFILTERS( TREATAS( {2}, 'Customer'[Number of Children] )),
    ""Test Measure"", [Test Measure]
)
ORDER BY 
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestTreatAsStringFilterQuery()
        {
            // specify that this model supports TreatAs
            mockFuncs.Setup(f => f.TreatAs).Returns(true);

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "ABC"});


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""ABC""}, 'Customer'[String1] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestTreatAsNumberFilterQuery()
        {
            // specify that this model supports TreatAs
            mockFuncs.Setup(f => f.TreatAs).Returns(true);

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Number 1", "'Customer'[Number 1]", typeof(long), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "123"});


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {123}, 'Customer'[Number 1] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestInListFilterQuery()
        {
            // specify that this model supports TreatAs
            mockFuncs.Setup(f => f.TreatAs).Returns(true);

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String 1]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.In, FilterValue = "red\ngreen\nblue"});


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""red"",""green"",""blue""}, 'Customer'[String 1] )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestNotInListFilterQuery()
        {
            // specify that this model supports TreatAs
            mockFuncs.Setup(f => f.TreatAs).Returns(true);

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            var filterCol = MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String 1]", typeof(string),
                ADOTabularObjectType.Column);

            filters.Add(new QueryBuilderFilter(filterCol, modelCaps, mockEventAggregator)
                {FilterType = FilterType.NotIn, FilterValue = "red\ngreen\nblue"});


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[String 1] ), NOT( 'Customer'[String 1] IN {""red"",""green"",""blue""} ))),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestInListFilterQueryWithoutTreatAs()
        {
            // specify that this model supports TreatAs
            mockFuncs.Setup(f => f.TreatAs).Returns(false);

            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            var filterCol = MockColumn.CreateADOTabularColumn("String 1", "'Customer'[String 1]", typeof(string),
                ADOTabularObjectType.Column);

            filters.Add(new QueryBuilderFilter(filterCol, modelCaps, mockEventAggregator)
            { FilterType = FilterType.In, FilterValue = "red\ngreen\nblue" });


            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( FILTER( ALL( 'Customer'[String 1] ), 'Customer'[String 1] IN {""red"",""green"",""blue""} )),
    ""Total Sales"", [Total Sales]
)
ORDER BY 
    'Product Category'[Category] ASC,
    'Product'[Color] ASC
/* END QUERY BUILDER */".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestOrderByQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            // override the default sort direction 
            cols[1].SortDirection = SortDirection.None;

            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "M"});
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator) {FilterType = FilterType.Is, FilterValue = "Red"});



            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""M""}, 'Customer'[Gender] )),
    KEEPFILTERS( TREATAS( {""Red""}, 'Product'[Color] ))
)
ORDER BY 
    'Product Category'[Category] ASC
/* END QUERY BUILDER */".Replace("\r", "");
            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);
        }

        [TestMethod]
        public void TestParameterQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            // override the default sort direction 
            cols[1].SortDirection = SortDirection.None;
            
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator)
                { FilterType = FilterType.Is, FilterValue = "M" });
            filters.Add(
                new QueryBuilderFilter(
                    MockColumn.CreateADOTabularColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column),
                    modelCaps, mockEventAggregator)
                { FilterType = FilterType.Is, FilterValue = "color", FilterValueIsParameter=true });



            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters, false);
            var expectedQry = @"/* START QUERY BUILDER */
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category],
    'Product'[Color],
    KEEPFILTERS( TREATAS( {""M""}, 'Customer'[Gender] )),
    KEEPFILTERS( TREATAS( {@color}, 'Product'[Color] ))
)
ORDER BY 
    'Product Category'[Category] ASC
/* END QUERY BUILDER */".Replace("\r", "");
            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);
        }

        [TestMethod,Ignore]
        public void TestSerializer()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> filters = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string),
                ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

            filters.Add(
                new QueryBuilderFilter(
                        MockColumn.CreateADOTabularColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column),
                        modelCaps, mockEventAggregator)
                    { FilterType = FilterType.Is, FilterValue = "M" });
            filters.Add(
                new QueryBuilderFilter(
                        MockColumn.CreateADOTabularColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column),
                        modelCaps, mockEventAggregator)
                    { FilterType = FilterType.Is, FilterValue = "Red" });

            var qry = QueryBuilder.BuildQuery(modelCaps, cols, filters,false);

            QueryBuilderViewModel vm = new QueryBuilderViewModel(new MockEventAggregator(), null , mockOptions  );
            cols.ForEach(c => vm.Columns.Add(c));
            filters.ForEach(f => vm.Filters.Add(f));

            var json = vm.GetJson();
            var expectedJson = @"{}";
            StringAssertion.ShouldEqualWithDiff(expectedJson, json, DiffStyle.Full);
        }
    }
}
