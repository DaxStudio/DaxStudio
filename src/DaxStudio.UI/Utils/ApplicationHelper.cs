using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;

namespace DaxStudio.UI.Utils
{
    public static class ApplicationHelper
    {

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();


        public static bool IsApplicationActive()
        {
            var foregroundHwnd = GetForegroundWindow();
            foreach (var wnd in Application.Current.Windows.OfType<Window>())
            {
                if (wnd == null) continue;
                if (new WindowInteropHelper(wnd).Handle == foregroundHwnd) return true;
            }
            return false;
        }

    }
}
