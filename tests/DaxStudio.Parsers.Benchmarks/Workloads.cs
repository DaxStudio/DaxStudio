using DaxStudio.Parsers.StorageEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DaxStudio.Parsers.Benchmarks
{
    /// <summary>
    /// Batches of storage engine queries, as the trace delivers them (raw xmSQL, CRLF, tabs, lineage ids).
    /// </summary>
    public static class Workloads
    {
        public const string Corpus = "Corpus";
        public const string ServerTimings = "ServerTimings";
        public const string LongInLists = "LongInLists";

        public static readonly string[] All = { Corpus, ServerTimings, LongInLists };

        public static string[] Get(string name)
        {
            switch (name)
            {
                case Corpus: return LoadCorpus();
                case ServerTimings: return BuildServerTimings(500);
                case LongInLists: return BuildLongInLists(20, 1000);
                default: throw new ArgumentOutOfRangeException(nameof(name), name, null);
            }
        }

        /// <summary>The queries collected from the DaxStudio parser tests (mostly small, some malformed).</summary>
        public static string[] LoadCorpus()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Corpus", "xmsql.jsonl");
            return File.ReadAllLines(path)
                .Where(l => l.Length > 0)
                .Select(l => JsonSerializer.Deserialize<string>(l))
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .ToArray();
        }

        /// <summary>
        /// The storage engine events of a heavy report query: group-bys over joined tables, WITH expressions,
        /// callbacks, semi-join DEFINE TABLE batches, date range filters, tuple filters and GUID-named
        /// auto date tables, each with varying filter values.
        /// </summary>
        public static string[] BuildServerTimings(int count)
        {
            var random = new Random(42);
            var queries = new string[count];
            for (var i = 0; i < count; i++)
            {
                string query;
                switch (i % 7)
                {
                    case 0: query = GroupByWithJoins(random); break;
                    case 1: query = WithExpression(random); break;
                    case 2: query = Callback(random); break;
                    case 3: query = SemiJoinBatch(random); break;
                    case 4: query = DateRange(random); break;
                    case 5: query = TupleFilter(random, 8); break;
                    default: query = AutoDateTable(random); break;
                }

                // QueryEnd events carry the estimated size, cache matches don't
                if (i % 3 != 0)
                {
                    query += "\r\n\r\n\r\n[Estimated size (volume, marshalling bytes): " + random.Next(1, 100000) + ", " + random.Next(16, 1600000) + "]";
                }

                queries[i] = query;
            }

            return queries;
        }

        /// <summary>Queries filtering on long value lists, like a slicer with many selected items.</summary>
        public static string[] BuildLongInLists(int count, int values)
        {
            var random = new Random(7);
            var queries = new string[count];
            for (var i = 0; i < count; i++)
            {
                queries[i] = i % 2 == 0 ? LongNumericInList(random, values) : TupleFilter(random, values / 5);
            }

            return queries;
        }

        private static string Join(string from, string table, string fromColumn, string toColumn)
            => "\tLEFT OUTER JOIN [" + table + "] ON [" + from + "].[" + fromColumn + "]=[" + table + "].[" + toColumn + "]";

        private static string Column(string table, string column) => "[" + table + "].[" + column + "]";

        private static string Selected(string table, string column) => Column(table, column) + " AS [" + table + "$" + column + "]";

        private static string GroupByWithJoins(Random r)
        {
            var years = string.Join(", ", Enumerable.Range(0, r.Next(1, 6)).Select(k => (2015 + k).ToString(CultureInfo.InvariantCulture)));
            var colors = string.Join(", ", new[] { "'Black'", "'Blue'", "'Red'", "'Silver'", "'White'", "'Yellow'" }.Take(r.Next(1, 7)));
            return "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + Selected("Date (13)", "Calendar Year (71)") + ", " + Selected("Product (19)", "Color (103)") + ",\r\n"
                + "SUM([Internet Sales (28)].[Sales Amount (142)]) AS [$Measure0], SUM([Internet Sales (28)].[Order Quantity (139)]) AS [$Measure1], COUNT () \r\n"
                + "FROM [Internet Sales (28)]\r\n"
                + Join("Internet Sales (28)", "Date (13)", "Order Date (147)", "Date (69)") + "\r\n"
                + Join("Internet Sales (28)", "Product (19)", "Product Id (131)", "Product Id (95)") + "\r\n"
                + "WHERE\r\n"
                + "\t" + Column("Date (13)", "Calendar Year (71)") + " IN (" + years + ") VAND\r\n"
                + "\t" + Column("Product (19)", "Color (103)") + " IN (" + colors + ");";
        }

        private static string WithExpression(Random r)
        {
            return "SET DC_KIND=\"AUTO\";\r\n"
                + "WITH\r\n"
                + "\t$Expr0 := ( PFCAST ( [Sales (31)].[Quantity (160)] AS INT ) * PFCAST ( [Sales (31)].[Net Price (163)] AS INT ) ) \r\n"
                + "\t$Expr1 := ( PFCAST ( [Sales (31)].[Quantity (160)] AS INT ) * PFCAST ( [Sales (31)].[Unit Cost (162)] AS INT ) ) \r\n"
                + "SELECT\r\n"
                + Selected("Store (22)", "Country (110)") + ", " + Selected("Product (19)", "Brand (100)") + ",\r\n"
                + "SUM(@$Expr0) AS [$Measure0], SUM(@$Expr1) AS [$Measure1]\r\n"
                + "FROM [Sales (31)]\r\n"
                + Join("Sales (31)", "Store (22)", "StoreKey (157)", "StoreKey (105)") + "\r\n"
                + Join("Sales (31)", "Product (19)", "ProductKey (155)", "ProductKey (96)") + "\r\n"
                + "WHERE\r\n"
                + "\t" + Column("Store (22)", "Status (112)") + " = 'On'"
                + " VAND\r\n\t" + Column("Sales (31)", "Order Date (150)") + " >= " + (43000 + r.Next(0, 1500)).ToString(CultureInfo.InvariantCulture) + ".000000;";
        }

        private static string Callback(Random r)
        {
            return "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + Selected("Customer (10)", "Education (49)") + ",\r\n"
                + "SUM([CallbackDataID(SUMX ( RELATEDTABLE ( 'Sales' ), 'Sales'[Quantity]] * 'Sales'[Net Price]] ))](PFDATAID( [Customer (10)].[CustomerKey (40)] ))) AS [$Measure0],\r\n"
                + "MAX([MinMaxColumnPositionCallback](PFDATAID( [Customer (10)].[Birth Date (44)] ))) AS [$Measure1]\r\n"
                + "FROM [Customer (10)]\r\n"
                + "WHERE\r\n"
                + "\t" + Column("Customer (10)", "Gender (50)") + " = '" + (r.Next(2) == 0 ? "M" : "F") + "';";
        }

        private static string SemiJoinBatch(Random r)
        {
            var brands = string.Join(", ", Enumerable.Range(0, r.Next(3, 12)).Select(k => "'Brand " + k + "'"));
            return "DEFINE TABLE [$TTable3] := SELECT\r\n"
                + Selected("Product (19)", "Brand (100)") + ",\r\n"
                + "SUM([Sales (31)].[Quantity (160)]) AS [$Measure0]\r\n"
                + "FROM [Sales (31)]\r\n"
                + Join("Sales (31)", "Product (19)", "ProductKey (155)", "ProductKey (96)") + "\r\n"
                + "WHERE\r\n"
                + "\t" + Column("Product (19)", "Brand (100)") + " IN (" + brands + "),\r\n"
                + "\r\n"
                + "DEFINE TABLE [$TTable4] := SELECT\r\n"
                + "SIMPLEINDEXN ( [$TTable3].[Product (19)$Brand (100)] )\r\n"
                + "FROM [$TTable3],\r\n"
                + "\r\n"
                + "DEFINE TABLE [$TTable1] := SELECT\r\n"
                + Selected("Product (19)", "Brand (100)") + ", " + Selected("Date (13)", "Year (72)") + ",\r\n"
                + "SUM([Sales (31)].[Quantity (160)]) AS [$Measure1]\r\n"
                + "FROM [Sales (31)]\r\n"
                + Join("Sales (31)", "Date (13)", "Order Date (150)", "Date (69)") + "\r\n"
                + Join("Sales (31)", "Product (19)", "ProductKey (155)", "ProductKey (96)") + "\r\n"
                + "WHERE\r\n"
                + "\t" + Column("Product (19)", "Brand (100)") + " ININDEX [$TTable4].[$Index1];";
        }

        private static string DateRange(Random r)
        {
            var start = 43831 + r.Next(0, 700);
            return "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + Selected("Date (13)", "Month (74)") + ",\r\n"
                + "SUM([Sales (31)].[Net Price (163)]) AS [$Measure0]\r\n"
                + "FROM [Sales (31)]\r\n"
                + Join("Sales (31)", "Date (13)", "Order Date (150)", "Date (69)") + "\r\n"
                + "\tREVERSE BITMAP JOIN [Product (19)] ON [Sales (31)].[ProductKey (155)]=[Product (19)].[ProductKey (96)]\r\n"
                + "WHERE\r\n"
                + "\tPFCASTCOALESCE ( [Date (13)].[Date (69)] AS INT ) >= COALESCE ( " + start + ".000000 ) VAND\r\n"
                + "\tPFCASTCOALESCE ( [Date (13)].[Date (69)] AS INT ) < COALESCE ( " + (start + 365) + ".000000 ) VAND\r\n"
                + "\t" + Column("Product (19)", "Color (103)") + " NIN ( 'Black', 'White' );";
        }

        private static readonly string[] Countries = { "United States", "Canada", "France", "Germany", "Italy", "Japan", "Australia", "United Kingdom" };

        private static string TupleFilter(Random r, int tuples)
        {
            var sb = new StringBuilder();
            sb.Append("SET DC_KIND=\"AUTO\";\r\n")
              .Append("SELECT\r\n")
              .Append(Selected("Customer (10)", "City (47)")).Append(",\r\n")
              .Append("SUM([Sales (31)].[Quantity (160)]) AS [$Measure0]\r\n")
              .Append("FROM [Sales (31)]\r\n")
              .Append(Join("Sales (31)", "Customer (10)", "CustomerKey (152)", "CustomerKey (40)")).Append("\r\n")
              .Append("WHERE\r\n")
              .Append("\t ( [Customer (10)].[Country (45)], [Customer (10)].[State (46)] ) IN { ");
            for (var k = 0; k < tuples; k++)
            {
                if (k > 0) sb.Append(" , ");
                sb.Append("( '").Append(Countries[r.Next(Countries.Length)]).Append("', 'State ").Append(k).Append("' )");
            }

            return sb.Append("};").ToString();
        }

        private static string AutoDateTable(Random r)
        {
            const string dates = "LocalDateTable_8f3e1c2a-5b7d-4e9f-a1b2-c3d4e5f60718 (55)";
            var from = 2015 + r.Next(0, 5);
            return "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + Selected(dates, "Year (201)") + ", " + Selected(dates, "Quarter (202)") + ",\r\n"
                + "SUM([Orders (40)].[Revenue (210)]) AS [$Measure0], DCOUNT([Orders (40)].[CustomerId (212)]) AS [$Measure1]\r\n"
                + "FROM [Orders (40)]\r\n"
                + Join("Orders (40)", dates, "OrderDate (211)", "Date (200)") + "\r\n"
                + "WHERE\r\n"
                + "\t" + Column(dates, "Year (201)") + " BETWEEN " + from + " AND " + (from + 3) + ";";
        }

        private static string LongNumericInList(Random r, int values)
        {
            var sb = new StringBuilder();
            sb.Append("SET DC_KIND=\"AUTO\";\r\n")
              .Append("SELECT\r\n")
              .Append(Selected("Customer (10)", "CustomerKey (40)")).Append(", ").Append(Selected("Product (19)", "Product Name (98)")).Append(",\r\n")
              .Append("SUM([Sales (31)].[Quantity (160)]) AS [$Measure0]\r\n")
              .Append("FROM [Sales (31)]\r\n")
              .Append(Join("Sales (31)", "Customer (10)", "CustomerKey (152)", "CustomerKey (40)")).Append("\r\n")
              .Append(Join("Sales (31)", "Product (19)", "ProductKey (155)", "ProductKey (96)")).Append("\r\n")
              .Append("WHERE\r\n")
              .Append("\t[Customer (10)].[CustomerKey (40)] IN (");
            for (var k = 0; k < values; k++)
            {
                if (k > 0) sb.Append(", ");
                sb.Append(11000 + r.Next(0, 20000));
            }

            sb.Append(") VAND\r\n\t[Product (19)].[Product Name (98)] IN (");
            for (var k = 0; k < values / 4; k++)
            {
                if (k > 0) sb.Append(", ");
                sb.Append("'Product ").Append(r.Next(0, 5000)).Append('\'');
            }

            return sb.Append(");").ToString();
        }
    }
}
