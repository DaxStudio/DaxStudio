using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.AnalysisServices.AdomdClient;
using Excel = Microsoft.Office.Interop.Excel;

namespace DaxStudio
{
    public partial class DaxStudioForm : Form
    {
        public DaxStudioForm()
        {
            InitializeComponent();
        }
        private Excel.Application app;

        public Excel.Application Application
        {
            get { return app;}
            set {app = value;}
        }

        private void tsbRun_Click(object sender, EventArgs e)
        {
            RunDaxQuery();
        }

        private void RunDaxQuery()
        {
            Excel.Workbook wb = app.ActiveWorkbook;
            string wrkbkPath = wb.FullName;
            string connStr = "Data Source=$embedded$;Location=" + wrkbkPath + ";";
            AdomdConnection conn = new AdomdConnection(connStr);
            conn.Open();
            AdomdCommand cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;

            cmd.CommandText = GetTextToExecute();

            DataTable dt = new DataTable("DAXQuery");
            AdomdDataAdapter da = new AdomdDataAdapter(cmd);
            try
            {

                ClearOutput();
                WriteOutputMessage(string.Format("{0} - Query Started", DateTime.Now));
                DateTime queryBegin = DateTime.UtcNow;

                // run query
                da.Fill(dt);
                DateTime queryComplete = DateTime.UtcNow;
                WriteOutputMessage(string.Format("{0} - Query Complete ({1:mm\\:ss\\.fff})", DateTime.Now, queryComplete - queryBegin));

                // output results
                CopyDataTableToRange(dt, wb);
                DateTime resultsEnd = DateTime.UtcNow;
                WriteOutputMessage(string.Format("{0} - Results Sent to Excel ({1:mm\\:ss\\.fff})", DateTime.Now, resultsEnd - queryComplete));
            }
            catch (Exception ex)
            {
                WriteOutputError(ex.Message);
            }
        }

        private string GetTextToExecute()
        {
            // if text is selected try to execute that
            if (this.userControl12.daxEditor.SelectionLength == 0)
                return this.userControl12.daxEditor.Text;
            else
                return this.userControl12.daxEditor.SelectedText;
        }

        private void ClearOutput()
        { 
            this.rtbOutput.Clear();
            this.rtbOutput.ForeColor = Color.Black;
        }

        private void WriteOutputMessage(string message)
        {
            this.rtbOutput.AppendText(message + "\n");
        }

        private void WriteOutputError(string message)
        {
            this.rtbOutput.ForeColor = Color.Red;
            this.rtbOutput.Text = message;
        }

        private void CopyDataTableToRange(DataTable dt, Excel.Workbook excelWorkbook)
        {

            //        // Calculate the final column letter
            string finalColLetter = string.Empty;
            string colCharset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int colCharsetLen = colCharset.Length;

            if (dt.Columns.Count > colCharsetLen)
            {
                finalColLetter = colCharset.Substring(
                    (dt.Columns.Count - 1) / colCharsetLen - 1, 1);
            }

            finalColLetter += colCharset.Substring(
                    (dt.Columns.Count - 1) % colCharsetLen, 1);

            // Create a new Sheet
            Excel.Worksheet excelSheet = (Excel.Worksheet)excelWorkbook.Sheets.Add(
                Type.Missing, excelWorkbook.Sheets.get_Item(excelWorkbook.Sheets.Count)
                , 1, Excel.XlSheetType.xlWorksheet);

            //excelSheet.Name = dt.TableName;
            
            // Fast data export to Excel
            string excelRange = string.Format("A1:{0}{1}",
                finalColLetter, dt.Rows.Count + 1);

            // copying an object array to Value2 means that there is only one
            // .Net to COM interop call
            excelSheet.get_Range(excelRange, Type.Missing).Value2 = dt.ToObjectArray() ;

            // Mark the first row as BOLD
            ((Excel.Range)excelSheet.Rows[1, Type.Missing]).Font.Bold = true;

        }

        private void DaxStudioForm_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.F5:
                    RunDaxQuery();
                    break;

            }

        }

        private void tspRunToTable_Click(object sender, EventArgs e)
        {
            Excel.Workbook excelWorkbook = app.ActiveWorkbook;
            
            // Create a new Sheet
            Excel.Worksheet excelSheet = (Excel.Worksheet)excelWorkbook.Sheets.Add(
                Type.Missing, excelWorkbook.Sheets.get_Item(excelWorkbook.Sheets.Count)
                , 1, Excel.XlSheetType.xlWorksheet);

            Excel.ListObject lo = excelSheet.ListObjects.AddEx(0
                , "OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
                , Type.Missing
                ,Excel.XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$3"]);
            lo.QueryTable.CommandType = Excel.XlCmdType.xlCmdDefault;
            lo.QueryTable.CommandText = GetTextToExecute();
            try
            {
                WriteOutputMessage(string.Format("{0} - Starting Query Table Refresh", DateTime.Now));
                lo.QueryTable.Refresh(false);
                WriteOutputMessage(string.Format("{0} - Query Table Refresh Complete", DateTime.Now));
            }
            catch (Exception ex)
            {
                WriteOutputError(ex.Message);
            }
        }

        private void tspExportMetadata_Click(object sender, EventArgs e)
        {
            //TODO
        }

        
    }
}
