using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using Microsoft.Office.Interop.Excel;
using Caliburn.Micro;
using System.Linq;

namespace DaxStudio
{
    

    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IDaxStudioHost))]
    public class DaxStudioExcelHost : PropertyChangedBase, IDaxStudioHost
    {
        const string NEW_SHEET = "<New Sheet>";
        const string DAX_RESULTS_SHEET = "<Query Results Sheet>";

        private readonly Workbook _workbook;
        private Application _app;
        private ExcelHelper _xlHelper;
        [ImportingConstructor]
        public DaxStudioExcelHost()
        {
            _app = Globals.ThisAddIn.Application;
            _workbook = _app.ActiveWorkbook;
            _app.WorkbookActivate += new AppEvents_WorkbookActivateEventHandler(_app_WorkbookActivate);
            _xlHelper = new ExcelHelper(_app);
        }

        void _app_WorkbookActivate(Workbook Wb)
        {
            // TODO - check powerpivot model, reload metadata??
            NotifyOfPropertyChange(() => Worksheets);
        }

        public bool IsExcel {
            get { return true; }
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
            get { return _workbook.FullName; }
            set {}
        }

        public IEnumerable<string> Worksheets {
            get
            {
                yield return DAX_RESULTS_SHEET;
                yield return NEW_SHEET;
                foreach (Worksheet sht in _app.ActiveWorkbook.Worksheets)
                {
                    yield return sht.Name;
                }
            }
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
    }
}
