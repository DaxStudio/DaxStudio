using Antlr4.Runtime;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using DqParser = DaxStudio.UI.Grammars.Generated.DirectQuerySqlParser;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// ANTLR4-based implementation of the xmSQL and DirectQuery SQL parser.
    /// Replaces the regex-based XmSqlParser with a formal grammar approach.
    /// </summary>
    public class AntlrXmSqlParser : IXmSqlParser
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

                // Lex and parse
                var inputStream = new AntlrInputStream(xmSql);
                var lexer = new xmSQLLexer(inputStream);
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new SilentErrorListener());

                var tokenStream = new CommonTokenStream(lexer);
                var parser = new xmSQLParser(tokenStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new SilentParseErrorListener());

                var tree = parser.query();

                // First pass: build lineage
                var visitor = new XmSqlAnalysisVisitor(analysis, metrics);
                visitor.BuildLineage(tree);

                // Second pass: extract analysis data
                visitor.Visit(tree);

                analysis.SuccessfullyParsedQueries++;

                if (metrics?.IsCacheHit == true)
                {
                    analysis.CacheHitQueries++;
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ANTLR: Failed to parse xmSQL query: {Query}",
                    xmSql.Substring(0, Math.Min(100, xmSql.Length)));
                analysis.FailedParseQueries++;
                return false;
            }
        }

        /// <inheritdoc />
        public bool ParseDirectQuerySql(string sql, XmSqlAnalysis analysis, XmSqlParser.SeEventMetrics metrics)
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

                // Lex and parse
                var inputStream = new AntlrInputStream(sql);
                var lexer = new DirectQuerySqlLexer(inputStream);
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new SilentErrorListener());

                var tokenStream = new CommonTokenStream(lexer);
                var parser = new DqParser(tokenStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new SilentParseErrorListener());

                var tree = parser.query();

                var visitor = new DirectQuerySqlAnalysisVisitor(analysis, metrics);
                visitor.Visit(tree);

                analysis.SuccessfullyParsedQueries++;
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ANTLR: Failed to parse DirectQuery SQL: {Query}",
                    sql.Substring(0, Math.Min(100, sql.Length)));
                analysis.FailedParseQueries++;
                return false;
            }
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

        /// <summary>
        /// Silent error listener that suppresses ANTLR lexer errors.
        /// xmSQL is not formally documented so we expect some unrecognized tokens.
        /// </summary>
        private class SilentErrorListener : IAntlrErrorListener<int>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                // Silently ignore lexer errors
            }
        }

        /// <summary>
        /// Silent error listener for parser errors.
        /// </summary>
        private class SilentParseErrorListener : IAntlrErrorListener<IToken>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                // Silently ignore parse errors - partial parsing is expected
            }
        }
    }
}
