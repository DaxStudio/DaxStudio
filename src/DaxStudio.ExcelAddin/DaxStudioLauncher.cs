using System.Runtime.InteropServices;
using DaxStudio.ExcelAddin;

namespace DaxStudio
{
    // This class allows for VBA to launch the DAX Studio UI
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class DaxStudioLauncher:IDaxStudioLauncher
    {
        private readonly DaxStudioRibbon _ribbon;
        public DaxStudioLauncher(DaxStudioRibbon ribbon)
        {
            _ribbon = ribbon;
        }

        public void Launch()
        {
            _ribbon.Launch(false);
        }

    }
}
