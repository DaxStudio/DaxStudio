using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DaxStudio.UI.Utils
{
    public static class WindowHandleHelper
    {
        public static IntPtr? GetHwnd(ContentControl view)
        {
            HwndSource hwnd = PresentationSource.FromVisual(view) as HwndSource;
            return hwnd?.Handle;
        }
    }
}
