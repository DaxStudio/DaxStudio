using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Tests for the ANTLR-based xmSQL parser.
    /// These mirror the tests in XmSqlParserTests but use AntlrXmSqlParser instead.
    /// </summary>
    [TestClass]
    public class AntlrXmSqlParserTests
    {
        private IXmSqlParser _parser;

        [TestInitialize]
        public void Setup()
        {
            _parser = new AntlrXmSqlParser();
        }

        // ==================== BASIC SELECT / FROM ====================

        [TestMethod]
        public void Antlr_ParseSimpleSelectFromQuery()
        {
            string xmSql = @"SET DC_KIND=""AUTO"";
SELECT
    'Product'[Color],
    'Product'[Class]
FROM 'Product';";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

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

            Assert.IsTrue(productTable.Columns["Color"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
            Assert.IsTrue(productTable.Columns["Class"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
        }

        [TestMethod]
        public void Antlr_ParseEmptyQuery_ReturnsFalse()
        {
            var analysis = new XmSqlAnalysis();
            Assert.IsFalse(_parser.ParseQuery("", analysis));
        }

        [TestMethod]
        public void Antlr_ParseNullQuery_ReturnsFalse()
        {
            var analysis = new XmSqlAnalysis();
            Assert.IsFalse(_parser.ParseQuery(null, analysis));
        }

        // ==================== JOIN TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithLeftOuterJoin()
        {
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
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            Assert.AreEqual(3, analysis.Tables.Count);
            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Geography"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales Territory"));

            var customerTable = analysis.Tables["Customer"];
            Assert.IsTrue(customerTable.IsFromTable);
            Assert.IsTrue(customerTable.Columns.ContainsKey("CustomerKey"));
            Assert.IsTrue(customerTable.Columns.ContainsKey("Full Name"));
            Assert.IsTrue(customerTable.Columns.ContainsKey("Education"));

            Assert.IsTrue(customerTable.Columns["Full Name"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(customerTable.Columns["Education"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));

            var geographyTable = analysis.Tables["Geography"];
            Assert.IsTrue(geographyTable.IsJoinedTable);

            var salesTerritoryTable = analysis.Tables["Sales Territory"];
            Assert.IsTrue(salesTerritoryTable.IsJoinedTable);
        }

        [TestMethod]
        public void Antlr_ParseQueryWithRelationship()
        {
            string xmSql = @"SELECT 'Sales'[Amount]
FROM 'Sales'
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
WHERE 'Product'[Category] = 'Bikes';";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            Assert.AreEqual(1, analysis.Relationships.Count);

            var relationship = analysis.Relationships.First();
            Assert.AreEqual("Sales", relationship.FromTable);
            Assert.AreEqual("ProductKey", relationship.FromColumn);
            Assert.AreEqual("Product", relationship.ToTable);
            Assert.AreEqual("ProductKey", relationship.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, relationship.JoinType);

            Assert.IsTrue(analysis.Tables["Sales"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
        }

        // ==================== AGGREGATION TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithAggregation()
        {
            string xmSql = @"SELECT
    'Product'[Category],
    SUM ( 'Sales'[Amount] ),
    COUNT ( 'Sales'[OrderKey] ),
    DCOUNT ( 'Customer'[CustomerKey] )
FROM 'Sales';";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            var salesTable = analysis.Tables["Sales"];

            Assert.IsTrue(salesTable.Columns.ContainsKey("Amount"));
            Assert.IsTrue(salesTable.Columns["Amount"].UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate));
            Assert.IsTrue(salesTable.Columns["Amount"].AggregationTypes.Contains("SUM"));

            Assert.IsTrue(salesTable.Columns.ContainsKey("OrderKey"));
            Assert.IsTrue(salesTable.Columns["OrderKey"].AggregationTypes.Contains("COUNT"));

            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            Assert.IsTrue(analysis.Tables["Customer"].Columns["CustomerKey"].AggregationTypes.Contains("DCOUNT"));
        }

        // ==================== WITH EXPRESSION TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithWithExpression()
        {
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
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            var salesTable = analysis.Tables["Sales"];

            Assert.IsTrue(salesTable.Columns.ContainsKey("Quantity"), "Quantity column should be found from WITH expression");
            Assert.IsTrue(salesTable.Columns["Quantity"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression));

            Assert.IsTrue(salesTable.Columns.ContainsKey("Net Price"), "Net Price column should be found from WITH expression");
            Assert.IsTrue(salesTable.Columns["Net Price"].UsageTypes.HasFlag(XmSqlColumnUsage.Expression));

            Assert.IsTrue(salesTable.Columns.ContainsKey("StoreKey"), "StoreKey should be in SELECT");
            Assert.IsTrue(salesTable.Columns["StoreKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
        }

        // ==================== FILTER VALUE TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithEqualityFilter()
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
        public void Antlr_ParseQueryWithInFilter()
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
        public void Antlr_ParseQueryWithBetweenFilter()
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

        // ==================== CALLBACK TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithCallbackDataId()
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

        // ==================== TEMP TABLE LINEAGE TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithDefineTable_ResolvesPhysicalTables()
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
            Assert.IsTrue(analysis.Tables.ContainsKey("Calendar"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            Assert.IsTrue(analysis.Tables["Sales"].Columns["DateKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
        }

        // ==================== SHALLOW RELATION TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithCreateShallowRelation()
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
        public void Antlr_ParseQueryWithManyToManyShallowRelation()
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
        public void Antlr_ParseQueryWithBothDirectionShallowRelation()
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

        // ==================== MULTIPLE QUERIES ====================

        [TestMethod]
        public void Antlr_ParseMultipleQueries()
        {
            var queries = new[]
            {
                "SELECT 'Product'[Color] FROM 'Product';",
                "SELECT 'Product'[Size], 'Customer'[Name] FROM 'Sales' LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey]='Product'[ProductKey];",
                "SELECT 'Product'[Color] FROM 'Product' WHERE 'Product'[Category] = 'Bikes';"
            };

            var analysis = _parser.ParseQueries(queries);

            Assert.AreEqual(3, analysis.TotalSEQueriesAnalyzed);
            Assert.AreEqual(3, analysis.SuccessfullyParsedQueries);
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));
            var productTable = analysis.Tables["Product"];
            Assert.IsTrue(productTable.Columns.ContainsKey("Color"));
            Assert.IsTrue(productTable.Columns.ContainsKey("Size"));
            Assert.IsTrue(productTable.Columns.ContainsKey("Category"));
            Assert.IsTrue(productTable.Columns["Color"].HitCount >= 2);
        }

        // ==================== METRICS TESTS ====================

        [TestMethod]
        public void Antlr_ParseQueryWithMetrics_TracksCacheHit()
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
        public void Antlr_ParseQueryWithMetrics_ScanEventCount()
        {
            var analysis = new XmSqlAnalysis();

            var metrics1 = new XmSqlParser.SeEventMetrics { IsCacheHit = false, DurationMs = 10 };
            _parser.ParseQueryWithMetrics("SELECT 'A'[X] FROM 'A';", analysis, metrics1);

            var metrics2 = new XmSqlParser.SeEventMetrics { IsCacheHit = true, DurationMs = 0 };
            _parser.ParseQueryWithMetrics("SELECT 'A'[Y] FROM 'A';", analysis, metrics2);

            Assert.AreEqual(1, analysis.ScanEventCount);
            Assert.AreEqual(1, analysis.CacheHitQueries);
            Assert.AreEqual(2, analysis.TotalSEQueriesAnalyzed);
        }

        // ==================== DIRECTQUERY TESTS ====================

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_SimpleQuery()
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
        public void Antlr_ParseDirectQuerySql_EmptyReturns_False()
        {
            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics();

            Assert.IsFalse(_parser.ParseDirectQuerySql("", analysis, metrics));
            Assert.IsFalse(_parser.ParseDirectQuerySql(null, analysis, metrics));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_ExtractsColumnsAndAggregations()
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
            _parser.ParseDirectQuerySql(sql, analysis, metrics);

            var table = analysis.Tables["DimProduct"];

            // Color should be in SELECT and GROUP BY
            Assert.IsTrue(table.Columns.ContainsKey("Color"), "Color column should exist");
            Assert.IsTrue(table.Columns["Color"].UsageTypes.HasFlag(XmSqlColumnUsage.GroupBy), "Color should be GroupBy");

            // ProductKey should be aggregated with DISTINCT
            Assert.IsTrue(table.Columns.ContainsKey("ProductKey"), "ProductKey column should exist");
            Assert.IsTrue(table.Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate), "ProductKey should be Aggregate");
            Assert.IsTrue(table.Columns["ProductKey"].AggregationTypes.Contains("DISTINCT"), "Should be DISTINCT aggregation");
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_WithJoin_ExtractsRelationshipAndColumns()
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

            // Relationship
            Assert.IsTrue(analysis.Relationships.Count >= 1);
            var rel = analysis.Relationships.First();
            Assert.AreEqual("ProductKey", rel.FromColumn);
            Assert.AreEqual("ProductKey", rel.ToColumn);

            // Join columns
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Sales"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));

            // ProductName in SELECT + GROUP BY
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductName"].UsageTypes.HasFlag(XmSqlColumnUsage.GroupBy));

            // Amount aggregated with SUM
            Assert.IsTrue(analysis.Tables["Sales"].Columns["Amount"].UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate));
            Assert.IsTrue(analysis.Tables["Sales"].Columns["Amount"].AggregationTypes.Contains("SUM"));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_TracksDirectQueryDuration()
        {
            string sql = @"SELECT [t4].[Name] AS [c1]
FROM (select [$Table].[Name] as [Name] from [dbo].[Product] as [$Table]) AS [t4]";

            var analysis = new XmSqlAnalysis();
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 200 };
            _parser.ParseDirectQuerySql(sql, analysis, metrics);

            Assert.AreEqual(200, analysis.TotalDirectQueryDurationMs);
            Assert.AreEqual(1, analysis.DirectQueryEventCount);

            // Column should be extracted
            Assert.IsTrue(analysis.Tables["Product"].Columns.ContainsKey("Name"));
            Assert.IsTrue(analysis.Tables["Product"].Columns["Name"].UsageTypes.HasFlag(XmSqlColumnUsage.Select));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_WhereClause_EqualityFilter()
        {
            string sql = @"SELECT
    [t4].[ProductName] AS [c10]
FROM
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[ProductName] as [ProductName],[$Table].[Category] as [Category] from [dbo].[Product] as [$Table]) AS [t4]
WHERE
    [t4].[Category] = 'Electronics'";

            var analysis = new XmSqlAnalysis();
            _parser.ParseDirectQuerySql(sql, analysis, null);

            Assert.IsTrue(analysis.Tables["Product"].Columns.ContainsKey("Category"));
            var catCol = analysis.Tables["Product"].Columns["Category"];
            Assert.IsTrue(catCol.UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(catCol.FilterValues.Contains("Electronics"));
            Assert.IsTrue(catCol.FilterOperators.Contains("="));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_WhereClause_MultipleConditions()
        {
            string sql = @"SELECT
    [t4].[Name] AS [c10],
    SUM([t4].[Amount]) AS [a0]
FROM
    (select [$Table].[Name] as [Name],[$Table].[Amount] as [Amount],[$Table].[Year] as [Year],[$Table].[Region] as [Region] from [dbo].[Sales] as [$Table]) AS [t4]
WHERE
    [t4].[Year] = 2023 AND [t4].[Region] = 'North'
GROUP BY [t4].[Name]";

            var analysis = new XmSqlAnalysis();
            _parser.ParseDirectQuerySql(sql, analysis, null);

            var sales = analysis.Tables["Sales"];
            Assert.IsTrue(sales.Columns["Year"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(sales.Columns["Year"].FilterValues.Contains("2023"));

            Assert.IsTrue(sales.Columns["Region"].UsageTypes.HasFlag(XmSqlColumnUsage.Filter));
            Assert.IsTrue(sales.Columns["Region"].FilterValues.Contains("North"));

            Assert.IsTrue(sales.Columns["Amount"].UsageTypes.HasFlag(XmSqlColumnUsage.Aggregate));
            Assert.IsTrue(sales.Columns["Name"].UsageTypes.HasFlag(XmSqlColumnUsage.GroupBy));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_MultipleAggregates()
        {
            string sql = @"SELECT
    [t4].[Category] AS [c10],
    SUM([t4].[Amount]) AS [a0],
    COUNT([t4].[OrderId]) AS [a1],
    AVG([t4].[Price]) AS [a2],
    MIN([t4].[OrderDate]) AS [a3],
    MAX([t4].[OrderDate]) AS [a4]
FROM
    (select [$Table].[Category] as [Category],[$Table].[Amount] as [Amount],[$Table].[OrderId] as [OrderId],[$Table].[Price] as [Price],[$Table].[OrderDate] as [OrderDate] from [dbo].[Sales] as [$Table]) AS [t4]
GROUP BY [t4].[Category]";

            var analysis = new XmSqlAnalysis();
            _parser.ParseDirectQuerySql(sql, analysis, null);

            var sales = analysis.Tables["Sales"];

            Assert.IsTrue(sales.Columns["Amount"].AggregationTypes.Contains("SUM"));
            Assert.IsTrue(sales.Columns["OrderId"].AggregationTypes.Contains("COUNT"));
            Assert.IsTrue(sales.Columns["Price"].AggregationTypes.Contains("AVG"));
            Assert.IsTrue(sales.Columns["OrderDate"].AggregationTypes.Contains("MIN"));
            Assert.IsTrue(sales.Columns["OrderDate"].AggregationTypes.Contains("MAX"));
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_DirectTableReference()
        {
            string sql = @"SELECT
    [t4].[Name] AS [c10]
FROM
    [dbo].[Customer] AS [t4]";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseDirectQuerySql(sql, analysis, null);

            Assert.IsTrue(result);
            Assert.IsTrue(analysis.Tables.ContainsKey("Customer"));
            Assert.IsTrue(analysis.Tables["Customer"].IsFromTable);
        }

        [TestMethod]
        public void Antlr_ParseDirectQuerySql_ThreeWayJoin()
        {
            string sql = @"SELECT
    [t4].[ProductName] AS [c10],
    [t8].[StoreName] AS [c11],
    SUM([t7].[Amount]) AS [a0]
FROM
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[ProductName] as [ProductName] from [dbo].[Product] as [$Table]) AS [t4]
    INNER JOIN
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[StoreKey] as [StoreKey],[$Table].[Amount] as [Amount] from [dbo].[Sales] as [$Table]) AS [t7]
    ON ([t4].[ProductKey] = [t7].[ProductKey])
    INNER JOIN
    (select [$Table].[StoreKey] as [StoreKey],[$Table].[StoreName] as [StoreName] from [dbo].[Store] as [$Table]) AS [t8]
    ON ([t7].[StoreKey] = [t8].[StoreKey])
GROUP BY [t4].[ProductName], [t8].[StoreName]";

            var analysis = new XmSqlAnalysis();
            _parser.ParseDirectQuerySql(sql, analysis, null);

            Assert.AreEqual(3, analysis.Tables.Count);
            Assert.IsTrue(analysis.Tables.ContainsKey("Product"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Sales"));
            Assert.IsTrue(analysis.Tables.ContainsKey("Store"));

            Assert.AreEqual(2, analysis.Relationships.Count);

            // Join columns
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Sales"].Columns["ProductKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Sales"].Columns["StoreKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));
            Assert.IsTrue(analysis.Tables["Store"].Columns["StoreKey"].UsageTypes.HasFlag(XmSqlColumnUsage.Join));

            // Aggregation
            Assert.IsTrue(analysis.Tables["Sales"].Columns["Amount"].AggregationTypes.Contains("SUM"));

            // GROUP BY
            Assert.IsTrue(analysis.Tables["Product"].Columns["ProductName"].UsageTypes.HasFlag(XmSqlColumnUsage.GroupBy));
            Assert.IsTrue(analysis.Tables["Store"].Columns["StoreName"].UsageTypes.HasFlag(XmSqlColumnUsage.GroupBy));
        }

        // ==================== TRAILING SPACE IN TABLE NAME ====================

        [TestMethod]
        public void Antlr_ParseLeftOuterJoin_TableNameWithTrailingSpace_RelationshipMatchesTable()
        {
            // Arrange - reproduces real xmSQL where table names have trailing spaces inside quotes
            string xmSql = @"SET DC_KIND=""C64"";
SELECT
    'TI_Mime'[CALENDAR_ID],
    'TI_Mime'[PK_DATE],
    'TI_Mime'[Period],
    'Period Definition ( SISO ) '[Period]
FROM 'TI_Mime'
    LEFT OUTER JOIN 'Period Definition ( SISO ) '
        ON 'TI_Mime'[Period]='Period Definition ( SISO ) '[Period]
WHERE
    'TI_Mime'[CALENDAR_NAME] = 'Aligned';";

            var analysis = new XmSqlAnalysis();

            // Act
            var result = _parser.ParseQuery(xmSql, analysis);

            // Assert
            Assert.IsTrue(result);

            // Both tables should be found (trimmed names)
            Assert.AreEqual(2, analysis.Tables.Count, "Should have 2 tables");
            Assert.IsTrue(analysis.Tables.ContainsKey("TI_Mime"), "Should have TI_Mime table");
            Assert.IsTrue(analysis.Tables.ContainsKey("Period Definition ( SISO )"), "Should have Period Definition ( SISO ) table");

            // The relationship should exist
            Assert.AreEqual(1, analysis.Relationships.Count, "Should have 1 relationship");
            var rel = analysis.Relationships.First();
            Assert.AreEqual("TI_Mime", rel.FromTable);
            Assert.AreEqual("Period", rel.FromColumn);
            Assert.AreEqual("Period Definition ( SISO )", rel.ToTable, "Relationship ToTable should be trimmed to match table name");
            Assert.AreEqual("Period", rel.ToColumn);
            Assert.AreEqual(XmSqlJoinType.LeftOuterJoin, rel.JoinType);
        }

        [TestMethod]
        public void Antlr_ParseBatchEvent_WithReverseBitmapJoin_FindsJoin()
        {
            string xmSql = "DEFINE TABLE '$TTable2' := SELECT 'Time Periods'[CompKeyTP] FROM 'Time Periods' WHERE 'Time Periods'[Period] = 'LP', " +
                "DEFINE TABLE '$TTable3' := SELECT RJOIN ( '$TTable2'[Time Periods$CompKeyTP] ) FROM '$TTable2' REVERSE BITMAP JOIN 'CME_Global_Data' ON '$TTable2'[Time Periods$CompKeyTP]='CME_Global_Data'[CompKeyGD], " +
                "DEFINE TABLE '$TTable1' := SELECT SUM ( 'CME_Global_Data'[Value] ) FROM 'CME_Global_Data' " +
                "WHERE 'CME_Global_Data'[Country] = 'Thailand' VAND 'CME_Global_Data'[Category] = 'Beverages' " +
                "VAND 'CME_Global_Data'[PEP/ROM] NIN ( 'Not Applicable' ) VAND 'CME_Global_Data'[Channel] NIN ( 'All' ) " +
                "VAND 'CME_Global_Data'[SubSegGrp] = 'Sub Segment' " +
                "VAND [CallbackDataID ( CME_Global_Data[Data_Flag]=SlicerSelect ) ] ( PFDATAID ( 'CME_Global_Data'[Data_Flag] ) ) " +
                "VAND 'CME_Global_Data'[CompKeyGD] ININDEX '$TTable3'[$SemijoinProjection];";

            var analysis = new XmSqlAnalysis();
            var result = _parser.ParseQuery(xmSql, analysis);

            Assert.IsTrue(result, "Parser should successfully parse the batch event query");

            // Both physical tables should be found
            Assert.IsTrue(analysis.Tables.ContainsKey("Time Periods"), "Should find 'Time Periods' table");
            Assert.IsTrue(analysis.Tables.ContainsKey("CME_Global_Data"), "Should find 'CME_Global_Data' table");

            // No temp tables should appear
            foreach (var tableName in analysis.Tables.Keys)
            {
                Assert.IsFalse(tableName.StartsWith("$T"), $"Temp table '{tableName}' should not appear in analysis");
            }

            // There should be a relationship between Time Periods and CME_Global_Data
            Assert.IsTrue(analysis.Relationships.Count > 0, 
                $"Should find at least one relationship. Tables found: {string.Join(", ", analysis.Tables.Keys)}");
        }
    }
}
