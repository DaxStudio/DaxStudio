using System;
using System.Diagnostics;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using DaxStudio.UI;
using Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

namespace DaxStudio 
{
    public class ExcelHelper:IDisposable
    {
        // ReSharper disable InconsistentNaming
        const string NEW_SHEET = "<New Sheet>";
        const string DAX_RESULTS_SHEET = "<Query Results Sheet>";
        // ReSharper restore InconsistentNaming

        private QueryTable _qryTable;
        private readonly Application _app ;

        public delegate void QueryTableRefreshedHandler(object sender, QueryTableRefreshEventArgs e);
        public event QueryTableRefreshedHandler QueryTableRefreshed;
        
        public ExcelHelper( Application app)
        {
            _app = app;
        }
        
        public void RefreshQueryTableAsync(QueryTable queryTable)
        {
            _qryTable = queryTable;
            _qryTable.AfterRefresh += OnQueryTableAfterRefresh;
            _qryTable.Refresh(true);
        }        
        
        public Worksheet GetTargetWorksheet(string sheetName)
        {
            var wb = _app.ActiveWorkbook;
            var shts = wb.Sheets;
                    switch (sheetName)
                {
                    case NEW_SHEET:
                        return CreateNewWorkSheeet(wb);
                    case DAX_RESULTS_SHEET:
                        return GetDaxResultsWorkSheet(wb);
                    default:
                        return (Worksheet)shts[sheetName];
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
            var r = excelSheet.Range[excelRange, Type.Missing];
            r.Value2 = dt.ToObjectArray();

            // Autofit the columns to the data
            var cols = r.EntireColumn;
            cols.AutoFit();

            // Mark the first row as BOLD
            var hdr = ((Range)excelSheet.Rows[1, Type.Missing]);
            var hdrFont = hdr.Font;
            hdrFont.Bold = true;
        }

        public  Worksheet CreateNewWorkSheeet(Workbook workbook)
        {
            // Create a new Sheet
            var shts = workbook.Sheets;
            return (Worksheet)shts.Add(
                Type.Missing, shts.Item[shts.Count]
                , 1, XlSheetType.xlWorksheet);
        }

        private  Worksheet _shtDaxResults;
        public  Worksheet GetDaxResultsWorkSheet(Workbook workbook)
        {
            var shts = workbook.Sheets;
            if (_shtDaxResults == null)
            {
                foreach (Worksheet s in from Worksheet s in shts where s.Name == "DaxResults" select s)
                {
                    _shtDaxResults = s;
                }
            }
            
            if (_shtDaxResults != null)
            {
                _app.DisplayAlerts = false;
                _shtDaxResults.Delete();
                _app.DisplayAlerts = true;
            }
            // Create a new Sheet
            _shtDaxResults = (Worksheet)shts.Add(
                    Type.Missing, shts.Item[shts.Count]
                    , 1, XlSheetType.xlWorksheet);
            _shtDaxResults.Name = "DaxResults";

            return _shtDaxResults;
        }

        public bool HasPowerPivotData()
        {
            var wb = _app.ActiveWorkbook;
            if (_app.ActiveWorkbook == null) return false;
            PivotCaches pvtcaches = wb.PivotCaches();
            
                if (pvtcaches.Count == 0)
                    return false;
                if (float.Parse(_app.Version) >= 15)
                    return (from PivotCache pvtc in pvtcaches
                            //let conn = pvtc.Connection.ToString()
                            where pvtc.OLAP
                                  && pvtc.CommandType == XlCmdType.xlCmdCube
                                  && (int) pvtc.WorkbookConnection.Type == 7
                            // xl15Model
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
            PivotCache pc;
            string connStr;
            var wb = _app.ActiveWorkbook;
            PivotCaches pvtcaches = wb.PivotCaches();
            if (float.Parse(_app.Version) >= 15)
            {
                pc = (from PivotCache pvtc in pvtcaches
                                 let conn = pvtc.Connection.ToString()
                                 where pvtc.OLAP
                                       && pvtc.CommandType == XlCmdType.xlCmdCube
                                       && (int) pvtc.WorkbookConnection.Type == 7 // xl15Model
                                 select pvtc).First();
                var wbc = ((dynamic)pc.WorkbookConnection);
                var modelCnn = wbc.ModelConnection;
                var cnn = modelCnn.ADOConnection;
                connStr = cnn.ConnectionString;
                connStr = string.Format("{0};location={1}", connStr, wb.FullName);
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
                var wbc = ((dynamic) pc.WorkbookConnection);
                var oledbCnn = wbc.OLEDBConnection;
                var cnn = oledbCnn.Connection;

                connStr = cnn.Replace("OLEDB;","");
                connStr = string.Format("{0};location={1}", connStr, wb.FullName);
                // for connections to Excel 2010 we need to use the AnalysisServices version of ADOMDClient
                return new ADOTabularConnection(connStr, AdomdType.AnalysisServices);
            }
        }


        public void EnsurePowerPivotDataIsLoaded()
        {
            var wb = _app.ActiveWorkbook;
            PivotCaches pvtcaches = wb.PivotCaches();
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

        public void Dispose()
        {
        //    _app.WorkbookActivate -= _wbkActivate;

            try
            {
            //    GC.Collect();
            //    GC.WaitForPendingFinalizers();
                //Marshal.FinalReleaseComObject(_app);
                //Marshal.FinalReleaseComObject(_qryTable);
                //    _qryTable.AfterRefresh -= OnQueryTableAfterRefresh;
            }
            finally
            {
                _qryTable = null;
            }
        }


        /*
     
     *=====================================================
     *  Query table functions
     *=====================================================
     */

        public void DaxQueryTable(Worksheet excelSheet, string connectionString, string daxQuery , IQueryRunner runner)
        {
            DaxQueryTable2013(excelSheet, connectionString, daxQuery, runner);
        }

        public static void DaxQueryTable2010(Worksheet excelSheet, ADOTabularConnection connection, string daxQuery, IQueryRunner runner)
        {
            ListObject lo;
            var listObjs = excelSheet.ListObjects;
            if (listObjs.Count > 0)
            {
                lo = listObjs[1]; //ListObjects collection is 1 based
            }
            else
            {
                lo = listObjs.AddEx(0
                , string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connection.ConnectionString)
                , Type.Missing
                , XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$1"]);
            }
            //System.Runtime.InteropServices.COMException
            //{"Exception from HRESULT: 0x800401A8"}

            //, "OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            //, @"OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source=.\SQL2012TABULAR;MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue"
            var qt = lo.QueryTable;

            qt.CommandType = XlCmdType.xlCmdDefault;
            qt.CommandText = daxQuery;
            try
            {
                //output.WriteOutputMessage(string.Format("{0} - Starting Query Table Refresh", DateTime.Now));
                qt.Refresh(false);
                //output.WriteOutputMessage(string.Format("{0} - Query Table Refresh Complete", DateTime.Now));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR");
                Debug.WriteLine(ex.Message);
                //output.WriteOutputError(ex.Message);
                //output.WriteOutputError("Error detected - collecting error details...");
                //DaxQueryDiscardResults(connection, daxQuery, output);
            }
            WriteQueryToExcelComment(excelSheet, daxQuery);
        }

        public static void DaxQueryTable2013(Worksheet excelSheet, string connectionString, string daxQuery, IQueryRunner runner)//, IOutputWindow output)
        {
            ListObject lo;
            var listObjs = excelSheet.ListObjects;
            if (listObjs.Count > 0)
            {
                lo = listObjs[1]; //ListObjects collection is 1 based
            }
            else
            {
                lo = listObjs.AddEx(0
                , string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connectionString)
                , Type.Missing
                , XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$1"]);
            }

            var connStr = string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;{0};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue", connectionString);

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
            
            var qt = lo.QueryTable;
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
                lo = excelSheet.ListObjects.Add( XlListObjectSourceType.xlSrcModel //4 //0
                , wc //connStr
                , Type.Missing
                , XlYesNoGuess.xlGuess
                , excelSheet.Range["$A$1"]);
                qt.RowNumbers = false;
                
                qt.PreserveFormatting = true;
                qt.RefreshStyle = XlCellInsertionMode.xlOverwriteCells;
                qt.AdjustColumnWidth = true;
                qt.ListObject.DisplayName = "DaxStudio";
                //qt.Refresh();
                string[] cmds = new string[1];
                cmds[0] = daxQuery;
                var wbc = qt.WorkbookConnection;
                var oledbCnn = wbc.OLEDBConnection;
                oledbCnn.CommandText = cmds;
                oledbCnn.CommandType = XlCmdType.xlCmdDAX;

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
                
                runner.OutputMessage("Linked Table Refresh Starting");
                //lo.QueryTable.Refresh(false);
                qt.Refresh();
                runner.OutputMessage("Linked Table Refresh Complete");
                
            }
            catch (Exception ex)
            {
                runner.OutputError(ex.Message);
                runner.ActivateOutput();
            }
            WriteQueryToExcelComment(excelSheet, daxQuery);
        }

        private static void WriteQueryToExcelComment(Worksheet excelSheet, string daxQuery)
        {
            // Using lots of intermediate viarables so that we can release and COM RCW objects
            // see: http://badecho.com/2010/08/outlook-com-interop-and-reference-counting-or-how-i-learned-to-stop-worrying-and-love-the-rcw/
            const string cmtPrefix = "DAX Query:";
            Range r = excelSheet.Range["A1"];
            var cmt = r.AddComment(string.Format("{0}\n{1}", cmtPrefix, daxQuery));
            var shp = cmt.Shape;
            var tf = shp.TextFrame;
            var c = tf.Characters(cmtPrefix.Length);
            var f = c.Font;
            f.Bold = Office.MsoTriState.msoFalse;
        }


        // TODO - look into creating a new connection if we can't find an existing pivotcache object
        /*
     
         * EXCEL VBA code to create a pivot cache - in Excel 2013+ if we find a "ThisWorkbookDataModel" connection
         *                                          but no PivotCache connection maybe we can create one...
         * 
         ActiveWorkbook.PivotCaches.Create(SourceType:=xlExternal, SourceData:= _
            ActiveWorkbook.Connections("ThisWorkbookDataModel"), Version:= _
            xlPivotTableVersion15)
      
     
         */

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
