using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows.Threading;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
//using DaxStudio.UI.Model;
using Microsoft.Office.Interop.Excel;
using Caliburn.Micro;
using System.Linq;

namespace DaxStudio
{
    
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IDaxStudioHost))]
    public class DaxStudioExcelHost : PropertyChangedBase, IDaxStudioHost , IDisposable
    {
        
        private readonly Application _app;
        private readonly ExcelHelper _xlHelper;
        
        [ImportingConstructor]
        public DaxStudioExcelHost()
        {
            var addin = Globals.ThisAddIn;
            _app = addin.Application;
            _xlHelper = new ExcelHelper(_app);
        }

        public IDaxStudioProxy Proxy
        {
            get { return null; }
        }
        public string WorksheetDaxResults
        {
            get { return DaxStudio.UI.Properties.Resources.DAX_Results_Sheet; }
        }

        public string WorksheetNew
        {
            get { return DaxStudio.UI.Properties.Resources.DAX_New_Sheet; }
        }
        void _app_WorkbookActivate(Workbook Wb)
        {
            // TODO - check powerpivot model, reload metadata??
            NotifyOfPropertyChange(() => Worksheets);
        }

        public bool IsExcel {
            get { return true; }
        }

        public string CommandLineFileName
        {
            get { return string.Empty; }
        }

        public bool SupportsQueryTable { get { return false; } }
        public bool SupportsStaticTable { get { return true; } }

        // TODO
        public bool HasPowerPivotModel { get { return _xlHelper.HasPowerPivotData(); } }
        // TODO
        //public bool HasPowerPivotData()
        //{
        //    return _xlHelper.HasPowerPivotData();
       // }

        public void EnsurePowerPivotDataIsLoaded()
        {
            PivotCaches pvtcaches = _app.ActiveWorkbook.PivotCaches();
            if (pvtcaches.Count == 0)
                return;

            foreach (PivotCache pvtc in from PivotCache pvtc in pvtcaches
                                        let conn = pvtc.Connection.ToString()
                                        where pvtc.OLAP
                                            && pvtc.CommandType == XlCmdType.xlCmdCube
                                            && ((string)conn).Contains("Data Source=$Embedded$")
                                            && !pvtc.IsConnected
                                        select pvtc)
            {
                pvtc.Refresh();
            }
        }
        
        public string WorkbookName {
            get
            {
                var wb = _app.ActiveWorkbook;
                return wb.FullName;
            }
            //set {}
        }

        public IEnumerable<string> Worksheets {
            get
            {
                yield return WorksheetDaxResults;
                yield return WorksheetNew;
                foreach (Worksheet sht in _app.ActiveWorkbook.Worksheets)
                {
                    yield return sht.Name;
                }
            }
        }

        public void OutputStaticResult(System.Data.DataTable results, string sheetName)
        { 
            if (Dispatcher.CurrentDispatcher.CheckAccess())
            {
                Debug.WriteLine("===>> Invoking on Dispatcher  <<===");
                Dispatcher.CurrentDispatcher.Invoke(new System.Action(
                    () => _xlHelper.CopyDataTableToRange(results, _xlHelper.GetTargetWorksheet(sheetName))));
            }
            else
            {
                _xlHelper.CopyDataTableToRange(results, _xlHelper.GetTargetWorksheet(sheetName));    
            }
            
        }

        public void OutputQueryTableResult(string connection, string daxQuery, string sheetName,IQueryRunner runner)
        {
            // TODO - write dynamic results
            var ws = _xlHelper.GetTargetWorksheet(sheetName);
            _xlHelper.DaxQueryTable(ws,connection, daxQuery,runner);
        }


        public ADOTabularConnection GetPowerPivotConnection()
        {
            PivotCache pc = null;
            string connStr = "";
            PivotCaches pvtcaches = _app.ActiveWorkbook.PivotCaches();
            if (float.Parse(_app.Version) >= 15)
            {
                pc = (from PivotCache pvtc in pvtcaches
                      let conn = pvtc.Connection.ToString()
                      where pvtc.OLAP
                            && pvtc.CommandType == XlCmdType.xlCmdCube
                            && (int)pvtc.WorkbookConnection.Type == 7 // xl15Model
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
                connStr = ((dynamic)pc.WorkbookConnection).OLEDBConnection.Connection.Replace("OLEDB;", "");
                connStr = string.Format("{0};location={1}", connStr, _app.ActiveWorkbook.FullName);
                // for connections to Excel 2010 we need to use the AnalysisServices version of ADOMDClient
                return new ADOTabularConnection(connStr, AdomdType.AnalysisServices);
            }
        }

        public void Dispose()
        {
            _xlHelper.Dispose();

        }
    }
}
