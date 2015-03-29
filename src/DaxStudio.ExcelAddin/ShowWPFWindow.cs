using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace DaxStudio
{
    
        public class UserForm//<T> where T : DaxStudioWindow, new()
        {
            //private static Thread thread;

            public static void ShowUserForm(Microsoft.Office.Interop.Excel.Application app, IntPtr hwnd)
            {
                var thread = new Thread(() => DoWork(app,hwnd) );
                thread.SetApartmentState(ApartmentState.STA);
                //thread.IsBackground = true;
                thread.Start();
            }


            private static void DoWork(Microsoft.Office.Interop.Excel.Application app, IntPtr hwnd)
            {
                DaxStudioWindow win = new DaxStudioWindow(app);
            //    var hwndHelper = new System.Windows.Interop.WindowInteropHelper(win);
            //    hwndHelper.Owner = hwnd;
                win.Show();
                win.Closed += (sender1, e1) => win.Dispatcher.InvokeShutdown();
                System.Windows.Threading.Dispatcher.Run();
            }
        }
    
}
