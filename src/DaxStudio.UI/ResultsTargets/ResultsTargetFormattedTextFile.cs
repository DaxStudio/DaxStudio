using ADOTabular.AdomdClientWrappers;
using DaxStudio.Common;
using DaxStudio.Interfaces;
using DaxStudio.UI.Converters.CircularProgressBar;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetFormattedTextFile : IResultsTarget
    {
        #region Standard Properties
        public string Name => "Formatted File";
        public string Group => "Standard";
        public bool IsDefault => false;
        public bool IsAvailable => true;
        public int DisplayOrder => 200;
        public string Message => "Results will be sent to a Text File";
        public OutputTarget Icon => OutputTarget.File;
        public string ImageResource => "results_file_formattedDrawingImage";
        public string Tooltip => "Exports Query results to csv or tab delimited files applying the same output formatting you see in the grid results";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".csv",
                Filter = "Comma separated text file - UTF8|*.csv|Comma separated text file - Unicode|*.csv|Json file|*.json|Tab separated text file|*.txt|Custom Export Format (Configure in Options)|*.csv",
                FilterIndex = runner.Options.DefaultTextFileType,
            };

            string fileName = "";
            long durationMs = 0;
            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Remember the file type that was chosen
                runner.Options.DefaultTextFileType = dlg.FilterIndex;

                // Save document 
                fileName = dlg.FileName;
                await Task.Run(() =>
                {

                    var sw = Stopwatch.StartNew();

                    string sep = "\t";
                    bool shouldQuoteStrings = true; //default to quoting all string fields
                    bool toJson = false;
                    string decimalSep = CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                    string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
                    Encoding enc = new UTF8Encoding(false);

                    switch (dlg.FilterIndex)
                    {

                        case 1: // utf-8 csv
                            sep =CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                            break;
                        
                        case 2: // unicode csv
                            enc = new UnicodeEncoding();
                            sep = CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                            break;
                        case 3:
                            toJson = true;
                            break;
                        case 4: // tab separated
                            sep = "\t";
                            break;
                        case 5:// custom export format
                            sep = runner.Options.GetCustomCsvDelimiter();
                            enc = runner.Options.GetCustomCsvEncoding();
                            shouldQuoteStrings = runner.Options.CustomCsvQuoteStringFields;
                            break;
                    }

                    var daxQuery = textProvider.QueryText;
                    
                    using (var reader = runner.ExecuteDataReaderQuery(daxQuery, textProvider.ParameterCollection))
                    using (var statusProgress = runner.NewStatusBarMessage("Starting Export"))
                    {

                        if (reader != null)
                        {
                            
                            runner.OutputMessage("Command Complete, writing output file");

                            if (toJson)
                            {
                                WriteToJsonFile(runner,fileName, reader, statusProgress);
                            }
                            else
                            {
                                WriteToTextFile(runner, fileName, sep, shouldQuoteStrings, enc, reader, statusProgress);
                            }
                            sw.Stop();
                            durationMs = sw.ElapsedMilliseconds;

                            runner.SetResultsMessage("Query results written to file", OutputTarget.File);
                            runner.ActivateOutput();
                        }
                    }
                });

            }
            else
            {
                // else dialog was cancelled so return an empty task.
                await Task.CompletedTask;
            }
        }

        private void WriteToTextFile(IQueryRunner runner, string fileName, string sep, bool shouldQuoteStrings, Encoding enc, ADOTabular.AdomdClientWrappers.AdomdDataReader reader, IStatusBarMessage statusProgress)
        {
            int iFileCnt = 1;
            bool moreResults = true;

            // Read the AutoFormat option from the options singleton
            bool autoFormat = runner.Options.ResultAutoFormat;
            string autoDateFormat = runner.Options.DefaultDateAutoFormat;

            while (moreResults)
            {
                var outputFilename = fileName;
                int iRowCnt = 0;
                if (iFileCnt > 1) outputFilename = AddFileCntSuffix(fileName, iFileCnt);
                var formatStrings = GetColumnFormatStrings(reader, runner, autoFormat, autoDateFormat);
                using (var textWriter = new System.IO.StreamWriter(outputFilename, false, enc))
                {
                    iRowCnt = reader.WriteToStreamWithFormatting(textWriter, sep, shouldQuoteStrings, formatStrings, statusProgress);
                }
                runner.OutputMessage(
                        string.Format("Query {2} Completed ({0:N0} row{1} returned)"
                                    , iRowCnt
                                    , iRowCnt == 1 ? "" : "s", iFileCnt)
                        );

                runner.RowCount = iRowCnt;

                moreResults = reader.NextResult();

                iFileCnt++;
            }

        }

        private void WriteToJsonFile(IQueryRunner runner, string fileName, ADOTabular.AdomdClientWrappers.AdomdDataReader reader, IStatusBarMessage statusProgress)
        {
            int iQueryCnt = 1;

            runner.OutputMessage("Command Complete, writing output file");

            bool moreResults = true;

            using (var textWriter = new StreamWriter(fileName))
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {

                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("results");
                jsonWriter.WriteStartArray();
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("tables");
                jsonWriter.WriteStartArray();


                while (moreResults)
                {
                    int iRowCnt = 0;
                    // Read the AutoFormat option from the options singleton
                    bool autoFormat = runner.Options.ResultAutoFormat;
                    string autoDateFormat = runner.Options.DefaultDateAutoFormat;

                    //var schemaTable = reader.GetSchemaTable();
                    var formatStrings = GetColumnFormatStrings(reader, runner, autoFormat, autoDateFormat);

                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("rows");
                    jsonWriter.WriteStartArray();
                    while (reader.Read())
                    {
                        jsonWriter.WriteStartObject();

                        for (int iCol = 0; iCol < reader.FieldCount; iCol++)
                        {
                            jsonWriter.WritePropertyName(reader.GetName(iCol));
                            if (!string.IsNullOrEmpty(formatStrings[iCol]))
                            {
                                switch (reader[iCol])
                                {
                                    case DateTime dateTimeValue:
                                        jsonWriter.WriteValue(dateTimeValue.ToString(formatStrings[iCol]));
                                        break;
                                    case long longValue:
                                        jsonWriter.WriteValue(longValue.ToString(formatStrings[iCol]));
                                        break;
                                    case decimal decimalValue:
                                        jsonWriter.WriteValue(decimalValue.ToString(formatStrings[iCol]));
                                        break;
                                    default:
                                        jsonWriter.WriteValue(reader[iCol]);
                                        break;
                                }
                            }
                            else
                            {
                                jsonWriter.WriteValue(reader[iCol]);
                            }
                        }

                        jsonWriter.WriteEndObject();
                        iRowCnt++;

                        if (iRowCnt % 1000 == 0)
                        {
                            statusProgress.Update($"Written {iRowCnt:n0} rows to the file output");
                        }
                    }

                    jsonWriter.WriteEndArray();
                    jsonWriter.WriteEndObject();

                    runner.OutputMessage(
                            string.Format("Query {2} Completed ({0:N0} row{1} returned)"
                                        , iRowCnt
                                        , iRowCnt == 1 ? "" : "s", iQueryCnt)
                            );

                    runner.RowCount = iRowCnt;

                    moreResults = reader.NextResult();

                    iQueryCnt++;
                }

                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();
                jsonWriter.WriteEndArray();
                jsonWriter.WriteEndObject();

            }
        }


        private string AddFileCntSuffix(string fileName, int iFileCnt)
        {
            FileInfo fi = new FileInfo(fileName);
            var newName = string.Format("{0}\\{1}_{2}{3}", fi.DirectoryName.TrimEnd('\\'), fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length), iFileCnt, fi.Extension);
            return newName;
        }

        private Dictionary<int,string> GetColumnFormatStrings(AdomdDataReader reader, IQueryRunner runner, bool autoFormat, string autoDateFormat)
        {
            ADOTabular.ADOTabularColumn daxCol;
            DataTable dtSchema = reader.GetSchemaTable();
            Dictionary<int, string> results = new Dictionary<int, string>(dtSchema.Rows.Count);
            int idx = 0;
            var tmpConn = reader.Connection;
            var localeId = tmpConn.LocaleIdentifier;
            if (dtSchema != null)
            {
                foreach (DataRow row in dtSchema.Rows)
                {
                    string columnName = Convert.ToString(row["ColumnName"]);
                    string columnDaxName = DaxHelper.GetQuotedColumnName(columnName);
                    string formatString = string.Empty;
                    Type columnType = (Type)row["DataType"];
                    if (columnType.Name == "XmlaDataReader") columnType = typeof(string);
                    DataColumn column = new DataColumn(columnName, columnType); // (Type)(row["DataType"]));
                    column.Unique = (bool)row[Constants.IsUnique];
                    column.AllowDBNull = (bool)row[Constants.AllowDbNull];
                    daxCol = null;
                    
                    runner.Connection.Columns.TryGetValue(columnName, out daxCol);
                    if (daxCol == null) tmpConn.Columns.TryGetValue(columnDaxName, out daxCol);
                    if (daxCol != null && !string.IsNullOrEmpty(daxCol.FormatString))
                    {
                        formatString = daxCol.FormatString;
                        if (localeId != 0) column.ExtendedProperties.Add(Constants.LocaleId, localeId);
                    }
                    else if (autoFormat)
                    {
                        
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
                         }
                    }
                    
                    results.Add(idx, formatString);
                    idx++;
                }
            }
            return results;
        }
    }
}
