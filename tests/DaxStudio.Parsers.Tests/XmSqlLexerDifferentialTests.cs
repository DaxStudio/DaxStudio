using DaxStudio.Parsers.StorageEngine;
using Antlr4.Runtime;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.StorageEngine.XmSql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Compares the token scanners of <see cref="XmSqlLexer"/> with the ANTLR lexer on random text made of the
    /// chars that matter to each rule.
    /// </summary>
    [TestClass]
    public class XmSqlLexerDifferentialTests
    {
        [DataTestMethod]
        [DataRow("[", "[]()a'\n ", xmSQLLexer.BRACKETED_NAME)]
        [DataRow("'", "'a\n ]", xmSQLLexer.QUOTED_TABLE_NAME)]
        [DataRow("\"", "\"a\n", xmSQLLexer.QUOTED_STRING)]
        [DataRow("-", "-0123.eE+a", xmSQLLexer.NUMBER)]
        [DataRow("1", "0123.eE+-a", xmSQLLexer.NUMBER)]
        public void FirstTokenMatchesAntlr(string prefix, string alphabet, int tokenType)
        {
            var random = new Random(1234);
            var sb = new StringBuilder();
            int matched = 0;
            for (int n = 0; n < 50000; n++)
            {
                sb.Clear().Append(prefix);
                var length = random.Next(0, 14);
                for (int i = 0; i < length; i++) sb.Append(alphabet[random.Next(alphabet.Length)]);
                var text = sb.ToString();

                var lexer = new xmSQLLexer(new AntlrInputStream(text));
                lexer.RemoveErrorListeners();
                var token = lexer.NextToken();
                var expected = token.Type == tokenType ? token.StopIndex - token.StartIndex + 1 : 0;

                int actual;
                switch (tokenType)
                {
                    case xmSQLLexer.BRACKETED_NAME: actual = XmSqlLexer.BracketedName(text.AsSpan()); break;
                    case xmSQLLexer.QUOTED_TABLE_NAME: actual = XmSqlLexer.QuotedTableName(text.AsSpan()); break;
                    case xmSQLLexer.QUOTED_STRING: actual = XmSqlLexer.QuotedString(text.AsSpan()); break;
                    default: actual = XmSqlLexer.Number(text.AsSpan()); break;
                }

                Assert.AreEqual(expected, actual, "Token length for " + Escape(text));
                if (expected > 0) matched++;
            }

            // The random text must produce tokens, not only failures
            Assert.IsTrue(matched > 1000, matched + " tokens matched");
        }

        [DataTestMethod]
        [DataRow("SELECT")]
        [DataRow("select")]
        [DataRow("COUNT_BIG")]
        [DataRow("SUMX")]
        [DataRow("IN32")]
        [DataRow("PFDATAID")]
        [DataRow("_x")]
        public void KeywordsMatchAntlr(string word)
        {
            var lexer = new xmSQLLexer(new AntlrInputStream(word));
            var token = lexer.NextToken();
            Assert.AreEqual(word.Length, token.StopIndex - token.StartIndex + 1);
            Assert.AreEqual(token.Type != xmSQLLexer.IDENTIFIER, XmSqlLexer.IsKeyword(word.AsSpan()), word);
        }

        private static string Escape(string text) => text.Replace("\n", "\\n");
    }
}
