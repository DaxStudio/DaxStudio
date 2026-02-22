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

        // ==================== BASIC SELECT / FROM ====================

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
        public void IsScanQuery_ReturnsFalseForNull()
        {
            Assert.IsFalse(XmSqlParser.IsScanQuery(null));
        }

        [TestMethod]
        public void IsScanQuery_ReturnsFalseForWhitespace()
        {
            Assert.IsFalse(XmSqlParser.IsScanQuery("   "));
        }

        // ==================== EXTRACT TABLE / COLUMN NAMES ====================

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
        public void ExtractTableName_ReturnsNullForInvalid()
        {
            var result = XmSqlParser.ExtractTableName("not a valid reference");
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractColumnName_ReturnsNullForInvalid()
        {
            var result = XmSqlParser.ExtractColumnName("not a valid reference");
            Assert.IsNull(result);
        }

        // ==================== JOIN TESTS ====================

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
        public void ParseQueryWithMinMaxAggregations()
        {
            string xmSql = @"SELECT
    MIN ( 'Sales'[OrderDate] ),
    MAX ( 'Sales'[Amount] )
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var salesTable = analysis.Tables["Sales"];
            Assert.IsTrue(salesTable.Columns["OrderDate"].AggregationTypes.Contains("MIN"));
            Assert.IsTrue(salesTable.Columns["Amount"].AggregationTypes.Contains("MAX"));
        }

        [TestMethod]
        public void ParseQueryWithCountAll()
        {
            // COUNT() without column should still parse successfully
            string xmSql = @"SELECT
    'Product'[Category],
    COUNT ( )
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));
        }

        // ==================== WITH EXPRESSION TESTS ====================

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
        public void ParseQueryWithMultipleExpressions()
        {
            string xmSql = @"SET DC_KIND=""AUTO"";
WITH
    $Expr0 := ( PFCAST ( 'Sales'[Quantity] AS INT ) * PFCAST ( 'Sales'[Net Price] AS INT ) )
    $Expr1 := ( PFCAST ( 'Sales'[Discount] AS INT ) + PFCAST ( 'Sales'[Tax] AS INT ) )
SELECT
    'Product'[Brand],
    SUM ( @$Expr0 ),
    SUM ( @$Expr1 )
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            var salesTable = analysis.Tables["Sales"];
            Assert.IsTrue(salesTable.Columns.ContainsKey("Quantity"));
            Assert.IsTrue(salesTable.Columns["Quantity"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression));
            Assert.IsTrue(salesTable.Columns.ContainsKey("Net Price"));
            Assert.IsTrue(salesTable.Columns.ContainsKey("Discount"));
            Assert.IsTrue(salesTable.Columns["Discount"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression));
            Assert.IsTrue(salesTable.Columns.ContainsKey("Tax"));
            Assert.IsTrue(salesTable.Columns["Tax"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression));
        }

        // ==================== FILTER VALUE EXTRACTION TESTS ====================

        [TestMethod]
        public void ParseQueryWithEqualityFilter()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE
    'Product'[Category] = 'Bikes';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var productTable = analysis.Tables["Product"];
            var categoryCol = productTable.Columns["Category"];
            Assert.IsTrue(categoryCol.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(categoryCol.FilterValues.Contains("Bikes"));
            Assert.IsTrue(categoryCol.FilterOperators.Contains("="));
        }

        [TestMethod]
        public void ParseQueryWithInFilter()
        {
            string xmSql = @"SELECT 'Customer'[Name]
FROM 'Customer'
WHERE
    'Customer'[City] IN ( 'New York', 'London', 'Paris' );";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var customerTable = analysis.Tables["Customer"];
            var cityCol = customerTable.Columns["City"];
            Assert.IsTrue(cityCol.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(cityCol.FilterValues.Contains("New York"));
            Assert.IsTrue(cityCol.FilterValues.Contains("London"));
            Assert.IsTrue(cityCol.FilterValues.Contains("Paris"));
            Assert.IsTrue(cityCol.FilterOperators.Contains("IN"));
        }

        [TestMethod]
        public void ParseQueryWithBetweenFilter()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE
    'Date'[Year] BETWEEN 2020 AND 2023;";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var dateTable = analysis.Tables["Date"];
            var yearCol = dateTable.Columns["Year"];
            Assert.IsTrue(yearCol.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(yearCol.FilterValues.Any(v => v.Contains("2020") && v.Contains("2023")));
            Assert.IsTrue(yearCol.FilterOperators.Contains("BETWEEN"));
        }

        [TestMethod]
        public void ParseQueryWithCoalesceFilter()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE
    PFCASTCOALESCE ( 'Date'[Year] AS INT ) > COALESCE ( 2020 );";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var dateTable = analysis.Tables["Date"];
            var yearCol = dateTable.Columns["Year"];
            Assert.IsTrue(yearCol.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(yearCol.FilterValues.Contains("2020"));
            Assert.IsTrue(yearCol.FilterOperators.Contains(">"));
        }

        [TestMethod]
        public void ParseQueryWithComparisonOperators()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
WHERE
    'Sales'[Amount] >= 100
    VAND
    'Sales'[Quantity] <> 0;";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var salesTable = analysis.Tables["Sales"];
            Assert.IsTrue(salesTable.Columns["Amount"].FilterOperators.Contains(">="));
            Assert.IsTrue(salesTable.Columns["Quantity"].FilterOperators.Contains("<>"));
        }

        // ==================== CALLBACK DETECTION TESTS ====================

        [TestMethod]
        public void ParseQueryWithCallbackDataId()
        {
            string xmSql = @"SELECT
    'Product'[Color] CALLBACKDATAID,
    COUNT ( )
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var productTable = analysis.Tables["Product"];
            var colorCol = productTable.Columns["Color"];
            Assert.IsTrue(colorCol.HasCallback);
            Assert.AreEqual("CallbackDataID", colorCol.CallbackType);
        }

        [TestMethod]
        public void ParseQueryWithEncodeCallback()
        {
            string xmSql = @"SELECT
    ENCODECALLBACK ( 'Product'[Color] ),
    COUNT ( )
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var productTable = analysis.Tables["Product"];
            var colorCol = productTable.Columns["Color"];
            Assert.IsTrue(colorCol.HasCallback);
        }

        [TestMethod]
        public void AnalysisCallbackTableCount()
        {
            string xmSql = @"SELECT
    'Product'[Color] CALLBACKDATAID,
    'Product'[Name],
    COUNT ( )
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            Assert.AreEqual(1, analysis.CallbackTableCount);
            Assert.IsTrue(analysis.Tables["Product"].HasCallbacks);
        }

        // ==================== TEMP TABLE LINEAGE TESTS ====================

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

        [TestMethod]
        public void ParseQueryWithDefineTable_ResolvesPhysicalTables()
        {
            string xmSql = @"DEFINE TABLE '$TTable1' :=
SELECT
    'Calendar'[Date]
FROM 'Calendar'
WHERE
    'Calendar'[Year] = 2023;
SELECT
    'Sales'[Amount]
FROM 'Sales'
WHERE
    'Sales'[DateKey] ININDEX '$TTable1'[$SemijoinProjection];";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);

            // Calendar should appear as a physical table
            Assert.IsTrue(analysis.Tables.ContainsKey("Calendar"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));

            // Sales[DateKey] should be marked as filtered (via ININDEX)
            Assert.IsTrue(analysis.Tables["Sales"].Columns["DateKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
        }

        [TestMethod]
        public void ParseQueryWithInIndex_MarksColumnAsFilter()
        {
            string xmSql = @"DEFINE TABLE '$TTable4' :=
SELECT
    'Date'[DateKey]
FROM 'Date'
WHERE
    'Date'[Year] = 2023;
SELECT
    'Sales'[Amount]
FROM 'Sales'
WHERE
    'Sales'[DateKey] ININDEX '$TTable4'[$SemijoinProjection];";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var salesTable = analysis.Tables["Sales"];
            Assert.IsTrue(salesTable.Columns.ContainsKey("DateKey"));
            Assert.IsTrue(salesTable.Columns["DateKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
        }

        [TestMethod]
        public void TempTablesShouldNotAppearInAnalysis()
        {
            // Temp tables ($T...) should be resolved to physical tables
            // and should not appear as standalone entries in the analysis tables
            string xmSql = @"SELECT
    'Product'[Color]
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            // No temp tables should be in the tables dictionary
            foreach (var tableName in analysis.Tables.Keys)
            {
                Assert.IsFalse(tableName.StartsWith("$T"), $"Temp table '{tableName}' should not appear in analysis");
            }
        }

        // ==================== SHALLOW RELATION TESTS ====================

        [TestMethod]
        public void ParseQueryWithCreateShallowRelation()
        {
            string xmSql = @"CREATE SHALLOW RELATION 'Rel_SalesDate'
FROM 'Sales'[DateKey]
TO 'Date'[DateKey];
SELECT 'Sales'[Amount]
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(analysis.Relationships.Count >= 1);
            var rel = analysis.Relationships.First(r =>
                r.FromTable == "Sales" && r.ToTable == "Date");
            Assert.AreEqual("DateKey", rel.FromColumn);
            Assert.AreEqual("DateKey", rel.ToColumn);
        }

        [TestMethod]
        public void ParseQueryWithManyToManyShallowRelation()
        {
            string xmSql = @"CREATE SHALLOW RELATION 'Rel_M2M' MANYTOMANY
FROM 'Bridge'[Key]
TO 'Detail'[Key];
SELECT 'Detail'[Value]
FROM 'Detail';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var rel = analysis.Relationships.First(r =>
                r.FromTable == "Bridge" && r.ToTable == "Detail");
            Assert.AreEqual(XmSqlCardinality.ManyToMany, rel.Cardinality);
        }

        [TestMethod]
        public void ParseQueryWithBothDirectionShallowRelation()
        {
            string xmSql = @"CREATE SHALLOW RELATION 'Rel_Both' BOTH
FROM 'Sales'[ProductKey]
TO 'Product'[ProductKey];
SELECT 'Sales'[Amount]
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var rel = analysis.Relationships.First(r =>
                r.FromTable == "Sales" && r.ToTable == "Product");
            Assert.AreEqual(XmSqlCrossFilterDirection.Both, rel.CrossFilterDirection);
        }

        // ==================== MULTIPLE QUERIES / HIT COUNTS ====================

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
        public void RelationshipDeduplication_BothDirections()
        {
            // Same relationship in both directions should be counted as one
            var queries = new[]
            {
                "SELECT 'Sales'[Amount] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];",
                "SELECT 'Product'[Name] FROM 'Product' LEFT OUTER JOIN 'Sales' ON 'Product'[ProductKey]='Sales'[ProductKey];"
            };

            var analysis = _parser.ParseQueries(queries);

            Assert.AreEqual(1, analysis.Relationships.Count, "Reverse relationships should be deduplicated");
            Assert.AreEqual(2, analysis.Relationships.First().HitCount);
        }

        // ==================== SE EVENT METRICS TESTS ====================

        [TestMethod]
        public void ParseQueryWithMetrics_TracksEstimatedRows()
        {
            string xmSql = @"SELECT 'Product'[Color] FROM 'Product';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                EstimatedRows = 50000,
                DurationMs = 15
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            var productTable = analysis.Tables["Product"];
            Assert.AreEqual(50000, productTable.TotalEstimatedRows);
            Assert.AreEqual(50000, productTable.MaxEstimatedRows);
        }

        [TestMethod]
        public void ParseQueryWithMetrics_TracksDuration()
        {
            string xmSql = @"SELECT 'Product'[Color] FROM 'Product';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                DurationMs = 25
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            var productTable = analysis.Tables["Product"];
            Assert.AreEqual(25, productTable.TotalDurationMs);
            Assert.AreEqual(25, productTable.MaxDurationMs);
        }

        [TestMethod]
        public void ParseQueryWithMetrics_TracksCacheHit()
        {
            string xmSql = @"SELECT 'Product'[Color] FROM 'Product';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                IsCacheHit = true,
                DurationMs = 0
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            Assert.AreEqual(1, analysis.CacheHitQueries);
            var productTable = analysis.Tables["Product"];
            Assert.AreEqual(1, productTable.CacheHits);
            Assert.AreEqual(0, productTable.CacheMisses);
        }

        [TestMethod]
        public void ParseQueryWithMetrics_TracksCacheMiss()
        {
            string xmSql = @"SELECT 'Product'[Color] FROM 'Product';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                IsCacheHit = false,
                DurationMs = 10
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            Assert.AreEqual(0, analysis.CacheHitQueries);
            var productTable = analysis.Tables["Product"];
            Assert.AreEqual(0, productTable.CacheHits);
            Assert.AreEqual(1, productTable.CacheMisses);
        }

        [TestMethod]
        public void ParseQueryWithMetrics_TracksParallelism()
        {
            string xmSql = @"SELECT 'Sales'[Amount] FROM 'Sales';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                DurationMs = 100,
                CpuTimeMs = 400,
                CpuFactor = 4.0,
                NetParallelDurationMs = 300
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            var salesTable = analysis.Tables["Sales"];
            Assert.AreEqual(400, salesTable.TotalCpuTimeMs);
            Assert.AreEqual(4.0, salesTable.MaxCpuFactor);
            Assert.AreEqual(1, salesTable.ParallelQueryCount);
            Assert.AreEqual(300, salesTable.TotalParallelDurationMs);
        }

        [TestMethod]
        public void ParseQueryWithMetrics_TracksQueryId()
        {
            string xmSql = @"SELECT 'Sales'[Amount] FROM 'Sales';";
            var analysis = new XmSqlAnalysis();

            var metrics = new XmSqlParser.SeEventMetrics
            {
                QueryId = 42,
                DurationMs = 10
            };

            _parser.ParseQueryWithMetrics(xmSql, analysis, metrics);

            var salesTable = analysis.Tables["Sales"];
            Assert.IsTrue(salesTable.QueryIds.Contains(42));
        }

        [TestMethod]
        public void ParseQueryWithMetrics_ScanEventCount()
        {
            var analysis = new XmSqlAnalysis();

            // Non-cache-hit query should increment ScanEventCount
            var metrics1 = new XmSqlParser.SeEventMetrics { IsCacheHit = false, DurationMs = 10 };
            _parser.ParseQueryWithMetrics("SELECT 'A'[X] FROM 'A';", analysis, metrics1);

            // Cache-hit query should NOT increment ScanEventCount
            var metrics2 = new XmSqlParser.SeEventMetrics { IsCacheHit = true, DurationMs = 0 };
            _parser.ParseQueryWithMetrics("SELECT 'A'[Y] FROM 'A';", analysis, metrics2);

            Assert.AreEqual(1, analysis.ScanEventCount);
            Assert.AreEqual(1, analysis.CacheHitQueries);
            Assert.AreEqual(2, analysis.TotalSEQueriesAnalyzed);
        }

        [TestMethod]
        public void ParseMultipleQueriesWithMetrics_AccumulatesTableMetrics()
        {
            var analysis = new XmSqlAnalysis();

            _parser.ParseQueryWithMetrics(
                "SELECT 'Sales'[Amount] FROM 'Sales';",
                analysis,
                new XmSqlParser.SeEventMetrics { EstimatedRows = 1000, DurationMs = 10 });

            _parser.ParseQueryWithMetrics(
                "SELECT 'Sales'[Quantity] FROM 'Sales';",
                analysis,
                new XmSqlParser.SeEventMetrics { EstimatedRows = 5000, DurationMs = 50 });

            var salesTable = analysis.Tables["Sales"];
            Assert.AreEqual(6000, salesTable.TotalEstimatedRows);
            Assert.AreEqual(5000, salesTable.MaxEstimatedRows);
            Assert.AreEqual(60, salesTable.TotalDurationMs);
            Assert.AreEqual(50, salesTable.MaxDurationMs);
        }

        // ==================== DIRECTQUERY SQL TESTS ====================

        [TestMethod]
        public void ParseDirectQuerySql_SimpleQuery()
        {
            string sql = @"SELECT
    [t4].[Color] AS [c10],
    COUNT_BIG(DISTINCT [t4].[ProductKey]) AS [a0]
FROM
    (
    select [$Table].[ProductKey] as [ProductKey],[$Table].[Color] as [Color]
    from [dbo].[DimProduct] as [$Table]
    )
    AS [t4]
GROUP BY [t4].[Color]";

            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 50, QueryId = 1 };

            var result = _parser.ParseDirectQuerySql(sql, analysis, metrics);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("DimProduct"));
            Assert.AreEqual(1, analysis.DirectQueryEventCount);
            Assert.AreEqual(50, analysis.TotalDirectQueryDurationMs);
        }

        [TestMethod]
        public void ParseDirectQuerySql_EmptyReturns_False()
        {
            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics();

            Assert.IsFalse(_parser.ParseDirectQuerySql("", analysis, metrics));
            Assert.IsFalse(_parser.ParseDirectQuerySql(null, analysis, metrics));
        }

        [TestMethod]
        public void ParseDirectQuerySql_TracksDirectQueryDuration()
        {
            string sql = @"SELECT [t4].[Name] AS [c1]
FROM (select [$Table].[Name] as [Name] from [dbo].[Product] as [$Table]) AS [t4]";

            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 200 };

            _parser.ParseDirectQuerySql(sql, analysis, metrics);

            Assert.AreEqual(200, analysis.TotalDirectQueryDurationMs);
            Assert.AreEqual(1, analysis.DirectQueryEventCount);
        }

        [TestMethod]
        public void ParseDirectQuerySql_WithJoin_ExtractsRelationship()
        {
            string sql = @"SELECT
    [t4].[ProductName] AS [c10],
    SUM([t7].[Amount]) AS [a0]
FROM
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[ProductName] as [ProductName] from [dbo].[Product] as [$Table]) AS [t4]
    INNER JOIN
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[Amount] as [Amount] from [dbo].[Sales] as [$Table]) AS [t7]
    ON ([t4].[ProductKey] = [t7].[ProductKey])
GROUP BY [t4].[ProductName]";

            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 100 };

            var result = _parser.ParseDirectQuerySql(sql, analysis, metrics);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            Assert.IsTrue(analysis.Relationships.Count >= 1);

            var rel = analysis.Relationships.First();
            Assert.AreEqual("ProductKey", rel.FromColumn);
            Assert.AreEqual("ProductKey", rel.ToColumn);
        }

        // ==================== ANALYSIS SUMMARY PROPERTIES ====================

        [TestMethod]
        public void AnalysisSummaryProperties()
        {
            string xmSql1 = @"SELECT 'Sales'[Amount], 'Product'[Color]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey];";

            string xmSql2 = @"SELECT 'Customer'[Name]
FROM 'Customer';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql1, analysis);
            _parser.ParseQuery(xmSql2, analysis);

            Assert.AreEqual(3, analysis.UniqueTablesCount);
            Assert.AreEqual(1, analysis.UniqueRelationshipsCount);
            Assert.AreEqual(2, analysis.TotalSEQueriesAnalyzed);
            Assert.AreEqual(2, analysis.SuccessfullyParsedQueries);
            Assert.AreEqual(0, analysis.FailedParseQueries);
        }

        [TestMethod]
        public void AnalysisClear_ResetsEverything()
        {
            string xmSql = @"SELECT 'Product'[Color] FROM 'Product';";
            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            Assert.AreEqual(1, analysis.Tables.Count);

            analysis.Clear();

            Assert.AreEqual(0, analysis.Tables.Count);
            Assert.AreEqual(0, analysis.Relationships.Count);
            Assert.AreEqual(0, analysis.TotalSEQueriesAnalyzed);
            Assert.AreEqual(0, analysis.SuccessfullyParsedQueries);
            Assert.AreEqual(0, analysis.FailedParseQueries);
            Assert.AreEqual(0, analysis.CacheHitQueries);
        }

        [TestMethod]
        public void AnalysisUniqueColumnsCount()
        {
            var queries = new[]
            {
                "SELECT 'Product'[Color], 'Product'[Size] FROM 'Product';",
                "SELECT 'Sales'[Amount] FROM 'Sales';"
            };

            var analysis = _parser.ParseQueries(queries);

            // Product has Color + Size, Sales has Amount = 3 total
            Assert.AreEqual(3, analysis.UniqueColumnsCount);
        }

        // ==================== COLUMN USAGE FLAGS ====================

        [TestMethod]
        public void ColumnCanHaveMultipleUsageTypes()
        {
            // A column can be used in SELECT, JOIN, and FILTER simultaneously
            string xmSql = @"SELECT 'Sales'[ProductKey]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE
    'Sales'[ProductKey] = 42;";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var col = analysis.Tables["Sales"].Columns["ProductKey"];
            Assert.IsTrue(col.UsageTypes.HasFlag(XmSqlColumnUsage.Select), "Should be marked as Select");
            Assert.IsTrue(col.UsageTypes.HasFlag(XmSqlColumnUsage.Join), "Should be marked as Join");
            Assert.IsTrue(col.UsageTypes.HasFlag(XmSqlColumnUsage.Filter), "Should be marked as Filter");
        }

        [TestMethod]
        public void TableJoinColumns_ReturnsOnlyJoinColumns()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE
    'Sales'[Amount] > 100;";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var salesTable = analysis.Tables["Sales"];
            var joinColumns = salesTable.JoinColumns.ToList();
            Assert.AreEqual(1, joinColumns.Count);
            Assert.AreEqual("ProductKey", joinColumns.First().ColumnName);
        }

        [TestMethod]
        public void TableFilteredColumns_ReturnsOnlyFilteredColumns()
        {
            string xmSql = @"SELECT 'Product'[Name]
FROM 'Product'
WHERE
    'Product'[Category] = 'Bikes'
    VAND
    'Product'[Color] = 'Red';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var productTable = analysis.Tables["Product"];
            var filteredColumns = productTable.FilteredColumns.ToList();
            Assert.AreEqual(2, filteredColumns.Count);
            Assert.IsTrue(filteredColumns.Any(c => c.ColumnName == "Category"));
            Assert.IsTrue(filteredColumns.Any(c => c.ColumnName == "Color"));
        }

        [TestMethod]
        public void TableSelectedColumns_ReturnsOnlySelectedColumns()
        {
            string xmSql = @"SELECT 'Product'[Name], 'Product'[Size]
FROM 'Product'
WHERE
    'Product'[Category] = 'Bikes';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            var productTable = analysis.Tables["Product"];
            var selectedColumns = productTable.SelectedColumns.ToList();
            Assert.AreEqual(2, selectedColumns.Count);
            Assert.IsTrue(selectedColumns.Any(c => c.ColumnName == "Name"));
            Assert.IsTrue(selectedColumns.Any(c => c.ColumnName == "Size"));
        }

        // ==================== EDGE CASES ====================

        [TestMethod]
        public void ParseWhitespaceOnlyQuery_ReturnsFalse()
        {
            var analysis = new XmSqlAnalysis();
            Assert.IsFalse(_parser.ParseQuery("   \t\n  ", analysis));
        }

        [TestMethod]
        public void ParseQueryWithTableNamesContainingSpaces()
        {
            string xmSql = @"SELECT 'Sales Territory'[Territory Name]
FROM 'Sales Territory';";

            var analysis = new XmSqlAnalysis();
            _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(analysis.Tables.ContainsKey("Sales Territory"));
            Assert.IsTrue(analysis.Tables["Sales Territory"].Columns.ContainsKey("Territory Name"));
        }

        [TestMethod]
        public void GetOrAddTable_CaseInsensitive()
        {
            var analysis = new XmSqlAnalysis();
            var table1 = analysis.GetOrAddTable("Product");
            var table2 = analysis.GetOrAddTable("product");
            var table3 = analysis.GetOrAddTable("PRODUCT");

            Assert.AreSame(table1, table2);
            Assert.AreSame(table1, table3);
            Assert.AreEqual(1, analysis.Tables.Count);
        }

        [TestMethod]
        public void GetOrAddColumn_CaseInsensitive()
        {
            var table = new XmSqlTableInfo("Test");
            var col1 = table.GetOrAddColumn("Color");
            var col2 = table.GetOrAddColumn("color");
            var col3 = table.GetOrAddColumn("COLOR");

            Assert.AreSame(col1, col2);
            Assert.AreSame(col1, col3);
            Assert.AreEqual(1, table.Columns.Count);
        }

        [TestMethod]
        public void GetOrAddTable_ReturnsNullForEmpty()
        {
            var analysis = new XmSqlAnalysis();
            Assert.IsNull(analysis.GetOrAddTable(""));
            Assert.IsNull(analysis.GetOrAddTable(null));
            Assert.IsNull(analysis.GetOrAddTable("   "));
        }

        [TestMethod]
        public void GetOrAddColumn_ReturnsNullForEmpty()
        {
            var table = new XmSqlTableInfo("Test");
            Assert.IsNull(table.GetOrAddColumn(""));
            Assert.IsNull(table.GetOrAddColumn(null));
            Assert.IsNull(table.GetOrAddColumn("   "));
        }

        [TestMethod]
        public void FilterValueLimit_DoesNotExceed50()
        {
            var column = new XmSqlColumnInfo("TestCol");

            // Add 60 unique filter values
            for (int i = 0; i < 60; i++)
            {
                column.AddFilterValue($"Value_{i}", "=");
            }

            Assert.AreEqual(50, column.FilterValues.Count, "Filter values should be limited to 50");
        }

        [TestMethod]
        public void FilterValueDuplicates_AreNotAdded()
        {
            var column = new XmSqlColumnInfo("TestCol");

            column.AddFilterValue("SameValue", "=");
            column.AddFilterValue("SameValue", "=");
            column.AddFilterValue("SameValue", "=");

            Assert.AreEqual(1, column.FilterValues.Count, "Duplicate filter values should not be added");
        }

        [TestMethod]
        public void RelationshipKey_FormatsCorrectly()
        {
            var rel = new XmSqlRelationship
            {
                FromTable = "Sales",
                FromColumn = "ProductKey",
                ToTable = "Product",
                ToColumn = "ProductKey"
            };

            Assert.AreEqual("Sales.ProductKey->Product.ProductKey", rel.Key);
        }

        [TestMethod]
        public void AddAggregation_SetsAggregateUsageFlag()
        {
            var column = new XmSqlColumnInfo("Amount");
            column.AddAggregation("SUM");

            Assert.IsTrue(column.UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate));
            Assert.IsTrue(column.AggregationTypes.Contains("SUM"));
        }

        [TestMethod]
        public void AddAggregation_NormalizesToUpperCase()
        {
            var column = new XmSqlColumnInfo("Amount");
            column.AddAggregation("sum");
            column.AddAggregation("Sum");
            column.AddAggregation("SUM");

            Assert.AreEqual(1, column.AggregationTypes.Count, "Aggregation types should be case-insensitive");
            Assert.IsTrue(column.AggregationTypes.Contains("SUM"));
        }

        // ==================== TRUNCATED IN CLAUSE TESTS ====================

        [TestMethod]
        public void ParseQueryWithTruncatedInClause_ExtractsTotalCount()
        {
            // Arrange - exact query from user testing with truncated IN clause
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
    'Date'[Date],
    'Geography'[Country Region Code],
    SUM ( 'Internet Sales'[Sales Amount] )
FROM 'Internet Sales'
    LEFT OUTER JOIN 'Date'
        ON 'Internet Sales'[Order Date]='Date'[Date]
    LEFT OUTER JOIN 'Customer'
        ON 'Internet Sales'[Customer Id]='Customer'[Customer Id]
    LEFT OUTER JOIN 'Geography'
        ON 'Customer'[Geography Id]='Geography'[Geography Id]
WHERE
    'Date'[Date] IN ( 40593.000000, 40174.000000, 38732.000000, 40556.000000, 40137.000000, 41998.000000, 40100.000000, 39681.000000, 41542.000000, 41961.000000..[3,652 total values, not all displayed] ) ;";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Date"));
            
            var dateTable = analysis.Tables["Date"];
            Assert.IsTrue(dateTable.Columns.ContainsKey("Date"));
            
            var dateColumn = dateTable.Columns["Date"];
            Assert.IsTrue(dateColumn.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            
            // The key assertion: TotalFilterValueCount should be 3652, not the number of comma-separated values
            Assert.AreEqual(3652, dateColumn.TotalFilterValueCount, 
                "TotalFilterValueCount should be extracted from the '[3,652 total values, not all displayed]' indicator");
            
            // FilterValues should contain some sample values (limited to 20 max)
            Assert.IsTrue(dateColumn.FilterValues.Count > 0);
            Assert.IsTrue(dateColumn.FilterValues.Count <= 20, "FilterValues should be limited to 20 for display");
            
            // Check that IN operator is recorded
            Assert.IsTrue(dateColumn.FilterOperators.Contains("IN"));
        }

        [TestMethod]
        public void ParseQueryWithChainedJoins_AllRelationshipsCaptured()
        {
            // Arrange - chained join: Internet Sales → Date, Internet Sales → Customer, Customer → Geography
            // The 3rd join (Customer → Geography) does NOT involve the FROM table directly.
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
    'Date'[Date],
    'Geography'[Country Region Code],
    SUM ( 'Internet Sales'[Sales Amount] )
FROM 'Internet Sales'
    LEFT OUTER JOIN 'Date'
        ON 'Internet Sales'[Order Date]='Date'[Date]
    LEFT OUTER JOIN 'Customer'
        ON 'Internet Sales'[Customer Id]='Customer'[Customer Id]
    LEFT OUTER JOIN 'Geography'
        ON 'Customer'[Geography Id]='Geography'[Geography Id]
WHERE
    'Date'[Date] IN ( 40593.000000, 40174.000000..[3,652 total values, not all displayed] ) ;";

            var analysis = new XmSqlAnalysis();

            // Act - use ParseQueryWithMetrics with QueryId=8 to match the event RowNumber
            var result = _parser.ParseQueryWithMetrics(xmSql, analysis, new XmSqlParser.SeEventMetrics
            {
                QueryId = 8,
                EstimatedRows = 4940,
                DurationMs = 6,
                IsCacheHit = false,
                CpuTimeMs = 0
            });

            // Assert
            Assert.IsTrue(result);

            // Should have 4 tables: Internet Sales (FROM), Date, Customer, Geography (all JOINed)
            Assert.AreEqual(4, analysis.Tables.Count, "Expected 4 tables");
            Assert.IsTrue(analysis.Tables.ContainsKey("Internet Sales"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Date"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Geography"));

            // Should have 3 relationships including the chained Customer→Geography
            Assert.AreEqual(3, analysis.Relationships.Count, 
                "Expected 3 relationships: Internet Sales→Date, Internet Sales→Customer, Customer→Geography");

            // Verify each relationship exists
            var relInternetSalesDate = analysis.Relationships.FirstOrDefault(r =>
                r.FromTable == "Internet Sales" && r.ToTable == "Date");
            Assert.IsNotNull(relInternetSalesDate, "Missing relationship: Internet Sales → Date");
            Assert.AreEqual("Order Date", relInternetSalesDate.FromColumn);
            Assert.AreEqual("Date", relInternetSalesDate.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, relInternetSalesDate.JoinType);

            var relInternetSalesCustomer = analysis.Relationships.FirstOrDefault(r =>
                r.FromTable == "Internet Sales" && r.ToTable == "Customer");
            Assert.IsNotNull(relInternetSalesCustomer, "Missing relationship: Internet Sales → Customer");
            Assert.AreEqual("Customer Id", relInternetSalesCustomer.FromColumn);
            Assert.AreEqual("Customer Id", relInternetSalesCustomer.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, relInternetSalesCustomer.JoinType);

            var relCustomerGeography = analysis.Relationships.FirstOrDefault(r =>
                r.FromTable == "Customer" && r.ToTable == "Geography");
            Assert.IsNotNull(relCustomerGeography, "Missing relationship: Customer → Geography");
            Assert.AreEqual("Geography Id", relCustomerGeography.FromColumn);
            Assert.AreEqual("Geography Id", relCustomerGeography.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, relCustomerGeography.JoinType);

            // Verify join columns are marked on all participating tables
            Assert.IsTrue(analysis.Tables["Internet Sales"].Columns["Order Date"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Internet Sales"].Columns["Customer Id"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Customer"].Columns["Customer Id"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Customer"].Columns["Geography Id"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Geography"].Columns["Geography Id"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Date"].Columns["Date"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));

            // Verify that ALL tables (including joined tables) have QueryId tracked
            // This is critical for the ERD event filter: tables must have the QueryId
            // so they appear in "detail pane" output for that event
            Assert.IsTrue(analysis.Tables["Internet Sales"].QueryIds.Contains(8), 
                "FROM table 'Internet Sales' should have QueryId 8");
            Assert.IsTrue(analysis.Tables["Date"].QueryIds.Contains(8), 
                "Joined table 'Date' should have QueryId 8");
            Assert.IsTrue(analysis.Tables["Customer"].QueryIds.Contains(8), 
                "Joined table 'Customer' should have QueryId 8");
            Assert.IsTrue(analysis.Tables["Geography"].QueryIds.Contains(8), 
                "Chained joined table 'Geography' should have QueryId 8");
        }

        [TestMethod]
        public void ParseQueryWithTruncatedInClause_LargeCount()
        {
            // Test with a larger count (over 10,000)
            string xmSql = @"SELECT 'Product'[ProductKey]
FROM 'Product'
WHERE
    'Product'[ProductKey] IN ( 1, 2, 3, 4, 5..[12,345 total values, not all displayed] ) ;";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            var productTable = analysis.Tables["Product"];
            var productKeyCol = productTable.Columns["ProductKey"];
            
            Assert.AreEqual(12345, productKeyCol.TotalFilterValueCount);
        }

        [TestMethod]
        public void ParseQueryWithNonTruncatedInClause_CountsActualValues()
        {
            // Test with a non-truncated IN clause (no "[X total values]" indicator)
            string xmSql = @"SELECT 'Product'[Name]
FROM 'Product'
WHERE
    'Product'[Color] IN ( 'Red', 'Blue', 'Green', 'Yellow', 'Black' ) ;";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            var productTable = analysis.Tables["Product"];
            var colorCol = productTable.Columns["Color"];
            
            // TotalFilterValueCount should equal the number of values added
            Assert.AreEqual(5, colorCol.TotalFilterValueCount, 
                "TotalFilterValueCount should equal the number of actual values when no truncation indicator");
            Assert.AreEqual(5, colorCol.FilterValues.Count);
            Assert.IsTrue(colorCol.FilterValues.Contains("Red"));
            Assert.IsTrue(colorCol.FilterValues.Contains("Blue"));
            Assert.IsTrue(colorCol.FilterValues.Contains("Green"));
            Assert.IsTrue(colorCol.FilterValues.Contains("Yellow"));
            Assert.IsTrue(colorCol.FilterValues.Contains("Black"));
        }

        [TestMethod]
        public void ParseQueryWithTruncatedInClause_SampleValuesExtracted()
        {
            // Test that sample values are correctly extracted before the truncation indicator
            string xmSql = @"SELECT 'Customer'[Name]
FROM 'Customer'
WHERE
    'Customer'[City] IN ( 'New York', 'Los Angeles', 'Chicago', 'Houston'..[500 total values, not all displayed] ) ;";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);
            var customerTable = analysis.Tables["Customer"];
            var cityCol = customerTable.Columns["City"];
            
            Assert.AreEqual(500, cityCol.TotalFilterValueCount);
            // Should have extracted the sample values before the truncation
            Assert.IsTrue(cityCol.FilterValues.Contains("New York"));
            Assert.IsTrue(cityCol.FilterValues.Contains("Los Angeles"));
            Assert.IsTrue(cityCol.FilterValues.Contains("Chicago"));
            Assert.IsTrue(cityCol.FilterValues.Contains("Houston"));
        }
    }
}
