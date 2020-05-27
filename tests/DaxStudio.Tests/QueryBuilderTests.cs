using System;
using System.Collections.Generic;
using ADOTabular;
using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Mocks;
using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class QueryBuilderTests
    {
        [TestMethod]
        public void TestColumnsOnlyQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

            var qry = QueryBuilder.BuildQuery(cols, fils);

            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
)
// END QUERY BUILDER".Replace("\r","");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }


        [TestMethod]
        public void TestColumnsAndFiltersQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            
            fils.Add(new QueryBuilderFilter(MockColumn.Create("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M"  } );
            fils.Add(new QueryBuilderFilter(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Gender] )), 'Customer'[Gender] = ""M"")
    ,FILTER(KEEPFILTERS(VALUES( 'Product'[Color] )), 'Product'[Color] = ""Red"")
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestColumnsAndMeasureAndFiltersQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add( MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add( MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add( MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter( MockColumn.Create("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M" });
            fils.Add(new QueryBuilderFilter( MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Gender] )), 'Customer'[Gender] = ""M"")
    ,FILTER(KEEPFILTERS(VALUES( 'Product'[Color] )), 'Product'[Color] = ""Red"")
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestNumericFilterQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number of Childer", "'Customer'[Number of Children]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2" });
            
            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number of Children] )), 'Customer'[Number of Children] = 2)
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestNumericFilterTypesQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number 1", "'Customer'[Number1]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.GreaterThan, FilterValue = "1" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number 2", "'Customer'[Number2]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.GreaterThanOrEqual, FilterValue = "2" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number 3", "'Customer'[Number3]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.LessThan, FilterValue = "3" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number 4", "'Customer'[Number4]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.LessThanOrEqual, FilterValue = "4" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number1] )), 'Customer'[Number1] > 1)
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number2] )), 'Customer'[Number2] >= 2)
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number3] )), 'Customer'[Number3] < 3)
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number4] )), 'Customer'[Number4] <= 4)
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestStringFilterTypesQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Contains, FilterValue = "ABC" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.DoesNotContain, FilterValue = "DEF" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 3", "'Customer'[String3]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.StartsWith, FilterValue = "GHI" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 4", "'Customer'[String4]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.DoesNotStartWith, FilterValue = "JKL" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String1] )), SEARCH(""ABC"", 'Customer'[String1], 1, 0) >= 1)
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String2] )), NOT(SEARCH(""DEF"", 'Customer'[String2], 1, 0) >= 1))
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String3] )), SEARCH(""GHI"", 'Customer'[String3], 1, 0) = 1)
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String4] )), NOT(SEARCH(""JKL"", 'Customer'[String4], 1, 0) = 1))
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestBlankFilterTypesQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.IsBlank, FilterValue = "" });
            fils.Add(new QueryBuilderFilter(MockColumn.Create("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.IsNotBlank, FilterValue = "" });
            
            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String1] )), ISBLANK('Customer'[String1]))
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[String2] )), NOT(ISBLANK('Customer'[String2])))
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestDateFilterQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Date 1", "'Customer'[Birth Date]", typeof(DateTime), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2019-11-24" });
            
            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Birth Date] )), 'Customer'[Birth Date] = DATE(2019,11,24))
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        //[ExpectedException(typeof(ArgumentException))]
        public void TestInvalideDateFilterQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Date 1", "'Customer'[Birth Date]", typeof(DateTime), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "24/24/2019" });

            ExceptionAssert.Throws<ArgumentException>(() =>  QueryBuilder.BuildQuery(cols, fils), "Unable to parse the value '24/24/2019' as a DateTime value");


        }

        [TestMethod]
        public void TestOnlyMeasuresQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));
            cols.Add(MockColumn.Create("Total Freight", "[Total Freight]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Date 1", "'Customer'[Birth Date]", typeof(DateTime), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2019-11-24" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
CALCULATETABLE(
    ROW(
    ""Total Sales"", [Total Sales]
    ,""Total Freight"", [Total Freight]
    )
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Birth Date] )), 'Customer'[Birth Date] = DATE(2019,11,24))
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestMeasureOverrideQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            var meas = MockColumn.Create("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure);
            meas.MeasureExpression = "123";
            var tab = new Mock<IADOTabularObject>();
            tab.SetupGet(t => t.DaxName).Returns("'Internet Sales'");
            meas.SelectedTable = tab.Object; 
            cols.Add(meas);

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number of Childer", "'Customer'[Number of Children]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
DEFINE
MEASURE 'Internet Sales'[Total Sales] = 123
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number of Children] )), 'Customer'[Number of Children] = 2)
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }


        [TestMethod]
        public void TestCustomMeasureQuery()
        {
            List<QueryBuilderColumn> cols = new List<QueryBuilderColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(MockColumn.Create("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            var meas = MockColumn.Create("Test Measure", null, typeof(double), ADOTabularObjectType.Measure, false);
            meas.MeasureExpression = "123";

            var tab = new Mock<IADOTabularObject>();
            tab.SetupGet(t => t.DaxName).Returns("'Internet Sales'");
            meas.SelectedTable = tab.Object;
            cols.Add(meas);

            fils.Add(new QueryBuilderFilter(MockColumn.Create("Number of Childer", "'Customer'[Number of Children]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
DEFINE
MEASURE 'Internet Sales'[Test Measure] = 123
EVALUATE
SUMMARIZECOLUMNS(
    'Product'[Color]
    ,FILTER(KEEPFILTERS(VALUES( 'Customer'[Number of Children] )), 'Customer'[Number of Children] = 2)
    ,""Test Measure"", [Test Measure]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

    }
}
