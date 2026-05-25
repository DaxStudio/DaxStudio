using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DaxStudio.Interfaces;

namespace DaxStudio.Core.Extensions
{
    public static class DataReaderExtensions
    {
        internal class DaxColumn
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public bool UseOriginalName { get; set; }
        }

        public static string[] CleanColumnNames(this IDataReader reader)
        {
            string[] columns = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns[i] = reader.GetName(i);
            }

            var columnPattern = new Regex(@"\[(?<col>.*)]\d*$", RegexOptions.Compiled);
            var mdxPattern = new Regex(@"\[[^\]]*\].\[[^\]]*\]");
            const string MEASURES_MDX = "[Measures].";
            var newColumnNames = new Dictionary<string, DaxColumn>();

            // If at least one column has the Mdx syntax, identify the result as an MDX query (hoping the assumption is always true...)
            bool isMdxResult = (from col in columns
                                where mdxPattern.IsMatch(col)
                                select col).Any();

            var measuresColumns = (from col in columns
                                   where col.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase) >= 0
                                   select col);
            bool hasPlainMeasures = !(from col in measuresColumns
                                      where col.IndexOf("].[", col.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase) + MEASURES_MDX.Length, StringComparison.OrdinalIgnoreCase) > 0
                                      select col).Any();
            foreach (string columnName in columns)
            {
                bool removeCaption = false;
                string name = columnName;
                bool removeSquareBrackets = !isMdxResult;
                int measuresMdxPos = name.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase);
                if (isMdxResult)
                {
                    if ((measuresMdxPos >= 0))
                    {
                        if ((name.IndexOf("].[", measuresMdxPos + MEASURES_MDX.Length, StringComparison.OrdinalIgnoreCase) == -1)
                        && (name.IndexOf("].[", 0, StringComparison.OrdinalIgnoreCase) == MEASURES_MDX.Length - 2))
                        {
                            removeSquareBrackets = true;
                        }
                        name = name.Replace(MEASURES_MDX, measuresMdxPos > 0 ? "\n" : "");
                    }
                    else
                    {
                        removeCaption = hasPlainMeasures;
                    }
                }

                if (removeSquareBrackets)
                {
                    var m = columnPattern.Match(name);
                    if (m.Success)
                    {
                        name = m.Groups["col"].Value;
                    }
                }
                var dc = new DaxColumn()
                {
                    OriginalName = columnName,
                    NewName = name,
                };
                newColumnNames.Add(dc.OriginalName, dc);
            }
            // check for duplicate names
            for (var outerIdx = 0; outerIdx < newColumnNames.Count; outerIdx++)
            {
                for (var innerIdx = outerIdx + 1; innerIdx < newColumnNames.Count; innerIdx++)
                {
                    if (newColumnNames.ElementAt(outerIdx).Value.NewName == newColumnNames.ElementAt(innerIdx).Value.NewName)
                    {
                        newColumnNames.ElementAt(outerIdx).Value.UseOriginalName = true;
                        newColumnNames.ElementAt(innerIdx).Value.UseOriginalName = true;
                    }
                }
            }
            string[] newNames = new string[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                var c = newColumnNames.ElementAt(i).Value;
                newNames[i] = c.UseOriginalName ? c.OriginalName : c.NewName;
            }
            return newNames;
        }

        /// <summary>
        /// Writes a DataTable object to a TextWriter
        /// </summary>
        public static int WriteToStream(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, TextWriter textWriter, string sep, bool shouldQuoteStrings, string isoDateFormat, IStatusBarMessage statusProgress)
        {
            int iRowCnt = 0;

            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = sep };

            using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
            {
                csvWriter.Context.TypeConverterOptionsCache.AddOptions(
                    typeof(DateTime),
                    new CsvHelper.TypeConversion.TypeConverterOptions() { Formats = new string[] { isoDateFormat } });

                foreach (var colName in reader.CleanColumnNames())
                {
                    csvWriter.WriteField(colName);
                }

                csvWriter.NextRecord();

                while (reader.Read())
                {
                    iRowCnt++;

                    for (int iCol = 0; iCol < reader.FieldCount; iCol++)
                    {
                        var fieldValue = reader[iCol];

                        if (reader.GetFieldType(iCol) == typeof(string))
                            if (reader.IsDBNull(iCol))
                                csvWriter.WriteField("", shouldQuoteStrings);
                            else
                                csvWriter.WriteField(fieldValue.ToString(), shouldQuoteStrings);
                        else
                            csvWriter.WriteField(fieldValue);
                    }

                    csvWriter.NextRecord();

                    if (iRowCnt % 1000 == 0)
                    {
                        statusProgress.Update($"Written {iRowCnt:n0} rows to the file output");
                    }
                }
            }

            return iRowCnt;
        }

        /// <summary>
        /// Writes a DataTable object to a TextWriter using format strings
        /// </summary>
        public static int WriteToStreamWithFormatting(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, TextWriter textWriter, string sep, bool shouldQuoteStrings, Dictionary<int, string> formatStrings, IStatusBarMessage statusProgress)
        {
            int iRowCnt = 0;

            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = sep };

            using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
            {
                foreach (var colName in reader.CleanColumnNames())
                {
                    csvWriter.WriteField(colName);
                }

                csvWriter.NextRecord();

                while (reader.Read())
                {
                    iRowCnt++;

                    for (int iCol = 0; iCol < reader.FieldCount; iCol++)
                    {
                        var fieldValue = reader[iCol];

                        if (reader.GetFieldType(iCol) == typeof(string))
                            if (reader.IsDBNull(iCol))
                                csvWriter.WriteField("", shouldQuoteStrings);
                            else
                                csvWriter.WriteField(fieldValue.ToString(), shouldQuoteStrings);
                        else
                            if (!string.IsNullOrEmpty(formatStrings[iCol]))
                            switch (fieldValue)
                            {
                                case int intValue:
                                    csvWriter.WriteField(intValue.ToString(formatStrings[iCol]));
                                    break;
                                case long longValue:
                                    csvWriter.WriteField(longValue.ToString(formatStrings[iCol]));
                                    break;
                                case decimal decimalValue:
                                    csvWriter.WriteField(decimalValue.ToString(formatStrings[iCol]));
                                    break;
                                case double doubleValue:
                                    csvWriter.WriteField(doubleValue.ToString(formatStrings[iCol]));
                                    break;
                                case DateTime dateTimeValue:
                                    csvWriter.WriteField(dateTimeValue.ToString(formatStrings[iCol]));
                                    break;
                                default:
                                    csvWriter.WriteField(fieldValue);
                                    break;
                            }

                        else
                            csvWriter.WriteField(fieldValue);
                    }

                    csvWriter.NextRecord();

                    if (iRowCnt % 1000 == 0)
                    {
                        statusProgress.Update($"Written {iRowCnt:n0} rows to the file output");
                    }
                }
            }

            return iRowCnt;
        }
    }
}
