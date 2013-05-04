using System;
using System.Collections.Generic;

namespace DaxStudio.Interfaces
{
    public interface IDaxStudioHost
    {
        bool SupportsQueryTable { get; }
        bool SupportsStaticTable { get; }
        bool HasPowerPivotModel { get; }
        List<string> Worksheets { get; }
       // void DaxQueryTable(string WorksheetName, ADOTabularConnection connection, string daxQuery);
       // void DaxQueryStaticResult(string WorksheetName, ADOTabularConnection connection, string daxQuery);
    }
}
