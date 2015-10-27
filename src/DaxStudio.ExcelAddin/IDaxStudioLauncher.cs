using System;
using System.Runtime.InteropServices;

namespace DaxStudio.ExcelAddin
{
    [ComVisible(true)]
    public interface IDaxStudioLauncher
    {
        void Launch();
    }
}
