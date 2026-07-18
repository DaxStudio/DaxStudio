using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;
using DaxStudio.Common;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Helper for applying DWM (Desktop Window Manager) window attributes so that the
    /// non-client window frame (title bar and the thin resize border that Windows 11 draws
    /// around a window) follows the currently selected DAX Studio theme instead of always
    /// rendering in the light system default.
    /// </summary>
    public static class DwmHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // DWMWA_USE_IMMERSIVE_DARK_MODE was introduced with build 18985 (attribute 20).
        // Earlier builds (1809 - 1903) used attribute 19.
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // DWMWA_BORDER_COLOR is supported on Windows 11 (build 22000+) and controls the color
        // of the thin border Windows draws around the window.
        private const int DWMWA_BORDER_COLOR = 34;

        // Special value that tells DWM to revert to the system default border color.
        private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

        /// <summary>
        /// Applies (or removes) a dark title bar / window border to the supplied window based on the
        /// active theme. When <paramref name="isDark"/> is true the window border is set to
        /// <paramref name="borderColor"/>; otherwise the border is reset to the system default.
        /// </summary>
        public static void ApplyThemeToWindow(Window window, bool isDark, System.Windows.Media.Color borderColor)
        {
            if (window == null) return;

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            ApplyThemeToWindow(hwnd, isDark, borderColor);
        }

        /// <summary>
        /// Applies (or removes) a dark title bar / window border to the supplied window handle.
        /// </summary>
        public static void ApplyThemeToWindow(IntPtr hwnd, bool isDark, System.Windows.Media.Color borderColor)
        {
            if (hwnd == IntPtr.Zero) return;

            try
            {
                int darkMode = isDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
                {
                    // Fall back to the pre-20H1 attribute value on older Windows 10 builds.
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));
                }

                // DWM expects a COLORREF (0x00BBGGRR). Reset to the default color for light themes.
                int colorRef = isDark
                    ? (borderColor.R | (borderColor.G << 8) | (borderColor.B << 16))
                    : unchecked((int)DWMWA_COLOR_DEFAULT);
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref colorRef, sizeof(int));
            }
            catch (Exception ex)
            {
                // DWM attributes are best-effort - failures (e.g. on older Windows versions) should
                // never crash the application, so just log and carry on.
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DwmHelper), nameof(ApplyThemeToWindow), "Failed to set DWM window attributes");
            }
        }
    }
}
