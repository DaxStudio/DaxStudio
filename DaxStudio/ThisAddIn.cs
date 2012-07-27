using System;
using System.Windows.Threading;

namespace DaxStudio
{
    public partial class ThisAddIn
    {
        private void ThisAddInStartup(object sender, EventArgs e)
        {
            CreateRibbonObjects();

        }

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            // this forces the wpf RibbonWindow to shutdown correctly
            // see http://go4answers.webhost4life.com/Example/ribbonribbonwindow-microsoft-ribbon-74444.aspx
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        }

        #region VSTO generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            Startup += ThisAddInStartup;
            Shutdown += ThisAddIn_Shutdown;
        }
        
        #endregion
    }
}
