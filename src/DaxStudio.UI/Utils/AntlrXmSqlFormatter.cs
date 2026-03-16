using Antlr4.Runtime;
using DaxStudio.UI.Grammars.Generated;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// ANTLR-based xmSQL formatter that replaces the regex chain in TraceStorageEngineExtensions.
    /// Parses the query with the xmSQL grammar, then walks the tree to emit formatted output.
    /// </summary>
    public static class AntlrXmSqlFormatter
    {
        // Standalone estimated size annotation on its own line (outside the grammar's scope)
        private static readonly Regex StandaloneEstimatedSizePattern = new Regex(
            @"[\r\n]+\s*\[Estimated\s+size\s*\([^)]*\)\s*:\s*(?<rows>\d+)\s*,\s*(?<bytes>\d+)\s*\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Formats and simplifies an xmSQL query using the ANTLR parser.
        /// </summary>
        /// <param name="xmSql">Raw xmSQL query text.</param>
        /// <param name="format">Apply structural formatting (indentation, line breaks).</param>
        /// <param name="simplify">Remove aliases, lineage, GUIDs, brackets, etc.</param>
        /// <param name="estimatedRows">Output: estimated row count if found.</param>
        /// <param name="estimatedBytes">Output: estimated byte size if found.</param>
        /// <param name="hasEstimatedSize">Output: true if estimated size annotation was found.</param>
        /// <returns>Formatted query string, or null if parsing failed.</returns>
        public static string Format(
            string xmSql,
            bool format,
            bool simplify,
            out long estimatedRows,
            out long estimatedBytes,
            out bool hasEstimatedSize,
            Dictionary<string, string> remapColumns = null,
            Dictionary<string, string> remapTables = null)
        {
            estimatedRows = 0;
            estimatedBytes = 0;
            hasEstimatedSize = false;

            if (string.IsNullOrWhiteSpace(xmSql))
                return xmSql;

            try
            {
                // Extract standalone estimated size annotation before parsing
                // (appears after the query, outside any statement — not in the grammar)
                var sizeMatch = StandaloneEstimatedSizePattern.Match(xmSql);
                if (sizeMatch.Success)
                {
                    if (long.TryParse(sizeMatch.Groups["rows"].Value, out long r) &&
                        long.TryParse(sizeMatch.Groups["bytes"].Value, out long b))
                    {
                        estimatedRows = r;
                        estimatedBytes = b;
                        hasEstimatedSize = true;
                    }
                    xmSql = xmSql.Remove(sizeMatch.Index, sizeMatch.Length);
                }

                var inputStream = new AntlrInputStream(xmSql);
                var lexer = new xmSQLLexer(inputStream);
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new SilentLexerErrorListener());

                var tokenStream = new CommonTokenStream(lexer);
                var parser = new xmSQLParser(tokenStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new SilentParserErrorListener());

                var tree = parser.query();

                var visitor = new XmSqlFormattingVisitor(simplify, format, remapColumns, remapTables);
                visitor.Visit(tree);

                // If visitor found size in a truncation indicator, use that
                if (visitor.HasEstimatedSize)
                {
                    estimatedRows = visitor.EstimatedRows;
                    estimatedBytes = visitor.EstimatedBytes;
                    hasEstimatedSize = true;
                }

                var result = visitor.GetFormattedText();

                // Append formatted estimated size if extracted standalone
                if (hasEstimatedSize && !visitor.HasEstimatedSize)
                {
                    result += "\r\n\r\n\r\nEstimated size: rows = " + estimatedRows.ToString("#,0") +
                              "  bytes = " + estimatedBytes.ToString("#,0") + "\r\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "{class} {method} {message}",
                    nameof(AntlrXmSqlFormatter), nameof(Format),
                    "ANTLR formatting failed, returning null for fallback");
                return null;
            }
        }

        /// <summary>
        /// Simplified overload that returns formatted text or the original on failure.
        /// </summary>
        public static string FormatOrFallback(
            string xmSql,
            bool format,
            bool simplify)
        {
            var result = Format(xmSql, format, simplify,
                out _, out _, out _);
            return result ?? xmSql;
        }

        private class SilentLexerErrorListener : IAntlrErrorListener<int>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e)
            {
                // Silently ignore lexer errors
            }
        }

        private class SilentParserErrorListener : IAntlrErrorListener<IToken>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e)
            {
                // Silently ignore parse errors - partial parsing is expected
            }
        }
    }
}
