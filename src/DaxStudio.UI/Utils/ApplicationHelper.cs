using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace DaxStudio.UI.Utils
{
    public static class ApplicationHelper
    {


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();


        public static bool IsApplicationActiveOld()
        {
            var foregroundHwnd = GetForegroundWindow();
            foreach (var wnd in Application.Current.Windows.OfType<Window>())
            {
                if (wnd == null) continue;
                if (new WindowInteropHelper(wnd).Handle == foregroundHwnd) return true;
            }
            return false;
        }


        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool IsApplicationActive()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            GetWindowThreadProcessId(activatedHandle, out var activeProcId);

            return activeProcId == procId;
        }



    }
}
