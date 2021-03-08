using DaxStudio.Common;
using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Extensions
{
    public static class DataReaderExtensions
    {
        

        internal class DaxColumn
        {
            public string OriginalName { get; set; }
            public string NewName { get; set; }
            public bool UseOriginalName { get; set; }
        }

        public static string[] CleanColumnNames(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader)
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
            //bool isMdxResult = (from DataColumn col in dataTable.Columns
            //                    where col.ColumnName.IndexOf("].[") > 0
            //                    select col).Count() > 0;
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
                int measuresMdxPos = name.IndexOf(MEASURES_MDX, StringComparison.OrdinalIgnoreCase);// + MEASURES_MDX.Length;
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
                    // Format column naming for DAX result or if it is a measure name
                    //int firstBracket = name.IndexOf('[') + 1;
                    //name = firstBracket == 0 ? name : name.Substring(firstBracket, name.Length - firstBracket - 1);
                }
                var dc = new DaxColumn()
                {
                    //OriginalCaption = col.Caption,
                    OriginalName = columnName,
                    //NewCaption = (removeCaption) ? "" : name,
                    //NewName = name.Replace(' ', '`').Replace(',', '`'),
                    NewName = name,
                };
                newColumnNames.Add(dc.OriginalName, dc);
                //col.Caption = (removeCaption) ? "" : name;
                //col.ColumnName = name.Replace(' ', '_');
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
            // Update names
            for (int i = 0; i < columns.Length; i++)
            {
                var c = newColumnNames.ElementAt(i).Value;
                newNames[i] = c.UseOriginalName ? c.OriginalName : c.NewName;
            }
            return newNames;
        }




        public static DataSet ConvertToDataSet(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, bool autoFormat, bool IsSessionsDmv, string autoDateFormat)
        {
            ADOTabular.ADOTabularColumn daxCol;
            DataSet ds = new DataSet();
            bool moreResults = true;
            int tableIdx = 1;
            int localeId = reader.Connection.LocaleIdentifier;
            while (moreResults)
            {
                DataTable dtSchema = reader.GetSchemaTable();
                DataTable dt = new DataTable(tableIdx.ToString());
                // You can also use an ArrayList instead of List<>
                List<DataColumn> listCols = new List<DataColumn>();
                if (dtSchema != null)
                {
                    foreach (DataRow row in dtSchema.Rows)
                    {
                        string columnName = Convert.ToString(row["ColumnName"]);
                        Type columnType = (Type)row["DataType"];
                        if (columnType.Name == "XmlaDataReader") columnType = typeof(string);
                        DataColumn column = new DataColumn(columnName, columnType); // (Type)(row["DataType"]));
                        column.Unique = (bool)row[Constants.IsUnique];
                        column.AllowDBNull = (bool)row[Constants.AllowDbNull];
                        daxCol = null;
                        reader.Connection.Columns.TryGetValue(columnName, out daxCol);
                        if (IsSessionsDmv && columnName == Common.Constants.SessionSpidColumn)
                        {
                            column.ExtendedProperties.Add(Constants.SessionSpidColumn, true);
                        }
                        if (daxCol != null && !string.IsNullOrEmpty(daxCol.FormatString)) {
                            column.ExtendedProperties.Add(Constants.FormatString, daxCol.FormatString);
                            if (localeId != 0) column.ExtendedProperties.Add(Constants.LocaleId, localeId);
                        }
                        else if (autoFormat) {
                            string formatString;
                            switch (column.DataType.Name)
                            {
                                case "Decimal":
                                case "Double":
                                case "Object":
                                    if (column.Caption.Contains(@"%") || column.Caption.Contains("Pct")) {
                                        formatString = "0.00%";
                                    }
                                    else {
                                        formatString = "#,0.00";
                                    }
                                    break;
                                case "Int64":
                                    formatString = "#,0";
                                    break;
                                case "DateTime":
                                    if (string.IsNullOrWhiteSpace(autoDateFormat)
                                        || column.Caption.ToLower().Contains(@"time") 
                                        || column.Caption.ToLower().Contains(@"hour") ) {
                                        formatString = null;
                                    }
                                    else
                                    {
                                        formatString = "yyyy-MM-dd";
                                    }
                                    break;
                                default:
                                    formatString = null;
                                    break;
                            }
                            if (formatString != null) {
                                column.ExtendedProperties.Add(Constants.FormatString, formatString);
                                if (localeId != 0) column.ExtendedProperties.Add(Constants.LocaleId, localeId);
                            }
                        }
                        listCols.Add(column);
                        dt.Columns.Add(column);
                    }
                }

                // Read rows from DataReader and populate the DataTable
                while (reader.Read())
                {
                    DataRow dataRow = dt.NewRow();
                    for (int i = 0; i < listCols.Count; i++)
                    {
                        if (reader.IsDataReader(i))
                            dataRow[((DataColumn)listCols[i])] = reader.GetDataReaderValue(i); 
                        else
                            dataRow[((DataColumn)listCols[i])] = reader[i] ?? DBNull.Value;
                    }
                    dt.Rows.Add(dataRow);
                }
                dt.FixColumnNaming(reader.CommandText);
                ds.Tables.Add(dt);
                moreResults = reader.NextResult();
                tableIdx++;
            }
            return ds;

        }

        /// <summary>
        /// Writes a DataTable object to a StreamWriter
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="textWriter"></param>
        /// <param name="sep"></param>
        /// <param name="shouldQuoteStrings"></param>
        /// <param name="isoDateFormat"></param>
        /// <param name="statusProgress"></param>
        /// <returns></returns>
        public static int WriteToStream(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, TextWriter textWriter, string sep, bool shouldQuoteStrings, string isoDateFormat, IStatusBarMessage statusProgress)
        {

            int iMaxCol = reader.FieldCount - 1;
            int iRowCnt = 0;
            
            // CSV Writer config
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) {Delimiter = sep};


            using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
            {


                // Datetime as ISOFormat
                csvWriter.Context.TypeConverterOptionsCache.AddOptions(
                    typeof(DateTime),
                    new CsvHelper.TypeConversion.TypeConverterOptions() { Formats = new string[] { isoDateFormat } });

                // write out clean column names

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

                        // quote all string fields
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
    }
}
