using System.Collections.Generic;
using System.Linq;
using DaxStudio.Parsers.Dax;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Produces editor foldings from the structure of the DAX code (via the ANTLR parser) rather than
    /// from indentation. It folds the DEFINE block and its definitions, VAR/RETURN blocks, EVALUATE
    /// blocks and their ORDER BY clause, and multi-line bracket pairs. In addition it folds runs of
    /// consecutive comment-script ASSERT TABLE continuation lines (lines beginning with <c>--&gt;&gt;</c>),
    /// which the DAX lexer treats as ordinary comments.
    /// </summary>
    public class StructuralFoldingStrategy : IFoldingStrategy
    {
        private readonly DaxParserService _parser = new DaxParserService(null);
        private ITextSourceVersion prevVersion;

        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            if (prevVersion != null && document.Version != null && document.Version.CompareAge(prevVersion) == 0) return;
            prevVersion = document.Version;

            var newFoldings = CreateNewFoldings(document, out int firstErrorOffset);
            manager.UpdateFoldings(newFoldings, firstErrorOffset);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            return CreateNewFoldings(document);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document)
        {
            var foldings = new List<NewFolding>();
            var seen = new HashSet<long>();

            AddParserFoldings(document, foldings, seen);
            AddAssertTableFoldings(document, foldings, seen);

            foldings.Sort((a, b) =>
            {
                int cmp = a.StartOffset.CompareTo(b.StartOffset);
                return cmp != 0 ? cmp : b.EndOffset.CompareTo(a.EndOffset);
            });
            return foldings;
        }

        /// <summary>
        /// Converts the parser's structural ranges into folds that keep the first line of each
        /// construct visible and collapse the remainder. Single-line constructs are discarded.
        /// </summary>
        private void AddParserFoldings(TextDocument document, List<NewFolding> foldings, HashSet<long> seen)
        {
            IReadOnlyList<FoldRange> ranges;
            try
            {
                ranges = _parser.GetFoldings(document.Text);
            }
            catch
            {
                return;
            }

            int textLength = document.TextLength;
            foreach (var range in ranges)
            {
                if (range.StartOffset < 0 || range.StartOffset >= textLength) continue;

                int end = range.EndOffset;
                if (end > textLength) end = textLength;

                var startLine = document.GetLineByOffset(range.StartOffset);
                int foldStart = startLine.EndOffset;
                if (foldStart >= end) continue; // single line construct - nothing to collapse

                AddFold(foldings, seen, foldStart, end, null);
            }
        }

        /// <summary>
        /// Folds runs of two or more consecutive comment-script continuation lines (prefixed with
        /// <c>--&gt;&gt;</c>) into a single region. When the run is immediately preceded by an
        /// <c>--&gt; ASSERT TABLE</c> header line the fold starts on that header line so it stays
        /// visible; otherwise it starts on the first continuation line.
        /// </summary>
        private void AddAssertTableFoldings(TextDocument document, List<NewFolding> foldings, HashSet<long> seen)
        {
            DocumentLine runStart = null;
            DocumentLine runEnd = null;
            int runCount = 0;
            var runRows = new List<string>();

            void Flush()
            {
                if (runCount >= 2 && runStart != null && runEnd != null)
                {
                    // Prefer starting the fold on the "--> ASSERT TABLE" header line if it directly
                    // precedes the run, so the header remains visible while the rows collapse.
                    var header = runStart.PreviousLine;
                    var foldStartLine = (header != null && IsAssertTableHeaderLine(document, header))
                        ? header
                        : runStart;
                    AddFold(foldings, seen, foldStartLine.EndOffset, runEnd.EndOffset, BuildAssertTableTitle(runRows));
                }
                runStart = null;
                runEnd = null;
                runCount = 0;
                runRows.Clear();
            }

            foreach (DocumentLine line in document.Lines)
            {
                if (IsAssertTableLine(document, line))
                {
                    if (runStart == null) runStart = line;
                    runEnd = line;
                    runCount++;
                    runRows.Add(StripAssertTableMarker(document, line));
                }
                else
                {
                    Flush();
                }
            }
            Flush();
        }

        // DAX type names recognized as an ASSERT TABLE type-declaration row. Kept in sync with
        // DaxStudio.Parsers.CommentScript.AssertTableCommand.DaxTypeMap.
        private static readonly HashSet<string> DaxTypeNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "STRING", "TEXT", "INT64", "INTEGER", "INT", "DOUBLE", "CURRENCY",
            "DECIMAL", "BOOLEAN", "BOOL", "DATETIME", "DATE",
        };

        private const int MaxTitleColumns = 4;

        /// <summary>
        /// Builds the collapsed-fold title for a run of <c>--&gt;&gt;</c> ASSERT TABLE rows in the
        /// form <c> [col1, col2, ...] (rowsxcols)</c>. The leading space lets the title read as a
        /// continuation of the visible <c>--&gt; ASSERT TABLE</c> header line. The column list is truncated after
        /// <see cref="MaxTitleColumns"/> columns with an ellipsis. Row count excludes separator and
        /// leading DAX-type rows, matching the real parser's counting. Falls back to
        /// <c>"--&gt;&gt; ..."</c> when no header row can be parsed.
        /// </summary>
        private static string BuildAssertTableTitle(IReadOnlyList<string> rows)
        {
            const string fallback = "-->> ...";
            if (rows == null || rows.Count == 0) return fallback;

            string[] headerCells = null;
            int dataRowCount = 0;
            bool typeRowConsumed = false;

            foreach (var raw in rows)
            {
                var cells = SplitRowCells(raw);
                if (cells.Length == 0) continue;
                if (IsSeparatorRow(cells)) continue;

                if (headerCells == null)
                {
                    headerCells = cells;
                    continue;
                }

                // A single leading type row (before any data row) is not counted as data.
                if (!typeRowConsumed && dataRowCount == 0 && IsTypeRow(cells))
                {
                    typeRowConsumed = true;
                    continue;
                }

                dataRowCount++;
            }

            if (headerCells == null || headerCells.Length == 0) return fallback;

            int colCount = headerCells.Length;
            string cols;
            if (colCount > MaxTitleColumns)
            {
                cols = string.Join(", ", headerCells.Take(MaxTitleColumns)) + ", \u2026";
            }
            else
            {
                cols = string.Join(", ", headerCells);
            }

            return $" [{cols}] ({dataRowCount}x{colCount})";
        }

        /// <summary>
        /// Splits an ASSERT TABLE row (marker already stripped) into trimmed cell values, dropping the
        /// empty outer cells produced by the leading/trailing pipe delimiters.
        /// </summary>
        private static string[] SplitRowCells(string strippedLine)
        {
            if (string.IsNullOrWhiteSpace(strippedLine)) return System.Array.Empty<string>();
            var parts = strippedLine.Split('|');
            var cells = new List<string>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                var trimmed = parts[i].Trim();
                // Drop the empty first/last cell that results from surrounding pipes.
                if (trimmed.Length == 0 && (i == 0 || i == parts.Length - 1)) continue;
                cells.Add(trimmed);
            }
            return cells.ToArray();
        }

        /// <summary>Returns true when every cell is a markdown separator cell (e.g. <c>---</c>, <c>:--:</c>).</summary>
        private static bool IsSeparatorRow(string[] cells)
        {
            if (cells.Length == 0) return false;
            foreach (var cell in cells)
            {
                if (cell.Length == 0) return false;
                foreach (var ch in cell)
                {
                    if (ch != '-' && ch != ':') return false;
                }
                if (cell.IndexOf('-') < 0) return false; // must contain at least one dash
            }
            return true;
        }

        /// <summary>Returns true when every cell is a recognized DAX type name.</summary>
        private static bool IsTypeRow(string[] cells)
        {
            if (cells.Length == 0) return false;
            foreach (var cell in cells)
            {
                if (!DaxTypeNames.Contains(cell)) return false;
            }
            return true;
        }

        /// <summary>Returns the text of a <c>--&gt;&gt;</c> line with the leading whitespace and marker removed.</summary>
        private static string StripAssertTableMarker(TextDocument document, DocumentLine line)
        {
            var text = document.GetText(line.Offset, line.Length).TrimStart();
            // text starts with "-->>"
            return text.Length > 4 ? text.Substring(4) : string.Empty;
        }

        private static bool IsAssertTableLine(TextDocument document, DocumentLine line)
        {
            int i = line.Offset;
            int end = line.EndOffset;
            while (i < end && char.IsWhiteSpace(document.GetCharAt(i))) i++;
            if (end - i < 4) return false;
            return document.GetCharAt(i) == '-'
                && document.GetCharAt(i + 1) == '-'
                && document.GetCharAt(i + 2) == '>'
                && document.GetCharAt(i + 3) == '>';
        }

        /// <summary>
        /// Returns true when the line is a comment-script <c>--&gt; ASSERT TABLE</c> header (a single
        /// <c>--&gt;</c> prefix, not the <c>--&gt;&gt;</c> continuation prefix, followed by the ASSERT
        /// TABLE keywords).
        /// </summary>
        private static bool IsAssertTableHeaderLine(TextDocument document, DocumentLine line)
        {
            var text = document.GetText(line.Offset, line.Length).TrimStart();
            if (!text.StartsWith("-->", System.StringComparison.Ordinal)) return false;
            if (text.Length > 3 && text[3] == '>') return false; // this is a "-->>" continuation line
            var rest = text.Substring(3).TrimStart();
            if (!rest.StartsWith("ASSERT", System.StringComparison.OrdinalIgnoreCase)) return false;
            return rest.IndexOf("TABLE", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddFold(List<NewFolding> foldings, HashSet<long> seen, int start, int end, string name)
        {
            if (start >= end) return;
            long key = ((long)start << 32) | (uint)end;
            if (!seen.Add(key)) return;

            var fold = new NewFolding(start, end);
            if (name != null) fold.Name = name;
            foldings.Add(fold);
        }
    }
}
