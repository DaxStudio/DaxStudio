using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ADOTabular;
using DaxStudio.UI;
using DaxStudio.Interfaces;
using Microsoft.Office.Interop.Excel;

namespace DaxStudio
{
    public class DaxStudioExcelHost : IDaxStudioHost
    {
        private readonly Workbook _workbook;
        private Application _app;
        private ExcelHelper _xlHelper;

        public DaxStudioExcelHost( Application app)
        {
            _workbook = app.ActiveWorkbook;
            _app = app;
            _xlHelper = new ExcelHelper(app);
        }

        public bool IsExcel {
            get { return true; }
        }
        public bool SupportsQueryTable { get { return false; } }
        public bool SupportsStaticTable { get { return true; } }
        public bool HasPowerPivotModel { get { return false; } }
        public bool HasPowerPivotData()
        {
            return false;
        }

        public bool EnsurePowerPivotDataIsLoaded()
        {
            return false;
        }
        /*
        public string BuildPowerPivotConnection()
        {
            throw new NotImplementedException();
        }
        */
        public string WorkbookName {
            get { return _workbook.FullName; }
            set {}
        }
        public List<string> Worksheets {
            get { return null; }
        }

        public ADOTabularConnection GetPowerPivotConnection()
        {
            throw new NotImplementedException();
        }
    }
}
