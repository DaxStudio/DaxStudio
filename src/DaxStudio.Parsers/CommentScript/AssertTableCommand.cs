using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class AssertTableCommand : ScriptCommand
    {
        private bool _headersSet;
        private Type[] _explicitTypes;

        // A cell equal to this token represents an explicit empty string (vs an empty cell, which is null/BLANK).
        private const string EmptyStringToken = "\"\"";

        // A cell beginning with this char is an escape: the leading backslash is dropped and the
        // remainder is taken as a literal string (bypassing null / empty-token interpretation).
        // e.g. "\\\"\"" -> literal "", "\\\"" -> literal ", "\\\\x" -> literal "\x".
        private const char EscapeChar = '\\';

        // Date formats accepted in ASSERT TABLE cells (most specific first)
        private static readonly string[] DateFormats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss.fff",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd",
            "MM/dd/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy",
        };

        // Recognized DAX type names mapped to .NET types
        private static readonly Dictionary<string, Type> DaxTypeMap = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "STRING",   typeof(string)   },
            { "TEXT",     typeof(string)   },
            { "INT64",    typeof(long)     },
            { "INTEGER",  typeof(long)     },
            { "INT",      typeof(long)     },
            { "DOUBLE",   typeof(double)   },
            { "CURRENCY", typeof(decimal)  },
            { "DECIMAL",  typeof(decimal)  },
            { "BOOLEAN",  typeof(bool)     },
            { "BOOL",     typeof(bool)     },
            { "DATETIME", typeof(DateTime) },
            { "DATE",     typeof(DateTime) },
        };

        public AssertTableCommand(AssertTableMode mode)
        {
            Mode = mode;
            Data = new DataTable();
        }

        public AssertTableMode Mode { get; }
        public DataTable Data { get; }

        /// <summary>
        /// The source of the expected table data. <see cref="AssertTableFormat.Inline"/> means the
        /// expected rows are authored inline as "--&gt;&gt;" continuation rows; any other value means the
        /// rows are loaded from <see cref="FilePath"/> at evaluation time.
        /// </summary>
        public AssertTableFormat Format { get; set; } = AssertTableFormat.Inline;

        /// <summary>
        /// The (possibly relative) path to the file that provides the expected table, when
        /// <see cref="Format"/> is not <see cref="AssertTableFormat.Inline"/>. Null for inline tables.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>1-based line of the source "--&gt; ASSERT TABLE" command (0 when unknown).</summary>
        public int Line { get; set; }

        /// <summary>0-based character position of the source "--&gt; ASSERT TABLE" command (0 when unknown).</summary>
        public int Column { get; set; }

        /// <summary>
        /// True once the assertion has a defined expected table: either at least one inline table row
        /// ("--&gt;&gt;") has defined the columns, or the command loads its rows from a file
        /// (<see cref="Format"/> is not <see cref="AssertTableFormat.Inline"/>).
        /// An ASSERT TABLE with no following table rows and no file clause leaves this false.
        /// </summary>
        public bool HasTableDefinition => Data.Columns.Count > 0 || Format != AssertTableFormat.Inline;

        /// <summary>
        /// Populates this assertion's expected table from externally-loaded rows (e.g. a
        /// CSV/TXT/MD/PARQUET file). The first row supplies the column headers; an optional following
        /// all-type-names row is treated as an explicit type declaration; the remaining rows are data.
        /// Column types are inferred (or taken from the type row) using the same rules as inline
        /// "--&gt;&gt;" rows, so file-based and inline assertions behave identically. Any previously
        /// loaded data is discarded first so the call is idempotent.
        /// </summary>
        public void LoadRows(IEnumerable<string[]> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            Data.Rows.Clear();
            Data.Columns.Clear();
            _headersSet = false;
            _explicitTypes = null;

            foreach (var row in rows)
            {
                AddRow(row);
            }

            if (Data.Rows.Count > 0)
            {
                InferColumnTypes();
            }
        }

        /// <summary>
        /// Adds a row of cell values. The first call sets column headers.
        /// If the second call contains all valid DAX type names, it's treated as a type declaration row.
        /// Subsequent calls add data rows. After all rows, call InferColumnTypes().
        /// </summary>
        internal void AddRow(string[] cells)
        {
            if (!_headersSet)
            {
                foreach (var header in cells)
                {
                    Data.Columns.Add(header, typeof(string));
                }
                _headersSet = true;
            }
            else if (_explicitTypes == null && Data.Rows.Count == 0 && IsTypeRow(cells))
            {
                _explicitTypes = cells.Select(c => DaxTypeMap[c.Trim()]).ToArray();
            }
            else
            {
                var row = Data.NewRow();
                for (int i = 0; i < cells.Length && i < Data.Columns.Count; i++)
                {
                    row[i] = cells[i];
                }
                Data.Rows.Add(row);
            }
        }

        /// <summary>
        /// Returns true if every cell is a recognized DAX type name.
        /// </summary>
        private static bool IsTypeRow(string[] cells)
        {
            return cells.Length > 0 && cells.All(c => DaxTypeMap.ContainsKey(c.Trim()));
        }

        /// <summary>
        /// Applies column types — uses explicit type row if provided, otherwise infers from values.
        /// Should be called after all rows have been added.
        /// </summary>
        internal void InferColumnTypes()
        {
            for (int col = 0; col < Data.Columns.Count; col++)
            {
                Type targetType;
                if (_explicitTypes != null && col < _explicitTypes.Length)
                {
                    targetType = _explicitTypes[col];
                }
                else
                {
                    targetType = InferColumnType(col);
                }

                ReplaceColumnWithType(col, targetType);
            }
        }

        private void ReplaceColumnWithType(int col, Type targetType)
        {
            var colName = Data.Columns[col].ColumnName;
            var newCol = new DataColumn(colName + "_typed", targetType);
            Data.Columns.Add(newCol);

            foreach (DataRow row in Data.Rows)
            {
                row[newCol] = NormalizeCellValue(row[col] as string, targetType);
            }

            var ordinal = Data.Columns[col].Ordinal;
            Data.Columns.Remove(Data.Columns[col]);
            newCol.ColumnName = colName;
            newCol.SetOrdinal(ordinal);
        }

        /// <summary>
        /// Converts a raw cell string to its typed value applying the null / empty-string / escape rules:
        /// a leading backslash escapes the rest as a literal string; an empty cell is null (DBNull) for
        /// every column type; the "" token is an explicit empty string for a string column; otherwise
        /// the raw value is converted to the target type.
        /// </summary>
        private static object NormalizeCellValue(string raw, Type targetType)
        {
            if (raw != null && raw.Length > 0 && raw[0] == EscapeChar)
            {
                var literal = raw.Substring(1);
                return targetType == typeof(string) ? (object)literal : ConvertValue(literal, targetType);
            }

            if (string.IsNullOrEmpty(raw))
                return DBNull.Value;

            if (targetType == typeof(string))
                return raw == EmptyStringToken ? string.Empty : raw;

            return ConvertValue(raw, targetType);
        }

        private static object ConvertValue(string val, Type targetType)
        {
            if (targetType == typeof(bool))
                return bool.Parse(val);
            if (targetType == typeof(long))
                return long.Parse(val, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return decimal.Parse(val, NumberStyles.Number, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(val, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            if (targetType == typeof(DateTime))
                return DateTime.ParseExact(val, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
            return val;
        }

        /// <summary>
        /// Infers the .NET type for a column by checking all non-empty values.
        /// Priority order: Boolean > Int64 > Double > Decimal > DateTime > String.
        /// Double is checked before Decimal so that plain decimals (3.14) become Double
        /// while values with thousands separators (1,234.56) become Decimal (currency).
        /// </summary>
        private Type InferColumnType(int colIndex)
        {
            var values = new List<string>(Data.Rows.Count);
            foreach (DataRow row in Data.Rows)
            {
                values.Add(row[colIndex] as string);
            }
            return InferColumnType(values);
        }

        /// <summary>
        /// Infers the .NET type for a column from its (string) values using the same rules the
        /// ASSERT TABLE parser applies. Shared with <see cref="TableAssertionFormatter"/> so
        /// generated blocks and parsed blocks agree on types.
        /// Priority order: Boolean > Int64 > Double > Decimal > DateTime > String.
        /// Double is checked before Decimal so that plain decimals (3.14) become Double
        /// while values with thousands separators (1,234.56) become Decimal (currency).
        /// </summary>
        internal static Type InferColumnType(IEnumerable<string> values)
        {
            bool allBoolean = true;
            bool allLong = true;
            bool allDouble = true;
            bool allDecimal = true;
            bool allDateTime = true;
            bool hasValues = false;

            foreach (var val in values)
            {
                if (string.IsNullOrEmpty(val)) continue;

                // An explicit empty-string token or an escaped literal forces the column to string.
                if (val == EmptyStringToken || val[0] == EscapeChar)
                    return typeof(string);

                hasValues = true;

                if (allBoolean && !IsBooleanValue(val))
                    allBoolean = false;
                if (allLong && !long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    allLong = false;
                if (allDouble && !double.TryParse(val, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    allDouble = false;
                if (allDecimal && !decimal.TryParse(val, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    allDecimal = false;
                if (allDateTime && !DateTime.TryParseExact(val, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    allDateTime = false;

                if (!allBoolean && !allLong && !allDouble && !allDecimal && !allDateTime) break;
            }

            if (!hasValues) return typeof(string);
            if (allBoolean) return typeof(bool);
            if (allLong) return typeof(long);
            if (allDouble) return typeof(double);
            if (allDecimal) return typeof(decimal);
            if (allDateTime) return typeof(DateTime);
            return typeof(string);
        }

        private static bool IsBooleanValue(string val)
        {
            return string.Equals(val, "TRUE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(val, "FALSE", StringComparison.OrdinalIgnoreCase);
        }
    }
}
