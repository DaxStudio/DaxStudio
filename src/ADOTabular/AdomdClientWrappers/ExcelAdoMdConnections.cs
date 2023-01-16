using Newtonsoft.Json;
using System;
using System.Buffers;
using System.CodeDom;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ADOTabular.AdomdClientWrappers
{
    //Microsoft.Excel.AdomdClient.dll path logic from Microsoft.ReportingServices.AdHoc.Excel.Client.ExcelAdoMdConnections
    //Microsoft.Excel.AdomdClient.dll assembly loading improved over that approach
    public static class ExcelAdoMdConnections
    {
        internal delegate void VoidDelegate();
        internal delegate T ReturnDelegate<out T>();

        private static Assembly _excelAdomdClientAssembly;
        private static string _excelAdomdClientAssemblyPath;

        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern uint GetModuleFileName([In] IntPtr hModule, [Out] char[] lpFilename, [In, MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("Kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static string RetrieveAdomdClientAssemblyPath()
        {
            string directoryName = RetrieveAdomdAssemblyFolder();
            return Path.Combine(directoryName, "Microsoft.Excel.AdomdClient.dll");
        }

        public static string RetrieveAdomdAssemblyFolder()
        {
            try
            {
                return RetrieveAdomdAssemblyFolderInternal("msolap110_xl.dll");
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                try
                {
                    return RetrieveAdomdAssemblyFolderInternal("msolap_xl.dll");
                }
                catch
                {
                    return RetrieveAdomdAssemblyFolderInternal("msmdlocal_xl.dll");
                }
            }
        }

        public static string RetrieveAdomdAssemblyFolderInternal(string dllName)
        {
            //IntPtr moduleHandle = GetModuleHandle("msmdlocal_xl.dll");
            IntPtr moduleHandle = GetModuleHandle(dllName);
            if (moduleHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            int lpFilenameLen = 2048;
            //StringBuilder lpFilename = new StringBuilder(lpFilenameLen);
            string lpFilename = string.Empty;
            char[] buffer = ArrayPool<char>.Shared.Rent(lpFilenameLen);
            try
            {
                int len = buffer.Length;
                var result = GetModuleFileName(moduleHandle, buffer, lpFilenameLen);
                if (result == 0)
                {
                    int num3 = Marshal.GetLastWin32Error();
                    throw new Win32Exception(num3);
                }
                lpFilename = new string(buffer);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(buffer);
            }
                                    
            string directoryName = Path.GetDirectoryName(lpFilename.ToString());
            return directoryName;
        }

        public static Assembly ExcelAdomdClientAssembly
        {
            get
            {
                if (_excelAdomdClientAssembly == null)
                {
                    _excelAdomdClientAssembly = Assembly.LoadFrom(ExcelAdomdClientAssemblyPath);
                }
                return _excelAdomdClientAssembly;
            }
        }

        private static string ExcelAdomdClientAssemblyPath =>
            _excelAdomdClientAssemblyPath ??= RetrieveAdomdClientAssemblyPath();
    }
}
