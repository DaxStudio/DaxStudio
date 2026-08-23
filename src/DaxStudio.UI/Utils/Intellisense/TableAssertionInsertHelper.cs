using System;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Shared logic for the "&lt;from Results&gt;" table-assertion completion: turns a document's
    /// current query results into the <c>--&gt;&gt; | ... |</c> continuation block that is inserted
    /// after the user types <c>--&gt; ASSERT TABLE</c>.
    /// </summary>
    internal static class TableAssertionInsertHelper
    {
        // A newline prefix so the block always starts on its own line, below the "--> ASSERT TABLE"
        // command the user just typed.
        public static string BuildFromResults(IDaxDocument document)
        {
            var table = (document as IResultsTableProvider)?.GetActiveResultsTable();

            if (table == null || table.Columns.Count == 0)
            {
                // no results to build from - insert an empty template the user can fill in
                return Environment.NewLine
                    + TableAssertionFormatter.ContinuationMarker + " | Column1 | Column2 |" + Environment.NewLine
                    + TableAssertionFormatter.ContinuationMarker + " | STRING | INT64 |";
            }

            return Environment.NewLine
                + TableAssertionFormatter.FormatDataTable(table, includeHeaderLine: false, includeTypeRow: true);
        }
    }
}
