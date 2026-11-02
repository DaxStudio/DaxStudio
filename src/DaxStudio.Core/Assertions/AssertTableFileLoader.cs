using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using DaxStudio.Parsers.CommentScript;
using Parquet;

namespace DaxStudio.Core.Assertions
{
    /// <summary>
    /// Loads the expected table for a file-based <c>--&gt; ASSERT TABLE (CSV|TXT|MD|PARQUET) "path"</c>
    /// command into the command's <see cref="AssertTableCommand.Data"/> table at evaluation time.
    ///
    /// All formats are reduced to a sequence of string rows (the first row supplying the column
    /// headers) and fed through <see cref="AssertTableCommand.LoadRows"/> so that column-type
    /// inference behaves exactly the same as for inline "--&gt;&gt;" rows. Parquet values are formatted
    /// with the invariant culture (ISO dates) so they re-parse to the same .NET types.
    /// </summary>
    public static class AssertTableFileLoader
    {
        // ISO-style format that round-trips through AssertTableCommand's DateFormats.
        private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";

        /// <summary>
        /// Resolves the command's file path (relative paths are resolved against
        /// <paramref name="baseDirectory"/>), reads the file according to its
        /// <see cref="AssertTableCommand.Format"/>, and populates the command's expected table.
        /// Throws a descriptive exception when the path cannot be resolved or the file cannot be read.
        /// </summary>
        public static void LoadInto(AssertTableCommand command, string baseDirectory)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.Format == AssertTableFormat.Inline) return;

            var path = ResolvePath(command.FilePath, baseDirectory);

            if (!File.Exists(path))
                throw new FileNotFoundException($"ASSERT TABLE file not found: '{path}'", path);

            var rows = ReadRows(command.Format, path);
            command.LoadRows(rows);
        }

        private static string ResolvePath(string filePath, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new InvalidOperationException("ASSERT TABLE file path is empty.");

            if (Path.IsPathRooted(filePath))
                return filePath;

            if (string.IsNullOrEmpty(baseDirectory))
                throw new InvalidOperationException(
                    $"Cannot resolve the relative ASSERT TABLE path '{filePath}' because the document has not been saved. Save the file or use an absolute path.");

            return Path.GetFullPath(Path.Combine(baseDirectory, filePath));
        }

        private static IEnumerable<string[]> ReadRows(AssertTableFormat format, string path)
        {
            switch (format)
            {
                case AssertTableFormat.Csv: return ReadDelimited(path, ",");
                case AssertTableFormat.Txt: return ReadDelimited(path, "\t");
                case AssertTableFormat.Md: return ReadMarkdown(path);
                case AssertTableFormat.Parquet: return ReadParquet(path);
                default:
                    throw new InvalidOperationException($"Unsupported ASSERT TABLE file format '{format}'.");
            }
        }

        private static List<string[]> ReadDelimited(string path, string delimiter)
        {
            var rows = new List<string[]>();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = delimiter,
                // The first record is the header row for the expected table, so let LoadRows treat it
                // as data (it sets the columns from the first row it receives).
                HasHeaderRecord = false,
                DetectColumnCountChanges = false,
                IgnoreBlankLines = true,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = null,
                MissingFieldFound = null,
            };

            using (var reader = new StreamReader(path))
            using (var parser = new CsvParser(reader, config))
            {
                while (parser.Read())
                {
                    var record = parser.Record;
                    if (record == null) continue;
                    rows.Add(record);
                }
            }

            return rows;
        }

        private static List<string[]> ReadMarkdown(string path)
        {
            var rows = new List<string[]>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (!line.StartsWith("|", StringComparison.Ordinal)) continue;

                var cells = SplitMarkdownRow(line);
                if (IsSeparatorRow(cells)) continue;
                rows.Add(cells);
            }
            return rows;
        }

        private static string[] SplitMarkdownRow(string line)
        {
            var trimmed = line;
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);

            return trimmed.Split('|').Select(c => c.Trim()).ToArray();
        }

        // A markdown alignment/separator row such as | --- | :---: | ---: |
        private static bool IsSeparatorRow(string[] cells)
        {
            return cells.Length > 0 && cells.All(c => Regex.IsMatch(c, @"^:?-{1,}:?$"));
        }

        private static List<string[]> ReadParquet(string path)
        {
            var rows = new List<string[]>();
            using (var stream = File.OpenRead(path))
            using (var reader = ParquetReader.CreateAsync(stream).GetAwaiter().GetResult())
            {
                var fields = reader.Schema.GetDataFields();
                rows.Add(fields.Select(f => f.Name).ToArray());

                for (int rg = 0; rg < reader.RowGroupCount; rg++)
                {
                    using (var rgReader = reader.OpenRowGroupReader(rg))
                    {
                        var data = new Array[fields.Length];
                        int rowCount = 0;
                        for (int i = 0; i < fields.Length; i++)
                        {
                            var column = rgReader.ReadColumnAsync(fields[i]).GetAwaiter().GetResult();
                            data[i] = column.Data;
                            rowCount = column.Data.Length;
                        }

                        for (int r = 0; r < rowCount; r++)
                        {
                            var cells = new string[fields.Length];
                            for (int c = 0; c < fields.Length; c++)
                            {
                                cells[c] = FormatParquetValue(data[c].GetValue(r));
                            }
                            rows.Add(cells);
                        }
                    }
                }
            }
            return rows;
        }

        private static string FormatParquetValue(object val)
        {
            switch (val)
            {
                case null:
                    return string.Empty;
                case DateTime dt:
                    return dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case DateTimeOffset dto:
                    return dto.UtcDateTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "TRUE" : "FALSE";
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return val.ToString();
            }
        }
    }
}
