using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace DaxStudio.Checker
{

    public enum ExcelVersions
    {
        Excel_2010 = 13,
        Excel_2013 = 14,
        Excel_2016 = 15,
        Excel_v16  = 16,  // Future Version
        Excel_v17  = 17,
    }

    public class Checker
    {
        private List<Version> AdomdVersions = new List<Version>();
        private List<Version> AmoVersions = new List<Version>();
        private List<Version> NetVersions = new List<Version>();
        private Regex reVer = new Regex(@"version=(?<ver>\d+\.\d+\.\d+\.\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const int minMSLibVer = 13;
        private const int maxMSLibVer = 13;
        private RichTextBox _output;
        private const string DEFAULT_DAX_STUDIO_PATH = @"c:\Program Files\DAX Studio\DAXStudio.exe";

       

        #region Public Properties
        public RichTextBox Output { get { return _output; } }
        #endregion

        #region Constructor
        public Checker(RichTextBox output)
        {
            _output = output;
        }
        #endregion

        #region Menu Functions
        internal void SetFusionLoggingState(MenuItem menuItem)
        {
            var isFusionLogEnabled = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Fusion", "LogFailures", 0) as int?;
            menuItem.IsChecked = isFusionLogEnabled != 0;
        }

        internal void SetVSTOLoggingState(MenuItem menuItem)
        {
            menuItem.IsChecked = (Environment.GetEnvironmentVariable("VSTO_SUPPRESSDISPLAYALERTS") == "0");
        }

        internal void ToggleVSTOLogging(bool isChecked)
        {
            // ren SET from cmdline
            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = string.Format("/C \"SETX VSTO_SUPPRESSDISPLAYALERTS {0}\"", isChecked ? "0" : "\"\"");
            var proc = Process.Start(startInfo);
            proc.WaitForExit(); // block until the process exits
            if (proc.ExitCode != 0) MessageBox.Show($"An Error occurred while setting VSTO_SUPPRESSDISPLAYALERTS environment variable. You may have to try setting this manually"); 
        }

        internal void ToggleFusionLogging(bool isChecked)
        {
            // run reg process
            //var currentPath = Assembly.GetEntryAssembly().Location;
            var tempPath = Path.GetTempPath();
            var fusionPath = Path.Combine(tempPath, "Fusion\\");
            var regPath = Path.Combine(tempPath, "DaxStudioFusionLogging.reg");
            Directory.CreateDirectory(fusionPath);

            // write reg file
            //var regFile = File.Open(regPath, FileMode.OpenOrCreate);
            var sw = File.CreateText(regPath);
            sw.WriteLine(@"Windows Registry Editor Version 5.00");
            sw.WriteLine();
            sw.WriteLine(@"[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Fusion]");
            sw.WriteLine(string.Format(@"""LogFailures""=dword:0000000{0}", isChecked ? "1" : "0"));
            sw.WriteLine($@"""LogPath""=""{fusionPath.Replace("\\", "\\\\")}""");
            sw.Close();

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.Verb = "runas";
            startInfo.FileName = "reg.exe";
            startInfo.Arguments = $"IMPORT {regPath}";

            var proc = Process.Start(startInfo);
            proc.WaitForExit();

            File.Delete(regPath);

            if (proc.ExitCode != 0) MessageBox.Show("An error occurred while trying to enable fusion logging");
        }

        public void OpenFusionLogFolder()
        {
            var tempPath = Path.GetTempPath();
            var fusionPath = Path.Combine(tempPath, "Fusion");

            var startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true;
            startInfo.FileName = "explorer.exe";
            startInfo.Arguments = fusionPath;
            Process.Start(startInfo);

        }

        #endregion

        #region Top Level Functions

        internal void ShowVersionInfo()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Output.AppendIndentedLine($"Version =  v{version}");
        }

        public void CheckOSInfo()
        {
            Output.AppendHeaderLine("Checking Operating System");
            Output.AppendLine("=======================");
            //Output.AppendRange("").Indent(20);
            Output.OutputOSInfo();
            Output.AppendLine();
            Output.OutputCultureInfo();
        }
        public void CheckLibrary(string shortName, string longNameFormat)
        {
            //"Microsoft.AnalysisServices.AdomdClient, Version = 13.0.0.0, Culture = neutral, PublicKeyToken = 89845dcd8080cc91"
            Output.AppendHeaderLine($"Checking {shortName} (GAC)");
            Output.AppendLine("=======================");

            for (int i = minMSLibVer; i <= maxMSLibVer + 2; i++)
            {
                try
                {
                    Assembly assembly = Assembly.Load(string.Format(longNameFormat, i));
                    if (assembly != null)
                    {
                        //Output.Indent();
                        Output.AppendRange("    PASS > " ).Color("Green").Bold();
                        Output.AppendLine(assembly.FullName);
                        
                        string version = this.reVer.Match(assembly.FullName).Groups["ver"].Value;
                        AdomdVersions.Add(new Version(version));
                    }
                }
                catch (Exception exception)
                {
                    //Output.Indent();
                    var result = i == minMSLibVer ? "    FAIL" : "    WARN";
                    var color = i == minMSLibVer ? "Red" : "Orange";
                    Output.AppendRange($"{result} > ").Color(color).Bold();
                    Output.AppendLine(exception.Message);
                }
            }
        }

        public void CheckLocalLibrary(string shortName, string relativeFilename)
        {
            Output.AppendHeaderLine($"Checking {shortName} (Local)");
            Output.AppendLine("=======================");
            var fullPath = Path.GetFullPath(relativeFilename);
            Output.AppendRange("    Attempting to load: ");
            Output.AppendLine(fullPath);
            try
            {
                Assembly assembly = Assembly.LoadFile(fullPath);
                if (assembly != null)
                {
                    //Output.Indent();
                    Output.AppendRange("    PASS > ").Color("Green").Bold();
                    Output.AppendLine(assembly.FullName);

                    string version = this.reVer.Match(assembly.FullName).Groups["ver"].Value;
                    AdomdVersions.Add(new Version(version));
                }
            }
            catch (Exception exception)
            {
                //Output.Indent();
                var result =  "    FAIL" ;
                var color = "Red";
                Output.AppendRange($"{result} > ").Color(color).Bold();
                Output.AppendLine(exception.Message);
            }
        }

        public void CheckDaxStudioBindings()
        {
            Output.AppendHeaderLine("Dax Studio Configuration");
            Output.AppendLine("========================");
            string str = TryGetPathFromRegistry();
            if (str == null)
            {
                Output.AppendRange("      WARN > ").Bold().Color("Orange");
                Output.AppendLine("Dax Studio registry key not found.");
                str = DEFAULT_DAX_STUDIO_PATH;
                Output.AppendIndentedLine($"  Attempting to use default installation path: {str}");
            }
            else
            {
                Output.AppendIndentedLine($"Path: {str}");
                if (str == "")
                {
                    Output.AppendRange("      WARN > ").Bold().Color("Orange");
                    Output.AppendLine("Dax Studio registry 'Path' value not found.");
                    str = DEFAULT_DAX_STUDIO_PATH;

                    string path = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
                    var directory = Path.GetDirectoryName(path);
                    str = Path.Combine(directory, "daxstudio.exe");
                    Output.AppendIndentedLine($"  Attempting to use current path: {str}");
                }

            }
            string configPath = this.GetConfigPath(str);
            if (!File.Exists(configPath))
            {
                Output.AppendIndentedLine(configPath + " file not found.");
            }
            else
            {
                ProcessConfigFile(configPath);
            }
        }

        private static string TryGetPathFromRegistry()
        {
            // get the registry value from either 32 or 64 bit hive
            var val = RegistryHelpers.GetRegValue(@"SOFTWARE\DaxStudio","Path" ,RegistryHive.LocalMachine);
            if (val == null) return string.Empty;
            return val.ToString();
        }

        public void CheckNetFramework()
        {
            Output.AppendHeaderLine("Checking .Net Framework");
            Output.AppendLine("=======================");
            //Output.AppendRange("").Indent(20);
            string name = @"SOFTWARE\Microsoft\NET Framework Setup\NDP";
            RegistryKey key = Registry.LocalMachine.OpenSubKey(name);
            RecurseKeysForValue(name, key, "Version");

        }

        public void CheckExcelAddin()
        {
            Output.AppendHeaderLine("Checking Excel Add-in");
            Output.AppendLine("=======================");
            // TODO
            var xlVer = GetCurrentExcelVersion();
            Output.AppendIndentedLine($"Detected Excel Version: {xlVer} - {(ExcelVersions)xlVer}");

            var excelBitness = GetExcelDetails();
            Output.AppendLine();

            // check registry entries
            string name = @"SOFTWARE\Microsoft\Office\Excel\Addins\DaxStudio.ExcelAddIn";
            RegistryKey baseKey= RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            switch (excelBitness)
            {
                case MachineType.x64:
                    baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    break;
                case MachineType.x86:
                    baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                    break;
                default:
                    Output.AppendRange("      ERROR:").Color("Red").Bold();
                    Output.AppendLine($" Unsupported Excel Architecture {excelBitness}");
                    break;
            }

            RegistryKey key = baseKey.OpenSubKey(name); //Registry.LocalMachine.OpenSubKey(name);

            Output.AppendIndentedLine("DAX Studio Excel Add-in Registry keys");
            if (key == null)
            {
                Output.AppendRange("      ERROR:").Color("Red").Bold();
                Output.AppendLine(" DAX Studio Excel addin registry keys not found!");
            }
            else
                PrintSubkeyValues(key);
            Output.AppendLine();
            // check that add-in is not hard disabled
            ListDisabledExcelAddins("2010", 14);

            ListDisabledExcelAddins("2013", 15);

            ListDisabledExcelAddins("2016", 16);

            Output.AppendLine();
            CheckForPowerPivotAddin();

            Output.AppendLine();
            TestLoadingOfExcelAddin();

            // Check VSTO
            CheckVSTO();
        }

        private void CheckForPowerPivotAddin()
        {
            var addinKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Office\Excel\Addins");
            if (addinKey != null)
            {
                var subKeys = addinKey.GetSubKeyNames();
                string addinName = "";
                for (int i = 0; i < subKeys.Length;i++)
                {
                    var subkey = addinKey.OpenSubKey(subKeys[i]);
                    addinName = subkey.GetValue("FriendlyName")?.ToString();
                    if (addinName != null && addinName.IndexOf("Power Pivot") > 0)
                    {
                        Output.AppendRange("      PASS > ").Color("Green").Bold();
                        Output.AppendLine($" Found Excel Addin: {addinName}");
                        break;
                    }
                }
                if (string.IsNullOrEmpty(addinName))
                {
                    Output.AppendRange("      ERROR > ").Color("Red").Bold();
                    Output.AppendLine(" could not locate the Excel Power Pivot add-in");
                }

            }

        }

        private void CheckVSTO()
        {
            Output.AppendLine();
            Output.AppendIndentedLine("Checking VSTO Configuration");
            
            RegistryKey basekey;
            RegistryKey key;
            var keyPath = @"SOFTWARE\WOW6432Node\Microsoft\VSTO Runtime Setup"; 
            var is64 = true;
            basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            if (basekey == null)
            {
                is64 = false;
                basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                keyPath = @"SOFTWARE\Microsoft\VSTO Runtime Setup";
            }
            var arch = is64 ? "x64" : "x86";
            Output.AppendIndentedLine($"Architecture: {arch}");
            key = basekey.OpenSubKey(keyPath);

            //            Output.AppendRange("").Indent(20);
            if (key == null)
            {
                Output.AppendRange("      WARN > ").Bold().Color("Orange");
                Output.AppendLine($"Unable to open {keyPath}");
            }
            else
            {
                foreach (var subkeyName in key.GetSubKeyNames())
                {
                    var subkey = key.OpenSubKey(subkeyName);
                    Output.AppendIndentedLine($"  {subkeyName}");
                    foreach (var valName in subkey.GetValueNames())
                    {
                        Output.AppendIndentedLine($"    {valName} {subkey.GetValue(valName)}");
                    }
                }
            }
        }

        #endregion

        #region Helper Functions

        private MachineType GetExcelDetails()
        {
            var appPath = (string)Registry.GetValue(  @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe",null,"");
            Output.AppendIndentedLine($"Excel Path: {appPath}");
            var excelArch = GetMachineType(appPath);
            Output.AppendIndentedLine($"Excel Architecture: {excelArch}");
            return excelArch;
        }

        public enum MachineType
        {
            Native = 0, x86 = 0x014c, Itanium = 0x0200, x64 = 0x8664
        }

        public static MachineType GetMachineType(string fileName)
        {
            const int PE_POINTER_OFFSET = 60;
            const int MACHINE_OFFSET = 4;
            byte[] data = new byte[4096];
            using (Stream s = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                s.Read(data, 0, 4096);
            }
            // dos header is 64 bytes, last element, long (4 bytes) is the address of the PE header
            int PE_HEADER_ADDR = BitConverter.ToInt32(data, PE_POINTER_OFFSET);
            int machineUint = BitConverter.ToUInt16(data, PE_HEADER_ADDR + MACHINE_OFFSET);
            return (MachineType)machineUint;
        }

        private void TestLoadingOfExcelAddin()
        {
            Output.AppendIndentedLine("Attempting to load Excel Add-in: ");
            var path = GetExcelAddinLocation();
            if (string.IsNullOrWhiteSpace(path))
            {
                Output.AppendRange("      ERROR:").Color("Red").Bold();
                Output.AppendLine(" could not locate the Excel add-in location in the registry");
            }
            else
            {
                try
                {
                    var ass = Assembly.LoadFile(path);
                    Output.AppendRange("      PASS >").Bold().Color("Green");
                    Output.AppendLine($" Loaded Excel Add-in: {ass.FullName}");
                }
                catch (Exception ex)
                {
                    Output.AppendRange("      ERROR >").Bold().Color("Red");
                    Output.AppendLine($" Loading Excel Add-in - {ex.Message}\r\r{ex.StackTrace}");
                }
            }
        }

        private string GetConfigPath(string path)
        {
            if (path.EndsWith("\""))
            {
                return (path.TrimStart(new char[] { '"' }).TrimEnd(new char[] { '"' }) + ".config");
            }
            return (path + ".config");
        }
        private string GetExcelAddinLocation()
        {
            string keyPath = @"Software\Microsoft\Office\Excel\Addins\DaxStudio.ExcelAddIn";
            string manifest = string.Empty;
            RegistryKey key;

            if (Environment.Is64BitProcess)
            {
                // check 32 & 64 bit keys
                var basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                key = basekey.OpenSubKey(keyPath);
                if (key == null)
                {
                    // if no 32 bit entry is found look for the 64 bit version
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                    key = basekey.OpenSubKey(keyPath);
                }

            }
            else
            {
                // use the default registry base key
                key = Registry.LocalMachine.OpenSubKey(keyPath);
            }

            if (key != null)
            {
                manifest = (string)key.GetValue("Manifest");
                manifest = manifest.Split('|')[0];
                manifest = manifest.Replace("file:///", "");
                manifest = manifest.Replace(".vsto", ".dll");
            }

            return manifest;
        }

        private void ListDisabledExcelAddins(string versionYear, int versionNumber)
        {
            Output.AppendIndentedLine($"Checking for Excel {versionYear} Disabled Add-ins");
            var keyName = $"Software\\Microsoft\\Office\\{versionNumber}.0\\Excel\\Resiliency\\DisabledItems";
            var key = Registry.CurrentUser.OpenSubKey(keyName);
            if (key != null)
            {
                foreach (var valName in key.GetValueNames())
                {
                    byte[] val = (byte[])key.GetValue(valName);
                    var str = System.Text.Encoding.Unicode.GetString(val);
                    str = str.Substring(6, str.Length - 9); // trim off first 5 and last 4 chars;
                    if (str.Contains("DaxStudio.vsto"))
                        Output.AppendRange("      FAIL > ").Bold().Color("Red");
                    else
                        Output.AppendRange("      N/A  > ").Bold().Color("Orange");
                    
                    Output.AppendLine($" - {str}");
                }
            }
            else
            {
                Output.AppendRange("      PASS >").Bold().Color("Green");
                Output.AppendLine(" No Disabled items found.");
            }

        }

        private void ProcessConfigFile(string path)
        {
            Output.AppendIndentedLine("Processing DaxStudio.exe.config file...");
            Output.AppendIndentedLine($"Path: '{path}'");
            XmlDocument document = new XmlDocument();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
            nsmgr.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");
            new XmlReaderSettings();
            FileStream inStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            document.Load(inStream);
            XmlNodeList list = document.SelectNodes("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly", nsmgr);
            Output.AppendIndentedLine( $"Bindings Found: {list.Count}");

            XmlNode node = document.SelectSingleNode("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly/asm:assemblyIdentity[@name='Microsoft.AnalysisServices.Core']", nsmgr);
            GetBindingRedirect(document, nsmgr, node, "AMO");
            node = document.SelectSingleNode("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly/asm:assemblyIdentity[@name='Microsoft.AnalysisServices.AdomdClient']", nsmgr);
            GetBindingRedirect(document, nsmgr, node, "ADOMD");
        }

        private void GetBindingRedirect(XmlDocument document, XmlNamespaceManager nsmgr, XmlNode node, string libraryName)
        {
            if (node != null)
            {
                XmlNode bindingNode = node.NextSibling;
                Output.AppendIndentedLine($"{libraryName} : {bindingNode.Attributes["newVersion"].Value}");
            }
            else
            {
                Output.AppendIndentedLine($"{libraryName} : <default>");
            }
        }

        public void RecurseKeysForValue(string rootPath, RegistryKey key, string valueName)
        {
            object obj2 = key.GetValue(valueName);
            if (obj2 != null)
            {
                Output.AppendIndentedLine(StripRootPath(key.Name, rootPath) + " -> " + obj2.ToString());
            }
            foreach (string str in key.GetSubKeyNames())
            {
                RecurseKeysForValue(rootPath, key.OpenSubKey(str), valueName);
            }
        }

        public void PrintSubkeyValues(RegistryKey key)
        {
            foreach (string valName in key.GetValueNames())
            {
                object val = key.GetValue(valName);
                Output.AppendIndentedLine($"   {valName}: {val.ToString()}");
            }
        }

        public int GetCurrentExcelVersion()
        {
            string excelApp= (string)Registry.GetValue(@"HKEY_CLASSES_ROOT\Excel.Application\CurVer", null, "Excel.Application.0");
            return int.Parse(excelApp.Replace("Excel.Application.", ""));
        }

        public static string StripRootPath(string name, string rootPath) =>
            name.Substring((name.IndexOf(rootPath) + rootPath.Length) + 1);

        #endregion
    }

}
