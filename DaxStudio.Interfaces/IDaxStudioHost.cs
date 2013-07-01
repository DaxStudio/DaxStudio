using System;
using System.Collections.Generic;
using System.Data;
using ADOTabular;
using DaxStudio;

namespace DaxStudio.Interfaces
{
    public interface IDaxStudioHost
    {
        bool IsExcel { get; }
        bool SupportsQueryTable { get; }
        bool SupportsStaticTable { get; }
        bool HasPowerPivotModel { get; }
        bool HasPowerPivotData();
        bool EnsurePowerPivotDataIsLoaded();
        //string BuildPowerPivotConnection();
        String WorkbookName { get;  set; }
        List<string> Worksheets { get; }
        
       // void DaxQueryTable(string WorksheetName, ADOTabularConnection connection, string daxQuery);
       // void DaxQueryStaticResult(string WorksheetName, ADOTabularConnection connection, string daxQuery);
        
        //void OutputStaticResult(DataTable results, string sheetName);
        //void OutputQueryTableResult(string daxQuery, string sheetName);

        ADOTabularConnection GetPowerPivotConnection();
    }
}
