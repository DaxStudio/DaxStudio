using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class AssemblyLoader
    {
        public static void PreJitControls()
        {
            Log.Debug("{class} {method} {message}", "AssemblyLoader", "PreJitControls", "start");
            //var thread = new Thread(() => {
                try
                {
                    //Xceed.Wpf.DataGrid.DataGridControl c = new Xceed.Wpf.DataGrid.DataGridControl();
                    var view = new DaxStudio.UI.Views.QueryHistoryPaneView();
                    Log.Debug("{class} {method} {message}", "AssemblyLoader", "PreJitControls", "end");
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {method} {message}", "AssemblyLoader", "PreJitControls", ex.Message);
                }
            //});

            //thread.SetApartmentState(ApartmentState.STA);
            //thread.Priority = ThreadPriority.Lowest;//We don't want prefetching to delay showing of primary window
            //thread.Start();

            //ThreadPool.QueueUserWorkItem((t) =>
            //{
            //    Log.Debug("{class} {method} {message}","AssemblyLoader","PreJitControls","start");
            //    Thread.Sleep(1000); // Or whatever reasonable amount of time
            //    try
            //    {
            //        Xceed.Wpf.DataGrid.DataGridControl c = new Xceed.Wpf.DataGrid.DataGridControl();
            //    }
            //    catch (Exception ex) {
            //        Log.Error("{class} {method} {message}", "AssemblyLoader", "PreJitControls", ex.Message);
            //    }
            //    Log.Debug("{class} {method} {message}", "AssemblyLoader", "PreJitControls", "end");
            //});
        }
    }
}
