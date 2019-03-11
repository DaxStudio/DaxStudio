using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public class WindowTitle
    {


        //static void Main(string[] args)
        //{
        //    var p = Process.GetProcessById(3484);
        //    var h = p.MainWindowHandle;

        //    string s = GetWindowTextTimeout(h, 100 /*msec*/);

        //}



        #region PInvoke calls to get the window title of a minimize window

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);


        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        [DllImport("User32.dll", SetLastError = true)]
        public unsafe static extern int SendMessageTimeout(
            IntPtr hWnd,
            uint uMsg,
            uint wParam,
            StringBuilder lParam,
            uint fuFlags,
            uint uTimeout,
            void* lpdwResult);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam,
            StringBuilder lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern long GetWindowText(IntPtr hwnd, StringBuilder lpString, long cch);

        const int WM_GETTEXT = 0x000D;
        const int WM_GETTEXTLENGTH = 0x000E;

        #endregion


        private static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id,
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }


        public static string GetWindowTitle(int procId)
        {
            foreach (var handle in EnumerateProcessWindowHandles(procId))
            {
                StringBuilder message = new StringBuilder(1000);
                if (IsWindowVisible(handle))
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
                title = GetWindowTextTimeout(handle, timeout);
                if (title.Length > 0) return title;
            }
            return title;
        }


        private static unsafe string GetWindowTextTimeout(IntPtr hWnd, uint timeout)
        {
            int length;
            if (SendMessageTimeout(hWnd, WM_GETTEXTLENGTH, 0, null, 2, timeout, &length) == 0)
            {
                return null;
            }
            if (length == 0)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(length + 1);  // leave room for null-terminator
            if (SendMessageTimeout(hWnd, WM_GETTEXT, (uint)sb.Capacity, sb, 2, timeout, null) == 0)
            {
                return null;
            }

            return sb.ToString();
        }

        private static string GetCaptionOfWindow(IntPtr hwnd)
        {
            string caption = "";
            StringBuilder windowText = null;
            try
            {
                int max_length = GetWindowTextLength(hwnd);
                windowText = new StringBuilder("", max_length + 5);
                GetWindowText(hwnd, windowText, max_length + 2);

                if (!String.IsNullOrEmpty(windowText.ToString()) && !String.IsNullOrWhiteSpace(windowText.ToString()))
                    caption = windowText.ToString();
            }
            catch (Exception ex)
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
}
