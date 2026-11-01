using DaxStudio.Parsers.DirectQuery;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Compares the Parlot DirectQuery SQL parser with the ANTLR one over the corpus: both fill an
    /// <see cref="XmSqlAnalysis"/>, which must be identical.
    /// </summary>
    [TestClass]
    public class ParlotDirectQueryDifferentialTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Queries where the Parlot grammar uses the T-SQL precedences of IS NULL and NOT and finds filters that ANTLR
        /// loses: its rule makes IS NULL apply to everything before it, so the IS node's operand is not a column.
        /// </summary>
        private static readonly string[] PrecedenceDeviations =
        {
            "WHERE [t4].[Email] IS NULL OR [t4].[Phone] IS NOT NULL",
            "WHERE [t4].[Age] = 40 AND [t4].[Email] IS NULL",
            "([t4].[Stock] = 0 OR [t4].[Stock] IS NULL)",
        };

        [TestMethod]
        public void DirectQueryAnalysisMatchesAntlr()
        {
            var report = new StringBuilder();
            int clean = 0, withAntlrErrors = 0, mismatches = 0, deviations = 0, mismatchesWithAntlrErrors = 0, parlotRejectsClean = 0, parlotAcceptsInvalid = 0;

            var antlrParser = new AntlrXmSqlParser();
            var antlrAll = new XmSqlAnalysis();
            var parlotAll = new XmSqlAnalysis();
            var allClean = true;

            for (int i = 0; i < Corpus.DirectQuery.Count; i++)
            {
                var query = Corpus.DirectQuery[i];
                var antlrErrors = Corpus.AntlrDirectQuerySyntaxErrors(query);
                if (antlrErrors > 0) withAntlrErrors++; else clean++;

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var parsed = DirectQuerySqlGrammar.Parse(query) != null;
                    if (antlrErrors == 0 && !parsed)
                    {
                        parlotRejectsClean++;
                        report.AppendLine($"#{i} is rejected by the Parlot grammar");
                    }
                    else if (antlrErrors > 0 && parsed)
                    {
                        parlotAcceptsInvalid++;
                        report.AppendLine($"#{i} is accepted by the Parlot grammar");
                    }
                }

                var antlr = new XmSqlAnalysis();
                var parlot = new XmSqlAnalysis();
                var antlrResult = antlrParser.ParseDirectQuerySql(query, antlr, Metrics(i));
                var parlotResult = ParlotDirectQuerySqlParser.Parse(query, parlot, Metrics(i));

                var deviates = PrecedenceDeviations.Any(query.Contains);
                if (antlrErrors == 0 && !deviates)
                {
                    antlrParser.ParseDirectQuerySql(query, antlrAll, Metrics(i));
                    ParlotDirectQuerySqlParser.Parse(query, parlotAll, Metrics(i));
                }

                var expected = Dump(antlr);
                var actual = Dump(parlot);
                if (antlrResult != parlotResult || expected != actual)
                {
                    if (deviates) deviations++; else if (antlrErrors > 0) mismatchesWithAntlrErrors++; else mismatches++;
                    report.AppendLine($"#{i} antlrErrors={antlrErrors} antlr={antlrResult} parlot={parlotResult}{(deviates ? " (precedence fix)" : "")}");
                    report.AppendLine("--- input");
                    report.AppendLine(query);
                    report.AppendLine("--- antlr");
                    report.AppendLine(expected);
                    report.AppendLine("--- parlot");
                    report.AppendLine(actual);
                    report.AppendLine();
                }
            }

            // All the clean queries but the precedence deviations accumulated into one analysis (shared tables, relationship dedup, counters).
            var expectedAll = Dump(antlrAll);
            var actualAll = Dump(parlotAll);
            if (expectedAll != actualAll)
            {
                allClean = false;
                report.AppendLine("--- accumulated antlr");
                report.AppendLine(expectedAll);
                report.AppendLine("--- accumulated parlot");
                report.AppendLine(actualAll);
            }

            report.Insert(0, $"{Corpus.DirectQuery.Count} queries, {clean} without ANTLR syntax errors, {withAntlrErrors} with; " +
                $"{mismatches} mismatches, {mismatchesWithAntlrErrors} more where ANTLR reports syntax errors\n");
            TestContext.WriteLine(report.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "parlot-directquery-diff.txt"), report.ToString());

            Assert.IsTrue(clean >= 40, $"only {clean} clean queries in the corpus");
            Assert.AreEqual(0, parlotRejectsClean, "queries ANTLR parses are rejected by the Parlot grammar");
            Assert.AreEqual(0, parlotAcceptsInvalid, "queries with ANTLR syntax errors are accepted by the Parlot grammar");
            Assert.AreEqual(0, mismatches, $"{mismatches} queries analyse differently ({mismatchesWithAntlrErrors} more where ANTLR reports syntax errors)");
            Assert.IsTrue(allClean, "the accumulated analysis differs");
            Assert.AreEqual(PrecedenceDeviations.Length, deviations, "each precedence deviation differs from ANTLR");
        }

        [TestMethod]
        [DataRow("[t1].[a] IS NULL OR [t1].[b] IS NOT NULL", "(([t1].[a] IS NULL) OR ([t1].[b] IS NOT NULL))")]
        [DataRow("[t1].[a] = 40 AND [t1].[b] IS NULL", "(([t1].[a] = 40) AND ([t1].[b] IS NULL))")]
        [DataRow("[t1].[a] + 1 IS NULL", "(([t1].[a] + 1) IS NULL)")]
        [DataRow("NOT [t1].[a] = 1 OR [t1].[b] = 2", "(NOT(([t1].[a] = 1)) OR ([t1].[b] = 2))")]
        [DataRow("NOT [t1].[a] IS NULL AND [t1].[b] = 2", "(NOT(([t1].[a] IS NULL)) AND ([t1].[b] = 2))")]
        [DataRow("[t1].[a] = 1 OR [t1].[b] = 2 AND [t1].[c] = 3", "(([t1].[a] = 1) OR (([t1].[b] = 2) AND ([t1].[c] = 3)))")]
        public void ExpressionsUseTSqlPrecedence(string where, string expected)
        {
            var statement = DirectQuerySqlGrammar.Parse("SELECT [t1].[a] AS [c1] FROM [dbo].[A] AS [t1] WHERE " + where);

            Assert.IsNotNull(statement);
            Assert.AreEqual(expected, Show(statement.Where));
        }

        [TestMethod]
        public void NullTestsAfterAndOrAreFilters()
        {
            var analysis = new XmSqlAnalysis();
            ParlotDirectQuerySqlParser.Parse(
                "SELECT [t1].[a] AS [c1] FROM [dbo].[A] AS [t1] WHERE [t1].[b] = 40 AND [t1].[c] IS NULL OR [t1].[d] IS NOT NULL",
                analysis, new XmSqlParser.SeEventMetrics());

            var columns = analysis.Tables["A"].Columns;
            foreach (var name in new[] { "b", "c", "d" })
            {
                Assert.IsTrue(columns[name].UsageTypes.HasFlag(XmSqlColumnUsage.Filter), name);
            }
        }

        private static string Show(DqExpression e)
        {
            switch (e.Kind)
            {
                case DqExpressionKind.Binary: return "(" + Show(e.Left) + " " + e.OperatorText + " " + Show(e.Right) + ")";
                case DqExpressionKind.IsNull: return "(" + Show(e.Left) + (e.IsNot ? " IS NOT NULL)" : " IS NULL)");
                case DqExpressionKind.Not: return "NOT(" + Show(e.Left) + ")";
                case DqExpressionKind.Parenthesized: return "[" + Show(e.Left) + "]";
                default: return e.Unary.Kind == DqUnaryKind.Literal ? e.Unary.Literal.ToString() : e.Unary.Name + "." + e.Unary.Column;
            }
        }

        [TestMethod]
        public void DirectQueryEmptyReturnsFalse()
        {
            var analysis = new XmSqlAnalysis();
            Assert.IsFalse(ParlotDirectQuerySqlParser.Parse("", analysis, new XmSqlParser.SeEventMetrics()));
            Assert.IsFalse(ParlotDirectQuerySqlParser.Parse(null, analysis, new XmSqlParser.SeEventMetrics()));
            Assert.IsFalse(ParlotDirectQuerySqlParser.Parse("  \r\n", analysis, null));
            Assert.AreEqual(0, analysis.TotalSEQueriesAnalyzed);
        }

        /// <summary>Varied metrics so that every metric path of the walkers is compared.</summary>
        private static XmSqlParser.SeEventMetrics Metrics(int i)
        {
            if (i % 7 == 6) return null;
            return new XmSqlParser.SeEventMetrics
            {
                DurationMs = i % 3 == 0 ? (long?)null : i * 7,
                EstimatedRows = i % 4 == 0 ? (long?)null : i * 100 - 150,
                CpuTimeMs = i % 5 == 0 ? (long?)null : i,
                IsCacheHit = i % 6 == 0,
                QueryId = i % 8 == 0 ? 0 : i,
            };
        }

        /// <summary>A deterministic dump of every public property, with unordered collections sorted.</summary>
        internal static string Dump(object value)
        {
            var sb = new StringBuilder();
            Dump(value, sb, 0);
            return sb.ToString();
        }

        private static void Dump(object value, StringBuilder sb, int indent)
        {
            var pad = new string(' ', indent * 2);
            if (value == null) { sb.Append("null"); return; }

            var type = value.GetType();
            if (value is string s) { sb.Append('"').Append(s).Append('"'); return; }
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            if (value is IDictionary dictionary)
            {
                var keys = dictionary.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal);
                sb.Append("{\n");
                foreach (var key in keys)
                {
                    sb.Append(pad).Append("  ").Append(Convert.ToString(key, CultureInfo.InvariantCulture)).Append(": ");
                    Dump(dictionary[key], sb, indent + 1);
                    sb.Append('\n');
                }

                sb.Append(pad).Append('}');
                return;
            }

            if (value is IEnumerable enumerable)
            {
                var items = enumerable.Cast<object>().Select(o => Dump(o)).ToList();
                var isSet = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>);
                if (isSet) items.Sort(StringComparer.Ordinal);
                sb.Append('[').Append(string.Join(", ", items)).Append(']');
                return;
            }

            if (type.FullName.StartsWith("System.ValueTuple", StringComparison.Ordinal))
            {
                sb.Append('(').Append(string.Join(", ", type.GetFields().Select(f => Dump(f.GetValue(value))))).Append(')');
                return;
            }

            sb.Append(type.Name).Append(" {\n");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                sb.Append(pad).Append("  ").Append(property.Name).Append(" = ");
                Dump(property.GetValue(value), sb, indent + 1);
                sb.Append('\n');
            }

            sb.Append(pad).Append('}');
        }
    }
}
