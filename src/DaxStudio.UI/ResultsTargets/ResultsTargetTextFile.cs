using DaxStudio.Interfaces;
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
        public string Name
        {
            get { return "File"; }
        }
        public string Group
        {
            get { return "Standard"; }
        }

        public string MyProperty { get; set; }

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
        
                return Task.Factory.StartNew(() =>
                {

                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();
                        string sep = "\t";
                        string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                        string isoDateFormat = string.Format("yyyy-MM-dd hh:mm:ss{0}000", decimalSep);
                        Encoding enc = Encoding.UTF8;
                        switch (dlg.FilterIndex)
                        { 
                            case 1:
                                enc = Encoding.UTF8;
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                            case 2:
                                enc = Encoding.Unicode;
                                sep = System.Globalization.CultureInfo.CurrentUICulture.TextInfo.ListSeparator;
                                break;
                        } 

                        var dq = runner.QueryText;
                        var res = runner.ExecuteQuery(dq);
                        if (res != null)
                        {
                            sw.Stop();
                            var durationMs = sw.ElapsedMilliseconds;
                            runner.ResultsTable = res;
                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                                res.Rows.Count == 1 ? "" : "s"), durationMs);

                            var sbLine = new StringBuilder();
                            using (var writer = new StreamWriter(File.Open(fileName, FileMode.Create), enc))
                            {
                                // write out column headers
                                IEnumerable<string> columnNames = res.Columns.Cast<DataColumn>().
                                                                    Select(column => column.ColumnName);
                                writer.WriteLine(string.Join(sep, columnNames));
                                
                                string[] columnTypes = res.Columns.Cast<DataColumn>().
                                                                    Select(column => column.DataType.ToString()).ToArray();
                                int iCol = 0;
                                int iColCnt = res.Columns.Count;
                                // write out data
                                foreach (DataRow row in res.Rows)
                                {
                                    iCol = 0;
                                    foreach (var col in row.ItemArray)
                                    {
                                        switch(columnTypes[iCol] )
                                        {
                                            case "System.Decimal":
                                            case "System.Int64":
                                                sbLine.Append(col.ToString());
                                                break;
                                            case "System.DateTime":
                                                //sbLine.Append("\"");
                                                //sbLine.Append(((DateTime)col).ToString( "s", System.Globalization.CultureInfo.InvariantCulture )); // ISO date format
                                                sbLine.Append(((DateTime)col).ToString( isoDateFormat)); // ISO date format
                                                //sbLine.Append("\"");
                                                break;
                                            default:
                                                sbLine.Append("\"");
                                                sbLine.Append(col.ToString().Replace("\"", "\"\"").Replace("\n"," "));
                                                sbLine.Append("\"");
                                                break;
                                        }

                                        iCol++;
                                        if (iCol < iColCnt)
                                        { sbLine.Append(sep); }
                                    }
                                    writer.WriteLine(sbLine);
                                    sbLine.Clear();
                                    //IEnumerable<string> fields = row.ItemArray.Select(field => string.Concat("\"", field.ToString().Replace("\"", "\"\"").Replace("\n"," "), "\""));
                                    //writer.WriteLine(string.Join(sep, fields));
                                }
                            }

                            runner.SetResultsMessage("Query results written to file", OutputTargets.Grid);
                            //runner.QueryCompleted();
                            runner.ActivateOutput();
                        }

                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
                    }

                });
                
            }
            return Task.Factory.StartNew(() => { });
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
    }
}
