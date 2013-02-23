using System;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DaxStudio.AdomdClientWrappers
{
    //Microsoft.Excel.AdomdClient.dll path logic from Microsoft.ReportingServices.AdHoc.Excel.Client.ExcelAdoMdConnections
    //Microsoft.Excel.AdomdClient.dll assembly loading improved over that approach
    public class ExcelAdoMdConnections
    {
        internal delegate void VoidDelegate();
        internal delegate T ReturnDelegate<T>();

        private static Assembly m_excelAdomdClientAssembly;
        private static string m_excelAdomdClientAssemblyPath;

        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern uint GetModuleFileName([In] IntPtr hModule, [Out] StringBuilder lpFilename, [In, MarshalAs(UnmanagedType.U4)] int nSize);
        [DllImport("Kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        protected static string RetrieveAdomdClientAssemblyPath()
        {
            //IntPtr moduleHandle = GetModuleHandle("msmdlocal_xl.dll");
            IntPtr moduleHandle = GetModuleHandle("msolap110_xl.dll");
            if (moduleHandle == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
            StringBuilder lpFilename = new StringBuilder(0x400);
            if (GetModuleFileName(moduleHandle, lpFilename, lpFilename.Capacity) == 0)
            {
                int num3 = Marshal.GetLastWin32Error();
                throw new Win32Exception(num3);
            }
            string directoryName = Path.GetDirectoryName(lpFilename.ToString());
            return Path.Combine(directoryName, "Microsoft.Excel.AdomdClient.dll");
        }

        public static Assembly ExcelAdomdClientAssembly
        {
            get
            {
                if (m_excelAdomdClientAssembly == null)
                {
                    m_excelAdomdClientAssembly = Assembly.LoadFrom(ExcelAdomdClientAssemblyPath);
                }
                return m_excelAdomdClientAssembly;
            }
        }

        protected static string ExcelAdomdClientAssemblyPath
        {
            get
            {
                if (m_excelAdomdClientAssemblyPath == null)
                {
                    m_excelAdomdClientAssemblyPath = RetrieveAdomdClientAssemblyPath();
                }
                return m_excelAdomdClientAssemblyPath;
            }
        }

    }
}
