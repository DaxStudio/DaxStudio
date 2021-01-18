using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public static class WindowTitle
    {


        //static void Main(string[] args)
        //{
        //    var p = Process.GetProcessById(3484);
        //    var h = p.MainWindowHandle;

        //    string s = GetWindowTextTimeout(h, 100 /*msec*/);

        //}




        const int WM_GETTEXT = 0x000D;
        const int WM_GETTEXTLENGTH = 0x000E;




        private static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                NativeMethods.EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }


        public static string GetWindowTitle(int procId)
        {
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                StringBuilder message = new StringBuilder(1000);
                if (NativeMethods.IsWindowVisible(handle))
                {
                    //SendMessage(handle, WM_GETTEXT, message.Capacity, message);
                    //if (message.Length > 0) return message.ToString();
                    return GetCaptionOfWindow(handle);
                }

            }
            return "";
        }

        /* ====================================== */

        public static string GetWindowTitleTimeout(int procId, uint timeout)
        {
            string title = "";
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                try
                {
                    // if there is an issue with the window handle we just
                    // ignore it and skip to the next one in the collection
                    title = GetWindowTextTimeout(handle, timeout);
                }
#pragma warning disable CA1031
                catch (Exception)
#pragma warning restore CA1031
                {
                    title = "";
                }
                if (title.Length > 0) return title;
                }
            return title;
        }


        private static unsafe string GetWindowTextTimeout(IntPtr hWnd, uint timeout)
        {
            int length;
            if (NativeMethods.SendMessageTimeout(hWnd, WM_GETTEXTLENGTH, 0, null, 2, timeout, &length) == 0)
            {
                return null;
            }
            if (length == 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(length + 1);  // leave room for null-terminator
            if (NativeMethods.SendMessageTimeout(hWnd, WM_GETTEXT, (uint)sb.Capacity, sb, 2, timeout, null) == 0)
            {
                return null;
            }

            return sb.ToString();
        }

        private static string GetCaptionOfWindow(IntPtr hwnd)
        {
            string caption = "";
            StringBuilder windowText;
            try
            {
                int max_length = NativeMethods.GetWindowTextLength(hwnd);
                windowText = new StringBuilder("", max_length + 5);
                NativeMethods.GetWindowText(hwnd, windowText, max_length + 2);

                if (!String.IsNullOrEmpty(windowText.ToString()) && !String.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                caption = ex.Message;
            }
            finally
            {
                windowText = null;
            }
            return caption;
        }

    }

    internal static class NativeMethods
    {

        internal delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);


        [DllImport("user32.dll")]
        internal static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

#pragma warning disable CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal unsafe static extern int SendMessageTimeout(
            IntPtr hWnd,
            uint uMsg,
            uint wParam,
            StringBuilder lParam,
            uint fuFlags,
            uint uTimeout,
            void* lpdwResult);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam,
            StringBuilder lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);
#pragma warning restore CA1838 // Avoid 'StringBuilder' parameters for P/Invokes
    }
}
