using DaxStudio.Parsers.DirectQuery;
using DaxStudio.Parsers.XmSql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading;

namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class XmSqlGrammarTests
    {
        [TestMethod]
        public void ValidQueryHasNoDiagnostics()
        {
            var query = XmSqlGrammar.Parse("SET DC_KIND=\"AUTO\";\r\nSELECT 'Sales'[Amount] FROM 'Sales' WHERE 'Sales'[Year] IN ( 2020, 2021 );");

            Assert.AreEqual(2, query.Statements.Count);
            Assert.AreEqual(0, query.Diagnostics.Count);
            Assert.IsFalse(query.HasErrors);
            Assert.AreEqual(-1, query.ErrorOffset);
        }

        [TestMethod]
        public void SkippedTextIsReportedAndTheNextStatementIsKept()
        {
            var text = "SELECT 'A'[X] FROM 'A' ; ?? ! SELECT 'B'[Y] FROM 'B' ;";
            var query = XmSqlGrammar.Parse(text);

            Assert.AreEqual(2, query.Statements.Count);
            Assert.IsTrue(query.HasErrors);
            Assert.AreEqual(text.IndexOf("??"), query.ErrorOffset);
            Assert.IsTrue(query.Diagnostics.All(d => d.Severity == Parlot.ParseDiagnosticSeverity.Warning));
        }

        [TestMethod]
        public void UnparsedPredicateKeepsTheOtherPredicates()
        {
            var text = "SELECT 'A'[X] FROM 'A' WHERE 'A'[X] = 1 VAND ?? 'A'[Y] ?? VAND 'A'[Z] IN ( 2, 3 ) ;";
            var query = XmSqlGrammar.Parse(text);

            var where = ((SelectQuery)query.Statements.Single()).Body.Where;
            CollectionAssert.AreEqual(
                new[] { FilterKind.Comparison, FilterKind.Unparsed, FilterKind.In },
                where.Predicates.Select(p => p.Kind).ToArray());
            Assert.AreEqual(text.IndexOf("??"), query.ErrorOffset);
        }

        [TestMethod]
        public void TracedEstimatedSizeIsNotAnError()
        {
            var query = XmSqlGrammar.Parse("SELECT 'A'[X] FROM 'A';\r\n\r\n\r\n[Estimated size (volume, marshalling bytes): 37, 592]");

            Assert.AreEqual(1, query.Statements.Count);
            Assert.AreEqual(0, query.Diagnostics.Count);
        }

        [TestMethod]
        public void FormatterOutputIsNotAnError()
        {
            var text = "SELECT 'A'[D] FROM 'A' WHERE 'A'[D] IN ( 45714.000000 /* 2025-02-26 */, 45690.000000 /* 2025-02-02 */ ) ;, "
                + "SELECT 'B'[Y] FROM 'B' ;\r\n\r\n\r\nEstimated size: rows = 3,712  bytes = 464\r\n";
            var query = XmSqlGrammar.Parse(text);

            Assert.AreEqual(2, query.Statements.Count);
            Assert.AreEqual(0, query.Diagnostics.Count);
        }

        [TestMethod]
        public void DeeplyNestedInputFailsInsteadOfOverflowingTheStack()
        {
            // Parlot fails a parse that would overflow the stack, which would end the process. The entry points
            // then return their fallback values. Run on a 1 MB stack, the default of a Windows thread.
            var depth = 100000;
            var nested = new string('(', depth) + "'T'[C] = 1" + new string(')', depth);
            var xmSql = "SELECT 'T'[C] FROM 'T' WHERE " + nested + ";";
            var directQuery = "SELECT [t1].[C] AS [c1] FROM [dbo].[T] AS [t1] WHERE " + nested.Replace("'T'[C]", "[t1].[C]");

            string formatted = "";
            bool parsed = true, directQueryParsed = true;
            XmSqlQueryFingerprint fingerprint = null;
            var thread = new Thread(() =>
            {
                formatted = ParlotXmSqlFormatter.Format(xmSql, true, true, out _, out _, out _, true, null, null, null);
                parsed = new ParlotXmSqlParser().ParseQuery(xmSql, new XmSqlAnalysis());
                fingerprint = new ParlotXmSqlQueryGrouper().ComputeFingerprint(xmSql);
                directQueryParsed = ParlotDirectQuerySqlParser.Parse(directQuery, new XmSqlAnalysis(), new XmSqlParser.SeEventMetrics());
            }, 1024 * 1024);
            thread.Start();
            thread.Join();

            Assert.IsNull(formatted);
            Assert.IsFalse(parsed);
            Assert.IsNull(fingerprint);
            Assert.IsFalse(directQueryParsed);
        }
    }
}
