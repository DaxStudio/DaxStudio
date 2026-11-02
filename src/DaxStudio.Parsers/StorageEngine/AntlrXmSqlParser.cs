using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using DaxStudio.Parsers.Grammars.Generated;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using DqParser = DaxStudio.Parsers.Grammars.Generated.DirectQuerySqlParser;

namespace DaxStudio.Parsers.StorageEngine
{
    /// <summary>
    /// ANTLR4-based implementation of the xmSQL and DirectQuery SQL parser.
    /// Replaces the regex-based XmSqlParser with a formal grammar approach.
    /// </summary>
    public class AntlrXmSqlParser : IXmSqlParser
    {
        // Shared stateless error listener singletons
        private static readonly SilentErrorListener _silentLexerListener = new SilentErrorListener();
        private static readonly SilentParseErrorListener _silentParserListener = new SilentParseErrorListener();

        // Reusable xmSQL lexer/parser/token stream (created lazily on first use)
        private xmSQLLexer _xmLexer;
        private CommonTokenStream _xmTokenStream;
        private xmSQLParser _xmParser;

        // Reusable DirectQuery lexer/parser/token stream (created lazily on first use)
        private DirectQuerySqlLexer _dqLexer;
        private CommonTokenStream _dqTokenStream;
        private DqParser _dqParser;

        private void EnsureXmSqlParserInitialized()
        {
            if (_xmLexer != null) return;

            var emptyStream = new AntlrInputStream(string.Empty);
            _xmLexer = new xmSQLLexer(emptyStream);
            _xmLexer.RemoveErrorListeners();
            _xmLexer.AddErrorListener(_silentLexerListener);

            _xmTokenStream = new CommonTokenStream(_xmLexer);

            _xmParser = new xmSQLParser(_xmTokenStream);
            _xmParser.RemoveErrorListeners();
            _xmParser.AddErrorListener(_silentParserListener);
            _xmParser.Interpreter.PredictionMode = PredictionMode.SLL;
        }

        private void EnsureDirectQueryParserInitialized()
        {
            if (_dqLexer != null) return;

            var emptyStream = new AntlrInputStream(string.Empty);
            _dqLexer = new DirectQuerySqlLexer(emptyStream);
            _dqLexer.RemoveErrorListeners();
            _dqLexer.AddErrorListener(_silentLexerListener);

            _dqTokenStream = new CommonTokenStream(_dqLexer);

            _dqParser = new DqParser(_dqTokenStream);
            _dqParser.RemoveErrorListeners();
            _dqParser.AddErrorListener(_silentParserListener);
        }

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

                // Reuse lexer/parser instances to avoid repeated construction
                EnsureXmSqlParserInitialized();

                _xmLexer.SetInputStream(new AntlrInputStream(xmSql));
                _xmTokenStream.SetTokenSource(_xmLexer);
                _xmParser.TokenStream = _xmTokenStream;
                _xmParser.Reset();

                var tree = _xmParser.query();

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

                // Reuse lexer/parser instances to avoid repeated construction
                EnsureDirectQueryParserInitialized();

                _dqLexer.SetInputStream(new AntlrInputStream(sql));
                _dqTokenStream.SetTokenSource(_dqLexer);
                _dqParser.TokenStream = _dqTokenStream;
                _dqParser.Reset();

                var tree = _dqParser.query();

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
