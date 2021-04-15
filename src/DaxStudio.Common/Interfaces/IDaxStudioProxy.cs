using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ADOTabular;

namespace DaxStudio.Common.Interfaces
{
    public interface IDaxStudioProxy : IDisposable
    {
        bool IsExcel { get; } 
        bool SupportsQueryTable { get; }
        bool SupportsStaticTable { get; }
        bool HasPowerPivotModel { get; }
        //bool HasPowerPivotData();
        void EnsurePowerPivotDataIsLoaded();
        //string BuildPowerPivotConnection();
        String WorkbookName { get;  }
        IEnumerable<string> Worksheets { get; }
        
       // void DaxQueryTable(string WorksheetName, ADOTabularConnection connection, string daxQuery);
       // void DaxQueryStaticResult(string WorksheetName, ADOTabularConnection connection, string daxQuery);
        
        Task OutputStaticResultAsync(DataTable results, string sheetName);
        Task OutputLinkedResultAsync(string daxQuery, string sheetName, string connectionString);
        int Port { get; }
        ADOTabularConnection GetPowerPivotConnection(string applicationName, string additionalsettings);
    }
}
