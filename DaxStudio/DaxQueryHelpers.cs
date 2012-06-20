using System;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.Excel;
//using Excel = Microsoft.Office.Interop.Excel;
using ADOTabular;

namespace DaxStudio
{
    public static class DaxQueryHelpers
    {
        public static void DaxClearCache(ADOTabularConnection conn, IOutputWindow output) {
            var queryBegin = DateTime.UtcNow;
            conn.ExecuteCommand(String.Format(@"
<Batch xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
   <ClearCache>
     <Object>
       <DatabaseID>{0}</DatabaseID>   
    </Object>
   </ClearCache>
 </Batch>
", conn.Database.Name));
            var queryComplete = DateTime.UtcNow;
            output.WriteOutputMessage(string.Format("{0} - Cleared Cache ({1:mm\\:ss\\.fff})", DateTime.Now, queryComplete - queryBegin));
        }

        public static void DaxQueryDiscardResults(ADOTabularConnection conn, string daxQuery, IOutputWindow output)
        {
            var queryBegin = DateTime.UtcNow;
            // run query
            try
            {
                //TODO - test using a cellset instead of a DataAdaptor
                conn.ExecuteDaxQueryDataTable(daxQuery);
                var queryComplete = DateTime.UtcNow;
                output.WriteOutputMessage(string.Format("{0} - Query Complete ({1:mm\\:ss\\.fff})", DateTime.Now, queryComplete - queryBegin));
            }
            catch (Exception ex)
            {
                output.WriteOutputError(ex.Message);
            }
        }


        public static void DaxQueryTable(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery, IOutputWindow output)
        {
            ListObject lo;
            if (excelSheet.ListObjects.Count > 0)
            {
                lo = excelSheet.ListObjects[1]; //ListObjects collection is 1 based
            }
            else
            {
                lo = excelSheet.ListObjects.AddEx(0
                , string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connection.ConnectionString)
                , Type.Missing
                , XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$1"]);
            }
            //System.Runtime.InteropServices.COMException
            //{"Exception from HRESULT: 0x800401A8"}

            //, "OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            //, @"OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            
            lo.QueryTable.CommandType = XlCmdType.xlCmdDefault;
            lo.QueryTable.CommandText = daxQuery;
            try
            {
                output.WriteOutputMessage(string.Format("{0} - Starting Query Table Refresh", DateTime.Now));
                lo.QueryTable.Refresh(false);
                output.WriteOutputMessage(string.Format("{0} - Query Table Refresh Complete", DateTime.Now));
            }
            catch (Exception ex)
            {
                output.WriteOutputError(ex.Message);
                output.WriteOutputError("Error detected - collecting error details...");
                DaxQueryDiscardResults(connection,daxQuery,output);
            }
            WriteQueryToExcelComment(excelSheet, daxQuery);
        }

        public static void DaxQueryStaticResult(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery, IOutputWindow window, ExcelHelper xlHelper)
        {
            try
            {
                window.ClearOutput();
                window.WriteOutputMessage(string.Format("{0} - Query Started", DateTime.Now));
                var queryBegin = DateTime.UtcNow;

                //TODO - test using a cellset instead of a DataAdaptor
                // run query
                System.Data.DataTable dt = connection.ExecuteDaxQueryDataTable(daxQuery);
                var queryComplete = DateTime.UtcNow;
                window.WriteOutputMessage(string.Format("{0} - Query Complete ({1:mm\\:ss\\.fff})", DateTime.Now, queryComplete - queryBegin));

                // output results
                xlHelper.CopyDataTableToRange(dt,excelSheet);
                var resultsEnd = DateTime.UtcNow;
                window.WriteOutputMessage(string.Format("{0} - Results Sent to Excel ({1:mm\\:ss\\.fff})", DateTime.Now, resultsEnd - queryComplete));
                WriteQueryToExcelComment(excelSheet, daxQuery);
            }
            catch (Exception ex)
            {
                window.WriteOutputError(ex.Message);
            }
        }

        private static void WriteQueryToExcelComment(Worksheet excelSheet, string daxQuery)
        {
            var cmtPrefix = "DAX Query:";
            Range r = excelSheet.Range["A1"];
            var cmt = r.AddComment(string.Format("{0}\n{1}", cmtPrefix,daxQuery));
            cmt.Shape.TextFrame.Characters(cmtPrefix.Length).Font.Bold = MsoTriState.msoFalse;
        }
        
    }
}
