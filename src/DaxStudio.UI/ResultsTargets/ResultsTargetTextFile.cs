using DaxStudio.Common;
using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using Serilog;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
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
        public string Tooltip => "Exports Query results to csv or tab delimited files";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".csv",
                Filter = "Comma separated text file - UTF8|*.csv|Tab separated text file|*.txt|Comma separated text file - Unicode|*.csv|Custom Export Format (Configure in Options)|*.csv"
            };

            string fileName = "";
            long durationMs = 0;
            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Save document 
                fileName = dlg.FileName;
                await Task.Run(() =>
                {

                    try
                    {
                        runner.OutputMessage("Query Started");

                        var sw = Stopwatch.StartNew();

                        string sep = "\t";
                        bool shouldQuoteStrings = true; //default to quoting all string fields
                        string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                        string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
                        Encoding enc = new UTF8Encoding(false);

                        switch (dlg.FilterIndex)
                        {
                            
                            case 1: // utf-8 csv
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                            case 2: // tab separated
                                sep = "\t";
                                break;
                            case 3: // unicode csv
                                enc = new UnicodeEncoding();
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                            case 4:// custom export format
                                sep = runner.Options.GetCustomCsvDelimiter();
                                shouldQuoteStrings = runner.Options.CustomCsvQuoteStringFields;
                                break;
                        }

                        var daxQuery = textProvider.QueryText;
                        var reader = runner.ExecuteDataReaderQuery(daxQuery,textProvider.ParameterCollection);

                        using (var statusProgress = runner.NewStatusBarMessage("Starting Export"))
                        {

                            try
                            {
                                if (reader != null)
                                {
                                    int iFileCnt = 1;
                                    

                                    runner.OutputMessage("Command Complete, writing output file");

                                    bool moreResults = true;

                                    while (moreResults)
                                    {
                                        var outputFilename = fileName;
                                        int iRowCnt = 0;
                                        if (iFileCnt > 1) outputFilename = AddFileCntSuffix(fileName, iFileCnt);
                                        using (var textWriter = new System.IO.StreamWriter(outputFilename, false, enc))
                                        {
                                            iRowCnt = reader.WriteToStream( textWriter, sep, shouldQuoteStrings, isoDateFormat,  statusProgress);
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

                                    sw.Stop();
                                    durationMs = sw.ElapsedMilliseconds;

                                    runner.SetResultsMessage("Query results written to file", OutputTarget.File);
                                    runner.ActivateOutput();
                                }
                                else
                                    runner.OutputError("Query Batch Completed with errors listed above (you may need to scroll up)", durationMs);
                            }
                            finally
                            {
                                if (reader != null)
                                {
                                    reader.Dispose();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ResultsTargetTextFile), nameof(OutputResultsAsync), ex.Message);
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
#if DEBUG
                        runner.OutputError(ex.StackTrace);
#endif
                    }
                    finally
                    {
                        runner.OutputMessage("Query Batch Completed", durationMs);
                        runner.QueryCompleted();
                    }

                });

            }
            // else dialog was cancelled so return an empty task.
            await Task.Run(() => { });
        }

       

        private string AddFileCntSuffix(string fileName, int iFileCnt)
        {
            FileInfo fi = new FileInfo(fileName);
            var newName = string.Format("{0}\\{1}_{2}{3}", fi.DirectoryName.TrimEnd('\\'), fi.Name.Substring(0, fi.Name.Length - fi.Extension.Length), iFileCnt, fi.Extension);
            return newName;
        }
    }
}
