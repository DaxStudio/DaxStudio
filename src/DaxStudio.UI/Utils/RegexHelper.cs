using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    internal static class RegexHelper
    {
        public static Regex QueryErrorRegex { get; } = new Regex(
                        @"^(?:Query \()(?<line>\d+)(?:\s*,\s*)(?<column>\d+)(?:\s*\))(?<err>.*)$|Line\s+(?<line>\d+),\s+Offset\s+(?<column>\d+),(?<err>.*)$",
                        RegexOptions.Compiled | RegexOptions.Multiline);

        public static (int Line,int Column) GetQueryErrorLocation(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage)) return (0, 0);

            var match = QueryErrorRegex.Match(errorMessage);
            if (match.Success && match.Groups["line"].Success && match.Groups["column"].Success)
            {
                int line = int.Parse(match.Groups["line"].Value);
                int column = int.Parse(match.Groups["column"].Value);
                return (line, column);
            }
            return (0, 0);
        }
    }

}
