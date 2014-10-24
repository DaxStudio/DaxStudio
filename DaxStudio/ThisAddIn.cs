using System;
using System.Diagnostics;
using System.Windows;
using ADOTabular.AdomdClientWrappers;
using Microsoft.Office.Tools.Ribbon;
using Microsoft.Owin.Hosting;
using Serilog;

namespace DaxStudio
{
    public partial class ThisAddIn
    {
        private static bool _inShutdown ;

        public ILogger log;
        private void ThisAddInStartup(object sender, EventArgs e)
        {
            
            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += currentDomain_AssemblyResolve;
            CreateRibbonObjects();
            log = new LoggerConfiguration().ReadAppSettings().CreateLogger();
            Log.Logger = log;
            Log.Information("============ Excel Add-in Startup =============");
        }

        private DaxStudioRibbon _ribbon;
        protected override Microsoft.Office.Tools.Ribbon.IRibbonExtension[] CreateRibbonObjects()
        {
            this._ribbon = new DaxStudioRibbon();
            return new IRibbonExtension[] {this._ribbon};
            //return base.CreateRibbonObjects();
        }

        //the Microsoft.Excel.AdomdClient.dll used for Excel Data Models in Excel 15 isn't in any of the paths .NET looks for assemblies in... so we have to catch the AssemblyResolve event and manually load that assembly
        //private static AdomdClientWrappers.ExcelAdoMdConnections _helper = new AdomdClientWrappers.ExcelAdoMdConnections();
        static System.Reflection.Assembly currentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                Log.Verbose("{Class} {Method} {args_Name} {args_RequestingAssembly}", "ThisAddIn", "currentDomain_AssemblyResolve", args.Name);
                System.Diagnostics.Debug.WriteLine("AssemblyResolve: " + args.Name);
                if (args.Name.Contains("Microsoft.Excel.AdomdClient"))
                {
                    var ass  = ExcelAdoMdConnections.ExcelAdomdClientAssembly;
                    Log.Verbose("{class} {method} {assembly}", "ThisAddin", "currentDomain_AssemblyResolve", "Microsoft.Excel.AdomdClient Resolved");
                    return ass;
                }

                if (args.Name.Contains("Microsoft.Excel.Amo"))
                {
                    var ass = Xmla.ExcelAmoWrapper.ExcelAmoAssembly;
                    Log.Verbose("{class} {method} {assembly}", "ThisAddin", "currentDomain_AssemblyResolve", "Microsoft.Excel.Amo Resolved");
                    return ass;
                }


                return null;
            }
            catch (Exception ex)
            {
                if (!_inShutdown)
                {
                    Log.Error("{class} {method} {args_Name} {ex_Message}","ThisAddIn","currentDomain_AssemblyResolve",args.Name,ex.Message);
                    MessageBox.Show(
                        string.Format("Problem during AssemblyResolve in Dax Studio\r\nFor Assembly {0} :\r\n{1}\r\n{2} "
                            , args.Name
                            , ex.Message 
                            , ex.StackTrace),
                        "Dax Studio");
                }
                return null;
            }
        }
		

        private void ThisAddIn_Shutdown(object sender, EventArgs e)
        {
            Log.Information("============ Excel Add-in Shutdown =============");
            // this forces the wpf RibbonWindow to shutdown correctly
            // see http://go4answers.webhost4life.com/Example/ribbonribbonwindow-microsoft-ribbon-74444.aspx
            try
            {
                _inShutdown = true;
                
                //_ribbon.CancelToken.Cancel();
                //Debug.WriteLine(string.Format("{0} ===>>> waiting for app shutdown", DateTime.Now));
                // wait upto 5 secs for app to shutdown
                //_ribbon.ShutDownSync.WaitOne(3000);
                //GC.Collect();
                //GC.WaitForPendingFinalizers();
                //Debug.WriteLine(string.Format("{0} ===>>> app shutdown", DateTime.Now ));
                //    Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            
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
