using System;
using System.Linq;
using Microsoft.Office.Interop.Excel;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows.Forms;

namespace DaxStudio
{
    public class ExcelHelper
    {
        // ReSharper disable InconsistentNaming
        const string NEW_SHEET = "<New Sheet>";
        const string DAX_RESULTS_SHEET = "<Query Results Sheet>";
        // ReSharper restore InconsistentNaming

        private readonly Excel.Application _app ;
        private readonly ToolStripComboBox _tcbOutputTo;
        public ExcelHelper(Excel.Application app, ToolStripComboBox tcbOutputTo)
        {
            _app = app;
            _tcbOutputTo = tcbOutputTo;
            _app.WorkbookActivate += AppWorkbookActivate;
            
            PopulateOutputOptions(_tcbOutputTo);

        }

        void AppWorkbookActivate(Workbook wb)
        {
            // re-populate the output options if the active workbook changes
            PopulateOutputOptions(_tcbOutputTo);
            EnsurePowerPivotDataIsLoaded();
            // TODO - reset workbook connection

            // TODO - repopulate metadata
            
        }

        private void PopulateOutputOptions(ToolStripComboBox outputTo)
        {
            if (_tcbOutputTo.ComboBox == null) return;
            _tcbOutputTo.Items.Clear();
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

        public Worksheet SelectedOutput
        {
            get
            {
                switch (_tcbOutputTo.Text)
                {
                    case NEW_SHEET:
                        return CreateNewWorkSheeet(_app.ActiveWorkbook);
                    case DAX_RESULTS_SHEET:
                        return GetDaxResultsWorkSheet(_app.ActiveWorkbook);
                    default:
                        return _app.ActiveWorkbook.Sheets[_tcbOutputTo.Text];
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
            return (from PivotCache pvtc in pvtcaches
                    let conn = pvtc.Connection.ToString()
                    where pvtc.OLAP 
                      && pvtc.CommandType == XlCmdType.xlCmdCube 
                      && ((string) conn).Contains("Data Source=$Embedded$")
                    select pvtc).Any();
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
    }
}
