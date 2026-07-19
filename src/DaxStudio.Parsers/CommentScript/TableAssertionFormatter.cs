using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// Builds the inline <c>--&gt; ASSERT TABLE</c> block (the <c>--&gt;&gt; | ... |</c> continuation
    /// rows) from either a live result <see cref="DataTable"/> or tab-delimited clipboard text.
    /// The output is designed to round-trip: feeding the produced <c>--&gt;&gt;</c> rows back through
    /// <see cref="AssertTableCommand.AddRow"/> reproduces an equivalent table.
    /// </summary>
    public static class TableAssertionFormatter
    {
        /// <summary>The comment-script header line that opens a table assertion.</summary>
        public const string HeaderLine = "--> ASSERT TABLE";

        /// <summary>The continuation marker that prefixes each table row.</summary>
        public const string ContinuationMarker = "-->>";

        // Canonical DAX type names emitted for each CLR type (must be keys of AssertTableCommand.DaxTypeMap).
        private const string DaxString = "STRING";
        private const string DaxInt64 = "INT64";
        private const string DaxDouble = "DOUBLE";
        private const string DaxCurrency = "CURRENCY";
        private const string DaxBoolean = "BOOLEAN";
        private const string DaxDateTime = "DATETIME";

        /// <summary>
        /// Formats a <see cref="DataTable"/> as an ASSERT TABLE block.
        /// </summary>
        /// <param name="table">The source table (e.g. a query result).</param>
        /// <param name="includeHeaderLine">When true, prefixes the block with <c>--&gt; ASSERT TABLE</c>.</param>
        /// <param name="includeTypeRow">When true, emits an explicit DAX type row.</param>
        public static string FormatDataTable(DataTable table, bool includeHeaderLine = true, bool includeTypeRow = true)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            // The results DataTable stores the display name in Caption (ColumnName is escaped - e.g.
            // spaces become backticks - as a grid-sorting workaround), so prefer Caption for headers.
            var headers = table.Columns.Cast<DataColumn>()
                .Select(c => string.IsNullOrEmpty(c.Caption) ? c.ColumnName : c.Caption)
                .ToList();
            var types = table.Columns.Cast<DataColumn>().Select(c => DaxTypeName(c.DataType)).ToList();
            var rightAlign = types.Select(IsNumericType).ToArray();

            var dataRows = new List<string[]>(table.Rows.Count);
            foreach (DataRow row in table.Rows)
            {
                var cells = new string[table.Columns.Count];
                for (int i = 0; i < table.Columns.Count; i++)
                {
                    cells[i] = FormatCell(row[i], table.Columns[i].DataType);
                }
                dataRows.Add(cells);
            }

            return Build(headers, includeTypeRow ? types : null, dataRows, rightAlign, includeHeaderLine);
        }

        /// <summary>
        /// Formats tab-delimited text (first line = headers, remaining lines = data) as an ASSERT
        /// TABLE block. Column types are inferred from the values using the same rules the parser
        /// applies, so an explicit type row can be emitted.
        /// </summary>
        public static string FormatTabDelimited(string clipboardText, bool includeHeaderLine = true, bool includeTypeRow = true)
        {
            if (string.IsNullOrEmpty(clipboardText)) throw new ArgumentException("Clipboard text is empty.", nameof(clipboardText));

            var lines = clipboardText.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n')
                .Where(l => l.Length > 0)
                .ToList();
            if (lines.Count == 0) throw new ArgumentException("Clipboard text contains no rows.", nameof(clipboardText));

            var headers = lines[0].Split('\t').Select(SanitizeCell).ToList();
            var dataRows = lines.Skip(1)
                .Select(l => l.Split('\t').Select(SanitizeCell).ToArray())
                .ToList();

            // Types are always inferred (used for both the optional type row and column alignment).
            var types = new List<string>(headers.Count);
            for (int col = 0; col < headers.Count; col++)
            {
                var colValues = dataRows.Select(r => col < r.Length ? r[col] : null);
                types.Add(DaxTypeName(AssertTableCommand.InferColumnType(colValues)));
            }
            var rightAlign = types.Select(IsNumericType).ToArray();

            return Build(headers, includeTypeRow ? types : null, dataRows, rightAlign, includeHeaderLine);
        }

        /// <summary>
        /// Returns true when the text looks like tab-delimited tabular data (at least one tab).
        /// </summary>
        public static bool LooksLikeTabDelimited(string text)
        {
            return !string.IsNullOrEmpty(text) && text.IndexOf('\t') >= 0;
        }

        // Builds the block, padding every column to a common width so the '|' separators line up.
        // The header and type rows are left-aligned; data cells in numeric columns are right-aligned
        // (so numbers line up) and left-aligned otherwise.
        private static string Build(IReadOnlyList<string> headers, IReadOnlyList<string> types, IReadOnlyList<string[]> dataRows, bool[] rightAlign, bool includeHeaderLine)
        {
            int cols = headers.Count;
            var widths = new int[cols];
            for (int c = 0; c < cols; c++) widths[c] = SanitizeCell(headers[c]).Length;
            if (types != null)
                for (int c = 0; c < cols && c < types.Count; c++)
                    widths[c] = Math.Max(widths[c], SanitizeCell(types[c]).Length);
            foreach (var row in dataRows)
                for (int c = 0; c < cols; c++)
                {
                    var v = c < row.Length ? SanitizeCell(row[c]) : string.Empty;
                    if (v.Length > widths[c]) widths[c] = v.Length;
                }

            var sb = new StringBuilder();
            var newLine = Environment.NewLine;

            if (includeHeaderLine) sb.Append(HeaderLine).Append(newLine);

            sb.Append(FormatRow(headers, widths, null)).Append(newLine);
            if (types != null) sb.Append(FormatRow(types, widths, null)).Append(newLine);

            for (int i = 0; i < dataRows.Count; i++)
            {
                sb.Append(FormatRow(dataRows[i], widths, rightAlign));
                if (i < dataRows.Count - 1) sb.Append(newLine);
            }

            return sb.ToString();
        }

        private static string FormatRow(IReadOnlyList<string> cells, int[] widths, bool[] rightAlign)
        {
            var sb = new StringBuilder();
            sb.Append(ContinuationMarker).Append(" |");
            for (int c = 0; c < widths.Length; c++)
            {
                var cell = SanitizeCell(c < cells.Count ? cells[c] : string.Empty);
                var padded = (rightAlign != null && c < rightAlign.Length && rightAlign[c])
                    ? cell.PadLeft(widths[c])
                    : cell.PadRight(widths[c]);
                sb.Append(' ').Append(padded).Append(" |");
            }
            return sb.ToString();
        }

        private static bool IsNumericType(string daxType)
        {
            return daxType == DaxInt64 || daxType == DaxDouble || daxType == DaxCurrency || daxType == "DECIMAL";
        }

        // '|' delimits cells and newlines terminate rows in the grammar, so neither can appear
        // inside a cell. Replace them so generated blocks always parse back correctly.
        private static string SanitizeCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/').Trim();
        }

        private static string FormatCell(object value, Type columnType)
        {
            if (value == null || value == DBNull.Value) return string.Empty;

            if (columnType == typeof(DateTime) || value is DateTime)
            {
                var dt = (DateTime)value;
                var format = dt.TimeOfDay == TimeSpan.Zero ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss";
                return dt.ToString(format, CultureInfo.InvariantCulture);
            }
            if (value is bool b) return b ? "TRUE" : "FALSE";
            if (value is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);

            return SanitizeCell(value.ToString());
        }

        private static string DaxTypeName(Type type)
        {
            if (type == typeof(string)) return DaxString;
            if (type == typeof(long) || type == typeof(int) || type == typeof(short) || type == typeof(byte)
                || type == typeof(sbyte) || type == typeof(uint) || type == typeof(ushort) || type == typeof(ulong))
                return DaxInt64;
            if (type == typeof(double) || type == typeof(float)) return DaxDouble;
            if (type == typeof(decimal)) return DaxCurrency;
            if (type == typeof(bool)) return DaxBoolean;
            if (type == typeof(DateTime)) return DaxDateTime;
            return DaxString;
        }
    }
}
