using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using ADOTabular;
using System.Diagnostics;

namespace DaxStudio.ExcelAddin.Xmla
{
    class ExcelAmoWrapper
    {

        internal delegate void VoidDelegate();
        internal delegate T ReturnDelegate<T>();

        private static Assembly m_excelAmoAssembly;
        private static string m_excelAmoAssemblyPath;
        private static Assembly m_excelAmoCoreAssembly;
        private static string m_excelAmoCoreAssemblyPath;
        private static Assembly m_excelTabularAssembly;
        private static string m_excelTabularAssemblyPath;

        //[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        //private static extern uint GetModuleFileName([In] IntPtr hModule, [Out] StringBuilder lpFilename, [In, MarshalAs(UnmanagedType.U4)] int nSize);
        //[DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        //private static extern IntPtr GetModuleHandle(string lpModuleName);

        protected static string RetrieveExcelAmoAssemblyPath(string dllName)
        {
            string directoryName = ADOTabular.AdomdClientWrappers.ExcelAdoMdConnections.RetrieveAdomdAssemblyFolder();
            Log.Debug("{Class} {Method} dir: {directoryName}", "ExcelAmoWrapper", "RetrieveExcelAmoAssemblyPath", directoryName);
            var assPath = Path.Combine(directoryName, dllName);
            if (!File.Exists(assPath)) { 
                Log.Error(Common.Constants.LogMessageTemplate, nameof(ExcelAmoWrapper), nameof(RetrieveExcelAmoAssemblyPath), $"unable to find Microsoft.Excel.Amo.dll path: {assPath}");
                //TODO try using vfs path
                UpdatePathForVfs(ref assPath);
            }

            assPath = Path.Combine(@"C:\Program Files\Microsoft Office\root\vfs\ProgramFilesCommonX64\Microsoft Shared\Office16\DataModelv16", dllName);
            return assPath;
            
        }

        public static Assembly ExcelAmoAssembly
        {
            get
            {
                try
                {
                    if (m_excelAmoAssembly == null)
                    {
                        m_excelAmoAssembly = Assembly.LoadFrom(ExcelAmoAssemblyPath);
                    }
                    return m_excelAmoAssembly;
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading AMO from '{ExcelAmoAssemblyPath}' - {e.Message}");
                }

            }
        }


        public static Assembly ExcelAmoCoreAssembly
        {
            get
            {
                try
                {
                    if (m_excelAmoCoreAssembly == null)
                    {
                        m_excelAmoCoreAssembly = Assembly.LoadFrom(ExcelAmoCoreAssemblyPath);
                    }
                    return m_excelAmoCoreAssembly;
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading AMO from '{ExcelAmoCoreAssemblyPath}' - {e.Message}");
                }

            }
        }

        public static Assembly ExcelTabularAssembly
        {
            get
            {
                try
                {
                    if (m_excelTabularAssembly == null)
                    {
                        m_excelTabularAssembly = Assembly.LoadFrom(ExcelTabularAssemblyPath);
                    }
                    return m_excelTabularAssembly;
                }
                catch (Exception e)
                {
                    throw new Exception($"Error loading AMO from '{ExcelTabularAssemblyPath}' - {e.Message}");
                }

            }
        }


        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        protected static string ExcelAmoAssemblyPath
        {
            get
            {
                if (m_excelAmoAssemblyPath == null)
                {
                    try
                    {
                        m_excelAmoAssemblyPath = RetrieveExcelAmoAssemblyPath("Microsoft.Excel.Amo.dll");
                        Log.Debug("{class} {method} AssemblyPath: {path}", "ExcelAmoWrapper", "ExcelAdomdClientAssemblyPath", m_excelAmoAssemblyPath);
                    }
                    catch
                    {
                        Debug.Assert(false, "We should never stop here");
                    }
                }
                return m_excelAmoAssemblyPath;
            }
        }

        protected static string ExcelAmoCoreAssemblyPath
        {
            get
            {
                if (m_excelAmoCoreAssemblyPath == null)
                {
                    try
                    {
                        m_excelAmoCoreAssemblyPath = RetrieveExcelAmoAssemblyPath("Microsoft.Excel.Amo.Core.dll");
                        Log.Debug("{class} {method} AssemblyPath: {path}", nameof(ExcelAmoWrapper), "ExcelAmoCoreAssemblyPath", m_excelAmoCoreAssemblyPath);
                    }
                    catch
                    {
                        Debug.Assert(false, "We should never stop here");
                    }
                }
                return m_excelAmoCoreAssemblyPath;
            }
        }

        protected static string ExcelTabularAssemblyPath
        {
            get
            {
                if (m_excelTabularAssemblyPath == null)
                {
                    try
                    {
                        m_excelTabularAssemblyPath = RetrieveExcelAmoAssemblyPath("Microsoft.Excel.Tabular.dll");
                        Log.Debug("{class} {method} AssemblyPath: {path}", "ExcelAmoWrapper", "ExcelAdomdClientAssemblyPath", m_excelAmoAssemblyPath);
                    }
                    catch
                    {
                        Debug.Assert(false, "We should never stop here");
                    }
                }
                return m_excelTabularAssemblyPath;
            }
        }

        internal static void UpdatePathForVfs(ref string m_excelAmoAssemblyPath)
        {
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ExcelAmoWrapper), nameof(UpdatePathForVfs), $"Updating path for VFS: {m_excelAmoAssemblyPath}");
            if (m_excelAmoAssemblyPath == null) return;
            if (File.Exists(m_excelAmoAssemblyPath)) return;

            //var commonProgramsPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);

            //if(m_excelAmoAssemblyPath.StartsWith(commonProgramsPath))
            //{
            //    var vfsCommonPrograms = NativeMethods.GetProgramFilesCommonVfsPath();
            //    vfsCommonPrograms = @"C:\Program Files\Microsoft Office\root\vfs\ProgramFilesCommonX64\";
            //    Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ExcelAmoWrapper), nameof(UpdatePathForVfs), $"Common Programs Path: {commonProgramsPath}");
            //    Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ExcelAmoWrapper), nameof(UpdatePathForVfs), $"VFS Common Programs Path: {vfsCommonPrograms}");
            //    m_excelAmoAssemblyPath.Replace(commonProgramsPath, vfsCommonPrograms);
            //    Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ExcelAmoWrapper), nameof(UpdatePathForVfs), $"Updated path for VFS: {m_excelAmoAssemblyPath}");
            //}
        }
    }
}
