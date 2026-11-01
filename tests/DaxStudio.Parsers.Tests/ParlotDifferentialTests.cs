using DaxStudio.Parsers.XmSql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Text;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Compares the Parlot parser with the ANTLR parser over the corpus.
    /// </summary>
    [TestClass]
    public class ParlotDifferentialTests
    {
        public TestContext TestContext { get; set; }

        // Lineage ids and full names, as DaxColumnsRemap and the date column metadata provide them
        private static readonly Dictionary<string, string> RemapColumns = BuildRemap("Column");
        private static readonly Dictionary<string, string> RemapTables = BuildRemap("Table");
        private static readonly HashSet<string> DateColumnIds = new HashSet<string> { "69", "71", "147", "Date" };

        private static Dictionary<string, string> BuildRemap(string prefix)
        {
            var remap = new Dictionary<string, string>();
            for (int i = 0; i < 200; i += 3) remap[i.ToString()] = prefix + " #" + i;
            remap["Product"] = prefix + " Product";
            remap["Sales Amount (142)"] = prefix + " Sales Amount";
            return remap;
        }

        [TestMethod]
        public void FormatterMatchesAntlr() => CompareFormatters(null, null, null);

        [TestMethod]
        public void FormatterMatchesAntlrWithRemaps() => CompareFormatters(RemapColumns, RemapTables, DateColumnIds);

        private void CompareFormatters(Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables, HashSet<string> dateColumnIds)
        {
            var report = new StringBuilder();
            int mismatches = 0, mismatchesWithAntlrErrors = 0, withAntlrErrors = 0, withParlotErrors = 0, comparisons = 0;

            for (int i = 0; i < Corpus.XmSql.Count; i++)
            {
                var query = Corpus.XmSql[i];
                var antlrErrors = Corpus.AntlrSyntaxErrors(query);
                if (antlrErrors > 0) withAntlrErrors++;
                if (XmSqlGrammar.Parse(query).HasErrors) withParlotErrors++;
                foreach (var format in new[] { true, false })
                foreach (var simplify in new[] { true, false })
                foreach (var convertDates in new[] { true, false })
                {
                    var expected = AntlrXmSqlFormatter.Format(query, format, simplify, out var r1, out var b1, out var h1, convertDates, remapColumns, remapTables, dateColumnIds);
                    comparisons++;
                    var actual = ParlotXmSqlFormatter.Format(query, format, simplify, out var r2, out var b2, out var h2, convertDates, remapColumns, remapTables, dateColumnIds);
                    if (expected != actual || r1 != r2 || b1 != b2 || h1 != h2)
                    {
                        if (antlrErrors > 0) mismatchesWithAntlrErrors++; else mismatches++;
                        report.AppendLine($"#{i} format={format} simplify={simplify} dates={convertDates} antlrErrors={antlrErrors} parlotErrors={XmSqlGrammar.Parse(query).HasErrors}");
                        report.AppendLine("--- input");
                        report.AppendLine(query);
                        report.AppendLine("--- antlr");
                        report.AppendLine(expected);
                        report.AppendLine("--- parlot");
                        report.AppendLine(actual);
                        report.AppendLine();
                        break;
                    }
                }
            }

            report.Insert(0, $"{Corpus.XmSql.Count} queries, {comparisons} comparisons, ANTLR errors in {withAntlrErrors}, Parlot errors in {withParlotErrors}\n");
            TestContext.WriteLine(report.ToString());
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "parlot-format-diff.txt"), report.ToString());
            Assert.IsTrue(Corpus.XmSql.Count > 100);
            Assert.AreEqual(0, mismatches, $"{mismatches} queries format differently ({mismatchesWithAntlrErrors} more where ANTLR reports syntax errors)");
        }
    }
}
