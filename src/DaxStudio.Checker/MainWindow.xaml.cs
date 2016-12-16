using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;


namespace DaxStudio.Checker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private List<Version> AdomdVersions = new List<Version>();
        private List<Version> AmoVersions = new List<Version>();
        private List<Version> NetVersions = new List<Version>();
        private Regex reVer;
        private string versionPattern = @"version=(?<ver>\d+\.\d+\.\d+\.\d+)";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void CheckADOMD()
        {
            this.Output.Text = this.Output.Text + "\n\nChecking ADOMD.NET\n";
            this.Output.Text = this.Output.Text + "=======================\n";
            string format = "Microsoft.AnalysisServices.adomdclient, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            for (int i = 11; i <= 13; i++)
            {
                try
                {
                    Assembly assembly = Assembly.Load(string.Format(format, i));
                    if (assembly != null)
                    {
                        this.Output.Text = this.Output.Text + "PASS > " + assembly.FullName + "\n";
                        string version = this.reVer.Match(assembly.FullName).Groups["ver"].Value;
                        this.AdomdVersions.Add(new Version(version));
                    }
                }
                catch (Exception exception)
                {
                    this.Output.Text = this.Output.Text + "FAIL > " + exception.Message + "\n";
                }
            }
        }

        private void CheckAMO()
        {
            this.Output.Text = this.Output.Text + "\n\nChecking AMO\n";
            this.Output.Text = this.Output.Text + "=======================\n";
            string format = "Microsoft.AnalysisServices, Version={0}.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91";
            for (int i = 11; i <= 13; i++)
            {
                try
                {
                    Assembly assembly = Assembly.Load(string.Format(format, i));
                    if (assembly != null)
                    {
                        this.Output.Text = this.Output.Text + "PASS > " + assembly.FullName + "\n";
                        string version = this.reVer.Match(assembly.FullName).Groups["ver"].Value;
                        this.AmoVersions.Add(new Version(version));
                    }
                }
                catch (Exception exception)
                {
                    this.Output.Text = this.Output.Text + "FAIL > " + exception.Message + "\n";
                }
            }
        }

        public void CheckDaxStudioBindings()
        {
            this.Output.Text = this.Output.Text + "\n";
            this.Output.Text = this.Output.Text + "Dax Studio Configuration\n";
            this.Output.Text = this.Output.Text + "========================\n";
            string str = (string)Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\DaxStudio", "Path", "");
            if (str == null)
            {
                this.Output.Text = this.Output.Text + "Dax Studio registry key not found.\n";
            }
            else
            {
                this.Output.Text = this.Output.Text + $"Path: {str}\n";
                if (str == "")
                {
                    this.Output.Text = this.Output.Text + "Dax Studio registry 'Path' value not found.\n";
                }
                else
                {
                    string configPath = this.GetConfigPath(str);
                    if (!File.Exists(configPath))
                    {
                        this.Output.Text = this.Output.Text + configPath + " file not found.";
                    }
                    else
                    {
                        this.ProcessConfigFile(configPath);
                    }
                }
            }
        }

        private void CheckNetFramework()
        {
            this.Output.Text = this.Output.Text + "\n\nChecking .Net Framework\n";
            this.Output.Text = this.Output.Text + "=======================\n";
            string name = @"SOFTWARE\Microsoft\NET Framework Setup\NDP";
            RegistryKey key = Registry.LocalMachine.OpenSubKey(name);
            StringBuilder builder = new StringBuilder();
            this.RecurseKeysForValue(name, key, "Version", builder);
            this.Output.Text = this.Output.Text + builder.ToString() + "\n";
        }

        private string GetConfigPath(string path)
        {
            if (path.EndsWith("\""))
            {
                return (path.TrimStart(new char[] { '"' }).TrimEnd(new char[] { '"' }) + ".config");
            }
            return (path + ".config");
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            base.Close();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                this.reVer = new Regex(this.versionPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                this.CheckNetFramework();
                this.CheckAMO();
                this.CheckADOMD();
                this.CheckDaxStudioBindings();
            }
            catch (Exception exception)
            {
                this.Output.Text = this.Output.Text + $"\n\nFATAL ERROR:\n{exception.Message}\n{exception.StackTrace}";
            }
        }

        private void ProcessConfigFile(string path)
        {
            this.Output.Text = this.Output.Text + "Processing DaxStudio.exe.config file...\n";
            this.Output.Text = this.Output.Text + $"Path: '{path}'\n";
            XmlDocument document = new XmlDocument();
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(document.NameTable);
            nsmgr.AddNamespace("asm", "urn:schemas-microsoft-com:asm.v1");
            new XmlReaderSettings();
            FileStream inStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            document.Load(inStream);
            XmlNodeList list = document.SelectNodes("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly", nsmgr);
            this.Output.Text = this.Output.Text + $"Bindings Found: {list.Count}\n";
            XmlNode node = document.SelectSingleNode("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly/asm:assemblyIdentity[@name='Microsoft.AnalysisServices']", nsmgr);
            if (node != null)
            {
                XmlNode nextSibling = node.NextSibling;
                this.Output.Text = this.Output.Text + $"AMO : {nextSibling.Attributes["newVersion"].Value}\n";
            }
            else
            {
                this.Output.Text = this.Output.Text + string.Format("AMO : 11.0.0.0 <default>\n", new object[0]);
            }
            if (document.SelectSingleNode("configuration/runtime/asm:assemblyBinding/asm:dependentAssembly/asm:assemblyIdentity[@name='Microsoft.AnalysisServices.AdomdClient']", nsmgr) != null)
            {
                XmlNode node4 = node.NextSibling;
                this.Output.Text = this.Output.Text + $"ADOMD : {node4.Attributes["newVersion"].Value}\n";
            }
            else
            {
                this.Output.Text = this.Output.Text + string.Format("ADOMD : 11.0.0.0 <default>\n", new object[0]);
            }
        }

        public void RecurseKeysForValue(string rootPath, RegistryKey key, string valueName, StringBuilder builder)
        {
            object obj2 = key.GetValue(valueName);
            if (obj2 != null)
            {
                builder.AppendLine(StripRootPath(key.Name, rootPath) + " -> " + obj2.ToString());
            }
            foreach (string str in key.GetSubKeyNames())
            {
                this.RecurseKeysForValue(rootPath, key.OpenSubKey(str), valueName, builder);
            }
        }

        public static string StripRootPath(string name, string rootPath) =>
            name.Substring((name.IndexOf(rootPath) + rootPath.Length) + 1);

        private void CopyToClipboardClick(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(this.Output.Text);
        }
    }
}
