using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace DaxStudio.UI.Utils
{
    public static class WindowHandleHelper
    {
        /// <summary>
        /// Returns the top-level window handle that should own modal popups such as the
        /// interactive Entra ID / MSAL (WAM) sign-in prompt.
        /// </summary>
        /// <remarks>
        /// The WAM broker parents its native sign-in dialog to this handle. If it receives
        /// IntPtr.Zero the dialog is owned by the desktop rather than DAX Studio, so it can be
        /// sent behind the main window. Equally important, the owner must be a <b>visible</b>
        /// window: some dialogs (e.g. the connection dialog) hide themselves before triggering
        /// the sign-in prompt, and a hidden - or about-to-close - owner lets the sign-in dialog
        /// fall behind the visible main window. We therefore skip non-visible windows and fall
        /// back to the active/main application window, which stays alive and visible for the
        /// whole session.
        /// </remarks>
        public static IntPtr? GetHwnd(ContentControl view)
        {
            // 1. Prefer the window hosting the view that initiated the prompt - but only when
            //    it is still visible.
            var viewWindow = view == null ? null : Window.GetWindow(view);
            var handle = GetVisibleWindowHandle(viewWindow);
            if (handle != IntPtr.Zero) return handle;

            // 2. Otherwise use the active/main visible application window.
            handle = GetVisibleWindowHandle(GetActiveOrMainWindow());
            if (handle != IntPtr.Zero) return handle;

            // 3. Last resort - the OS-reported main window handle for the process.
            var processHandle = Process.GetCurrentProcess().MainWindowHandle;
            return processHandle == IntPtr.Zero ? (IntPtr?)null : processHandle;
        }

        private static IntPtr GetVisibleWindowHandle(Window window)
        {
            if (window == null || !window.IsVisible) return IntPtr.Zero;
            return new WindowInteropHelper(window).EnsureHandle();
        }

        private static Window GetActiveOrMainWindow()
        {
            var app = Application.Current;
            if (app == null) return null;

            // Prefer the active window when it is visible.
            foreach (Window window in app.Windows)
            {
                if (window.IsActive && window.IsVisible) return window;
            }

            // Then the main window if it is visible.
            if (app.MainWindow != null && app.MainWindow.IsVisible) return app.MainWindow;

            // Finally, any other visible window.
            foreach (Window window in app.Windows)
            {
                if (window.IsVisible) return window;
            }

            return app.MainWindow;
        }
    }
}
