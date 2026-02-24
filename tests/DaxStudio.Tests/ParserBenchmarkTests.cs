using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;

namespace DaxStudio.Tests
{
    [TestClass]
    public class ParserBenchmarkTests
    {
        // Test queries of increasing complexity
        private static readonly string[] TestQueries = new[]
        {
            // 1. Simple SELECT/FROM
            @"SET DC_KIND=""AUTO"";
SELECT
    'Product'[Color],
    'Product'[Class]
FROM 'Product';",

            // 2. JOIN with WHERE (IN + equality)
            @"SET DC_KIND=""AUTO"";
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
    'Customer'[Education] = 'Bachelors';",

            // 3. WITH expression + aggregation + multiple JOINs
            @"SET DC_KIND=""AUTO"";
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
    'Date'[Day of Week Number] = 4;",

            // 4. DEFINE TABLE + ININDEX
            @"DEFINE TABLE '$TTable1' :=
SELECT
    'Calendar'[Date]
FROM 'Calendar'
WHERE
    'Calendar'[Year] = 2023;
SELECT
    'Sales'[Amount]
FROM 'Sales'
WHERE
    'Sales'[DateKey] ININDEX '$TTable1'[$SemijoinProjection];",

            // 5. CREATE SHALLOW RELATION + SELECT
            @"CREATE SHALLOW RELATION 'Rel_SalesDate'
FROM 'Sales'[DateKey]
TO 'Date'[DateKey];
SELECT 'Sales'[Amount]
FROM 'Sales';",

            // 6. Multiple WITH expressions + complex WHERE
            @"SET DC_KIND=""AUTO"";
WITH
    $Expr0 := ( PFCAST ( 'Sales'[Quantity] AS INT ) * PFCAST ( 'Sales'[Net Price] AS INT ) )
    $Expr1 := ( PFCAST ( 'Sales'[Quantity] AS INT ) * PFCAST ( 'Sales'[Discount] AS INT ) + PFCAST ( 'Sales'[Tax] AS INT ) )
SELECT
    'Product'[Brand],
    'Product'[Category],
    'Store'[Store Name],
    SUM ( @$Expr0 ),
    SUM ( @$Expr1 )
FROM 'Sales'
    LEFT OUTER JOIN 'Date'
        ON 'Sales'[Order Date]='Date'[Date]
    LEFT OUTER JOIN 'Product'
        ON 'Sales'[ProductKey]='Product'[ProductKey]
    LEFT OUTER JOIN 'Store'
        ON 'Sales'[StoreKey]='Store'[StoreKey]
WHERE
    'Date'[Year] = 2023
    VAND
    'Product'[Category] IN ( 'Electronics', 'Clothing', 'Food' )
    VAND
    'Store'[Region] = 'North America';"
        };

        private static readonly string DirectQuerySql = @"SELECT
    [t4].[ProductName] AS [c10],
    SUM([t7].[Amount]) AS [a0]
FROM
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[ProductName] as [ProductName] from [dbo].[Product] as [$Table]) AS [t4]
    INNER JOIN
    (select [$Table].[ProductKey] as [ProductKey],[$Table].[Amount] as [Amount] from [dbo].[Sales] as [$Table]) AS [t7]
    ON ([t4].[ProductKey] = [t7].[ProductKey])
GROUP BY [t4].[ProductName]";

        private const int WarmupIterations = 50;
        private const int BenchmarkIterations = 500;

        [TestMethod]
        public void Benchmark_RegexVsAntlr_SingleQuery()
        {
            var regexParser = new XmSqlParser();
            var antlrParser = new AntlrXmSqlParser();

            Console.WriteLine("=== Single Query Benchmark ===");
            Console.WriteLine($"Warmup: {WarmupIterations} iterations | Measured: {BenchmarkIterations} iterations");
            Console.WriteLine($"{"Query",-12} {"Regex (ms)",-14} {"ANTLR (ms)",-14} {"Ratio",-10}");
            Console.WriteLine(new string('-', 50));

            for (int q = 0; q < TestQueries.Length; q++)
            {
                var query = TestQueries[q];

                // Warmup
                for (int i = 0; i < WarmupIterations; i++)
                {
                    regexParser.ParseQuery(query, new XmSqlAnalysis());
                    antlrParser.ParseQuery(query, new XmSqlAnalysis());
                }

                // Benchmark regex
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < BenchmarkIterations; i++)
                {
                    regexParser.ParseQuery(query, new XmSqlAnalysis());
                }
                sw.Stop();
                double regexMs = sw.Elapsed.TotalMilliseconds;

                // Benchmark ANTLR
                sw.Restart();
                for (int i = 0; i < BenchmarkIterations; i++)
                {
                    antlrParser.ParseQuery(query, new XmSqlAnalysis());
                }
                sw.Stop();
                double antlrMs = sw.Elapsed.TotalMilliseconds;

                double ratio = antlrMs / regexMs;
                Console.WriteLine($"Query {q + 1,-6} {regexMs,10:F2}    {antlrMs,10:F2}    {ratio,6:F2}x");
            }
        }

        [TestMethod]
        public void Benchmark_RegexVsAntlr_BatchQueries()
        {
            var regexParser = new XmSqlParser();
            var antlrParser = new AntlrXmSqlParser();

            Console.WriteLine("=== Batch Query Benchmark (all queries together) ===");
            Console.WriteLine($"Warmup: {WarmupIterations} iterations | Measured: {BenchmarkIterations} iterations");

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                regexParser.ParseQueries(TestQueries);
                antlrParser.ParseQueries(TestQueries);
            }

            // Benchmark regex
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                regexParser.ParseQueries(TestQueries);
            }
            sw.Stop();
            double regexMs = sw.Elapsed.TotalMilliseconds;

            // Benchmark ANTLR
            sw.Restart();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                antlrParser.ParseQueries(TestQueries);
            }
            sw.Stop();
            double antlrMs = sw.Elapsed.TotalMilliseconds;

            double ratio = antlrMs / regexMs;
            Console.WriteLine($"Regex total:  {regexMs,10:F2} ms ({regexMs / BenchmarkIterations:F3} ms/batch)");
            Console.WriteLine($"ANTLR total:  {antlrMs,10:F2} ms ({antlrMs / BenchmarkIterations:F3} ms/batch)");
            Console.WriteLine($"Ratio:        {ratio:F2}x");
        }

        [TestMethod]
        public void Benchmark_RegexVsAntlr_DirectQuerySql()
        {
            var regexParser = new XmSqlParser();
            var antlrParser = new AntlrXmSqlParser();

            Console.WriteLine("=== DirectQuery SQL Benchmark ===");
            Console.WriteLine($"Warmup: {WarmupIterations} iterations | Measured: {BenchmarkIterations} iterations");

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                regexParser.ParseDirectQuerySql(DirectQuerySql, new XmSqlAnalysis(), null);
                antlrParser.ParseDirectQuerySql(DirectQuerySql, new XmSqlAnalysis(), null);
            }

            // Benchmark regex
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                regexParser.ParseDirectQuerySql(DirectQuerySql, new XmSqlAnalysis(), null);
            }
            sw.Stop();
            double regexMs = sw.Elapsed.TotalMilliseconds;

            // Benchmark ANTLR
            sw.Restart();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                antlrParser.ParseDirectQuerySql(DirectQuerySql, new XmSqlAnalysis(), null);
            }
            sw.Stop();
            double antlrMs = sw.Elapsed.TotalMilliseconds;

            double ratio = antlrMs / regexMs;
            Console.WriteLine($"Regex total:  {regexMs,10:F2} ms ({regexMs / BenchmarkIterations:F3} ms/iter)");
            Console.WriteLine($"ANTLR total:  {antlrMs,10:F2} ms ({antlrMs / BenchmarkIterations:F3} ms/iter)");
            Console.WriteLine($"Ratio:        {ratio:F2}x");
        }

        [TestMethod]
        public void Benchmark_RegexVsAntlr_ColdStart()
        {
            Console.WriteLine("=== Cold Start (first parse, no warmup) ===");

            // Regex cold start
            var regexParser = new XmSqlParser();
            var sw = Stopwatch.StartNew();
            regexParser.ParseQuery(TestQueries[2], new XmSqlAnalysis());
            sw.Stop();
            double regexColdMs = sw.Elapsed.TotalMilliseconds;

            // ANTLR cold start
            var antlrParser = new AntlrXmSqlParser();
            sw.Restart();
            antlrParser.ParseQuery(TestQueries[2], new XmSqlAnalysis());
            sw.Stop();
            double antlrColdMs = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Regex cold start:  {regexColdMs:F3} ms");
            Console.WriteLine($"ANTLR cold start:  {antlrColdMs:F3} ms");
            Console.WriteLine($"Ratio:             {antlrColdMs / regexColdMs:F2}x");
        }

        [TestMethod]
        public void Diagnostic_DirectQuerySql_OutputComparison()
        {
            var metrics = new XmSqlParser.SeEventMetrics { DurationMs = 50, QueryId = 1 };

            var regexAnalysis = new XmSqlAnalysis();
            new XmSqlParser().ParseDirectQuerySql(DirectQuerySql, regexAnalysis, metrics);

            var antlrAnalysis = new XmSqlAnalysis();
            new AntlrXmSqlParser().ParseDirectQuerySql(DirectQuerySql, antlrAnalysis, metrics);

            Console.WriteLine("=== REGEX DirectQuery Output ===");
            PrintAnalysis(regexAnalysis);
            Console.WriteLine("\n=== ANTLR DirectQuery Output ===");
            PrintAnalysis(antlrAnalysis);
        }

        private static void PrintAnalysis(XmSqlAnalysis a)
        {
            Console.WriteLine($"Tables ({a.Tables.Count}): {string.Join(", ", a.Tables.Keys)}");
            foreach (var t in a.Tables)
            {
                Console.WriteLine($"  {t.Key}: IsFrom={t.Value.IsFromTable} IsJoin={t.Value.IsJoinedTable} Hits={t.Value.HitCount}");
                foreach (var c in t.Value.Columns)
                    Console.WriteLine($"    [{c.Key}]: Usage={c.Value.UsageTypes} Agg=[{string.Join(",", c.Value.AggregationTypes)}] Filters=[{string.Join(",", c.Value.FilterValues)}]");
            }
            Console.WriteLine($"Relationships ({a.Relationships.Count}):");
            foreach (var r in a.Relationships)
                Console.WriteLine($"  {r.FromTable}[{r.FromColumn}] -> {r.ToTable}[{r.ToColumn}] ({r.JoinType})");
        }
    }
}
