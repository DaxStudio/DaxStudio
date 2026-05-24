using DaxStudio.Common;
using DaxStudio.Core.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
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
        public static DataSet ConvertToDataSet(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, bool autoFormat, bool IsSessionsDmv, string autoDateFormat, ConnectionManager connection)
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
                        string columnDaxName = DaxHelper.GetQuotedColumnName(columnName);
                        Type columnType = (Type)row["DataType"];
                        if (columnType.Name == "XmlaDataReader") columnType = typeof(string);
                        DataColumn column = new DataColumn(columnName, columnType); // (Type)(row["DataType"]));
                        column.Unique = (bool)row[Constants.IsUnique];
                        column.AllowDBNull = (bool)row[Constants.AllowDbNull];
                        daxCol = null;
                        connection.Columns.TryGetValue(columnName, out daxCol);
                        if (daxCol == null) connection.Columns.TryGetValue(columnDaxName, out daxCol);
                        if (IsSessionsDmv && columnName == Constants.SessionSpidColumn)
                        {
                            column.ExtendedProperties.Add(Constants.SessionSpidColumn, true);
                        }
                        if (daxCol != null && !string.IsNullOrEmpty(daxCol.FormatString))
                        {
                            column.ExtendedProperties.Add(Constants.FormatString, daxCol.FormatString);
                            if (localeId != 0) column.ExtendedProperties.Add(Constants.LocaleId, localeId);
                        }
                        else if (autoFormat)
                        {
                            string formatString;
                            switch (column.DataType.Name)
                            {
                                case "Decimal":
                                case "Double":
                                case "Object":
                                    if (column.Caption.Contains(@"%") || column.Caption.Contains("Pct"))
                                    {
                                        formatString = "0.00%";
                                    }
                                    else
                                    {
                                        formatString = "#,0.00";
                                    }
                                    break;
                                case "Int64":
                                    formatString = "#,0";
                                    break;
                                case "DateTime":
                                    if (string.IsNullOrWhiteSpace(autoDateFormat)
                                        || column.Caption.ToLower().Contains(@"time")
                                        || column.Caption.ToLower().Contains(@"hour"))
                                    {
                                        formatString = null;
                                    }
                                    else
                                    {
                                        formatString = autoDateFormat;
                                    }
                                    break;
                                default:
                                    formatString = null;
                                    break;
                            }
                            if (formatString != null)
                            {
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
                        {
                            dataRow[i] = reader[i] ?? DBNull.Value;
                        }

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
        public static int WriteToStreamWithFormatting(this ADOTabular.AdomdClientWrappers.AdomdDataReader reader, TextWriter textWriter, string sep, bool shouldQuoteStrings, Dictionary<int,string> formatStrings, IStatusBarMessage statusProgress)
        {

            int iMaxCol = reader.FieldCount - 1;
            int iRowCnt = 0;

            // CSV Writer config
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture) { Delimiter = sep };


            using (var csvWriter = new CsvHelper.CsvWriter(textWriter, config))
            {

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
