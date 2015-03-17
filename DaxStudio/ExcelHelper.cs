using System;
using System.Diagnostics;
using System.Linq;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;
using System.Data;
using System.Globalization;
using Serilog;

namespace DaxStudio 
{
    public class ExcelHelper:IDisposable
    {
        // ReSharper disable InconsistentNaming
        const string NEW_SHEET = "<New Sheet>";
        const string DAX_RESULTS_SHEET = "<Query Results Sheet>";
        // ReSharper restore InconsistentNaming

        //private QueryTable _qryTable;
        private readonly Application _app ;

        public delegate void QueryTableRefreshedHandler(object sender, QueryTableRefreshEventArgs e);
        public event QueryTableRefreshedHandler QueryTableRefreshed;
        
        public ExcelHelper( Application app)
        {
            _app = app;
        }
        
        /*
        public void RefreshQueryTableAsync(QueryTable queryTable)
        {
            _qryTable = queryTable;
            _qryTable.AfterRefresh += OnQueryTableAfterRefresh;
            _qryTable.Refresh(true);
        }        
        */

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

            var iCol = 1;     // Excel ranges are 1 based
            foreach (DataColumn c in dt.Columns)
            {
                if (c.DataType == typeof(DateTime))
                {
                    Range col = r.Columns[iCol];
                    col.NumberFormat = "m/d/yyyy"; // US format appears to set the default date format for the current culture

                }
                iCol++;
            }

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

        internal bool IsExcel2013OrLater
        {
            get {
                return float.Parse(_app.Version, CultureInfo.InvariantCulture) >= 15;
            }
        }

        public bool HasPowerPivotData()
        {
            Log.Verbose("{Class} {method} {event}", "ExcelHelper", "HasPowerPivotData", "Start");
            try
            {
                var wb = _app.ActiveWorkbook;
                if (_app.ActiveWorkbook == null) return false;

                if (IsExcel2013OrLater)
                {
                    var conns = wb.Connections;
                    var wbc = conns["ThisWorkbookDataModel"];
                    Log.Verbose("{Class} {method} {event}", "ExcelHelper", "HasPowerPivotData", "End (2013)");
                    return (wbc != null);
                }

                // if Excel 2010
                PivotCaches pvtcaches = wb.PivotCaches();
                if (pvtcaches.Count == 0)
                    return false;   // without a pivot table cache we have no way of "waking up" the data model

                // TODO - at this point can I try creating a pivottable cache and refreshing it???

                var ptc = (from PivotCache pvtc in pvtcaches
                           let conn = pvtc.Connection.ToString()
                           where pvtc.OLAP
                              && pvtc.CommandType == XlCmdType.xlCmdCube
                              && (((string)conn).IndexOf("Data Source=$Embedded$", StringComparison.InvariantCultureIgnoreCase) >= 0)
                           select pvtc).First();// Any();
                /*
                 //TODO - try creating a pivot cache or connection
                if (ptc == null)
                {

                    ptc = pvtcaches.Create(XlPivotTableSourceType.xlExternal);
                    ptc.CommandType = XlCmdType.xlCmdCube;

                    ptc.Connection = new AdomdConnection( )
                }
                 */ 
                if (ptc != null)
                {
                    ptc.Refresh();
                    Log.Verbose("{Class} {method} {event}", "ExcelHelper", "HasPowerPivotData", "End (2010) - true");
                    return true;
                }
                Log.Verbose("{Class} {method} {event}", "ExcelHelper", "HasPowerPivotData", "End (2010) - false");
                return false;
            }
            catch(Exception ex)
            {
                Log.Error("{Class} {method} {exception} {stacktrace}", "ExcelHelper", "HasPowerPivotData", ex.Message, ex.StackTrace);
                throw;
            }
        }



        //((dynamic)pc.WorkbookConnection).ModelConnection.ADOConnection.ConnectionString
        //"Provider=MSOLAP.5;Persist Security Info=True;Initial Catalog=Microsoft_SQLServer_AnalysisServices;Data Source=$Embedded$;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error;Subqueries=0;Optimize Response=7"
        public ADOTabularConnection GetPowerPivotConnection()
        {
            PivotCache pc;
            string connStr;
            var wb = _app.ActiveWorkbook;
            PivotCaches pvtcaches = wb.PivotCaches();
            if (IsExcel2013OrLater)
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
            if (IsExcel2013OrLater)
            { EnsurePowerPivotDataIsLoaded2013(); }
            else
            { EnsurePowerPivotDataIsLoaded2010(); }
        }

        public void EnsurePowerPivotDataIsLoaded2013()
        {
            var wbc = FindPowerPivotConnection(_app.ActiveWorkbook);
            wbc.Refresh();
        }

        public void EnsurePowerPivotDataIsLoaded2010()
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
        
        }


/*
*=====================================================
*  Query table functions
*=====================================================
*/


        public void DaxQueryTable(Worksheet excelSheet, string daxQuery , string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                DaxQueryTable(excelSheet, daxQuery);
                return;
            }

            if (IsExcel2013OrLater)
            {    DaxQueryTable2013(excelSheet, daxQuery, connectionString);    }
            else
            {    DaxQueryTable2010(excelSheet, daxQuery, connectionString);    }
        }

        private void DaxQueryTable2010(Worksheet excelSheet, string daxQuery, string connectionString)
        {
            throw new NotImplementedException();
        }

        public void DaxQueryTable(Worksheet excelSheet, string daxQuery )
        {
            if (IsExcel2013OrLater)
            {    DaxQueryTable2013(excelSheet, daxQuery);    }
            else
            {    DaxQueryTable2010(excelSheet, daxQuery);    }
        }



        public static void DaxQueryTable2010(Worksheet excelSheet, string daxQuery)
        {
            Workbook wb = excelSheet.Parent;
            string path = wb.FullName;
            ListObject lo;
            var listObjs = excelSheet.ListObjects;
            if (listObjs.Count > 0)
            {
                lo = listObjs[1]; //ListObjects collection is 1 based
            }
            else
            {
                lo = listObjs.AddEx(0
                    , string.Format("OLEDB;Provider=MSOLAP.5;Persist Security Info=True;Data Source={0};Location={1};MDX Compatibility=1;Safety Options=2;ConnectTo=11.0;MDX Missing Member Mode=Error;Optimize Response=3;Cell Error Mode=TextValue;Initial Catalog={1}"
                                , "$Embedded$"
                                , "Microsoft_SQLServer_AnalysisServices"
                                , path)
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

        private static WorkbookConnection FindPowerPivotConnection(Workbook wb)
        {
            WorkbookConnection wbc = null;
            foreach (WorkbookConnection c in wb.Connections)
            {
                Debug.WriteLine("WorkbookConnection: " + c.Name);
                if (!c.InModel) continue;
                if (c.Type == XlConnectionType.xlConnectionTypeMODEL) continue;
                //if (c.Name == "ThisWorkbookDataModel") continue;
                if (c.ModelTables == null) continue;
                if (c.ModelTables.Count == 0) continue;
                
                // otherwise
                wbc = c;
                break; 
            }
            return wbc;
        }

        public static void DaxQueryTable2013(Worksheet excelSheet, string daxQuery)//, IOutputWindow output)
        {

            Worksheet ws = excelSheet;
            Workbook wb = excelSheet.Parent;
            WorkbookConnection wbc=null;

            // TODO - find connections
            wbc = FindPowerPivotConnection(wb);

            if (wbc == null) throw new Exception("Workbook table connection not found");

            var listObjs = ws.ListObjects;
            var r = ws.Cells[1, 1];
            var lo = listObjs.Add( SourceType: XlListObjectSourceType.xlSrcModel
                , Source: wbc
                , Destination: r);

            var to = lo.TableObject;
            to.RowNumbers = false;
            to.PreserveFormatting = true;
            to.RefreshStyle = XlCellInsertionMode.xlInsertEntireRows;
            to.AdjustColumnWidth = true;
            //to.ListObject.DisplayName = "DAX query";
            var oleCnn = to.WorkbookConnection.OLEDBConnection;
            oleCnn.CommandType = XlCmdType.xlCmdDAX;
            string[] qryArray = daxQuery.Split(new char[]{'\r'},StringSplitOptions.RemoveEmptyEntries);
            oleCnn.CommandText = qryArray;
            oleCnn.Refresh();
            WriteQueryToExcelComment(excelSheet, daxQuery);
        }


        public static void DaxQueryTable2013(Worksheet excelSheet, string daxQuery, string connectionString)        
        {

            Workbook wb = excelSheet.Parent;
            string path = wb.FullName;
            ListObject lo;
            var listObjs = excelSheet.ListObjects;
            if (listObjs.Count > 0)
            {
                lo = listObjs[1]; //ListObjects collection is 1 based
            }
            else
            {
                lo = listObjs.AddEx(0
                    , string.Format("OLEDB;Provider=MSOLAP.5;Integrated Security=SSPI;{0}"
                                , FixMDXCompatibilitySetting(connectionString))
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

            /*
        With ActiveSheet.ListObjects.Add(SourceType:=0, Source:=Array( _
        "OLEDB;Provider=MSOLAP.5;Integrated Security=SSPI;Persist Security Info=True;Initial Catalog=AdventureWorks Tabular Model SQL 2012;Data Source=wsapp2254\tabular;MDX Compatibility=1;Safety Options=2;MDX Missing Member Mode=Error") _
        , Destination:=Range("$A$1")).QueryTable
        .CommandType = xlCmdDefault
        .CommandText = Array("Evaluate currency")
        .Refresh BackgroundQuery:=False
            */

            /*
            Worksheet ws = excelSheet;
            Workbook wb = excelSheet.Parent;
            WorkbookConnection wbc = wb.Connections.Add2("dax", "dax", connectionString, daxQuery, XlCmdType.xlCmdDefault);
            // TODO - find connections
            
            var listObjs = ws.ListObjects;
            var r = ws.Cells[1, 1];
            var lo = listObjs.Add(SourceType: XlListObjectSourceType.xlSrcExternal
                , Source: wbc
                , Destination: r);

            var to = lo.TableObject;
            to.RowNumbers = false;
            to.PreserveFormatting = true;
            to.RefreshStyle = XlCellInsertionMode.xlInsertEntireRows;
            to.AdjustColumnWidth = true;
            //to.ListObject.DisplayName = "DAX query";
            var oleCnn = to.WorkbookConnection.OLEDBConnection;
            oleCnn.CommandText = new string[] { daxQuery };
            oleCnn.CommandType = XlCmdType.xlCmdDAX;
            oleCnn.Refresh();
            WriteQueryToExcelComment(excelSheet, daxQuery);
            */
        }

        private static string FixMDXCompatibilitySetting(string connectionString)
        {
            var rex = new System.Text.RegularExpressions.Regex("MDX\\sCompatibility=\\d;");
            return rex.Replace(connectionString, "");
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
