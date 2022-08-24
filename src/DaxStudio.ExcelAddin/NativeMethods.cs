using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.ExcelAddin
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("shell32.dll")]
        static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr pszPath);

        internal readonly static Guid GUID_ProgramFilesCommon = new Guid("F7F1ED05-9F6D-47A2-AAAE-29D317C6F066");

        [Flags]
        public enum KnownFolderFlag : uint
        {
            None = 0x0,
            CREATE = 0x8000,
            DONT_VERFIY = 0x4000,
            DONT_UNEXPAND = 0x2000,
            NO_ALIAS = 0x1000,
            INIT = 0x800,
            DEFAULT_PATH = 0x400,
            NOT_PARENT_RELATIVE = 0x200,
            SIMPLE_IDLIST = 0x100,
            ALIAS_ONLY = 0x80000000
        }


        public static string GetProgramFilesCommonVfsPath()
        {
            return GetFolderFromKnownFolderGUID(GUID_ProgramFilesCommon);
        }

        public static string GetFolderFromKnownFolderGUID(Guid guid)
        {
            return pinvokePath(guid, KnownFolderFlag.DEFAULT_PATH);
        }

        private static string pinvokePath(Guid guid, KnownFolderFlag flags)
        {
            IntPtr pPath;
            SHGetKnownFolderPath(guid, (uint)flags, IntPtr.Zero, out pPath); // public documents

            string path = System.Runtime.InteropServices.Marshal.PtrToStringUni(pPath);
            System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pPath);
            return path;
        }

        public static void EnumerateKnownFolders()
        {
            KnownFolderFlag[] flags = new KnownFolderFlag[] {
            KnownFolderFlag.None,
            KnownFolderFlag.ALIAS_ONLY | KnownFolderFlag.DONT_VERFIY,
            KnownFolderFlag.DEFAULT_PATH | KnownFolderFlag.NOT_PARENT_RELATIVE,
        };


            foreach (var flag in flags)
            {
                Debug.WriteLine(string.Format("{0}; P/Invoke==>{1}", flag, pinvokePath(GUID_ProgramFilesCommon, flag)));
            }
            
        }

    }
}
