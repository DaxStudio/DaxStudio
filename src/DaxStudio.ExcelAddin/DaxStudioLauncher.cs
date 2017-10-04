using System.Runtime.InteropServices;
using DaxStudio.ExcelAddin;

namespace DaxStudio
{
    // This class allows for VBA to launch the DAX Studio UI
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public class DaxStudioLauncher:IDaxStudioLauncher
    {
        private readonly IDaxStudioLauncher _launcher;
        public DaxStudioLauncher(IDaxStudioLauncher launcher)
        {
            _launcher = launcher;
        }

        public void Launch()
        {
            _launcher.Launch(false);
        }

        public void Launch(bool enableLogging)
        {
            _launcher.Launch(enableLogging);
        }
    }
}
