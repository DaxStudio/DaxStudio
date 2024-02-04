using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Utils;
using LargeXlsx;
using Serilog;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static LargeXlsx.XlsxAlignment;

namespace DaxStudio.UI.ResultsTargets
{
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelFile : IResultsTarget
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private static Regex columnNameRegex = new Regex("(?:'?)(?<table>[^'\\[]+)(?:'?)(?<column>.+)(?:$)", RegexOptions.Compiled | RegexOptions.Singleline);
        [ImportingConstructor]
        public ResultsTargetExcelFile(IDaxStudioHost host, IEventAggregator eventAggregator)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnPublishedThread(this);
        }

        #region Standard Properties
        public string Name => "Static";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => !_host.IsExcel;
        public int DisplayOrder => 410;
        public string Message => "Results will be sent to an XLSX File";
        public OutputTarget Icon => OutputTarget.File;
        public string ImageResource => "results_excelDrawingImage";
        public string Tooltip => "Query results will be written to an Excel file.";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".xlsx",
                Filter = "Excel file (*.xlsx)|*.xlsx"
            };

            // Show save file dialog box
            var result = dlg.ShowDialog();

            // Process save file dialog box results 
            if (result == true)
            {
                await OutputResultsAsync(runner, textProvider, dlg.FileName);
            }
            else
            {
                await Task.CompletedTask;
            }
        }

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string fileName)
        {

            long durationMs = 0;

            // Save document 
            await Task.Run(() =>
            {

                runner.OutputMessage("Query Started");

                var sw = Stopwatch.StartNew();

                var daxQuery = textProvider.QueryText;

                using (var reader = runner.ExecuteDataReaderQuery(daxQuery, textProvider.ParameterCollection))
                using (var statusProgress = runner.NewStatusBarMessage("Starting Export"))
                {

                    if (reader != null)
                    {
                        int iFileCnt = 1;


                        runner.OutputMessage("Command Complete, writing output file");

                        bool moreResults = true;

                        using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                        using (var xlsxWriter = new XlsxWriter(stream))
                        {
                            while (moreResults)
                            {
                                // create a worksheet for the current resultset
                                xlsxWriter.BeginWorksheet($"Query{iFileCnt}", 1);

                                // write out the current resultset
                                var iRowCnt = WriteToWorksheet(reader, xlsxWriter, statusProgress, runner);

                                // setup Excel Autofilters
                                xlsxWriter.SetAutoFilter(1, 1, xlsxWriter.CurrentRowNumber, reader.FieldCount);

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

                        sw.Stop();
                        durationMs = sw.ElapsedMilliseconds;

                        runner.SetResultsMessage("Query results written to file", OutputTarget.File);
                        runner.ActivateOutput();
                    }

                }

            });
        }




        /// <summary>
        /// Writes a ADODataReader to a Worksheet
        /// </summary>
        public static int WriteToWorksheet(ADOTabular.AdomdClientWrappers.AdomdDataReader reader, XlsxWriter xlsxWriter,  IStatusBarMessage statusProgress, IQueryRunner runner)
        {

            int iMaxCol = reader.FieldCount - 1;
            int iRowCnt = 0;
            ADOTabularColumn daxCol;
            int colIdx = 0;
            XlsxStyle[] columnStyles = new XlsxStyle[reader.FieldCount];
            var headerStyle = new XlsxStyle(
                            new XlsxFont("Segoe UI", 9, Color.White, bold: true),
                            new XlsxFill(Color.FromArgb(0, 0x45, 0x86)),
                            XlsxStyle.Default.Border,
                            XlsxStyle.Default.NumberFormat,
                            XlsxAlignment.Default);
            var wrapStyle = XlsxStyle.Default.With(new XlsxAlignment(vertical: Vertical.Top, wrapText: true));
            var defaultStyle = XlsxStyle.Default;
            // Write out Header Row
            xlsxWriter.SetDefaultStyle(headerStyle).BeginRow();
            foreach (var colName in reader.CleanColumnNames())
            {
                // write out the column name
                xlsxWriter.Write(colName);
                var daxColumnName = reader.GetName(colIdx);
                // cache the column formatstrings as Excel Styles
                reader.Connection.Columns.TryGetValue(daxColumnName, out daxCol);
                // in case the table name is quote, try stripping the quotes and checking for  the column
                if (daxCol == null && daxColumnName[0] != '\'')
                {
                    daxColumnName = columnNameRegex.Replace(daxColumnName, "'${table}'${column}");
                    reader.Connection.Columns.TryGetValue(daxColumnName, out daxCol);
                }


                if (daxCol != null)
                    columnStyles[colIdx] = GetStyle(daxCol);
                else
                    columnStyles[colIdx] = defaultStyle;
                colIdx++;
            }
            xlsxWriter.SetDefaultStyle(defaultStyle);
            

            while (reader.Read())
            {
                
                // check if we have reached the limit of an xlsx file
                if (iRowCnt >= 999999)
                {
                    runner.OutputWarning("Results truncated, reached the maximum row limit for an Excel file");
                    break;
                }

                // increment row count
                iRowCnt++;
                
                // start outputting the next row
                xlsxWriter.BeginRow();
                for (int iCol = 0; iCol < reader.FieldCount; iCol++)
                {
                    var fieldValue = reader[iCol];
                    switch (fieldValue)
                    {
                        case int i:
                            xlsxWriter.Write(i,columnStyles[iCol]);
                            break;
                        case double dbl:
                            xlsxWriter.Write(dbl, columnStyles[iCol]);
                            break;
                        case decimal dec:
                            xlsxWriter.Write(dec, columnStyles[iCol]);
                            break;
                        case DateTime dt:
                            // if this is day 0 for tabular 30 dec 1899 move it to the day 0 for Excel 1 Jan 1900
                            if ((dt.Year == 1899) && (dt.Month == 12) && (dt.Day == 30)) dt = dt.AddDays(2);
                            xlsxWriter.Write(dt, columnStyles[iCol]);
                            break;
                        case string str:
                            if (str.Contains("\n") || str.Contains("\r"))
                                xlsxWriter.Write(str, wrapStyle);
                            else
                                xlsxWriter.Write(str);
                            break;
                        case bool b:
                            xlsxWriter.Write(b.ToString());   // Writes out TRUE/FALSE
                            break;
                        case null:
                            xlsxWriter.Write();
                            break;
                        case long lng:
                            
                            if (lng < int.MaxValue && lng > int.MinValue)
                                xlsxWriter.Write(Convert.ToInt32(lng), columnStyles[iCol]);
                            else                                   // TODO - should we be converting large long values to double??
                                xlsxWriter.Write(lng.ToString());  // write numbers outside the size of int as strings

                            break;
                        default:
                            xlsxWriter.Write( fieldValue.ToString());
                            break;
                    }
                    
                }
   
                if (iRowCnt % 1000 == 0)
                {
                    statusProgress.Update($"Written {iRowCnt:n0} rows to the file output");
                }

            }

            return iRowCnt;
        }

        private static XlsxStyle GetStyle(ADOTabularColumn col)
        {
            return XlsxHelper.GetStyle(col.SystemType.Name, col.FormatString);
        }

    }
}
