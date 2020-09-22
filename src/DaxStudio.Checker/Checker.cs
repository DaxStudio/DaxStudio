using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace DaxStudio.CheckerApp
{

    public enum ExcelVersion
    {
        Excel2010 = 13,
        Excel2013 = 14,
        Excel2016 = 15,
        Excelv16  = 16,  // Future Version
        Excelv17  = 17,
    }

    public class Checker
    {
        private readonly List<Version> AdomdVersions = new List<Version>();

        private Regex reVer = new Regex(@"version=(?<ver>\d+\.\d+\.\d+\.\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const int minMSLibVer = 13;
        private const int maxMSLibVer = 13;
        private RichTextBox _output;
        private Version RequiredMinumumNetVersion = new Version(4,7,1);
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
            startInfo.Arguments = $"/C \"SETX VSTO_SUPPRESSDISPLAYALERTS {(isChecked ? "0" : "\"\"")}\"";
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
            sw.WriteLine($@"""LogFailures""=dword:0000000{(isChecked ? "1" : "0")}");
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

        public static void OpenFusionLogFolder()
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

            var uiPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daxstudio.exe");
            var uiAss = Assembly.LoadFile(uiPath);
            var uiVersion = uiAss.GetName().Version;
            Output.AppendIndentedLine($"DaxStudio.exe Version =  v{uiVersion}");
        }

        public void CheckOSInfo()
        {
            Output.AppendHeaderLine("Checking Operating System");

            //Output.AppendRange("").Indent(20);
            Output.OutputOSInfo();
            Output.AppendLine();
            Output.OutputCultureInfo();
        }

        public void CheckScreenInfo()
        {
            Output.AppendHeaderLine("Checking Displays");

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                Output.AppendLine($"Display: {screen.DeviceName} {(screen.Primary ? "(Primary)" : "")} ");
                Output.AppendLine($"         X: {screen.Bounds.X} Y: {screen.Bounds.Y} Width: {screen.Bounds.Width} Height: {screen.Bounds.Height}");

            }

            float dpiX, dpiY;
            using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                dpiX = graphics.DpiX;
                dpiY = graphics.DpiY;
            }
            Output.AppendLine($"DPI X: {dpiX} Y: {dpiY} (Scaling: {dpiX / 96:0.0%})");
        }
        public void CheckLibrary(string shortName, string longNameFormat)
        {
            //"Microsoft.AnalysisServices.AdomdClient, Version = 13.0.0.0, Culture = neutral, PublicKeyToken = 89845dcd8080cc91"
            Output.AppendHeaderLine($"Checking {shortName} (GAC)");

            for (int i = minMSLibVer; i <= maxMSLibVer + 2; i++)
            {
                CheckLibraryExact(string.Format(System.Globalization.CultureInfo.InvariantCulture, longNameFormat, i), i == minMSLibVer);
            }
        }

        public void CheckLibraryExact(string assemblyName, bool failOnError)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyName);
                if (assembly != null)
                {
                    //Output.Indent();
                    Output.AppendRange("    PASS > ").Color("Green").Bold();
                    Output.AppendLine(assembly.FullName);
                    Output.AppendLine("           " + assembly.Location);
                    string version = this.reVer.Match(assembly.FullName).Groups["ver"].Value;
                    AdomdVersions.Add(new Version(version));
                }
            }
            catch (Exception exception)
            {
                //Output.Indent();
                var result = failOnError ? "    FAIL" : "    WARN";
                var color = failOnError ? "Red" : "Orange";
                Output.AppendRange($"{result} > ").Color(color).Bold();
                Output.AppendLine(exception.Message);
            }
        }



        public void CheckLocalLibrary(string shortName, string relativeFilename)
        {
            Output.AppendHeaderLine($"Checking {shortName} (Local)");

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
                var result = "    FAIL";
                var color = "Red";
                Output.AppendRange($"{result} > ").Color(color).Bold();
                Output.AppendLine(exception.Message);
            }
        }

        public void CheckDaxStudioBindings()
        {
            Output.AppendHeaderLine("Dax Studio Configuration");

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
                if (string.IsNullOrEmpty(str))
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
            string configPath = GetConfigPath(str);
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
            var val = RegistryHelpers.GetRegValue(@"SOFTWARE\DaxStudio", "Path", RegistryHive.LocalMachine);
            if (val == null) return string.Empty;
            return val.ToString();
        }

        public void CheckNetFramework()
        {
            Output.AppendHeaderLine("Checking .Net Framework");
            
            //Output.AppendRange("").Indent(20);
            string name = @"SOFTWARE\Microsoft\NET Framework Setup\NDP";
            RegistryKey key = Registry.LocalMachine.OpenSubKey(name);
            RecurseKeysForValue(name, key, "Version");

            Output.AppendLine("");
            string name45 = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full";
            RegistryKey key45 = Registry.LocalMachine.OpenSubKey(name45);
            var rel = key45.GetValue("Release");
            string versionString = null;
            if (rel != null) {
                var relNumber = (int)rel;
                versionString = Get45PlusVersion(relNumber);
            }
            Output.AppendIndentedLine($@"v4\Full\Release = {rel} ({versionString??"<Unknown>"})");

            if (!string.IsNullOrEmpty(versionString))
            {
                Version v45PlusVersion = Version.Parse(versionString);
                if (v45PlusVersion < RequiredMinumumNetVersion)
                {
                    Output.AppendIndentedLine($"ERROR > .Net Version {versionString} is lower than the required minimum ({RequiredMinumumNetVersion.ToString(3)})", "Red").Bold();
                }
                else
                {
                    Output.AppendIndentedLine($"PASS > .Net Version {versionString} is above the required minimum", "Green").Bold();
                }
            }
        }

        // Sourced from: CheckFor45PlusVersion function at
        // https://github.com/jmalarcon/DotNetVersions/blob/7bb9069d239d63ddc71ca7dba2eb44cde93248f0/Program.cs#L138

        private string Get45PlusVersion(int releaseKey)
        {
            if (releaseKey >= 528040)
                return "4.8";
            if (releaseKey >= 461808)
                return "4.7.2";
            if (releaseKey >= 461308)
                return "4.7.1";
            if (releaseKey >= 460798)
                return "4.7";
            if (releaseKey >= 394802)
                return "4.6.2";
            if (releaseKey >= 394254)
                return "4.6.1";
            if (releaseKey >= 393295)
                return "4.6";
            if (releaseKey >= 379893)
                return "4.5.2";
            if (releaseKey >= 378675)
                return "4.5.1";
            if (releaseKey >= 378389)
                return "4.5";
            // This code should never execute. A non-null release key should mean
            // that 4.5 or later is installed.
            return "";
        }

        public void CheckExcelAddin()
        {
            Output.AppendHeaderLine("Checking Excel Add-in");

            // Get Excel Version
            var xlVer = GetCurrentExcelVersion();
            Output.AppendIndentedLine($"Detected Excel Version: {xlVer} - {(ExcelVersion)xlVer}");

            var excelBitness = GetExcelDetails();
            Output.AppendLine();

            // check registry entries
            string keyName = @"SOFTWARE\Microsoft\Office\Excel\Addins\DaxStudio.ExcelAddIn";
            RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            try {


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

                RegistryKey xlKey = baseKey.OpenSubKey(keyName); //Registry.LocalMachine.OpenSubKey(name);
                try
                {
                    Output.AppendIndentedLine("DAX Studio Excel Add-in Registry keys");
                    if (xlKey == null)
                    {
                        Output.AppendRange("      ERROR:").Color("Red").Bold();
                        Output.AppendLine(" DAX Studio Excel addin registry keys not found!");
                    }
                    else
                        PrintSubkeyValues(xlKey);
                }
                finally
                {
                    xlKey?.Dispose();
                }
            }
            finally {
                baseKey.Dispose();
            }
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
                for (int i = 0; i < subKeys.Length; i++)
                {
                    Debug.WriteLine($"Processing Excel Addin Subkey {i}");
                    var subkey = addinKey.OpenSubKey(subKeys[i]);

                    addinName = subkey.GetValue("FriendlyName")?.ToString();
                    if (addinName != null && addinName.IndexOf("Power Pivot", StringComparison.InvariantCultureIgnoreCase) > 0)
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
            try
            {
                if (basekey == null)
                {
                    is64 = false;
                    basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                    keyPath = @"SOFTWARE\Microsoft\VSTO Runtime Setup";
                }
                var arch = is64 ? "x64" : "x86";
                Output.AppendIndentedLine($"Architecture: {arch}");
                key = basekey.OpenSubKey(keyPath);
                try
                {
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
                                Output.AppendIndentedLine($"    {valName} {subkey.GetValue(valName, "")}");
                            }
                        }
                    }
                }
                finally
                {
                    key.Dispose();
                }
            }
            finally
            {
                basekey.Dispose();
            }
        }

        #endregion

        #region Helper Functions

        private MachineType GetExcelDetails()
        {
            var appPath = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\excel.exe", null, "");
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

        private static string GetConfigPath(string path)
        {
            if (path.EndsWith("\"", StringComparison.InvariantCultureIgnoreCase))
            {
                return (path.TrimStart(new char[] { '"' }).TrimEnd(new char[] { '"' }) + ".config");
            }
            return (path + ".config");
        }
        private static string GetExcelAddinLocation()
        {
            string keyPath = @"Software\Microsoft\Office\Excel\Addins\DaxStudio.ExcelAddIn";
            string manifest = string.Empty;
            RegistryKey key;

            if (Environment.Is64BitProcess)
            {
                RegistryKey basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32); ;
                // check 32 & 64 bit keys
                try {
                    // try the 32bit key 
                    key = basekey.OpenSubKey(keyPath);
                    if (key == null)
                    {
                        // if no 32 bit entry is found look for the 64 bit version
                        basekey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                        key = basekey.OpenSubKey(keyPath);
                    }
                }
                finally
                {
                    basekey.Dispose();
                }

            }
            else
            {
                // use the default registry base key for 32 bit
                key = Registry.LocalMachine.OpenSubKey(keyPath);
            }

            if (key != null)
            {
                manifest = (string)key.GetValue("Manifest");
                manifest = manifest.Split('|')[0];
                manifest = manifest.Replace("file:///", "");
                manifest = manifest.Replace(".vsto", ".dll");
                key.Dispose();
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
            XmlDocument document = new XmlDocument() { XmlResolver = null };
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
            nsmgr.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");
            //new XmlReaderSettings();
            using (FileStream inStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (XmlReader reader = XmlReader.Create(inStream, new XmlReaderSettings() { XmlResolver = null }))
            {

                document.Load(reader);
            }
            XmlNodeList list = document.SelectNodes("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly", nsmgr);
            Output.AppendIndentedLine($"Bindings Found: {list.Count}");

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

        internal void RecurseKeysForValue(string rootPath, RegistryKey key, string valueName)
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

        internal void PrintSubkeyValues(RegistryKey key)
        {
            foreach (string valName in key.GetValueNames())
            {
                object val = key.GetValue(valName);
                Output.AppendIndentedLine($"   {valName}: {val.ToString()}");
            }
        }

        internal static int GetCurrentExcelVersion()
        {
            string excelApp = (string)Registry.GetValue(@"HKEY_CLASSES_ROOT\Excel.Application\CurVer", null, "Excel.Application.0");
            return int.Parse(excelApp.Replace("Excel.Application.", ""), CultureInfo.InvariantCulture);
        }

        internal static string StripRootPath(string name, string rootPath) =>
            name.Substring((name.IndexOf(rootPath, StringComparison.InvariantCultureIgnoreCase) + rootPath.Length) + 1);

        internal string GetCurrentPath()
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            return Path.GetDirectoryName(path);
        }

        internal bool IsInPortableMode() {
            var directory = GetCurrentPath();

            // check for .portable file in bin folder
            var portableFile = Path.Combine(directory, @"bin\.portable");
            if (File.Exists(portableFile)) return true;

            // check for .portable file in current folder
            portableFile = Path.Combine(directory, @".portable");
            if (File.Exists(portableFile)) return true;

            // else return false
            return false;
        }

        public void CheckSettings()
        {
            if (IsInPortableMode())
            {
                CheckPortableSettings();
            }
            else
            {
                CheckRegistrySettings();
            }
        }

        internal void CheckRegistrySettings()
        {
            Output.AppendHeaderLine("Registry Settings");

            var daxStudioKey = Registry.CurrentUser.OpenSubKey(@"Software\DaxStudio");
            if (daxStudioKey == null)
            {
                Output.AppendIndentedLine($"WARNING: No Registry settings found", "Orange");
                return;
            }

            var names = daxStudioKey.GetValueNames().OrderBy(n => n);
            foreach (var settingName in names)
            {
                var settingValue = daxStudioKey.GetValue(settingName);
                Output.AppendIndentedLine($"{settingName} = {settingValue.ToString()}");
            }
        }

        internal void CheckPortableSettings()
        {
            Output.AppendHeaderLine("Portable Settings");
            var settingsJson = File.ReadAllText(Path.Combine(GetCurrentPath(), "settings.json"));
            // todo - deserialize json
            Output.AppendIndentedLine(settingsJson);
        }

        #endregion
    }

}
