using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class XmSqlParserTests
    {
        private XmSqlParser _parser;

        [TestInitialize]
        public void Setup()
        {
            _parser = new XmSqlParser();
        }

        [TestMethod]
        public void ParseSimpleSelectFromQuery()
        {
            // Arrange
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
    'Product'[Color],
    'Product'[Class]
FROM 'Product';";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, analysis.TotalSEQueriesAnalyzed);
            Assert.AreEqual(1, analysis.SuccessfullyParsedQueries);
            Assert.AreEqual(1, analysis.Tables.Count);
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));

            var productTable = analysis.Tables["Product"];
            Assert.IsTrue(productTable.IsFromTable);
            Assert.AreEqual(2, productTable.Columns.Count);
            Assert.IsTrue(productTable.Columns.ContainsKey("Color"));
            Assert.IsTrue(productTable.Columns.ContainsKey("Class"));

            // Check that columns are marked as Selected
            Assert.IsTrue(productTable.Columns["Color"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
            Assert.IsTrue(productTable.Columns["Class"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
        }

        [TestMethod]
        public void ParseQueryWithLeftOuterJoin()
        {
            // Arrange - based on the screenshot example
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
    'Customer'[CustomerKey],
    'Sales Territory'[Sales Territory Name]
FROM 'Customer'
    LEFT OUTER JOIN 'Geography'[GeographyKey]
    LEFT OUTER JOIN 'Sales Territory'
        ON 'Geography'[GeographyKey]='Geography'[GeographyKey]
WHERE
    'Customer'[Full Name] IN ( 'Arianna G Bailey', 'Alvin Wang' )
    VAND
    'Customer'[Education] = 'Bachelors';";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(3, analysis.Tables.Count); // Customer, Geography, Sales Territory

            // Check Customer table
            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            var customerTable = analysis.Tables["Customer"];
            Assert.IsTrue(customerTable.IsFromTable);
            Assert.IsTrue(customerTable.Columns.ContainsKey("CustomerKey"));
            Assert.IsTrue(customerTable.Columns.ContainsKey("Full Name"));
            Assert.IsTrue(customerTable.Columns.ContainsKey("Education"));

            // Check filtered columns
            Assert.IsTrue(customerTable.Columns["Full Name"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(customerTable.Columns["Education"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));

            // Check Geography table (joined)
            Assert.IsTrue(analysis.Tables.ContainsKey("Geography"));
            var geographyTable = analysis.Tables["Geography"];
            Assert.IsTrue(geographyTable.IsJoinedTable);

            // Check Sales Territory table (joined)
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales Territory"));
            var salesTerritoryTable = analysis.Tables["Sales Territory"];
            Assert.IsTrue(salesTerritoryTable.IsJoinedTable);
        }

        [TestMethod]
        public void ParseQueryWithRelationship()
        {
            // Arrange
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE 'Product'[Category] = 'Bikes';";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, analysis.Relationships.Count);

            var relationship = analysis.Relationships.First();
            Assert.AreEqual("Sales", relationship.FromTable);
            Assert.AreEqual("ProductKey", relationship.FromColumn);
            Assert.AreEqual("Product", relationship.ToTable);
            Assert.AreEqual("ProductKey", relationship.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, relationship.JoinType);

            // Check that join columns are marked
            Assert.IsTrue(analysis.Tables["Sales"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
        }

        [TestMethod]
        public void ParseQueryWithAggregation()
        {
            // Arrange
            string xmSql = @"SELECT
    'Product'[Category],
    SUM ( 'Sales'[Amount] ),
    COUNT ( 'Sales'[OrderKey] ),
    DCOUNT ( 'Customer'[CustomerKey] )
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);

            // Check Sales table aggregations
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            var salesTable = analysis.Tables["Sales"];
            
            Assert.IsTrue(salesTable.Columns.ContainsKey("Amount"));
            Assert.IsTrue(salesTable.Columns["Amount"].UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate));
            Assert.IsTrue(salesTable.Columns["Amount"].AggregationTypes.Contains("SUM"));

            Assert.IsTrue(salesTable.Columns.ContainsKey("OrderKey"));
            Assert.IsTrue(salesTable.Columns["OrderKey"].AggregationTypes.Contains("COUNT"));

            // Check Customer table aggregation
            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            Assert.IsTrue(analysis.Tables["Customer"].Columns["CustomerKey"].AggregationTypes.Contains("DCOUNT"));
        }

        [TestMethod]
        public void ParseQueryWithWithExpression()
        {
            // Arrange - WITH $Expr contains columns used in calculated expressions
            string xmSql = @"SET DC_KIND=""AUTO"";
WITH
    $Expr0 := ( PFCAST ( 'Sales'[Quantity] AS INT ) * PFCAST ( 'Sales'[Net Price] AS INT ) )
SELECT
    'Sales'[StoreKey],
    'Product'[Brand],
    SUM ( @$Expr0 )
FROM 'Sales'
    LEFT OUTER JOIN 'Date'
        ON 'Sales'[Order Date]='Date'[Date]
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE
    'Date'[Day of Week Number] = 4;";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            
            // Check Sales table has expression columns
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            var salesTable = analysis.Tables["Sales"];
            
            // These columns are in the WITH expression
            Assert.IsTrue(salesTable.Columns.ContainsKey("Quantity"), "Quantity column should be found from WITH expression");
            Assert.IsTrue(salesTable.Columns["Quantity"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression), 
                "Quantity should be marked as Expression usage");
            
            Assert.IsTrue(salesTable.Columns.ContainsKey("Net Price"), "Net Price column should be found from WITH expression");
            Assert.IsTrue(salesTable.Columns["Net Price"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression),
                "Net Price should be marked as Expression usage");
            
            // These columns are in the SELECT (not the WITH)
            Assert.IsTrue(salesTable.Columns.ContainsKey("StoreKey"), "StoreKey should be in SELECT");
            Assert.IsTrue(salesTable.Columns["StoreKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Select),
                "StoreKey should be marked as Select usage");
        }

        [TestMethod]
        public void ParseMultipleQueries()
        {
            // Arrange
            var queries = new[]
            {
                "SELECT 'Product'[Color] FROM 'Product';",
                "SELECT 'Product'[Size], 'Customer'[Name] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];",
                "SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"
            };

            // Act
            var analysis = _parser.ParseQueries(queries);

            // Assert
            Assert.AreEqual(3, analysis.TotalSEQueriesAnalyzed);
            Assert.AreEqual(3, analysis.SuccessfullyParsedQueries);
            
            // Product table should have multiple columns from different queries
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));
            var productTable = analysis.Tables["Product"];
            Assert.IsTrue(productTable.Columns.ContainsKey("Color"));
            Assert.IsTrue(productTable.Columns.ContainsKey("Size"));
            Assert.IsTrue(productTable.Columns.ContainsKey("Category"));

            // Color should have been hit multiple times
            Assert.IsTrue(productTable.Columns["Color"].HitCount >= 2);
        }

        [TestMethod]
        public void ParseQueryWithInnerJoin()
        {
            // Arrange
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
    INNER JOIN 'Date'
        ON 'Sales'[DateKey]='Date'[DateKey];";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(1, analysis.Relationships.Count);
            Assert.AreEqual(XmSqlJoinType.InnerJoin, analysis.Relationships.First().JoinType);
        }

        [TestMethod]
        public void ParseQueryWithMultipleJoins()
        {
            // Arrange - complex query with multiple joins
            string xmSql = @"SELECT
    'Sales'[Amount],
    'Product'[Name],
    'Customer'[City]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
    LEFT OUTER JOIN 'Customer'
        ON 'Sales'[CustomerKey]='Customer'[CustomerKey];";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(3, analysis.Tables.Count); // Sales, Product, Customer
            Assert.AreEqual(2, analysis.Relationships.Count); // Two relationships

            // Verify both relationships exist
            Assert.IsTrue(analysis.Relationships.Any(r => 
                r.FromTable == "Sales" && r.ToTable == "Product"));
            Assert.IsTrue(analysis.Relationships.Any(r => 
                r.FromTable == "Sales" && r.ToTable == "Customer"));
        }

        [TestMethod]
        public void ExtractTableName_ReturnsCorrectTable()
        {
            // Act
            var result = XmSqlParser.ExtractTableName("'Sales Territory'[Territory Name]");

            // Assert
            Assert.AreEqual("Sales Territory", result);
        }

        [TestMethod]
        public void ExtractColumnName_ReturnsCorrectColumn()
        {
            // Act
            var result = XmSqlParser.ExtractColumnName("'Sales Territory'[Territory Name]");

            // Assert
            Assert.AreEqual("Territory Name", result);
        }

        [TestMethod]
        public void IsScanQuery_ReturnsTrueForValidScan()
        {
            // Arrange
            string xmSql = "SELECT 'Product'[Color] FROM 'Product';";

            // Act
            var result = XmSqlParser.IsScanQuery(xmSql);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsScanQuery_ReturnsFalseForNonScan()
        {
            // Arrange
            string xmSql = "SET DC_KIND=\"AUTO\";";

            // Act
            var result = XmSqlParser.IsScanQuery(xmSql);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ParseEmptyQuery_ReturnsFalse()
        {
            // Arrange
            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery("", analysis);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ParseNullQuery_ReturnsFalse()
        {
            // Arrange
            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(null, analysis);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RelationshipHitCount_IncrementsForDuplicates()
        {
            // Arrange
            var queries = new[]
            {
                "SELECT 'Sales'[Amount] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];",
                "SELECT 'Sales'[Quantity] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];",
                "SELECT 'Sales'[Discount] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];"
            };

            // Act
            var analysis = _parser.ParseQueries(queries);

            // Assert
            Assert.AreEqual(1, analysis.Relationships.Count); // Same relationship, just counted multiple times
            Assert.AreEqual(3, analysis.Relationships.First().HitCount);
        }

        [TestMethod]
        public void TableHitCount_IncrementsAcrossQueries()
        {
            // Arrange
            var queries = new[]
            {
                "SELECT 'Product'[Color] FROM 'Product';",
                "SELECT 'Product'[Size] FROM 'Product';",
                "SELECT 'Product'[Weight] FROM 'Product';"
            };

            // Act
            var analysis = _parser.ParseQueries(queries);

            // Assert
            Assert.AreEqual(1, analysis.Tables.Count);
            Assert.AreEqual(3, analysis.Tables["Product"].HitCount);
        }

        [TestMethod]
        public void ParseTimeIntelligenceQueryWithJoinAndFilter()
        {
            // Arrange - exact query from user testing
            string xmSql = @"DEFINE TABLE '$TTable5' :=
SELECT
    'Calendar'[Date],
    'Time Intelligence'[Period]
FROM 'Time Intelligence'
    LEFT OUTER JOIN 'Calendar'
        ON 'Time Intelligence'[Date]='Calendar'[Date]
WHERE
    'Time Intelligence'[Period] NIN ( 'Current Year' ) ,";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            
            // Should have 3 tables: $TTable5, Calendar, Time Intelligence
            // But $TTable5 is an intermediate table
            Assert.IsTrue(analysis.Tables.ContainsKey("Calendar"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Time Intelligence"));
            
            // Check Time Intelligence table
            var timeIntelligenceTable = analysis.Tables["Time Intelligence"];
            Assert.IsTrue(timeIntelligenceTable.IsFromTable);
            Assert.IsTrue(timeIntelligenceTable.Columns.ContainsKey("Date"));
            Assert.IsTrue(timeIntelligenceTable.Columns.ContainsKey("Period"));
            
            // Date should be marked as Join (in ON clause)
            Assert.IsTrue(timeIntelligenceTable.Columns["Date"].UsageTypes.HasFlag(XmSqlColumnUsage.Join), 
                "Time Intelligence[Date] should be marked as Join column");
            
            // Period should be marked as Select and Filter
            Assert.IsTrue(timeIntelligenceTable.Columns["Period"].UsageTypes.HasFlag(XmSqlColumnUsage.Select),
                "Time Intelligence[Period] should be marked as Select column");
            Assert.IsTrue(timeIntelligenceTable.Columns["Period"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter),
                "Time Intelligence[Period] should be marked as Filter column");
            
            // Check Calendar table
            var calendarTable = analysis.Tables["Calendar"];
            Assert.IsTrue(calendarTable.IsJoinedTable);
            Assert.IsTrue(calendarTable.Columns.ContainsKey("Date"));
            
            // Calendar Date should also be marked as Join (in ON clause)
            Assert.IsTrue(calendarTable.Columns["Date"].UsageTypes.HasFlag(XmSqlColumnUsage.Join),
                "Calendar[Date] should be marked as Join column");
            
            // Check relationship exists
            Assert.AreEqual(1, analysis.Relationships.Count);
            var rel = analysis.Relationships[0];
            Assert.AreEqual("Time Intelligence", rel.FromTable);
            Assert.AreEqual("Date", rel.FromColumn);
            Assert.AreEqual("Calendar", rel.ToTable);
            Assert.AreEqual("Date", rel.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, rel.JoinType);
        }
    }
}
