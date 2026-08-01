using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
            ("CONNECT",   "Connect to a server, Power BI Desktop (DESKTOP) or SSDT instance"),
            ("USE",       "Switch to a different database/model on the current connection"),
            ("PARAMETER", "Declare a query parameter (PARAMETER [type] @name = value)"),
            ("SET",       "Define a script variable (SET name = value) for use as $(name) in later commands"),
            ("OUTPUT",    "Send query results to a CSV, XLSX or JSON file"),
            ("TEST",      "Group the following asserts into a named test (TEST \"name\")"),
            ("ASSERT",    "Assert a condition on the results or timings"),
            ("RESULTS",   "Show or hide the results grid (RESULTS ON|OFF)"),
            ("CLEARCACHE", "Clear the database cache"),
            ("TRACE",     "Turn a trace on or off"),
            ("EXPORT",    "Export VertiPaq Analyzer metrics to a VPAX file"),
            ("SHOW",      "Show dependency, last-updated, diagram, metrics or delta information"),
            ("SAVEAS",    "Save a snapshot of the query to a .dax / .daxx file after it runs"),
            ("GO",        "Separate the script into batches"),
        };

        /// <summary>
        /// Matches a <c>--&gt; SET &lt;name&gt; =</c> command line so the variables already defined in the
        /// script can be offered when the user types a <c>$</c> reference.
        /// </summary>
        private static readonly Regex SetCommandRegex = new Regex(
            @"^\s*-->\s*SET\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Matches the <c>SET &lt;name&gt; =</c> at the start of the current command line (the text after the
        /// <c>--&gt;</c> marker) so the variable being defined can be excluded from its own value's completions.
        /// </summary>
        private static readonly Regex CurrentSetLineRegex = new Regex(
            @"^\s*SET\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The built-in <c>$(...)</c> namespaces. The values are offered with a sample argument which
        /// the user can then edit (e.g. the date format or the environment variable name).
        /// </summary>
        private static readonly (string Reference, string Description)[] BuiltInVariables =
        {
            ("now:yyyy-MM-dd",    "The local current date/time formatted with a .NET format string"),
            ("utcnow:yyyy-MM-dd", "The UTC current date/time formatted with a .NET format string"),
            ("env:NAME",          "The value of the NAME environment variable"),
        };

        private static readonly Dictionary<string, (string Keyword, string Description)[]> SubCommands =
            new Dictionary<string, (string, string)[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["CONNECT"] = new[]
                {
                    ("SERVER", "Connect to an Analysis Services server"),
                    ("DESKTOP", "Connect to a Power BI Desktop file (running instance name or full .pbix path)"),
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
                ["EXPORT"] = new[]
                {
                    ("METRICS", "Export VertiPaq Analyzer metrics to a VPAX file"),
                },
                ["SHOW"] = new[]
                {
                    ("DEPENDENCIES", "Show object dependencies"),
                    ("LAST_UPDATED", "Show the last refresh time"),
                    ("MAX_UPDATED",  "Show the most recent refresh time"),
                    ("DIAGRAM",      "Open the model diagram, filtered to the query's tables"),
                    ("METRICS",      "Open the VertiPaq Analyzer (Metrics) view"),
                    ("DELTA",        "Open the Delta Analyzer view"),
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
        /// <param name="scriptTextBeforeCaret">
        /// The script text preceding the caret, used to discover the <c>--&gt; SET</c> variables that are in
        /// scope when the caret sits inside a <c>$(...)</c> reference. May be <c>null</c>, in which case only
        /// the built-in variables are offered.
        /// </param>
        public static IReadOnlyList<CompletionItem> GetCompletions(string lineUpToCaret, string scriptTextBeforeCaret = null)
        {
            if (!IsCommentScriptLine(lineUpToCaret)) return new List<CompletionItem>();

            // strip the marker and everything before it
            var trimmed = lineUpToCaret.TrimStart();
            var afterMarker = trimmed.Substring(Marker.Length);

            // A "$" starts a script-variable reference in a command argument, so offer the variables
            // that are in scope (plus the built-ins) rather than the command keywords. On a SET line the
            // variable being defined is excluded - a SET value is expanded eagerly so it cannot reference
            // itself.
            if (TryGetVariableReferencePrefix(afterMarker, out var variablePrefix))
            {
                return GetVariableCompletions(scriptTextBeforeCaret, variablePrefix, GetVariableBeingDefined(afterMarker));
            }

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

        /// <summary>
        /// Determines whether the caret sits inside an unterminated <c>$(...)</c> script-variable
        /// reference on a comment-script command line and, if so, returns the partial reference typed so
        /// far (the text after the <c>$</c> and its optional opening parenthesis).
        /// </summary>
        /// <param name="afterMarker">The command line text following the <c>--&gt;</c> marker, up to the caret.</param>
        /// <param name="prefix">The partial reference name, or an empty string when only the <c>$</c> has been typed.</param>
        internal static bool TryGetVariableReferencePrefix(string afterMarker, out string prefix)
        {
            prefix = null;
            if (string.IsNullOrEmpty(afterMarker)) return false;

            var dollar = afterMarker.LastIndexOf('$');
            if (dollar < 0) return false;

            // "$$(" is the escape for a literal "$(" so it is not a reference.
            if (dollar > 0 && afterMarker[dollar - 1] == '$') return false;

            var rest = afterMarker.Substring(dollar + 1);
            if (rest.Length == 0)
            {
                prefix = string.Empty;
                return true;
            }

            // Only the opening parenthesis may follow the '$'; anything else (e.g. "$SYSTEM") is not a
            // script-variable reference.
            if (rest[0] != '(') return false;

            var partial = rest.Substring(1);
            // A closed reference or one broken by whitespace is complete/invalid - nothing to complete.
            if (partial.IndexOf(')') >= 0 || partial.Any(char.IsWhiteSpace)) return false;

            prefix = partial;
            return true;
        }

        /// <summary>
        /// Returns the name of the variable being defined when the current command line is a
        /// <c>SET &lt;name&gt; = ...</c>, otherwise <c>null</c>.
        /// </summary>
        /// <param name="afterMarker">The command line text following the <c>--&gt;</c> marker, up to the caret.</param>
        internal static string GetVariableBeingDefined(string afterMarker)
        {
            if (string.IsNullOrEmpty(afterMarker)) return null;
            var match = CurrentSetLineRegex.Match(afterMarker);
            return match.Success ? match.Groups["name"].Value : null;
        }

        /// <summary>
        /// Returns the names of the script variables defined by <c>--&gt; SET</c> commands in
        /// <paramref name="scriptTextBeforeCaret"/>, in the order they were first defined. Because only the
        /// text preceding the caret is scanned, this matches the runtime rule that a SET is visible only to
        /// the commands that follow it.
        /// </summary>
        public static IReadOnlyList<string> GetDefinedVariables(string scriptTextBeforeCaret)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(scriptTextBeforeCaret)) return names;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in SetCommandRegex.Matches(scriptTextBeforeCaret))
            {
                var name = match.Groups["name"].Value;
                if (seen.Add(name)) names.Add(name);
            }
            return names;
        }

        /// <summary>
        /// Builds the completion list shown inside a <c>$(...)</c> reference: the script variables defined
        /// above the caret followed by the built-in namespaces. The <see cref="CompletionItem.InsertText"/>
        /// is the bare reference (so the list filters on what the user types after the <c>$</c>) while the
        /// <see cref="CompletionItem.Label"/> shows the full <c>$(name)</c> syntax that gets inserted.
        /// </summary>
        /// <param name="scriptTextBeforeCaret">The script text preceding the caret.</param>
        /// <param name="prefix">The partial reference typed so far.</param>
        /// <param name="excludeVariable">
        /// A variable to leave out of the list - the one being defined by the <c>SET</c> on the current
        /// line, which cannot reference itself.
        /// </param>
        public static IReadOnlyList<CompletionItem> GetVariableCompletions(string scriptTextBeforeCaret, string prefix = "", string excludeVariable = null)
        {
            var items = GetDefinedVariables(scriptTextBeforeCaret)
                .Where(name => string.IsNullOrEmpty(excludeVariable)
                               || !string.Equals(name, excludeVariable, StringComparison.OrdinalIgnoreCase))
                .Select(name => new CompletionItem(
                    $"$({name})",
                    CompletionItemKind.Variable,
                    "Script variable defined by --> SET",
                    name))
                .ToList();

            items.AddRange(BuiltInVariables.Select(b => new CompletionItem(
                $"$({b.Reference})",
                CompletionItemKind.Variable,
                b.Description,
                b.Reference)));

            if (string.IsNullOrEmpty(prefix)) return items;

            return items
                .Where(i => i.InsertText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }
}
