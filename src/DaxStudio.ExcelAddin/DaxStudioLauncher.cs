
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.ExcelAddin;

namespace DaxStudio
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class DaxStudioLauncher:IDaxStudioLauncher
    {
        private readonly IDaxStudioLauncher _launcher;
        public DaxStudioLauncher(IDaxStudioLauncher launcher)
        {
            _launcher = launcher;
        }
        //public void Launch()
        //{
        //    _launcher.Launch();
        //}

        public void Launch(bool enableLogging)
        {
            _launcher.Launch(enableLogging);
        }
    }
}
