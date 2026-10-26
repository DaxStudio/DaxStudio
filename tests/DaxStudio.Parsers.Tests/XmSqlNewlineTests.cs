using Antlr4.Runtime;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.StorageEngine;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class XmSqlNewlineTests
    {
        [DataTestMethod]
        [DataRow("\r\n")]
        [DataRow("\n")]
        [DataRow("\r")]
        public void CallbackWrapperNewlines_ArePreserved(string newline)
        {
            var callback = "[" + newline + "CallbackDataID" + newline
                + "(IF ( 'Sales'[Amount]] > 1000, 'Sales'[Amount]] ))" + newline + "]";
            AssertSingleToken(callback, xmSQLLexer.BRACKETED_NAME);
            var query = "WITH $Expr0 := " + callback + "(PFDATAID('Sales'[Amount]))"
                + " $Expr1 := (1) SELECT SUM(@$Expr0), SUM(@$Expr1) FROM 'Sales';";
            AssertParses(query);

            foreach (bool format in new[] { false, true })
            foreach (bool simplify in new[] { false, true })
            {
                var result = AntlrXmSqlFormatter.Format(query, format, simplify, out _, out _, out _);
                Assert.IsNotNull(result);
                StringAssert.Contains(result, callback.Replace("]]", "]"));
                StringAssert.Contains(result, "PFDATAID ( 'Sales'[Amount] )");
                StringAssert.Contains(result, "$Expr1 := (1)");
                StringAssert.Contains(result, "SUM ( @$Expr0 )");
                StringAssert.Contains(result, "SUM ( @$Expr1 )");
                StringAssert.Contains(result, "FROM 'Sales'");
            }
        }

        [DataTestMethod]
        [DataRow("\r\n", "'")]
        [DataRow("\n", "'")]
        [DataRow("\r", "'")]
        [DataRow("\r\n", "\"")]
        [DataRow("\n", "\"")]
        [DataRow("\r", "\"")]
        public void MultilineFilterLiterals_ArePreserved(string newline, string quote)
        {
            var literal = quote + "first" + newline + "second" + quote;
            AssertSingleToken(literal, quote == "'" ? xmSQLLexer.QUOTED_TABLE_NAME : xmSQLLexer.QUOTED_STRING);
            var query = "SELECT 'Sales'[Name] FROM 'Sales' WHERE 'Sales'[Name] = " + literal
                + " VAND 'Sales'[Amount] > 0;";
            AssertParses(query);

            foreach (bool format in new[] { false, true })
            foreach (bool simplify in new[] { false, true })
            {
                var result = AntlrXmSqlFormatter.Format(query, format, simplify, out _, out _, out _);
                Assert.IsNotNull(result);
                StringAssert.Contains(result, literal);
                StringAssert.Contains(result, "VAND");
                StringAssert.Contains(result, "'Sales'[Amount]");
            }
        }

        [DataTestMethod]
        [DataRow("\r\n")]
        [DataRow("\n")]
        [DataRow("\r")]
        public void MultilineTableNames_ParseWithoutRecovery(string newline)
        {
            var name = "Sales" + newline + "Data";
            AssertSingleToken("'" + name + "'", xmSQLLexer.QUOTED_TABLE_NAME);
            AssertSingleToken("[" + name + "]", xmSQLLexer.BRACKETED_NAME);
            AssertParses("SELECT '" + name + "'[Amount] FROM '" + name + "';");
            AssertParses("SELECT [" + name + "].[Amount] FROM [" + name + "];");
        }

        private static void AssertSingleToken(string text, int tokenType)
        {
            var lexer = new xmSQLLexer(new AntlrInputStream(text));
            var token = lexer.NextToken();
            Assert.AreEqual(tokenType, token.Type);
            Assert.AreEqual(text, token.Text);
            Assert.AreEqual(TokenConstants.EOF, lexer.NextToken().Type);
        }

        private static void AssertParses(string query)
        {
            var lexer = new xmSQLLexer(new AntlrInputStream(query));
            var parser = new xmSQLParser(new CommonTokenStream(lexer));
            parser.query();
            Assert.AreEqual(0, parser.NumberOfSyntaxErrors, "Newlines must not trigger parser recovery.");
        }
    }
}
