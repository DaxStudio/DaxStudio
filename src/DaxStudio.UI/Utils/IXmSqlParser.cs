using DaxStudio.UI.Model;
using System.Collections.Generic;
using static DaxStudio.UI.Utils.XmSqlParser;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Interface for xmSQL and DirectQuery SQL parsing.
    /// Allows switching between regex-based and ANTLR-based parser implementations.
    /// </summary>
    public interface IXmSqlParser
    {
        /// <summary>
        /// Parses a single xmSQL query and adds the results to the analysis.
        /// </summary>
        bool ParseQuery(string xmSql, XmSqlAnalysis analysis, long? estimatedRows = null, long? durationMs = null);

        /// <summary>
        /// Parses a single xmSQL query with full SE event metrics.
        /// </summary>
        bool ParseQueryWithMetrics(string xmSql, XmSqlAnalysis analysis, SeEventMetrics metrics);

        /// <summary>
        /// Parses a DirectQuery SQL query to extract table dependencies.
        /// </summary>
        bool ParseDirectQuerySql(string sql, XmSqlAnalysis analysis, SeEventMetrics metrics);

        /// <summary>
        /// Parses multiple xmSQL queries and aggregates the results.
        /// </summary>
        XmSqlAnalysis ParseQueries(IEnumerable<string> queries);
    }
}
