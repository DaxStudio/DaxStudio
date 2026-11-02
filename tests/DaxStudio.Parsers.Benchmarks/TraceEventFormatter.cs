using DaxStudio.Parsers.StorageEngine;
using DaxStudio.Core.Trace;
using System.Collections.Generic;

namespace DaxStudio.Parsers.Benchmarks
{
    public enum Engine
    {
        Regex,
        Antlr,
        Parlot,
    }

    /// <summary>
    /// What <c>TraceStorageEngineEvent.FormatQuery</c> does for every storage engine event, with the default
    /// options (format, simplify, replace names and convert dates all on).
    /// </summary>
    public static class TraceEventFormatter
    {
        public static string FormatQuery(Engine engine, string textData,
            Dictionary<string, string> remapColumns = null, Dictionary<string, string> remapTables = null)
        {
            string query = null;
            long estimatedRows = 0;

            if (engine != Engine.Regex)
            {
                var result = engine == Engine.Antlr
                    ? AntlrXmSqlFormatter.Format(textData, true, true, out long rows, out _, out bool hasSize, true, remapColumns, remapTables)
                    : ParlotXmSqlFormatter.Format(textData, true, true, out rows, out _, out hasSize, true, remapColumns, remapTables);
                if (result != null)
                {
                    query = result;
                    if (hasSize) estimatedRows = rows;
                }
            }

            if (query == null)
            {
                string rawText = textData.RemovePremiumTags();
                string queryFormatted = rawText.FormatXmSql();
                queryFormatted = queryFormatted.Replace("\t", "    ");
                string queryRemapped = queryFormatted.ReplaceTableOrColumnNames(remapColumns);
                queryRemapped = queryRemapped.ReplaceTableOrColumnNames(remapTables);
                query = queryRemapped
                    .RemoveDaxGuids()
                    .RemoveXmSqlSquareBrackets()
                    .RemoveAlias()
                    .RemoveLineage()
                    .FixEmptyArguments()
                    .RemoveRowNumberGuid()
                    .RemoveDoubleBracketsInCallbacks()
                    .FormatIndexSize();
                query = query.ConvertCoalesceDatesToIso();
            }

            if (query.Length > 0)
            {
                if (query.ExtractEstimatedSize(out long rows, out _, out string formattedQuery, true))
                {
                    query = formattedQuery;
                    estimatedRows = rows;
                }

                // HighlightQuery
                query.ContainsCallback();
            }

            return query;
        }
    }
}
