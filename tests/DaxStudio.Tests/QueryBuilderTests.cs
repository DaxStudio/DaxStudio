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
            List<TreeViewColumnFilter> fils = new List<TreeViewColumnFilter>();

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
            List<TreeViewColumnFilter> fils = new List<TreeViewColumnFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            
            fils.Add( new TreeViewColumnFilter(new MockColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M"  } );
            fils.Add( new TreeViewColumnFilter(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(VALUES( 'Customer'[Gender] ), 'Customer'[Gender] = ""M"")
    ,FILTER(VALUES( 'Product'[Color] ), 'Product'[Color] = ""Red"")
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }

        [TestMethod]
        public void TestColumnsAndMeasureAndFiltersQuery()
        {
            List<IADOTabularColumn> cols = new List<IADOTabularColumn>();
            List<TreeViewColumnFilter> fils = new List<TreeViewColumnFilter>();

            cols.Add(new MockColumn("Category", "'Product Category'[Category]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column));
            cols.Add(new MockColumn("Total Sales", "[Total Sales]", typeof(double), ADOTabularObjectType.Measure));

            fils.Add(new TreeViewColumnFilter(new MockColumn("Gender", "'Customer'[Gender]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "M" });
            fils.Add(new TreeViewColumnFilter(new MockColumn("Color", "'Product'[Color]", typeof(string), ADOTabularObjectType.Column)) { FilterType = UI.Enums.FilterType.Is, FilterValue = "Red" });

            var qry = QueryBuilder.BuildQuery(cols, fils);
            var expectedQry = @"// START QUERY BUILDER
EVALUATE
SUMMARIZECOLUMNS(
    'Product Category'[Category]
    ,'Product'[Color]
    ,FILTER(VALUES( 'Customer'[Gender] ), 'Customer'[Gender] = ""M"")
    ,FILTER(VALUES( 'Product'[Color] ), 'Product'[Color] = ""Red"")
    ,""Total Sales"", [Total Sales]
)
// END QUERY BUILDER".Replace("\r", "");

            StringAssertion.ShouldEqualWithDiff(expectedQry, qry, DiffStyle.Full);

        }
    }
}
