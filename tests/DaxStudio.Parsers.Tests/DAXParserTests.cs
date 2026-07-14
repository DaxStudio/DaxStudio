using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class DAXParserTests
    {
        #region Test Helpers

        private static DAXParser.DaxQueryContext ParseQuery(string input, out List<string> errors)
        {
            errors = new List<string>();
            var errorList = errors;

            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new CollectingErrorListener(errorList));
            ITokenStream stream = new CommonTokenStream(lexer);
            var parser = new DAXParser(stream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new CollectingErrorListener(errorList));
            return parser.daxQuery();
        }

        private static List<IToken> Tokenize(string input)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();
            return lexer.GetAllTokens().ToList();
        }

        private class CollectingErrorListener : BaseErrorListener, IAntlrErrorListener<int>
        {
            private readonly List<string> _errors;
            public CollectingErrorListener(List<string> errors) { _errors = errors; }

            public override void SyntaxError(
                System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
                string msg, RecognitionException e)
            {
                _errors.Add($"line {line}:{charPositionInLine} {msg}");
            }

            public void SyntaxError(
                System.IO.TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine,
                string msg, RecognitionException e)
            {
                _errors.Add($"line {line}:{charPositionInLine} {msg}");
            }
        }

        #endregion

        #region Lexer Tests

        [TestMethod]
        public void Lexer_BasicOperators()
        {
            var tokens = Tokenize("= == => <> <= >= && || + - * / ^ & : .");
            var types = tokens.Where(t => t.Type != DAXLexer.Eof).Select(t => t.Type).ToList();

            Assert.Contains(DAXLexer.EQUALS, types);
            Assert.Contains(DAXLexer.STRICT_EQUALS, types);
            Assert.Contains(DAXLexer.LAMBDA_ARROW, types);
            Assert.Contains(DAXLexer.OP_NE, types);
            Assert.Contains(DAXLexer.OP_LE, types);
            Assert.Contains(DAXLexer.OP_GE, types);
            Assert.Contains(DAXLexer.OP_AND, types);
            Assert.Contains(DAXLexer.OP_OR, types);
            Assert.Contains(DAXLexer.COLON, types);
            Assert.Contains(DAXLexer.DOT, types);
        }

        [TestMethod]
        public void Lexer_StrictEqualsBeforeEquals()
        {
            var tokens = Tokenize("==");
            var nonWs = tokens.Where(t => t.Type != DAXLexer.Eof).ToList();
            Assert.HasCount(1, nonWs);
            Assert.AreEqual(DAXLexer.STRICT_EQUALS, nonWs[0].Type);
        }

        [TestMethod]
        public void Lexer_LambdaArrow()
        {
            var tokens = Tokenize("=>");
            var nonWs = tokens.Where(t => t.Type != DAXLexer.Eof).ToList();
            Assert.HasCount(1, nonWs);
            Assert.AreEqual(DAXLexer.LAMBDA_ARROW, nonWs[0].Type);
        }

        [TestMethod]
        public void Lexer_FunctionTokens()
        {
            var tokens = Tokenize("SUM CALCULATE FILTER TOTALWTD RANK WINDOW OFFSET INDEX");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.AreEqual(DAXLexer.SUM, types[0]);
            Assert.AreEqual(DAXLexer.CALCULATE, types[1]);
            Assert.AreEqual(DAXLexer.FILTER, types[2]);
            Assert.AreEqual(DAXLexer.TOTALWTD, types[3]);
            Assert.AreEqual(DAXLexer.RANK, types[4]);
            Assert.AreEqual(DAXLexer.WINDOW, types[5]);
            Assert.AreEqual(DAXLexer.OFFSET, types[6]);
            Assert.AreEqual(DAXLexer.INDEX, types[7]);
        }

        [TestMethod]
        public void Lexer_DotFunctions()
        {
            var tokens = Tokenize("BETA.DIST T.DIST.2T NORM.S.DIST");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.AreEqual(DAXLexer.BETADIST, types[0]);
            Assert.AreEqual(DAXLexer.TDIST2T, types[1]);
            Assert.AreEqual(DAXLexer.NORMSDIST, types[2]);
        }

        [TestMethod]
        public void Lexer_Literals()
        {
            var tokens = Tokenize("42 3.14 \"hello\" TRUE FALSE");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.AreEqual(DAXLexer.INTEGER_LITERAL, types[0]);
            Assert.AreEqual(DAXLexer.REAL_LITERAL, types[1]);
            Assert.AreEqual(DAXLexer.STRING_LITERAL, types[2]);
            Assert.AreEqual(DAXLexer.TRUE, types[3]);
            Assert.AreEqual(DAXLexer.FALSE, types[4]);
        }

        [TestMethod]
        public void Lexer_StringLiteralStripsQuotes()
        {
            var tokens = Tokenize("\"hello world\"");
            var str = tokens.First(t => t.Type == DAXLexer.STRING_LITERAL);
            Assert.AreEqual("hello world", str.Text);
        }

        [TestMethod]
        public void Lexer_StringLiteralEscapedQuotes()
        {
            var tokens = Tokenize("\"say \"\"hi\"\"\"");
            var str = tokens.First(t => t.Type == DAXLexer.STRING_LITERAL);
            Assert.AreEqual("say \"\"hi\"\"", str.Text);
        }

        [TestMethod]
        public void Lexer_TableRef()
        {
            var tokens = Tokenize("'Product Category'");
            var tbl = tokens.First(t => t.Type == DAXLexer.TABLE_REF);
            Assert.AreEqual("Product Category", tbl.Text);
        }

        [TestMethod]
        public void Lexer_ColumnOrMeasure()
        {
            var tokens = Tokenize("[Sales Amount]");
            var col = tokens.First(t => t.Type == DAXLexer.COLUMN_OR_MEASURE);
            Assert.AreEqual("Sales Amount", col.Text);
        }

        [TestMethod]
        public void Lexer_Parameter()
        {
            var tokens = Tokenize("@startDate");
            var param = tokens.First(t => t.Type == DAXLexer.PARAMETER);
            Assert.AreEqual("@startDate", param.Text);
        }

        [TestMethod]
        public void Lexer_Keywords()
        {
            var tokens = Tokenize("DEFINE EVALUATE MEASURE VAR RETURN ORDER BY");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.AreEqual(DAXLexer.DEFINE, types[0]);
            Assert.AreEqual(DAXLexer.EVALUATE, types[1]);
            Assert.AreEqual(DAXLexer.MEASURE, types[2]);
            Assert.AreEqual(DAXLexer.VAR, types[3]);
            Assert.AreEqual(DAXLexer.RETURN, types[4]);
            Assert.AreEqual(DAXLexer.ORDER, types[5]);
            Assert.AreEqual(DAXLexer.BY, types[6]);
        }

        [TestMethod]
        public void Lexer_DocComment()
        {
            var tokens = Tokenize("/// This is a doc comment\n42");
            var docComment = tokens.First(t => t.Type == DAXLexer.DOC_COMMENT);
            Assert.StartsWith("///", docComment.Text);
            Assert.AreEqual(DAXLexer.COMMENTS_CHANNEL, docComment.Channel);
        }

        [TestMethod]
        public void Lexer_SingleLineCommentDoesNotMatchDocComment()
        {
            var tokens = Tokenize("// regular comment\n42");
            var comment = tokens.First(t => t.Type == DAXLexer.SINGLE_LINE_COMMENT);
            Assert.DoesNotStartWith("///", comment.Text);
        }

        [TestMethod]
        public void Lexer_UdfTypeKeywords()
        {
            var tokens = Tokenize("ANYVAL SCALAR ANYREF VARIANT INT64 VAL EXPR");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.AreEqual(DAXLexer.ANYVAL, types[0]);
            Assert.AreEqual(DAXLexer.SCALAR, types[1]);
            Assert.AreEqual(DAXLexer.ANYREF, types[2]);
            Assert.AreEqual(DAXLexer.VARIANT, types[3]);
            Assert.AreEqual(DAXLexer.INT64, types[4]);
            Assert.AreEqual(DAXLexer.VAL_KW, types[5]);
            Assert.AreEqual(DAXLexer.EXPR_KW, types[6]);
        }

        [TestMethod]
        public void Lexer_Identifier()
        {
            var tokens = Tokenize("MyVariable");
            var id = tokens.First(t => t.Type == DAXLexer.IDENTIFIER);
            Assert.IsNotNull(id);
        }

        [TestMethod]
        public void Lexer_NoMinusInLiteral()
        {
            // Verify minus is a separate token, not part of integer literal
            var tokens = Tokenize("3-2");
            var types = tokens
                .Where(t => t.Channel == 0 && t.Type != DAXLexer.Eof)
                .Select(t => t.Type).ToList();

            Assert.HasCount(3, types);
            Assert.AreEqual(DAXLexer.INTEGER_LITERAL, types[0]);
            Assert.AreEqual(DAXLexer.MINUS, types[1]);
            Assert.AreEqual(DAXLexer.INTEGER_LITERAL, types[2]);
        }

        #endregion

        #region Parser - Simple Queries

        [TestMethod]
        public void Parser_SimpleEvaluate()
        {
            var tree = ParseQuery("EVALUATE 'Product'", out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.HasCount(1, tree.evaluateBlock());
        }

        [TestMethod]
        public void Parser_EvaluateWithFilter()
        {
            var input = @"EVALUATE
FILTER(
    ALL('Product'[Color]),
    'Product'[Color] = ""Red""
)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_MultipleEvaluate()
        {
            var input = @"EVALUATE 'Product'
EVALUATE 'Sales'
EVALUATE 'Customer'";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.HasCount(3, tree.evaluateBlock());
        }

        [TestMethod]
        public void Parser_EvaluateWithOrderBy()
        {
            var input = @"EVALUATE 'Product'
ORDER BY 'Product'[Color] ASC, 'Product'[ListPrice] DESC";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsNotNull(tree.evaluateBlock()[0].orderByClause());
        }

        [TestMethod]
        public void Parser_EvaluateWithStartAt()
        {
            var input = @"EVALUATE 'Product'
ORDER BY 'Product'[Color]
START AT ""Blue""";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsNotNull(tree.evaluateBlock()[0].startAtClause());
        }

        #endregion

        #region Parser - DEFINE Block

        [TestMethod]
        public void Parser_DefineVar()
        {
            var input = @"DEFINE
    VAR _value = 42
EVALUATE { _value }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.IsNotNull(tree.defineBlock());
        }

        [TestMethod]
        public void Parser_DefineMeasure()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Total Sales] = SUM('Sales'[Amount])
EVALUATE { [Total Sales] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineTable()
        {
            var input = @"DEFINE
    TABLE MyTable = FILTER('Product', 'Product'[Color] = ""Red"")
EVALUATE MyTable";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineColumn()
        {
            var input = @"DEFINE
    COLUMN 'Product'[FullName] = 'Product'[Brand] & "" "" & 'Product'[Name]
EVALUATE 'Product'";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineFunction()
        {
            var input = @"DEFINE
    FUNCTION MyFunc = (x : SCALAR INT64 VAL, y : SCALAR INT64 VAL) => x + y
EVALUATE { MyFunc(1, 2) }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineFunctionNoTypes()
        {
            var input = @"DEFINE
    FUNCTION Double = (x) => x * 2
EVALUATE { Double(21) }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineFunctionTableParam()
        {
            var input = @"DEFINE
    FUNCTION CountRows2 = (t : TABLE) => COUNTROWS(t)
EVALUATE { CountRows2('Product') }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_DefineMultipleEntities()
        {
            var input = @"DEFINE
    VAR _threshold = 100
    MEASURE 'Sales'[Big Sales] = CALCULATE(SUM('Sales'[Amount]), 'Sales'[Amount] > _threshold)
    TABLE HighValueProducts = FILTER('Product', 'Product'[ListPrice] > _threshold)
EVALUATE HighValueProducts";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.HasCount(3, tree.defineBlock().definition());
        }

        #endregion

        #region Parser - Expressions

        [TestMethod]
        public void Parser_ArithmeticExpression()
        {
            var input = "EVALUATE { 1 + 2 * 3 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ComparisonWithStrictEquals()
        {
            var input = @"EVALUATE FILTER('Product', 'Product'[Color] == ""Red"")";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_LogicalAndOr()
        {
            var input = @"EVALUATE FILTER('Product', 'Product'[Color] = ""Red"" && 'Product'[ListPrice] > 100 || 'Product'[Color] = ""Blue"")";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_NotExpression()
        {
            var input = @"EVALUATE FILTER('Product', NOT 'Product'[Color] = ""Red"")";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_UnaryMinus()
        {
            var input = "EVALUATE { -42 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_UnaryPlus()
        {
            var input = "EVALUATE { +42 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_PowerExpression()
        {
            var input = "EVALUATE { 2 ^ 3 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ConcatExpression()
        {
            var input = @"EVALUATE { ""Hello"" & "" "" & ""World"" }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_InExpression()
        {
            var input = @"EVALUATE FILTER('Product', 'Product'[Color] IN { ""Red"", ""Blue"", ""Green"" })";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_InExpressionMultiColumn()
        {
            var input = @"EVALUATE FILTER('Product', ('Product'[Color], 'Product'[Size]) IN { (""Red"", ""L""), (""Blue"", ""M"") })";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParenthesizedExpression()
        {
            var input = "EVALUATE { (1 + 2) * 3 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Parser - VAR/RETURN

        [TestMethod]
        public void Parser_VarReturnInExpression()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Pct] =
        VAR TotalSales = SUM('Sales'[Amount])
        VAR TargetSales = 1000000
        RETURN TotalSales / TargetSales
EVALUATE { [Pct] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_NestedVarReturn()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Complex] =
        VAR A = 1
        RETURN
            VAR B = A + 1
            RETURN B * 2
EVALUATE { [Complex] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Parser - Function Calls

        [TestMethod]
        public void Parser_NestedFunctionCall()
        {
            var input = @"EVALUATE
SUMMARIZECOLUMNS(
    'Product'[Color],
    'Date'[Year],
    ""Total"", SUM('Sales'[Amount])
)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_CalculateWithFilters()
        {
            var input = @"EVALUATE
{ CALCULATE(
    SUM('Sales'[Amount]),
    'Product'[Color] = ""Red"",
    'Date'[Year] = 2024
) }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_SwitchExpression()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Grade] = SWITCH(
        TRUE(),
        [Total Sales] > 1000, ""A"",
        [Total Sales] > 500, ""B"",
        ""C""
    )
EVALUATE { [Grade] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_NewFunctions()
        {
            var input = @"EVALUATE
ADDCOLUMNS(
    'Product',
    ""Rank"", RANK(, ORDERBY('Product'[ListPrice], DESC)),
    ""RowNum"", ROWNUMBER(, ORDERBY('Product'[Name]))
)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Parser - Table Constructors

        [TestMethod]
        public void Parser_SimpleTableConstructor()
        {
            var input = @"EVALUATE { 1, 2, 3 }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_RowTableConstructor()
        {
            var input = @"EVALUATE { (1, ""A""), (2, ""B""), (3, ""C"") }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Parser - Column References

        [TestMethod]
        public void Parser_QualifiedColumnRef()
        {
            var input = @"EVALUATE { 'Product'[Color] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_UnqualifiedColumnRef()
        {
            var input = @"EVALUATE { [Sales Amount] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Parser - Complex Real-World Queries

        [TestMethod]
        public void Parser_ComplexSummarizeColumns()
        {
            var input = @"DEFINE VAR _value = @value
EVALUATE
SUMMARIZECOLUMNS (
    'Product'[Color],
    Reseller[Business Type],
    FILTER ( ALL ( 'Product'[List Price] ), 'Product'[List Price] > _value ),
    TREATAS ( { ""Accessories"", ""Bikes"" }, 'Product'[Category] ),
    ""Total Sales"", SUM(Sales[Sales Amount])
)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ComplexDateCalculation()
        {
            var input = @"DEFINE
    VAR ReferenceDate =
        DATE(HolidayYear, 1
            + MOD( [MonthNumber] - 1 + IF( [OffsetWeek] < 0, 1), 12 ), 1 )
            - IF( [OffsetWeek] < 0, 1 )
EVALUATE {ReferenceDate}";

            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_TotalWtdWithCalendar()
        {
            var input = @"EVALUATE { TOTALWTD(SUM('Sales'[Amount]), 'Date'[Date]) }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_WindowFunction()
        {
            var input = @"EVALUATE
ADDCOLUMNS(
    SUMMARIZE('Sales', 'Product'[Category]),
    ""Running Total"", CALCULATE(
        SUM('Sales'[Amount]),
        WINDOW(-2, ABS, 0, REL, ORDERBY('Product'[Category]))
    )
)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_MultiEvaluateWithDefine()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Total] = SUM('Sales'[Amount])
EVALUATE
    SUMMARIZECOLUMNS('Product'[Color], ""Sales"", [Total])
    ORDER BY 'Product'[Color]
EVALUATE
    SUMMARIZECOLUMNS('Date'[Year], ""Sales"", [Total])
    ORDER BY 'Date'[Year] DESC";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
            Assert.HasCount(2, tree.evaluateBlock());
        }

        [TestMethod]
        public void Parser_UdfDefinitionAndCall()
        {
            var input = @"DEFINE
    FUNCTION Finance.NetMargin = (revenue : SCALAR DECIMAL VAL, cost : SCALAR DECIMAL VAL)
        => DIVIDE(revenue - cost, revenue)
    MEASURE 'Sales'[Net Margin] = Finance.NetMargin(SUM('Sales'[Revenue]), SUM('Sales'[Cost]))
EVALUATE { [Net Margin] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region DateTable Tests

        [TestMethod]
        public void Parser_DateTableExpression()
        {
            // Parse the massive real-world DateTable DAX expression (~1500 lines)
            // This covers: deeply nested VARs, DATATABLE with type keywords,
            // GENERATE, GENERATESERIES, SELECTCOLUMNS, ADDCOLUMNS, FILTER,
            // UNION, ERROR, IF, SWITCH, CONTAINSROW, CALENDAR, nested VAR/RETURN,
            // complex arithmetic, string concatenation, and many more functions.
            var input = DaxDateTable.DateTable;
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors,
                $"DateTable should parse with no errors. First errors: {string.Join("; ", errors.Take(5))}");
            Assert.IsNotNull(tree);
            Assert.IsNotNull(tree.evaluateBlock(), "Should have an EVALUATE block");
        }

        #endregion

        #region Parameter Tests

        [TestMethod]
        public void Lexer_ParameterToken()
        {
            var tokens = Tokenize("@myParam");
            Assert.AreEqual(1, tokens.Count(t => t.Type != DAXLexer.Eof));
            Assert.AreEqual(DAXLexer.PARAMETER, tokens[0].Type);
            Assert.AreEqual("@myParam", tokens[0].Text);
        }

        [TestMethod]
        public void Lexer_MultipleParameters()
        {
            var tokens = Tokenize("@StartDate @EndDate @ProductKey").Where(t => t.Type != DAXLexer.Eof && t.Channel == 0).ToList();
            Assert.HasCount(3, tokens);
            Assert.IsTrue(tokens.All(t => t.Type == DAXLexer.PARAMETER));
            Assert.AreEqual("@StartDate", tokens[0].Text);
            Assert.AreEqual("@EndDate", tokens[1].Text);
            Assert.AreEqual("@ProductKey", tokens[2].Text);
        }

        [TestMethod]
        public void Lexer_ParameterWithKeywordName()
        {
            // Parameters whose names are DAX keywords should still be recognized
            var tokens = Tokenize("@value @table @filter");
            var paramTokens = tokens.Where(t => t.Type == DAXLexer.PARAMETER).ToList();
            Assert.HasCount(3, paramTokens);
            Assert.AreEqual("@value", paramTokens[0].Text);
            Assert.AreEqual("@table", paramTokens[1].Text);
            Assert.AreEqual("@filter", paramTokens[2].Text);
        }

        [TestMethod]
        public void Parser_ParameterInExpression()
        {
            var input = "EVALUATE FILTER('Sales', 'Sales'[Amount] > @minAmount)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInCalculate()
        {
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]), 'Sales'[ProductKey] = @ProductKey)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_MultipleParametersInQuery()
        {
            var input = @"EVALUATE
    CALCULATETABLE(
        'Sales',
        'Sales'[OrderDate] >= @StartDate,
        'Sales'[OrderDate] <= @EndDate,
        'Sales'[ProductKey] = @ProductKey
    )";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInStartAt()
        {
            var input = @"EVALUATE 'Sales'
ORDER BY 'Sales'[OrderDate]
START AT @StartDate";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInArithmetic()
        {
            var input = "EVALUATE { @Price * @Quantity + @Tax }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInDefineVar()
        {
            var input = @"DEFINE
    VAR FilterValue = @SelectedColor
EVALUATE
    FILTER('Product', 'Product'[Color] = FilterValue)";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInDefineMeasure()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Filtered Sales] =
        CALCULATE(SUM('Sales'[Amount]), 'Sales'[Amount] > @Threshold)
EVALUATE { [Filtered Sales] }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInStringConcatenation()
        {
            var input = @"EVALUATE { ""Result: "" & @UserInput & "" end"" }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterAsStandaloneEvaluate()
        {
            var input = "EVALUATE { @ScalarValue }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInComparison()
        {
            var input = @"EVALUATE
    FILTER(
        'Product',
        'Product'[Color] = @Color
            && 'Product'[Category] <> @ExcludeCategory
    )";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        [TestMethod]
        public void Parser_ParameterInComplexRealWorldQuery()
        {
            // Realistic query with multiple parameters as used by external callers
            var input = @"DEFINE
    VAR StartDate = @StartDate
    VAR EndDate = @EndDate
    MEASURE 'Sales'[Total] = SUM('Sales'[Amount])
    MEASURE 'Sales'[Filtered Total] =
        CALCULATE(
            [Total],
            'Sales'[OrderDate] >= StartDate,
            'Sales'[OrderDate] <= EndDate
        )
EVALUATE
    SUMMARIZECOLUMNS(
        'Product'[Category],
        'Product'[Color],
        FILTER(ALL('Product'[Color]), 'Product'[Color] = @Color),
        ""Total Sales"", [Filtered Total],
        ""Average"", DIVIDE([Filtered Total], @DayCount)
    )
ORDER BY 'Product'[Category]";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors, string.Join("; ", errors));
        }

        #endregion

        #region Keyword-as-Identifier Rejection Tests

        [TestMethod]
        public void Parser_ReturnAsVarName_ProducesError()
        {
            // RETURN is a structural keyword and cannot be used as a variable name
            var input = @"DEFINE
VAR return = 1
EVALUATE
{return}";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'return' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_VarAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR var = 1
EVALUATE { var }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'var' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_DefineAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR define = 1
EVALUATE { define }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'define' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_EvaluateAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR evaluate = 1
EVALUATE { evaluate }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'evaluate' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_MeasureAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR measure = 1
EVALUATE { measure }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'measure' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_OrderAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR order = 1
EVALUATE { order }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'order' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_TableAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR table = 1
EVALUATE { table }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'table' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_InAsVarName_ProducesError()
        {
            var input = @"DEFINE
VAR in = 1
EVALUATE { in }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsNotEmpty(errors,
                "Using 'in' as a variable name should produce parse errors");
        }

        [TestMethod]
        public void Parser_FunctionNameAsVarName_IsAllowed()
        {
            // Function names like Offset, Filter, etc. ARE valid as variable names
            var input = @"DEFINE
VAR Offset = 42
VAR Filter = 100
EVALUATE { Offset + Filter }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors,
                $"Function names should be usable as variable names. Errors: {string.Join("; ", errors)}");
        }

        [TestMethod]
        public void Parser_TypeKeywordAsVarName_IsAllowed()
        {
            // Type keywords like Double, Integer, etc. ARE valid as variable names
            var input = @"DEFINE
VAR Double = 2
VAR Integer = 3
EVALUATE { Double + Integer }";
            var tree = ParseQuery(input, out var errors);
            Assert.IsEmpty(errors,
                $"Type keywords should be usable as variable names. Errors: {string.Join("; ", errors)}");
        }

        #endregion
    }
}
