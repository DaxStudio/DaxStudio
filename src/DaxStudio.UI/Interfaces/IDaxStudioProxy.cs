using System;
using System.Collections.Generic;
using System.Data;
using ADOTabular;
using DaxStudio;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public interface IDaxStudioProxy : IDisposable
    {
        bool IsExcel { get; } 
        bool HasPowerPivotModel(int TimeoutSecs);

        String WorkbookName { get;  }
        IEnumerable<string> Worksheets { get; }      
        
        Task OutputStaticResultAsync(DataTable results, string sheetName);
        Task OutputLinkedResultAsync(string daxQuery, string sheetName, string connectionString);
        int Port { get; set; }
        ADOTabularConnection GetPowerPivotConnection(string applicationName, string additionalsettings);
    }
}
