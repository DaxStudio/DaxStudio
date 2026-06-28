using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace DaxStudio.Standalone
{
    internal class ConsoleHandler
    {
        internal static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern bool AttachConsole(uint dwProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern uint GetFileType(SafeFileHandle handle);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int mode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern IntPtr GetStdHandle(int nStdHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern IntPtr CreateFile(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                IntPtr lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern bool WriteConsoleInput(
                IntPtr hConsoleInput,
                INPUT_RECORD[] lpBuffer,
                uint nLength,
                out uint lpNumberOfEventsWritten);

            [StructLayout(LayoutKind.Sequential)]
            internal struct INPUT_RECORD
            {
                public ushort EventType;
                public KEY_EVENT_RECORD KeyEvent;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct KEY_EVENT_RECORD
            {
                [MarshalAs(UnmanagedType.Bool)]
                public bool bKeyDown;
                public ushort wRepeatCount;
                public ushort wVirtualKeyCode;
                public ushort wVirtualScanCode;
                public char UnicodeChar;
                public uint dwControlKeyState;
            }
        }

        private const int STDOUT_HANDLE_NAME = -11;
        private const int STDERR_HANDLE_NAME = -12;
        private const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;
        private const ushort KEY_EVENT = 0x0001;
        private const ushort VK_RETURN = 0x0D;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;

        /// <summary>
        /// <see cref="System.Console.IsHandleRedirected(IntPtr ioHandle)" />
        /// </summary>
        [SecuritySafeCritical]
        private static bool IsHandleRedirected(IntPtr ioHandle)
        {
            const int FileTypeDisk = 0x0001;
            //const int FileTypeChar = 0x0002;
            const int FileTypePipe = 0x0003;
            //const int FileTypeRemote = 0x8000;
            //const int FileTypeUnknown = 0x0000;

            using (var handle = new SafeFileHandle(ioHandle, ownsHandle: false))
            {
                var type = NativeMethods.GetFileType(handle);
                if (type == FileTypeDisk || type == FileTypePipe)
                    return true;
            }
            //return !GetConsoleMode(ioHandle, out var num);
            return false;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static void RedirectToParent(bool throwOnFailure = false)
        {
            var stdoutRedirected = IsHandleRedirected(NativeMethods.GetStdHandle(STDOUT_HANDLE_NAME));
            if (stdoutRedirected)
            {
                var stdoutStream = Console.Out;
            }

            var stderrRedirected = IsHandleRedirected(NativeMethods.GetStdHandle(STDERR_HANDLE_NAME));
            if (stderrRedirected)
            {
                var stderrStream = Console.Error;
            }

            if (!NativeMethods.AttachConsole(ATTACH_PARENT_PROCESS) && throwOnFailure)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!stderrRedirected)
            {
                NativeMethods.SetStdHandle(STDERR_HANDLE_NAME, NativeMethods.GetStdHandle(STDOUT_HANDLE_NAME));
            }
        }

        /// <summary>
        /// Posts an Enter keystroke into the attached parent console's input
        /// buffer so the shell prompt redraws on its own line after a
        /// windowed-subsystem app finishes writing to the console (otherwise
        /// the prompt sits on the same line as our last output).
        /// </summary>
        public static void PostEnterToParentConsole()
        {
            // CONIN$ is the console's input buffer for the currently attached
            // console (works under conhost.exe and Windows Terminal).
            var hInput = NativeMethods.CreateFile(
                "CONIN$",
                GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hInput == IntPtr.Zero || hInput == new IntPtr(-1))
                return;

            try
            {
                var records = new[]
                {
                    new NativeMethods.INPUT_RECORD
                    {
                        EventType = KEY_EVENT,
                        KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                        {
                            bKeyDown = true,
                            wRepeatCount = 1,
                            wVirtualKeyCode = VK_RETURN,
                            wVirtualScanCode = 0x1C,
                            UnicodeChar = '\r',
                            dwControlKeyState = 0,
                        },
                    },
                    new NativeMethods.INPUT_RECORD
                    {
                        EventType = KEY_EVENT,
                        KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                        {
                            bKeyDown = false,
                            wRepeatCount = 1,
                            wVirtualKeyCode = VK_RETURN,
                            wVirtualScanCode = 0x1C,
                            UnicodeChar = '\r',
                            dwControlKeyState = 0,
                        },
                    },
                };

                NativeMethods.WriteConsoleInput(hInput, records, (uint)records.Length, out _);
            }
            finally
            {
                NativeMethods.CloseHandle(hInput);
            }
        }
    }
}
