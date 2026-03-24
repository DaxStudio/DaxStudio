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
        public void Formatter_QuotedFunctionNames_OutputWithoutQuotes()
        {
            // xmSQL engine wraps some callback function names in single quotes;
            // the formatter should strip those quotes in the output.
            string xmSql = "SELECT\r\n"
                + "'Cond'('Product'[Color], 'Product'[Size])\r\n"
                + "FROM 'Product';";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: false,
                out _, out _, out _);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Cond"), "Result should contain Cond function");
            Assert.IsFalse(result.Contains("'Cond'"), "Cond should not be wrapped in single quotes");
        }

        [TestMethod]
        public void Formatter_QuotedCallbackNames_AllStripped()
        {
            string xmSql = "SELECT\r\n"
                + "'LogAbsValueCallback'('Sales'[Amount]),\r\n"
                + "'RoundValueCallback'('Sales'[Price]),\r\n"
                + "'MinMaxColumnPositionCallback'('Sales'[Qty])\r\n"
                + "FROM 'Sales';";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: false,
                out _, out _, out _);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Contains("'LogAbsValueCallback'"), "LogAbsValueCallback should not be quoted");
            Assert.IsFalse(result.Contains("'RoundValueCallback'"), "RoundValueCallback should not be quoted");
            Assert.IsFalse(result.Contains("'MinMaxColumnPositionCallback'"), "MinMaxColumnPositionCallback should not be quoted");
            Assert.IsTrue(result.Contains("LogAbsValueCallback"), "LogAbsValueCallback should be present");
            Assert.IsTrue(result.Contains("RoundValueCallback"), "RoundValueCallback should be present");
            Assert.IsTrue(result.Contains("MinMaxColumnPositionCallback"), "MinMaxColumnPositionCallback should be present");
        }

        [TestMethod]
        public void Formatter_BracketedCallbackInAggregation_NotQuoted()
        {
            // Real-world xmSQL: callback function name in brackets inside MAX()
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Period Definition  SISO  (139)].[Period (1438)] AS [Period Definition  SISO  (139)$Period (1438)],\r\n"
                + "MAX([MinMaxColumnPositionCallback](PFDATAID( [Period Definition  SISO  (139)].[Previous Period (1439)] ))) AS [$Measure0]\r\n"
                + "FROM [Period Definition  SISO  (139)]\r\n"
                + "WHERE\r\n"
                + "\t[Period Definition  SISO  (139)].[Period (1438)] IN ('YTD', 'L13W', 'LW', 'L4W');\r\n"
                + "\r\n\r\n"
                + "[Estimated size (volume, marshalling bytes): 37, 592]";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out long rows,
                out long bytes,
                out bool hasSize);

            Assert.IsNotNull(result, "Formatter should return a non-null result");
            Assert.IsTrue(result.Contains("MinMaxColumnPositionCallback"), "Should contain callback function name");
            Assert.IsFalse(result.Contains("'MinMaxColumnPositionCallback'"), "Callback should not be wrapped in single quotes");
            Assert.IsFalse(result.Contains("[MinMaxColumnPositionCallback]"), "Callback should not be wrapped in brackets");
            Assert.IsTrue(result.Contains("MAX"), "Should contain MAX aggregation");
            Assert.IsTrue(result.Contains("PFDATAID"), "Should contain PFDATAID function");
        }

        [TestMethod]
        public void Formatter_FormattedOutput_UsesSpacesNotTabs()
        {
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "'Product'[Color],\r\n"
                + "'Product'[Class]\r\n"
                + "FROM 'Product'\r\n"
                + "\tLEFT OUTER JOIN 'Category' ON 'Product'[CategoryId]='Category'[CategoryId]\r\n"
                + "WHERE\r\n"
                + "\t'Product'[Color] IN ('Red', 'Blue');";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: false,
                out _, out _, out _);

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Contains("\t"), "Formatted output should not contain tab characters");
            Assert.IsTrue(result.Contains("    "), "Formatted output should use 4-space indentation");
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

        // ==================== DATE CONVERSION TESTS ====================

        [TestMethod]
        public void TryConvertOADateToIso_DateOnly_AppendsComment()
        {
            // 46087.000000 = 2026-03-06
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("46087.000000");
            Assert.AreEqual("46087.000000 /* 2026-03-06 */", result);
        }

        [TestMethod]
        public void TryConvertOADateToIso_DateWithTime_AppendsDateTimeComment()
        {
            // 46087.5 = 2026-03-06 12:00:00
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("46087.500000");
            Assert.AreEqual("46087.500000 /* 2026-03-06 12:00:00 */", result);
        }

        [TestMethod]
        public void TryConvertOADateToIso_IntegerDate_AppendsComment()
        {
            // 46023 = 2026-01-01
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("46023");
            Assert.IsTrue(result.StartsWith("46023 /* 20"), "Should append date comment for integer value");
        }

        [TestMethod]
        public void TryConvertOADateToIso_OutOfRange_ReturnsOriginal()
        {
            // 100000 is way beyond 2099
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("100000.000000");
            Assert.AreEqual("100000.000000", result, "Out-of-range value should not be converted");
        }

        [TestMethod]
        public void TryConvertOADateToIso_SmallNumber_ReturnsOriginal()
        {
            // 5 is not a plausible date filter
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("5");
            // 5 is in range (> 366 is the minimum), so it should NOT be converted
            Assert.AreEqual("5", result, "Small number below range should not be converted");
        }

        [TestMethod]
        public void TryConvertOADateToIso_NonNumeric_ReturnsOriginal()
        {
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("'hello'");
            Assert.AreEqual("'hello'", result, "Non-numeric value should be returned unchanged");
        }

        [TestMethod]
        public void TryConvertOADateToIso_NegativeNumber_ReturnsOriginal()
        {
            var result = XmSqlFormattingVisitor.TryConvertOADateToIso("-100.000000");
            Assert.AreEqual("-100.000000", result, "Negative number should not be converted");
        }

        [TestMethod]
        public void Formatter_ConvertDates_CoalesceFilterWithDates()
        {
            // Single xmSQL predicate with PFCASTCOALESCE and COALESCE-wrapped date value
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Date (16)].[Calendar Date (167)] AS [Date (16)$Calendar Date (167)]\r\n"
                + "FROM [Date (16)]\r\n"
                + "WHERE\r\n"
                + "\t(PFCASTCOALESCE( [Date (16)].[Calendar Date (167)] AS  REAL ) >= COALESCE(46087.000000));";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out _, out _, out _,
                convertDates: true);

            Assert.IsNotNull(result, "Formatter should return a non-null result");
            Assert.IsTrue(result.Contains("46087.000000"), "Should still contain original numeric value");
            Assert.IsTrue(result.Contains("/* 2026-03-06 */"), "Should contain ISO date comment for 46087");
        }

        [TestMethod]
        public void Formatter_ConvertDatesDisabled_NoDateComments()
        {
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Date (16)].[Calendar Date (167)] AS [Date (16)$Calendar Date (167)]\r\n"
                + "FROM [Date (16)]\r\n"
                + "WHERE\r\n"
                + "\t(PFCASTCOALESCE( [Date (16)].[Calendar Date (167)] AS  REAL ) >= COALESCE(46087.000000));";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out _, out _, out _,
                convertDates: false);

            Assert.IsNotNull(result, "Formatter should return a non-null result");
            Assert.IsFalse(result.Contains("/*"), "Should not contain date comments when convertDates is false");
        }

        [TestMethod]
        public void Formatter_ConvertDates_InListWithDates()
        {
            // IN list with date values
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Date (13)].[Date (69)] AS [Date (13)$Date (69)]\r\n"
                + "FROM [Date (13)]\r\n"
                + "WHERE\r\n"
                + "\t[Date (13)].[Date (69)] IN (40593.000000, 40174.000000, 38732.000000);";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out _, out _, out _,
                convertDates: true);

            Assert.IsNotNull(result, "Formatter should return a non-null result");
            // All three values should have date comments
            Assert.IsTrue(result.Contains("/* 2011-02-19 */"), "Should annotate 40593");
            Assert.IsTrue(result.Contains("/* 2009-12-27 */"), "Should annotate 40174");
        }

        [TestMethod]
        public void Formatter_CoalesceWithCallbackAndVand_ParsesCompletely()
        {
            // Regression test: COALESCE wrapping a callback expression followed by VAND
            // was losing the callback content and the second predicate entirely.
            string xmSql = "SET DC_KIND=\"AUTO\";\r\n"
                + "SELECT\r\n"
                + "[Product (19)].[Color (101)] AS [Product (19)$Color (101)],\r\n"
                + "SUM([Internet Sales (28)].[Sales Amount (142)]) AS [$Measure0]\r\n"
                + "FROM [Internet Sales (28)]\r\n"
                + "\tLEFT OUTER JOIN [Product (19)] ON [Internet Sales (28)].[Product Id (127)]=[Product (19)].[Product Id (93)]\r\n"
                + "WHERE\r\n"
                + "\t(COALESCE([CallbackDataID('Internet Sales'[Sales Amount])]"
                + "(PFDATAID( [Internet Sales (28)].[Sales Amount (142)] ))) > COALESCE(501.000000)) VAND\r\n"
                + "\t(COALESCE([CallbackDataID('Internet Sales'[Sales Amount])]"
                + "(PFDATAID( [Internet Sales (28)].[Sales Amount (142)] ))) < COALESCE(2501.000000));";

            var result = AntlrXmSqlFormatter.Format(
                xmSql,
                format: true,
                simplify: true,
                out _, out _, out _);

            Assert.IsNotNull(result, "Formatter should return a non-null result");

            // Both predicates should be present (VAND was being dropped)
            Assert.IsTrue(result.Contains("VAND"), "Should contain VAND between predicates");

            // Both comparison values should be present
            Assert.IsTrue(result.Contains("501.000000"), "Should contain first comparison value");
            Assert.IsTrue(result.Contains("2501.000000"), "Should contain second comparison value");

            // The callback expression should not be empty
            Assert.IsTrue(result.Contains("CallbackDataID"), "Should contain CallbackDataID in output");
        }
    }
}
