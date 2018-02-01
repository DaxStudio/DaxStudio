using System;
using System.Runtime.InteropServices;

namespace DaxStudio.ExcelAddin
{
    [ComVisible(true)]
    public interface IDaxStudioLauncher
    {
        // This Interface is used by VBA clients (ie. PowerPivot Utilities)
        // in order to start the DAX Studio UI.
        // WARNING: don't add overloads for this function as the COM/VBA marshalling
        //          only seems to find the 'first' overload
        void Launch();
        
    }
    
}
