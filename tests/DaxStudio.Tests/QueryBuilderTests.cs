using System;
using System.Collections.Generic;
using ADOTabular;
using DaxStudio.Tests.Assertions;
using DaxStudio.Tests.Mocks;
using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class QueryBuilderTests
    {
        [TestMethod]
        public void TestColumnsOnlyQuery()
        {
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));

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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            
            fils.Add( new QueryBuilderFilter(new MockColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M"  } );
            fils.Add( new QueryBuilderFilter(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(new MockColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M" });
            fils.Add(new QueryBuilderFilter(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(new MockColumn("Number of Childer", "'Customer'[Number of Children]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "2" });
            
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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(new MockColumn("Number 1", "'Customer'[Number1]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.GreaterThan, FilterValue = "1" });
            fils.Add(new QueryBuilderFilter(new MockColumn("Number 2", "'Customer'[Number2]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.GreaterThanOrEqual, FilterValue = "2" });
            fils.Add(new QueryBuilderFilter(new MockColumn("Number 3", "'Customer'[Number3]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.LessThan, FilterValue = "3" });
            fils.Add(new QueryBuilderFilter(new MockColumn("Number 4", "'Customer'[Number4]", typeof(int), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.LessThanOrEqual, FilterValue = "4" });

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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(new MockColumn("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Contains, FilterValue = "ABC" });
            fils.Add(new QueryBuilderFilter(new MockColumn("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.DoesNotContain, FilterValue = "DEF" });
            fils.Add(new QueryBuilderFilter(new MockColumn("String 3", "'Customer'[String3]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.StartsWith, FilterValue = "GHI" });
            fils.Add(new QueryBuilderFilter(new MockColumn("String 4", "'Customer'[String4]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.DoesNotStartWith, FilterValue = "JKL" });

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
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<QueryBuilderFilter> fils = new List<QueryBuilderFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new QueryBuilderFilter(new MockColumn("String 1", "'Customer'[String1]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.IsBlank, FilterValue = "" });
            fils.Add(new QueryBuilderFilter(new MockColumn("String 2", "'Customer'[String2]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.IsNotBlank, FilterValue = "" });
            
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
    }
}
