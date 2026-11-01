using DaxStudio.Parsers.XmSql;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// The analysis and the grouping read the formatted query of each event, which is not plain xmSQL:
    /// simplified PFCASTs, date comments, estimated sizes in truncated lists. The ANTLR grammar recovers
    /// from these with partial results; the Parlot grammar reads them.
    /// </summary>
    [TestClass]
    public class ParlotFormattedTextTests
    {
        [TestMethod]
        public void ParsesFormattedCorpusWithoutErrors()
        {
            int formatted = 0, antlrErrors = 0;
            foreach (var query in Corpus.XmSql)
            {
                foreach (var format in new[] { true, false })
                foreach (var convertDates in new[] { true, false })
                {
                    var text = ParlotXmSqlFormatter.Format(query, format, true, out _, out _, out _, convertDates);
                    if (string.IsNullOrWhiteSpace(text) || XmSqlGrammar.Parse(query).HasErrors && !query.Contains("[Estimated size")) continue;
                    formatted++;
                    if (Corpus.AntlrSyntaxErrors(text) > 0) antlrErrors++;
                    Assert.IsFalse(XmSqlGrammar.Parse(text).HasErrors, text);
                }
            }

            Assert.IsTrue(formatted > 400);
            Assert.IsTrue(antlrErrors > 0, "some formatted queries are not valid for the ANTLR grammar");
        }

        [TestMethod]
        public void AnalysisOfFormattedQueryFindsAtLeastWhatAntlrFinds()
        {
            foreach (var query in Corpus.XmSql)
            {
                var text = ParlotXmSqlFormatter.Format(query, true, true, out _, out _, out _, true);
                if (string.IsNullOrWhiteSpace(text)) continue;
                var antlr = new XmSqlAnalysis();
                new AntlrXmSqlParser().ParseQuery(text, antlr);
                var parlot = new XmSqlAnalysis();
                new ParlotXmSqlParser().ParseQuery(text, parlot);

                foreach (var table in antlr.Tables)
                {
                    Assert.IsTrue(parlot.Tables.TryGetValue(table.Key, out var parlotTable), table.Key + " in " + text);
                    foreach (var column in table.Value.Columns)
                    {
                        Assert.IsTrue(parlotTable.Columns.TryGetValue(column.Key, out var parlotColumn), column.Key + " in " + text);
                        Assert.IsTrue(column.Value.FilterValues.All(parlotColumn.FilterValues.Contains), column.Key + " values in " + text);
                    }
                }
            }
        }
    }
}
