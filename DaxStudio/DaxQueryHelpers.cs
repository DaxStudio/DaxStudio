using System;
using ADOTabular;
using DaxStudio.UI;
using Microsoft.Office.Core;
using Microsoft.Office.Interop.Excel;
//using Excel = Microsoft.Office.Interop.Excel;
using DaxStudio;

namespace DaxStudio
{
    public static class DaxQueryHelpers
    {
        
        public static void DaxQueryTable(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery)
        {
            DaxQueryTable2010(excelSheet, connection, daxQuery);
        }

        public static void DaxQueryTable2010(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery)
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


        public static void DaxQueryTable2013(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery, IOutputWindow output)
        {
            ListObject lo;
            var connStr = string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connection.ConnectionString);

            //connStr = string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connection.ConnectionString)
            //connStr = "OLEDB;Provider=SQLOLEDB.1;Integrated Security=SSPI;Persist Security Info=True;Data Source=.;Use Procedure for Prepare=1;Auto Translate=True;Packet Size=4096;Workstation ID=W8B059096PR830;Use Encryption for Data=False;Tag with column collation when possible=False;Initial Catalog=NSW_Crime";
            //var c = ((Workbook)excelSheet.Parent).Connections.Add("DaxStudio", "DaxStudio Conection", connStr, daxQuery, (dynamic)8);
            //lo.QueryTable.Connection = c;

            /*
             With ActiveSheet.ListObjects.Add(SourceType:=4, Source:=ActiveWorkbook. _
        Connections(". NSW_Crime Offences"), Destination:=Range("$C$1")).TableObject
        .RowNumbers = False
        .PreserveFormatting = True
        .RefreshStyle = 1
        .AdjustColumnWidth = True
        .ListObject.DisplayName = "Table_Offences"
        .Refresh
    End With
    Range("D2").Select
    With Selection.ListObject.TableObject.WorkbookConnection.OLEDBConnection
        .CommandText = Array("evaluate values(Offences[lga])")
        .CommandType = xlCmdDAX
    End With
    ActiveWorkbook.Connections("ModelConnection_Offences").Refresh
             */
            Workbook wb = excelSheet.Parent;
            WorkbookConnection wc = null;
            foreach (WorkbookConnection c in wb.Connections)
            {
                if (c.Name == ". NSW_Crime Offences")
                    wc = c;
            }
            if (excelSheet.ListObjects.Count > 0)
            {
                lo = excelSheet.ListObjects[1]; //ListObjects collection is 1 based
            }
            else
            {
                // TODO - if Excel 15 ...
                lo = excelSheet.ListObjects.Add(XlListObjectSourceType.xlSrcModel //4 //0
                , wc //connStr
                , Type.Missing
                , XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$1"]);
                lo.TableObject.RowNumbers = false;
                lo.TableObject.PreserveFormatting = true;
                lo.TableObject.RefreshStyle = XlCellInsertionMode.xlOverwriteCells;
                lo.TableObject.AdjustColumnWidth = true;
                lo.TableObject.ListObject.DisplayName = "DaxStudio";
                //lo.TableObject.Refresh();
                string[] cmds = new string[1];
                cmds[0] = daxQuery;
                lo.TableObject.WorkbookConnection.OLEDBConnection.CommandText = cmds;
                lo.TableObject.WorkbookConnection.OLEDBConnection.CommandType = XlCmdType.xlCmdDAX;

                /*
                var c = ((Workbook)excelSheet.Parent).Connections["LinkedTable_Population"];
                lo = excelSheet.ListObjects.Add(XlListObjectSourceType.xlSrcQuery, c, Type.Missing,
                                                XlYesNoGuess.xlGuess, excelSheet.Range["$A$1"]);
                */
            }
            //System.Runtime.InteropServices.COMException
            //{"Exception from HRESULT: 0x800401A8"}

            //, "OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            //, @"OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            /*  
              With ActiveSheet.ListObjects.Add(SourceType:=4, Source:=ActiveWorkbook. _
          Connections("LinkedTable_Sales"), Destination:=Range("$A$13")).TableObject
          .RowNumbers = False
          .PreserveFormatting = True
          .RefreshStyle = 1
          .AdjustColumnWidth = True
          .ListObject.DisplayName = "Table_Sales_1"
          .Refresh
      End With
      With Selection.ListObject.TableObject.WorkbookConnection.OLEDBConnection
          .CommandText = Array("EVALUATE Sales")
          .CommandType = xlCmdDAX
      End With
              */


            //lo.QueryTable.CommandType = (XlCmdType)8; // xlCmdDAX
            //lo.QueryTable.CommandType = (dynamic)Enum.Parse(lo.QueryTable.CommandType.GetType(), "xlCmdDAX");

            // TODO - if client = Excel 2013

            //lo.TableObject.WorkbookConnection.OLEDBConnection.CommandType = XlCmdType.xlCmdDAX;
            //lo.TableObject.WorkbookConnection.OLEDBConnection.CommandText = daxQuery;

            //lo.QueryTable.CommandType = XlCmdType.xlCmdDAX;
            //lo.QueryTable.CommandText = daxQuery;

            //var p = ((Worksheet) ((Workbook) excelSheet.Parent).Sheets["Sheet2]"]).ListObjects["Table_Population"];

            //lo.QueryTable.CommandText = daxQuery;
            try
            {
                output.WriteOutputMessage(string.Format("{0} - Starting Query Table Refresh", DateTime.Now));
                //lo.QueryTable.Refresh(false);
                lo.TableObject.Refresh();
                output.WriteOutputMessage(string.Format("{0} - Query Table Refresh Complete", DateTime.Now));
            }
            catch (Exception ex)
            {
                output.WriteOutputError(ex.Message);
                output.WriteOutputError("Error detected - collecting error details...");
                DaxQueryDiscardResults(connection, daxQuery, output);
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
