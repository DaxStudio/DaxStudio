using System;
using DaxStudio.ExcelAddin;
using Serilog;

namespace DaxStudio
{
    public static class ExcelInfo
    {

        internal static void WriteToLog(ThisAddIn thisAddIn)
        {
            Log.Information($"Excel Version: {thisAddIn.Application.Version}");
            Log.Information($"Excel Build: {thisAddIn.Application.Build}");
            Log.Information($"Excel Operating System: {thisAddIn.Application.OperatingSystem}");
            // list all addins
            foreach (Microsoft.Office.Core.COMAddIn addin in thisAddIn.Application.COMAddIns)
            {
                Log.Information($"   Addin : {addin.Description} Loaded: {addin.Connect}");
            }
        }
    }
}
