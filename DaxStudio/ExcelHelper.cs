using System;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio;
using Microsoft.Office.Interop.Excel;
//using Microsoft.Windows.Controls.Ribbon;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows.Forms;
using System.Data;
using Office = Microsoft.Office.Core;
using System.ComponentModel;

namespace DaxStudio
{
    public class ExcelHelper
    {
        // ReSharper disable InconsistentNaming
        const string NEW_SHEET = "<New Sheet>";
        const string DAX_RESULTS_SHEET = "<Query Results Sheet>";
        // ReSharper restore InconsistentNaming
        private QueryTable _qryTable;
        private readonly Excel.Application _app ;
        private readonly ToolStripComboBox _tcbOutputTo;
        private readonly Fluent.ComboBox _fcbOutputTo;
        //private readonly RibbonComboBox _cboOutputTo;
        public delegate void QueryTableRefreshedHandler(object sender, QueryTableRefreshEventArgs e);
        public event QueryTableRefreshedHandler QueryTableRefreshed;

        public ExcelHelper(Excel.Application app, Fluent.ComboBox fcbOutputTo)
        {
            _app = app;
            _fcbOutputTo = fcbOutputTo;
            _app.WorkbookActivate += AppWorkbookActivate;
            PopulateOutputOptions(_fcbOutputTo);
        }

        public ExcelHelper(Excel.Application app)//, ToolStripComboBox tcbOutputTo)
        {
            _app = app;
            //_tcbOutputTo = tcbOutputTo;
            _app.WorkbookActivate += AppWorkbookActivate;
            PopulateOutputOptions(_tcbOutputTo);
        }
        /*
        public ExcelHelper(Excel.Application app, RibbonComboBox cboOutputTo)
        {
            _app = app;
            _cboOutputTo = cboOutputTo;
            _app.WorkbookActivate += AppWorkbookActivate;
            PopulateOutputOptions(cboOutputTo);
        }
        */
        public void RefreshQueryTableAsync(QueryTable queryTable)
        {
            _qryTable = queryTable;
            _qryTable.AfterRefresh += OnQueryTableAfterRefresh;
            _qryTable.Refresh(true);
        }

        void AppWorkbookActivate(Workbook wb)
        {
            // re-populate the output options if the active workbook changes
            if (_tcbOutputTo != null)
            {
                PopulateOutputOptions(_tcbOutputTo);
            }
            /*if (_cboOutputTo != null)
            {
                PopulateOutputOptions(_cboOutputTo);
            }*/
            if (_fcbOutputTo != null)
            {
                PopulateOutputOptions(_fcbOutputTo);
            }
            EnsurePowerPivotDataIsLoaded();
            // TODO - reset workbook connection

            // TODO - repopulate metadata
            
        }

        private void PopulateOutputOptions(Fluent.ComboBox outputTo)
        {
            if (outputTo == null) return;
            outputTo.Items.Clear();
            Workbook wb = _app.ActiveWorkbook;
            outputTo.Items.Add(DAX_RESULTS_SHEET);
            foreach (Worksheet ws in wb.Worksheets)
            {
                outputTo.Items.Add(ws.Name);
            }
            outputTo.Items.Add(NEW_SHEET);
            // set the default 
            outputTo.Text = DAX_RESULTS_SHEET;
        }

        private void PopulateOutputOptions(ToolStripComboBox outputTo)
        {
            if (outputTo.ComboBox == null) return;
            outputTo.Items.Clear();
            Workbook wb = _app.ActiveWorkbook;
            outputTo.Items.Add(DAX_RESULTS_SHEET);
            foreach (Worksheet ws in wb.Worksheets)
            {
                outputTo.Items.Add(ws.Name);
            }
            outputTo.Items.Add(NEW_SHEET);
            // set the default 
            outputTo.Text = DAX_RESULTS_SHEET;
        }
        /*
        private void PopulateOutputOptions(Fluent.ComboBox outputTo)
        {
            if (outputTo == null) return;
            outputTo.Items.Clear();
            Workbook wb = _app.ActiveWorkbook;
            outputTo.Items.Add(DAX_RESULTS_SHEET);
            foreach (Worksheet ws in wb.Worksheets)
            {
                outputTo.Items.Add(ws.Caption);
            }
            outputTo.Items.Add(NEW_SHEET);
            // set the default 
            outputTo.Text = DAX_RESULTS_SHEET;
        }
        */
        public Worksheet SelectedOutput
        {
            get
            {
                string outputToText=DAX_RESULTS_SHEET; //default to results sheet
                if (_tcbOutputTo != null)
                {
                    outputToText = _tcbOutputTo.Text;
                }
               /* TODO 
                if (_cboOutputTo != null)
                {
                    outputToText = _cboOutputTo.Text;
                }
                */ 
                switch (outputToText)
                {
                    case NEW_SHEET:
                        return CreateNewWorkSheeet(_app.ActiveWorkbook);
                    case DAX_RESULTS_SHEET:
                        return GetDaxResultsWorkSheet(_app.ActiveWorkbook);
                    default:
                        return (Worksheet)_app.ActiveWorkbook.Sheets[outputToText];
                }
            }
        }

        public  void CopyDataTableToRange(System.Data.DataTable dt, Worksheet excelSheet)
        {
            
            // Calculate the final column letter
            var finalColLetter = string.Empty;
            const string colCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var colCharsetLen = colCharset.Length;

            if (dt.Columns.Count > colCharsetLen)
                finalColLetter = colCharset.Substring(
                    (dt.Columns.Count - 1) / colCharsetLen - 1, 1);

            finalColLetter += colCharset.Substring(
                    (dt.Columns.Count - 1) % colCharsetLen, 1);

            // Fast data export to Excel
            string excelRange = string.Format("A1:{0}{1}",
                finalColLetter, dt.Rows.Count + 1);

            // copying an object array to Value2 means that there is only one
            // .Net to COM interop call
            excelSheet.Range[excelRange, Type.Missing].Value2 = dt.ToObjectArray();

            // Autofit the columns to the data
            excelSheet.Range[excelRange, Type.Missing].EntireColumn.AutoFit();

            // Mark the first row as BOLD
            ((Range)excelSheet.Rows[1, Type.Missing]).Font.Bold = true;
        }

        public  Worksheet CreateNewWorkSheeet(Workbook workbook)
        {
            // Create a new Sheet
            return (Worksheet)workbook.Sheets.Add(
                Type.Missing, workbook.Sheets.Item[workbook.Sheets.Count]
                , 1, XlSheetType.xlWorksheet);
        }

        private  Worksheet _shtDaxResults;
        public  Worksheet GetDaxResultsWorkSheet(Workbook workbook)
        {
            
            if (_shtDaxResults == null)
            {
                foreach (Worksheet s in from Worksheet s in workbook.Sheets where s.Name == "DaxResults" select s)
                {
                    _shtDaxResults = s;
                    //return _shtDaxResults;
                }
            }
            /*else
            {
                return _shtDaxResults;
            }*/
            if (_shtDaxResults != null)
            {
                _app.DisplayAlerts = false;
                _shtDaxResults.Delete();
                _app.DisplayAlerts = true;
            }
            // Create a new Sheet
            _shtDaxResults = (Worksheet)workbook.Sheets.Add(
                    Type.Missing, workbook.Sheets.Item[workbook.Sheets.Count]
                    , 1, XlSheetType.xlWorksheet);
            _shtDaxResults.Name = "DaxResults";

            return _shtDaxResults;
        }

        public bool HasPowerPivotData()
        {
            
            if (_app.ActiveWorkbook == null) return false;
            PivotCaches pvtcaches = _app.ActiveWorkbook.PivotCaches();
            if (pvtcaches.Count == 0)
                return false;
            if (float.Parse(_app.Version) >= 15 )
                return (from PivotCache pvtc in pvtcaches
                        let conn = pvtc.Connection.ToString()
                        where pvtc.OLAP
                          && pvtc.CommandType == XlCmdType.xlCmdCube
                          && (int)pvtc.WorkbookConnection.Type == 7 // xl15Model
                        select pvtc).Any();
            
            return (from PivotCache pvtc in pvtcaches
                    let conn = pvtc.Connection.ToString()
                    where pvtc.OLAP 
                      && pvtc.CommandType == XlCmdType.xlCmdCube 
                      && ((string) conn).Contains("Data Source=$Embedded$")
                    select pvtc).Any();
        }

        //((dynamic)pc.WorkbookConnection).ModelConnection.ADOConnection.ConnectionString
        //"Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7"
        public ADOTabularConnection GetPowerPivotConnection()
        {
            PivotCache pc=null;
            string connStr = "";
            PivotCaches pvtcaches = _app.ActiveWorkbook.PivotCaches();
            if (float.Parse(_app.Version) >= 15)
            {
                pc = (from PivotCache pvtc in pvtcaches
                                 let conn = pvtc.Connection.ToString()
                                 where pvtc.OLAP
                                       && pvtc.CommandType == XlCmdType.xlCmdCube
                                       && (int) pvtc.WorkbookConnection.Type == 7 // xl15Model
                                 select pvtc).First();
                connStr = (string)((dynamic)pc.WorkbookConnection).ModelConnection.ADOConnection.ConnectionString;
                connStr = string.Format("{0};location={1}", connStr, _app.ActiveWorkbook.FullName);
                // for connections to Excel 2013 or later we need to use the Excel version of ADOMDClient
                return new ADOTabularConnection(connStr, AdomdType.Excel);
            }
            else
            {
                pc = (from PivotCache pvtc in pvtcaches
                                 let conn = pvtc.Connection.ToString()
                                 where pvtc.OLAP
                                       && pvtc.CommandType == XlCmdType.xlCmdCube
                                       //&& (int)pvtc.WorkbookConnection.Type == 7
                                 select pvtc).First();
                connStr = ((dynamic) pc.WorkbookConnection).OLEDBConnection.Connection.Replace("OLEDB;","");
                connStr = string.Format("{0};location={1}", connStr, _app.ActiveWorkbook.FullName);
                // for connections to Excel 2010 we need to use the AnalysisServices version of ADOMDClient
                return new ADOTabularConnection(connStr, AdomdType.AnalysisServices);
            }
        }


        public void EnsurePowerPivotDataIsLoaded()
        {
            PivotCaches pvtcaches = _app.ActiveWorkbook.PivotCaches();
            if (pvtcaches.Count == 0)
                return;
            
            foreach (PivotCache pvtc in from PivotCache pvtc in pvtcaches let conn = pvtc.Connection.ToString() 
            where pvtc.OLAP 
                && pvtc.CommandType == XlCmdType.xlCmdCube 
                && ((string) conn).Contains("Data Source=$Embedded$") 
                && !pvtc.IsConnected 
            select pvtc)
            {
                pvtc.Refresh();
            }

        }

        public void OnQueryTableAfterRefresh(bool success) 
        {
            QueryTableRefreshed(this, new QueryTableRefreshEventArgs(success));
        }
    }

    public class QueryTableRefreshEventArgs : EventArgs
    {
        public QueryTableRefreshEventArgs(bool success)
        {
            Success = success;
        }

        public bool Success { get; set; }
    
    }
    
}
