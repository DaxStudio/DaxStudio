using DaxStudio.Common;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using Newtonsoft.Json;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using DaxStudio.Interfaces;

namespace DaxStudio.UI.ResultsTargets
{
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetTextFile : IResultsTarget
    {
        #region Standard Properties
        public string Name => "File";
        public string Group => "Standard";
        public bool IsDefault => false;
        public bool IsAvailable => true;
        public int DisplayOrder => 200;
        public string Message => "Results will be sent to a Text File";
        public OutputTarget Icon => OutputTarget.File;
        public string ImageResource => "results_fileDrawingImage";
        public string Tooltip => "Exports Query results to csv or tab delimited files";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        //private string Separator = "\t";
        private Encoding FileEncoding = new UTF8Encoding(false);
        //private bool ShouldQuoteStrings = true;

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string fileName = null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".csv",
                Filter = "Comma separated text file - UTF8|*.csv|Comma separated text file - Unicode|*.csv|Json file|*.json|Tab separated text file|*.txt|Custom Export Format (Configure in Options)|*.csv",
                FilterIndex = runner.Options.DefaultTextFileType,
            };

            // Show save file dialog box
            bool? result = false;
            if (string.IsNullOrEmpty(fileName))
            {
                result = dlg.ShowDialog();
                // Remember the file type that was chosen
                runner.Options.DefaultTextFileType = dlg.FilterIndex;

                // Save document 
                fileName = dlg.FileName;
            } else
            {
                result = true;
                
            }
            if (result == true)
            {
                
                await Task.Run(() =>
                {

                    var sw = Stopwatch.StartNew();
                    long durationMs = 0;
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
                                WriteToTextFile(runner, fileName, sep, shouldQuoteStrings, isoDateFormat, enc, reader, statusProgress);
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

        private void WriteToTextFile(IQueryRunner runner, string fileName, string sep, bool shouldQuoteStrings, string isoDateFormat, Encoding enc, ADOTabular.AdomdClientWrappers.AdomdDataReader reader, IStatusBarMessage statusProgress)
        {
            int iFileCnt = 1;
            bool moreResults = true;

            while (moreResults)
            {
                var outputFilename = fileName;
                int iRowCnt = 0;
                if (iFileCnt > 1) outputFilename = AddFileCntSuffix(fileName, iFileCnt);
                using (var textWriter = new System.IO.StreamWriter(outputFilename, false, enc))
                {
                    iRowCnt = reader.WriteToStream(textWriter, sep, shouldQuoteStrings, isoDateFormat, statusProgress);
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

                    var schemaTable = reader.GetSchemaTable();

                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName("rows");
                    jsonWriter.WriteStartArray();
                    while (reader.Read())
                    {
                        jsonWriter.WriteStartObject();

                        for (int iCol = 0; iCol < reader.FieldCount; iCol++)
                        {
                            jsonWriter.WritePropertyName(reader.GetName(iCol));
                            jsonWriter.WriteValue(reader[iCol]);
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
    }
}
