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

                if (targetType == typeof(string)) continue;

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
                var val = row[col] as string;
                if (string.IsNullOrEmpty(val))
                {
                    row[newCol] = DBNull.Value;
                }
                else
                {
                    row[newCol] = ConvertValue(val, targetType);
                }
            }

            var ordinal = Data.Columns[col].Ordinal;
            Data.Columns.Remove(Data.Columns[col]);
            newCol.ColumnName = colName;
            newCol.SetOrdinal(ordinal);
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
