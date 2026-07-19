using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Provides code-completion suggestions for comment-script command lines (lines that begin with the
    /// <c>--&gt;</c> marker). The suggestions are derived from the comment-script grammar
    /// (see PreProcessorParser.g4) and are surfaced by the editor's ANTLR-based intellisense provider.
    /// </summary>
    public static class CommentScriptCompletionProvider
    {
        /// <summary>The marker that introduces a comment-script command line.</summary>
        public const string Marker = "-->";

        /// <summary>
        /// Sentinel <see cref="CompletionItem.InsertText"/> used by the "&lt;from Results&gt;"
        /// completion offered after <c>--&gt; ASSERT TABLE</c>. The editor layer detects this marker
        /// and, instead of inserting the literal text, generates an assertion table block from the
        /// current query results.
        /// </summary>
        public const string FromResultsInsertText = "\u0001FROM_RESULTS";

        /// <summary>The label shown for the "insert from current results" table-assertion helper.</summary>
        public const string FromResultsLabel = "<from Results>";

        private static readonly (string Keyword, string Description)[] TopLevelCommands =
        {
            ("CONNECT",   "Connect to a server, Power BI Desktop (PBIX) or SSDT instance"),
            ("USE",       "Switch to a different database/model on the current connection"),
            ("PARAMETER", "Declare a query parameter (PARAMETER [type] @name = value)"),
            ("OUTPUT",    "Send query results to a CSV, XLSX or JSON file"),
            ("TEST",      "Group the following asserts into a named test (TEST \"name\")"),
            ("ASSERT",    "Assert a condition on the results or timings"),
            ("RESULTS",   "Show or hide the results grid (RESULTS ON|OFF)"),
            ("CLEARCACHE", "Clear the database cache"),
            ("TRACE",     "Turn a trace on or off"),
            ("METRICS",   "Export or view VertiPaq Analyzer metrics"),
            ("SHOW",      "Show dependency or last-updated information"),
            ("GO",        "Separate the script into batches"),
        };

        private static readonly Dictionary<string, (string Keyword, string Description)[]> SubCommands =
            new Dictionary<string, (string, string)[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["CONNECT"] = new[]
                {
                    ("SERVER", "Connect to an Analysis Services server"),
                    ("PBIX",   "Connect to a Power BI Desktop file (running instance name or full .pbix path)"),
                    ("SSDT",   "Connect to a running SSDT / Tabular project"),
                },
                ["OUTPUT"] = new[]
                {
                    ("CSV",  "Output to a CSV file"),
                    ("XLSX", "Output to an Excel file"),
                    ("JSON", "Output to a JSON file"),
                },
                ["RESULTS"] = new[]
                {
                    ("ON",  "Show the results grid"),
                    ("OFF", "Hide the results grid"),
                },
                ["ASSERT"] = new[]
                {
                    ("DURATION",   "Assert on total query duration"),
                    ("SE_CPU",     "Assert on storage engine CPU time"),
                    ("SE_QUERIES", "Assert on the storage engine query count"),
                    ("ROWCOUNT",   "Assert on the number of rows returned"),
                    ("TABLE",      "Assert the results match an expected table"),
                },
                ["TRACE"] = new[]
                {
                    ("SERVERTIMINGS", "Server Timings trace"),
                    ("QUERYPLAN",     "Query Plan trace"),
                    ("ALLQUERIES",    "All Queries trace"),
                },
                ["METRICS"] = new[]
                {
                    ("EXPORT", "Export metrics to a VPAX file"),
                    ("VIEW",   "View metrics in the VertiPaq Analyzer pane"),
                },
                ["SHOW"] = new[]
                {
                    ("DEPENDENCIES", "Show object dependencies"),
                    ("LAST_UPDATED", "Show the last refresh time"),
                    ("MAX_UPDATED",  "Show the most recent refresh time"),
                },
            };

        private static readonly (string Keyword, string Description)[] OnOff =
        {
            ("ON",  "Turn the trace on"),
            ("OFF", "Turn the trace off"),
        };

        /// <summary>
        /// Returns true when the supplied line (the text up to the caret) is a comment-script command line.
        /// </summary>
        public static bool IsCommentScriptLine(string lineUpToCaret)
        {
            return lineUpToCaret != null && lineUpToCaret.TrimStart().StartsWith(Marker, StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets completion items for a comment-script command line.
        /// </summary>
        /// <param name="lineUpToCaret">The text of the current line from its start up to the caret position.</param>
        public static IReadOnlyList<CompletionItem> GetCompletions(string lineUpToCaret)
        {
            if (!IsCommentScriptLine(lineUpToCaret)) return new List<CompletionItem>();

            // strip the marker and everything before it
            var trimmed = lineUpToCaret.TrimStart();
            var afterMarker = trimmed.Substring(Marker.Length);

            var endsWithSpace = afterMarker.Length > 0 && char.IsWhiteSpace(afterMarker[afterMarker.Length - 1]);
            var tokens = afterMarker.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            // typing (or about to type) the first command keyword
            if (tokens.Length == 0 || (tokens.Length == 1 && !endsWithSpace))
            {
                var prefix = tokens.Length == 1 ? tokens[0] : string.Empty;
                return Filter(TopLevelCommands, prefix);
            }

            var command = tokens[0];

            // typing (or about to type) the second keyword (a subcommand)
            if (tokens.Length == 1 || (tokens.Length == 2 && !endsWithSpace))
            {
                if (SubCommands.TryGetValue(command, out var subs))
                {
                    var prefix = tokens.Length == 2 ? tokens[1] : string.Empty;
                    return Filter(subs, prefix);
                }
                return new List<CompletionItem>();
            }

            // third keyword for TRACE ... ON/OFF
            if (string.Equals(command, "TRACE", StringComparison.OrdinalIgnoreCase)
                && (tokens.Length == 2 || (tokens.Length == 3 && !endsWithSpace)))
            {
                var prefix = tokens.Length == 3 ? tokens[2] : string.Empty;
                return Filter(OnOff, prefix);
            }

            // "<from Results>" helper offered at the point where ASSERT TABLE rows begin -
            // i.e. right after "--> ASSERT TABLE " or after an optional UNORDERED/PARTIAL modifier.
            if (string.Equals(command, "ASSERT", StringComparison.OrdinalIgnoreCase)
                && tokens.Length >= 2
                && string.Equals(tokens[1], "TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var afterTable = tokens.Length == 2 && endsWithSpace;
                var afterModifier = tokens.Length == 3 && endsWithSpace
                    && (string.Equals(tokens[2], "UNORDERED", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(tokens[2], "PARTIAL", StringComparison.OrdinalIgnoreCase));
                if (afterTable || afterModifier)
                {
                    return new List<CompletionItem>
                    {
                        new CompletionItem(FromResultsLabel, CompletionItemKind.Keyword,
                            "Insert an assertion table built from the current query results",
                            FromResultsInsertText)
                    };
                }
            }

            return new List<CompletionItem>();
        }

        private static IReadOnlyList<CompletionItem> Filter((string Keyword, string Description)[] source, string prefix)
        {
            return source
                .Where(c => string.IsNullOrEmpty(prefix)
                            || c.Keyword.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(c => new CompletionItem(c.Keyword, CompletionItemKind.Keyword, c.Description))
                .ToList();
        }
    }
}
