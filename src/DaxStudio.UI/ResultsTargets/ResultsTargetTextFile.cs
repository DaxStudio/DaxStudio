using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace DaxStudio.UI.Model
{
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetTextFile : IResultsTarget
    {
        #region Standard Properties
        public string Name
        {
            get { return "File"; }
        }
        public string Group
        {
            get { return "Standard"; }
        }
        public bool IsDefault
        {
            get { return false; }
        }

        public bool IsEnabled
        {
            get { return true; }
        }

        public int DisplayOrder
        {
            get { return 300; }
        }


        public string Message
        {
            get { return "Results will be sent to a Text File"; }
        }

        public OutputTargets Icon
        {
            get { return OutputTargets.File; }
        }
        #endregion

        public Task OutputResultsAsync(IQueryRunner runner)
        {

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Tab separated text file|*.txt|Comma separated text file - UTF8|*.csv|Comma separated text file - Unicode|*.csv"
            };

            string fileName = "";

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                // Save document 
                fileName = dlg.FileName;
                return Task.Run(() =>
                {

                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();
                        string sep = "\t";
                        string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                        string isoDateFormat = string.Format("yyyy-MM-dd HH:mm:ss{0}000", decimalSep);
                        Encoding enc = Encoding.UTF8;
                        switch (dlg.FilterIndex)
                        {
                            case 1: // tab separated
                                sep = "\t";
                                break;
                            case 2: // utf-8 csv
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                            case 3: //unicode csv
                                enc = Encoding.Unicode;
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                        }

                        var dq = runner.QueryText;
                        //var res = runner.ExecuteDataTableQuery(dq);
                        AdomdDataReader res = runner.ExecuteDataReaderQuery(dq);

                        if (res != null)
                        {
                            sw.Stop();
                            var durationMs = sw.ElapsedMilliseconds;
                            //runner.ResultsTable = res;
                            runner.OutputMessage("Command Complete, writing output file");

                            var sbLine = new StringBuilder();
                            bool moreResults = true;
                            int iFileCnt = 1;
                            while (moreResults)
                            {
                                int iMaxCol = res.FieldCount - 1;
                                int iRowCnt = 0;
                                if (iFileCnt > 1) fileName = AddFileCntSuffix(fileName, iFileCnt);
                                using (var writer = new StreamWriter(File.Open(fileName, FileMode.Create), enc))
                                {
                                    // write out clean column names
                                    writer.WriteLine(string.Join(sep, res.CleanColumnNames()));
                                    
                                    // write out data
                                    while (res.Read())
                                    {
                                        iRowCnt++;
                                        for (int iCol = 0; iCol < res.FieldCount; iCol++)
                                        {
                                            switch (res.GetDataTypeName(iCol) )
                                            {
                                                case "Decimal":
                                                case "Int64":
                                                    if (!res.IsDBNull(iCol)) sbLine.Append(res.GetString(iCol));
                                                    break;
                                                case "DateTime":
                                                    if (res.IsDBNull(iCol)) { sbLine.Append("\"\""); }
                                                    else { sbLine.Append(res.GetDateTime(iCol).ToString(isoDateFormat)); } // ISO date format
                                                    break;
                                                default:
                                                    sbLine.Append("\"");
                                                    if (!res.IsDBNull(iCol)) sbLine.Append(res.GetString(iCol).Replace("\"", "\"\"").Replace("\n", " "));
                                                    sbLine.Append("\"");
                                                    break;
                                            }

                                            if (iCol < iMaxCol)
                                            { sbLine.Append(sep); }
                                        }
                                        writer.WriteLine(sbLine);
                                        sbLine.Clear();
                                        if (iRowCnt % 1000 == 0)
                                        {
                                            runner.NewStatusBarMessage(string.Format("Written {0:n0} rows to the file output", iRowCnt));
                                        }
                                    }

                                    
                                }
                                runner.OutputMessage(
                                        string.Format("Query Completed ({0:N0} row{1} returned)"
                                                    , iRowCnt
                                                    , iRowCnt == 1 ? "" : "s"), durationMs);
                                runner.RowCount = iRowCnt;
                                moreResults = res.NextResult();
                                iFileCnt++;
                            }
                            runner.SetResultsMessage("Query results written to file", OutputTargets.Grid);
                            //runner.QueryCompleted();
                            runner.ActivateOutput();
                        }
                        res.Close();
                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
#if DEBUG
                        runner.OutputError(ex.StackTrace);
#endif
                    }
                    finally
                    {
                        runner.QueryCompleted();
                        
                    }

                });

            }
            // else dialog was cancelled so return an empty task.
            return Task.Run(() => { });
        }

        private string AddFileCntSuffix(string fileName, int iFileCnt)
        {
            FileInfo fi = new FileInfo(fileName);
            var newName = string.Format("{0}\\{1}_{2}{3}", fi.DirectoryName.TrimEnd('\\'), fi.Name.Substring(0,fi.Name.Length - fi.Extension.Length), iFileCnt, fi.Extension);
            return newName;
        }
    }
}
