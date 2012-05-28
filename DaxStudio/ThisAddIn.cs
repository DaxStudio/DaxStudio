using System;

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
