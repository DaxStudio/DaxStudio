using DaxStudio.Parsers.StorageEngine;
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
    /// Compares the Parlot analysis and fingerprinting with the ANTLR ones over the corpus.
    /// </summary>
    [TestClass]
    public class ParlotAnalysisDifferentialTests
    {
        public TestContext TestContext { get; set; }

        private static IEnumerable<(string Name, XmSqlParser.SeEventMetrics Metrics)> MetricsVariants()
        {
            yield return ("null", null);
            yield return ("empty", new XmSqlParser.SeEventMetrics());
            yield return ("cacheHit", new XmSqlParser.SeEventMetrics { IsCacheHit = true, QueryId = 7 });
            yield return ("full", new XmSqlParser.SeEventMetrics
            {
                EstimatedRows = 1000,
                DurationMs = 12,
                CpuTimeMs = 30,
                CpuFactor = 2.5,
                NetParallelDurationMs = 5,
                QueryId = 42,
            });
            yield return ("serial", new XmSqlParser.SeEventMetrics
            {
                EstimatedRows = 0,
                DurationMs = 3,
                CpuTimeMs = 0,
                CpuFactor = 1.0,
                QueryId = 0,
            });
        }

        [TestMethod]
        public void AnalysisMatchesAntlr()
        {
            var report = new StringBuilder();
            int mismatches = 0, mismatchesWithAntlrErrors = 0, comparisons = 0;

            for (int i = 0; i < Corpus.XmSqlRawAndFormatted.Count; i++)
            {
                var query = Corpus.XmSqlRawAndFormatted[i];
                var antlrErrors = Corpus.AntlrSyntaxErrors(query);

                var cases = MetricsVariants()
                    .Select(v => (v.Name, (Func<IXmSqlParser, XmSqlAnalysis, bool>)((p, a) => p.ParseQueryWithMetrics(query, a, v.Metrics))))
                    .Append(("ParseQuery", (p, a) => p.ParseQuery(query, a, 100, 20)));

                foreach (var (name, parse) in cases)
                {
                    comparisons++;
                    var expectedAnalysis = new XmSqlAnalysis();
                    var expectedResult = parse(new AntlrXmSqlParser(), expectedAnalysis);
                    var actualAnalysis = new XmSqlAnalysis();
                    var actualResult = parse(new ParlotXmSqlParser(), actualAnalysis);

                    var expected = expectedResult + "\n" + Dump(expectedAnalysis);
                    var actual = actualResult + "\n" + Dump(actualAnalysis);
                    if (expected != actual)
                    {
                        if (antlrErrors > 0) mismatchesWithAntlrErrors++; else mismatches++;
                        ReportMismatch(report, $"#{i} metrics={name} antlrErrors={antlrErrors}", query, expected, actual);
                        break;
                    }
                }
            }

            Finish(report, "analysis", comparisons, mismatches, mismatchesWithAntlrErrors);
        }

        [TestMethod]
        public void BatchAnalysisMatchesAntlr()
        {
            var antlr = new AntlrXmSqlParser();
            var parlot = new ParlotXmSqlParser();
            var expectedAnalysis = new XmSqlAnalysis();
            var actualAnalysis = new XmSqlAnalysis();
            var variants = MetricsVariants().ToList();

            for (int i = 0; i < Corpus.XmSqlComparable.Count; i++)
            {
                var metrics = variants[i % variants.Count].Metrics;
                antlr.ParseQueryWithMetrics(Corpus.XmSqlComparable[i], expectedAnalysis, metrics);
                parlot.ParseQueryWithMetrics(Corpus.XmSqlComparable[i], actualAnalysis, metrics);
            }

            var expected = Dump(expectedAnalysis);
            var actual = Dump(actualAnalysis);
            if (expected != actual)
            {
                var report = new StringBuilder();
                ReportMismatch(report, "batch", "(all corpus queries)", expected, actual);
                TestContext.WriteLine(report.ToString());
            }

            Assert.AreEqual(Corpus.XmSqlComparable.Count(q => !string.IsNullOrWhiteSpace(q)), expectedAnalysis.TotalSEQueriesAnalyzed);
            Assert.IsTrue(expectedAnalysis.Tables.Count > 5 && expectedAnalysis.Relationships.Count > 0
                && expectedAnalysis.Tables.Values.Any(t => t.Columns.Values.Any(c => c.FilterValues.Count > 0)), "the corpus exercises the analysis");
            Assert.AreEqual(expected, actual);

            // ParseQueries runs the same batch without metrics
            Assert.AreEqual(Dump(antlr.ParseQueries(Corpus.XmSqlComparable)), Dump(parlot.ParseQueries(Corpus.XmSqlComparable)));
        }

        [TestMethod]
        public void FingerprintMatchesAntlr()
        {
            var report = new StringBuilder();
            int mismatches = 0, mismatchesWithAntlrErrors = 0, comparisons = 0;
            var antlr = new XmSqlQueryGrouper();
            var parlot = new ParlotXmSqlQueryGrouper();

            for (int i = 0; i < Corpus.XmSqlRawAndFormatted.Count; i++)
            {
                var query = Corpus.XmSqlRawAndFormatted[i];
                var antlrErrors = Corpus.AntlrSyntaxErrors(query);

                foreach (var text in new[] { query, XmSqlQueryGrouper.NormalizeQueryText(query) })
                {
                    comparisons++;
                    var expected = Dump(antlr.ComputeFingerprint(text));
                    var actual = Dump(parlot.ComputeFingerprint(text));
                    if (expected != actual)
                    {
                        if (antlrErrors > 0) mismatchesWithAntlrErrors++; else mismatches++;
                        ReportMismatch(report, $"#{i} antlrErrors={antlrErrors}", text, expected, actual);
                        break;
                    }
                }
            }

            Finish(report, "fingerprint", comparisons, mismatches, mismatchesWithAntlrErrors);
        }

        [TestMethod]
        public void GroupingMatchesAntlr()
        {
            var queries = Corpus.XmSqlComparable.Select((q, i) => (i + 1, q)).ToList();
            var expected = Dump(new XmSqlQueryGrouper().GroupQueries(queries));
            var actual = Dump(new ParlotXmSqlQueryGrouper().GroupQueries(queries));
            Assert.AreEqual(expected, actual);
        }

        private static void ReportMismatch(StringBuilder report, string title, string query, string expected, string actual)
        {
            report.AppendLine(title);
            report.AppendLine("--- input");
            report.AppendLine(query);
            report.AppendLine("--- antlr");
            report.AppendLine(expected);
            report.AppendLine("--- parlot");
            report.AppendLine(actual);
            report.AppendLine();
        }

        private void Finish(StringBuilder report, string name, int comparisons, int mismatches, int mismatchesWithAntlrErrors)
        {
            report.Insert(0, $"{Corpus.XmSqlRawAndFormatted.Count} queries, {comparisons} comparisons, {mismatches} mismatches, {mismatchesWithAntlrErrors} mismatches where ANTLR reports syntax errors\n");
            TestContext.WriteLine(report.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"parlot-{name}-diff.txt"), report.ToString());
            Assert.IsTrue(Corpus.XmSqlRawAndFormatted.Count > 100);
            Assert.AreEqual(0, mismatches, $"{mismatches} queries differ ({mismatchesWithAntlrErrors} more where ANTLR reports syntax errors)");
        }

        // ==================== deterministic dump ====================

        /// <summary>
        /// Writes every public property and field of an object graph, in declaration order. Collections keep
        /// their enumeration order, which is the insertion order for the dictionaries and sets of the model.
        /// </summary>
        internal static string Dump(object value)
        {
            var sb = new StringBuilder();
            Dump(sb, value, 0);
            return sb.ToString();
        }

        private static void Dump(StringBuilder sb, object value, int depth)
        {
            if (depth > 12) throw new InvalidOperationException("Object graph too deep");

            switch (value)
            {
                case null:
                    sb.Append("null");
                    return;
                case string s:
                    sb.Append('"').Append(s).Append('"');
                    return;
                case IFormattable f when value.GetType().IsPrimitive || value is Enum || value is decimal:
                    sb.Append(f.ToString(null, CultureInfo.InvariantCulture));
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case IDictionary dictionary:
                    sb.Append('{');
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        NewLine(sb, depth + 1);
                        Dump(sb, entry.Key, depth + 1);
                        sb.Append(": ");
                        Dump(sb, entry.Value, depth + 1);
                    }
                    NewLine(sb, depth);
                    sb.Append('}');
                    return;
                case IEnumerable enumerable:
                    sb.Append('[');
                    foreach (var item in enumerable)
                    {
                        NewLine(sb, depth + 1);
                        Dump(sb, item, depth + 1);
                    }
                    NewLine(sb, depth);
                    sb.Append(']');
                    return;
            }

            var type = value.GetType();
            sb.Append(type.Name).Append(" {");
            var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m is FieldInfo || m is PropertyInfo p && p.CanRead && p.GetIndexParameters().Length == 0)
                .OrderBy(m => m.MetadataToken);
            foreach (var member in members)
            {
                NewLine(sb, depth + 1);
                sb.Append(member.Name).Append(" = ");
                var memberValue = member is FieldInfo field ? field.GetValue(value) : ((PropertyInfo)member).GetValue(value);
                Dump(sb, memberValue, depth + 1);
            }
            NewLine(sb, depth);
            sb.Append('}');
        }

        private static void NewLine(StringBuilder sb, int depth) => sb.Append('\n').Append(' ', depth * 2);
    }
}
