using Serilog;
using System;

namespace DaxStudio.Parsers.DirectQuery
{
    /// <summary>
    /// Parlot-based DirectQuery SQL parser, the counterpart of <see cref="AntlrXmSqlParser.ParseDirectQuerySql"/>.
    /// </summary>
    internal static class ParlotDirectQuerySqlParser
    {
        /// <summary>
        /// Parses a DirectQuery SQL statement and adds its tables, columns and relationships to the analysis.
        /// Returns false for an empty statement or when the analysis failed.
        /// </summary>
        public static bool Parse(string sql, XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return false;

            try
            {
                analysis.TotalSEQueriesAnalyzed++;
                analysis.DirectQueryEventCount++;

                if (metrics?.DurationMs.HasValue == true && metrics.DurationMs.Value > 0)
                {
                    analysis.TotalDirectQueryDurationMs += metrics.DurationMs.Value;
                }

                var statement = DirectQuerySqlGrammar.Parse(sql, out _);

                if (statement != null)
                {
                    var walker = new DirectQuerySqlAnalysisWalker(analysis, metrics);
                    walker.VisitSelectStatement(statement);
                }

                // Like the ANTLR parser, which recovers from syntax errors, a statement that does not match the
                // grammar still counts as parsed, with what was read before the error.
                analysis.SuccessfullyParsedQueries++;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Parlot: Failed to parse DirectQuery SQL: {Query}",
                    sql.Substring(0, Math.Min(100, sql.Length)));
                analysis.FailedParseQueries++;
                return false;
            }
        }
    }
}
