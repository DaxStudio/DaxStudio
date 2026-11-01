using DaxStudio.Parsers.XmSql;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DaxStudio.Parsers
{
    /// <summary>
    /// xmSQL formatter built on the Parlot grammar. Same contract and output as <see cref="AntlrXmSqlFormatter"/>.
    /// </summary>
    public static class ParlotXmSqlFormatter
    {
        // Standalone estimated size annotation on its own line (outside the grammar's scope)
        private static readonly Regex StandaloneEstimatedSizePattern = new Regex(
            @"[\r\n]+\s*\[Estimated\s+size\s*\([^)]*\)\s*:\s*(?<rows>\d+)\s*,\s*(?<bytes>\d+)\s*\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Formats and simplifies an xmSQL query.
        /// </summary>
        /// <param name="xmSql">Raw xmSQL query text.</param>
        /// <param name="format">Apply structural formatting (indentation, line breaks).</param>
        /// <param name="simplify">Remove aliases, lineage, GUIDs, brackets, etc.</param>
        /// <param name="estimatedRows">Output: estimated row count if found.</param>
        /// <param name="estimatedBytes">Output: estimated byte size if found.</param>
        /// <param name="hasEstimatedSize">Output: true if estimated size annotation was found.</param>
        /// <param name="convertDates">When true, converts OA date numbers in COALESCE filters to ISO 8601 dates.</param>
        /// <param name="remapColumns">Optional dictionary mapping lineage IDs to friendly column names.</param>
        /// <param name="remapTables">Optional dictionary mapping lineage IDs to friendly table names.</param>
        /// <param name="dateColumnIds">Optional set of column IDs whose data type is DateTime.</param>
        /// <returns>Formatted query string, or null if formatting failed.</returns>
        public static string Format(
            string xmSql,
            bool format,
            bool simplify,
            out long estimatedRows,
            out long estimatedBytes,
            out bool hasEstimatedSize,
            bool convertDates = false,
            Dictionary<string, string> remapColumns = null,
            Dictionary<string, string> remapTables = null,
            HashSet<string> dateColumnIds = null)
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
                if (xmSql.IndexOf("Estimated", StringComparison.OrdinalIgnoreCase) >= 0)
                {
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
                }

                var query = XmSqlGrammar.Parse(xmSql);

                var walker = new XmSqlFormattingWalker(simplify, format, convertDates, remapColumns, remapTables, dateColumnIds);
                walker.Walk(query);

                // If the walker found size in a truncation indicator, use that
                if (walker.HasEstimatedSize)
                {
                    estimatedRows = walker.EstimatedRows;
                    estimatedBytes = walker.EstimatedBytes;
                    hasEstimatedSize = true;
                }

                var result = walker.GetFormattedText();

                // Append formatted estimated size if extracted standalone
                if (hasEstimatedSize && !walker.HasEstimatedSize)
                {
                    result += "\r\n\r\n\r\nEstimated size: rows = " + estimatedRows.ToString("#,0") +
                              "  bytes = " + estimatedBytes.ToString("#,0") + "\r\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "{class} {method} {message}",
                    nameof(ParlotXmSqlFormatter), nameof(Format),
                    "Parlot formatting failed, returning null for fallback");
                return null;
            }
        }

        /// <summary>
        /// Simplified overload that returns formatted text or the original on failure.
        /// </summary>
        public static string FormatOrFallback(string xmSql, bool format, bool simplify)
        {
            var result = Format(xmSql, format, simplify, out _, out _, out _);
            return result ?? xmSql;
        }
    }
}
