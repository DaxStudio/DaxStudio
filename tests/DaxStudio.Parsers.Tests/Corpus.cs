using Antlr4.Runtime;
using DaxStudio.Parsers.Grammars.Generated;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Queries collected from the parser tests, used to compare the Parlot parser with the ANTLR one.
    /// One JSON string per line.
    /// </summary>
    internal static class Corpus
    {
        public static IReadOnlyList<string> XmSql { get; } = Load("xmsql.jsonl");

        public static IReadOnlyList<string> DirectQuery { get; } = Load("directquery.jsonl");

        private static IReadOnlyList<string> Load(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Corpus", name);
            return File.ReadAllLines(path)
                .Where(l => l.Length > 0)
                .Select(l => JsonSerializer.Deserialize<string>(l))
                .ToList();
        }

        public static IEnumerable<object[]> XmSqlCases => XmSql.Select((q, i) => new object[] { i });

        /// <summary>Number of syntax errors the ANTLR xmSQL grammar reports for a query.</summary>
        public static int AntlrSyntaxErrors(string xmSql)
        {
            var lexer = new xmSQLLexer(new AntlrInputStream(xmSql));
            lexer.RemoveErrorListeners();
            var parser = new xmSQLParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.query();
            return parser.NumberOfSyntaxErrors;
        }

        /// <summary>Number of syntax errors the ANTLR DirectQuery SQL grammar reports for a query.</summary>
        public static int AntlrDirectQuerySyntaxErrors(string sql)
        {
            var lexer = new DirectQuerySqlLexer(new AntlrInputStream(sql));
            lexer.RemoveErrorListeners();
            var parser = new DaxStudio.Parsers.Grammars.Generated.DirectQuerySqlParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners();
            parser.query();
            return parser.NumberOfSyntaxErrors;
        }
    }
}
