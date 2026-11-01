using DaxStudio.Parsers.DirectQuery;
using DaxStudio.Parsers.XmSql;
using Serilog;
using System;
using System.Collections.Generic;

namespace DaxStudio.Parsers
{
    /// <summary>
    /// xmSQL parser built on the Parlot grammar. Same contract and results as <see cref="AntlrXmSqlParser"/>.
    /// </summary>
    public class ParlotXmSqlParser : IXmSqlParser
    {
        /// <inheritdoc />
        public bool ParseQuery(string xmSql, XmSqlAnalysis analysis, long? estimatedRows = null, long? durationMs = null)
        {
            return ParseQueryWithMetrics(xmSql, analysis, new XmSqlParser.SeEventMetrics
            {
                EstimatedRows = estimatedRows,
                DurationMs = durationMs,
                IsCacheHit = false,
                CpuTimeMs = null,
                CpuFactor = null,
                NetParallelDurationMs = null
            });
        }

        /// <inheritdoc />
        public bool ParseQueryWithMetrics(string xmSql, XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            if (string.IsNullOrWhiteSpace(xmSql))
                return false;

            try
            {
                analysis.TotalSEQueriesAnalyzed++;

                // Track Scan event count (non-cache-hit)
                if (!(metrics?.IsCacheHit ?? false))
                {
                    analysis.ScanEventCount++;
                }

                // Track CPU time for total analysis
                if (metrics?.CpuTimeMs.HasValue == true && metrics.CpuTimeMs.Value > 0)
                {
                    analysis.TotalScanCpuTimeMs += metrics.CpuTimeMs.Value;
                }

                var tree = XmSqlGrammar.Parse(xmSql);

                // First pass: build lineage
                var walker = new XmSqlAnalysisWalker(analysis, metrics);
                walker.BuildLineage(tree);

                // Second pass: extract analysis data
                walker.Walk(tree);

                analysis.SuccessfullyParsedQueries++;

                if (metrics?.IsCacheHit == true)
                {
                    analysis.CacheHitQueries++;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Parlot: Failed to parse xmSQL query: {Query}",
                    xmSql.Substring(0, Math.Min(100, xmSql.Length)));
                analysis.FailedParseQueries++;
                return false;
            }
        }

        /// <inheritdoc />
        public bool ParseDirectQuerySql(string sql, XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
        {
            return ParlotDirectQuerySqlParser.Parse(sql, analysis, metrics);
        }

        /// <inheritdoc />
        public XmSqlAnalysis ParseQueries(IEnumerable<string> queries)
        {
            var analysis = new XmSqlAnalysis();
            foreach (var query in queries)
            {
                ParseQuery(query, analysis);
            }
            return analysis;
        }
    }
}
