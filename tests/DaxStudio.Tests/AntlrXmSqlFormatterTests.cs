using Antlr4.Runtime;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Tests for the ANTLR-based xmSQL formatting visitor (XmSqlFormattingVisitor).
    /// </summary>
    [TestClass]
    public class AntlrXmSqlFormatterTests
    {
        /// <summary>
        /// Directly invokes the visitor (no try-catch wrapper) to verify it doesn't throw.
        /// </summary>
        private string FormatDirect(string xmSql, bool format, bool simplify)
        {
            var inputStream = new AntlrInputStream(xmSql);
            var lexer = new xmSQLLexer(inputStream);
            lexer.RemoveErrorListeners();
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new xmSQLParser(tokenStream);
            parser.RemoveErrorListeners();
            var tree = parser.query();

            var visitor = new XmSqlFormattingVisitor(simplify, format);
            visitor.Visit(tree);
            return visitor.GetFormattedText();
        }

        [TestMethod]
        public void Formatter_AggWithoutParens_DoesNotThrow()
        {
            // ANTLR error recovery creates aggregationExpr with null aggFunction
            // when a keyword like SUM appears without proper parenthesized syntax.
            // This reproduces the NullReferenceException at VisitAggregationExpr line 395.
            string xmSql = "SELECT\r\n"
                + "SUM [Amount]\r\n"
                + "FROM 'Sales';";

            // Call the visitor directly — any NullReferenceException will fail the test
            var result = FormatDirect(xmSql, format: true, simplify: true);
            Assert.IsNotNull(result, "Formatter should return a non-null result");
        }

        [TestMethod]
        public void Formatter_BracketTableRef_DoesNotThrow()
        {
            // xmSQL with bracket-based table references instead of single-quoted:
            // [Date (13)].[Calendar Mth (90071)] instead of 'Date (13)'[Calendar Mth (90071)]
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Date (13)].[Calendar Mth (90071)] AS [Date (13)$Calendar Mth (90071)]\r\n"
                + "FROM [Date (13)];\r\n"
                + "\r\n\r\n"
                + "[Estimated size (volume, marshalling bytes): 123, 984]";

            // Call the visitor directly — any NullReferenceException will fail the test
            var result = FormatDirect(xmSql, format: true, simplify: true);
            Assert.IsNotNull(result, "Formatter should return a non-null result");
        }

        [TestMethod]
        public void Formatter_SimpleSelectFrom_FormatsCorrectly()
        {
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "'Product'[Color],\r\n"
                + "'Product'[Class]\r\n"
                + "FROM 'Product';";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: false,
                out _, out _, out _);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("SELECT"), "Result should contain SELECT");
            Assert.IsTrue(result.Contains("FROM"), "Result should contain FROM");
        }

        [TestMethod]
        public void Formatter_EstimatedSize_ExtractsValues()
        {
            string xmSql = "SELECT\r\n"
                + "'Sales'[Amount]\r\n"
                + "FROM 'Sales'\r\n"
                + "WHERE 'Sales'[Year] IN ('2020', '2021'..[Estimated size (volume, marshalling bytes): 500, 4000]);";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out long rows,
                out long bytes,
                out bool hasSize);

            Assert.IsNotNull(result);
            Assert.IsTrue(hasSize, "Should have extracted estimated size");
            Assert.AreEqual(500, rows);
            Assert.AreEqual(4000, bytes);
        }

        [TestMethod]
        public void Formatter_SimplifyRemovesAliasAndLineage()
        {
            string xmSql = "SELECT\r\n"
                + "'Product'[Color ( 123 ) ] AS 'col1'\r\n"
                + "FROM 'Product';";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out _, out _, out _);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Contains("123"), "Lineage should be removed");
            Assert.IsFalse(result.Contains("AS 'col1'"), "Alias should be removed");
        }

        [TestMethod]
        public void Formatter_BracketTableRefs_FullQuery()
        {
            string xmSql =
                "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Date (13)].[Date (69)] AS [Date (13)$Date (69)], [Geography (16)].[Country Region Code (89)] AS [Geography (16)$Country Region Code (89)],\r\n"
                + "SUM([Internet Sales (28)].[Sales Amount (142)]) AS [$Measure0]\r\n"
                + "FROM [Internet Sales (28)]\r\n"
                + "\tLEFT OUTER JOIN [Date (13)] ON [Internet Sales (28)].[Order Date (147)]=[Date (13)].[Date (69)]\r\n"
                + "\tLEFT OUTER JOIN [Customer (10)] ON [Internet Sales (28)].[Customer Id (128)]=[Customer (10)].[Customer Id (44)]\r\n"
                + "\tLEFT OUTER JOIN [Geography (16)] ON [Customer (10)].[Geography Id (45)]=[Geography (16)].[Geography Id (85)]\r\n"
                + "WHERE\r\n"
                + "\t[Date (13)].[Date (69)] IN (40593.000000, 40174.000000, 38732.000000, 40556.000000, 40137.000000, 41998.000000, 40100.000000, 39681.000000, 41542.000000, 41961.000000...[3652 total values, not all displayed]);\r\n"
                + "\r\n\r\n"
                + "[Estimated size (volume, marshalling bytes): 4940, 59280]";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out long rows,
                out long bytes,
                out bool hasSize);

            Assert.IsNotNull(result, "Formatter should return a non-null result");

            // Verify estimated size extraction
            Assert.IsTrue(hasSize, "Should have extracted estimated size");
            Assert.AreEqual(4940, rows, "Estimated rows");
            Assert.AreEqual(59280, bytes, "Estimated bytes");

            // Verify lineage IDs removed
            Assert.IsFalse(result.Contains("(13)"), "Date lineage should be removed");
            Assert.IsFalse(result.Contains("(16)"), "Geography lineage should be removed");
            Assert.IsFalse(result.Contains("(28)"), "Internet Sales lineage should be removed");
            Assert.IsFalse(result.Contains("(69)"), "Date column lineage should be removed");
            Assert.IsFalse(result.Contains("(89)"), "Country Region Code lineage should be removed");
            Assert.IsFalse(result.Contains("(142)"), "Sales Amount lineage should be removed");

            // Verify aliases removed
            Assert.IsFalse(result.Contains("AS ["), "Aliases should be removed");
            Assert.IsFalse(result.Contains("$Measure0"), "Measure alias should be removed");

            // Verify table names converted from brackets to quotes
            Assert.IsTrue(result.Contains("'Date'"), "Table name should be quoted");
            Assert.IsTrue(result.Contains("'Internet Sales'"), "Table name should be quoted");
            Assert.IsTrue(result.Contains("'Geography'"), "Table name should be quoted");
            Assert.IsTrue(result.Contains("'Customer'"), "Table name should be quoted");

            // Verify structural elements
            Assert.IsTrue(result.Contains("SELECT"), "Should contain SELECT");
            Assert.IsTrue(result.Contains("FROM 'Internet Sales'"), "Should contain FROM");
            Assert.IsTrue(result.Contains("LEFT OUTER JOIN"), "Should contain JOINs");
            Assert.IsTrue(result.Contains("SUM"), "Should contain aggregation");
            Assert.IsTrue(result.Contains("WHERE"), "Should contain WHERE");
            Assert.IsTrue(result.Contains("IN ("), "Should contain IN clause");

            // Verify truncation indicator with formatted number
            Assert.IsTrue(result.Contains("3,652 total values"), "Truncation indicator should have formatted number");

            // Verify estimated size in output
            Assert.IsTrue(result.Contains("Estimated size: rows = 4,940"), "Should have formatted estimated rows");
            Assert.IsTrue(result.Contains("bytes = 59,280"), "Should have formatted estimated bytes");
        }
    }
}
