using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DAXEditorControl.Renderers
{
    public enum CommentScriptCommandCategory
    {
        Scenario,
        Connection,
        Diagnostics,
        BatchSeparator,
        Results,
        Validation
    }

    public sealed class CommentScriptCommandStyle
    {
        private static readonly Regex DirectiveRegex =
            new Regex(@"^[ \t]*-->(?!>)[ \t]*([A-Za-z]+)\b", RegexOptions.Compiled);

        private static readonly IReadOnlyDictionary<string, CommentScriptCommandCategory> Categories =
            new Dictionary<string, CommentScriptCommandCategory>(StringComparer.OrdinalIgnoreCase)
            {
                ["TEST"] = CommentScriptCommandCategory.Scenario,
                ["BASELINE"] = CommentScriptCommandCategory.Scenario,
                ["CONNECT"] = CommentScriptCommandCategory.Connection,
                ["USE"] = CommentScriptCommandCategory.Connection,
                ["SET"] = CommentScriptCommandCategory.Connection,
                ["OPEN"] = CommentScriptCommandCategory.Connection,
                ["PARAMETER"] = CommentScriptCommandCategory.Connection,
                ["CLEARCACHE"] = CommentScriptCommandCategory.Diagnostics,
                ["TRACE"] = CommentScriptCommandCategory.Diagnostics,
                ["GO"] = CommentScriptCommandCategory.BatchSeparator,
                ["RESULTS"] = CommentScriptCommandCategory.Results,
                ["SHOW"] = CommentScriptCommandCategory.Results,
                ["SAVEAS"] = CommentScriptCommandCategory.Results,
                ["OUTPUT"] = CommentScriptCommandCategory.Results,
                ["METRICS"] = CommentScriptCommandCategory.Results,
                ["EXPORT"] = CommentScriptCommandCategory.Results,
                ["ASSERT"] = CommentScriptCommandCategory.Validation
            };

        private CommentScriptCommandStyle(string command, CommentScriptCommandCategory category)
        {
            Command = command;
            Category = category;
        }

        public string Command { get; }
        public CommentScriptCommandCategory Category { get; }
        public bool IsBatchSeparator => Category == CommentScriptCommandCategory.BatchSeparator;
        public double IndicatorWidth => 4.0;
        public string CommandColorName => $"Cs{Category}Command";
        public string RowBackgroundColorName => $"Cs{Category}RowBackground";

        public static bool TryClassify(string line, out CommentScriptCommandStyle style)
        {
            style = null;
            if (string.IsNullOrEmpty(line)) return false;

            var match = DirectiveRegex.Match(line);
            if (!match.Success) return false;

            var command = match.Groups[1].Value;
            if (!Categories.TryGetValue(command, out var category)) return false;

            style = new CommentScriptCommandStyle(command, category);
            return true;
        }
    }
}
