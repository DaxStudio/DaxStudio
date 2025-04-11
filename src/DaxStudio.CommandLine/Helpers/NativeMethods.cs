using System;
using System.Runtime.InteropServices;

namespace DaxStudio.CommandLine.Helpers
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
    }
}
