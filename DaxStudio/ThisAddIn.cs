using System;
using System.Windows;
using System.Windows.Threading;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.UI;

namespace DaxStudio
{
    public partial class ThisAddIn
    {
        private void ThisAddInStartup(object sender, EventArgs e)
        {
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += currentDomain_AssemblyResolve;
            
            
            CreateRibbonObjects();

        }

        //the Microsoft.Excel.AdomdClient.dll used for Excel Data Models in Excel 15 isn't in any of the paths .NET looks for assemblies in... so we have to catch the AssemblyResolve event and manually load that assembly
        //private static AdomdClientWrappers.ExcelAdoMdConnections _helper = new AdomdClientWrappers.ExcelAdoMdConnections();
        static System.Reflection.Assembly currentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("AssemblyResolve: " + args.Name);
                return args.Name.Contains("Microsoft.Excel.AdomdClient") ? ExcelAdoMdConnections.ExcelAdomdClientAssembly : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Problem during AssemblyResolve in Dax Studio:\r\n" + ex.Message + "\r\n" + ex.StackTrace, "Dax Studio");
                return null;
            }
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
