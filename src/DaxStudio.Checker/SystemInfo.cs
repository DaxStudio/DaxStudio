using Microsoft.Win32;
using System;
using System.Management;
using System.Threading;

namespace DaxStudio.CheckerApp
{
    public static class SystemInfo
    {
        public static void OutputOSInfo(this System.Windows.Controls.RichTextBox output)
        {
            var osInfo = GetOSInfo();
            output.AppendIndentedLine($"OSCaption       = {osInfo.Name}");
            output.AppendIndentedLine($"OSRelease       = {osInfo.Release}");
            output.AppendIndentedLine($"OSVersion       = {osInfo.Version.ToString()}");
            output.AppendIndentedLine($"OSArchitecture  = {osInfo.Architecture}");
            output.AppendIndentedLine($"VisibleMemoryGB = {osInfo.TotalVisibleMemory.ToString("n2")}");
            output.AppendIndentedLine($"FreeMemoryGB    = {osInfo.TotalFreeMemory.ToString("n2")}");

        }

        public static void OutputCultureInfo(this System.Windows.Controls.RichTextBox output)
        {
            var curCulture = Thread.CurrentThread.CurrentCulture;
            //output.AppendRange("").Indent();
            output.AppendIndentedLine($"Culture Name              = {curCulture.Name}");
            output.AppendIndentedLine($"Culture DisplayName       = {curCulture.DisplayName}");
            output.AppendIndentedLine($"Culture EnglishName       = {curCulture.EnglishName}");
            output.AppendIndentedLine($"Culture 2-Letter ISO Name = {curCulture.TwoLetterISOLanguageName}");
            output.AppendIndentedLine($"Culture DecimalSeparator  = {curCulture.NumberFormat.NumberDecimalSeparator}");
            output.AppendIndentedLine($"Culture GroupSeparator    = {curCulture.NumberFormat.NumberGroupSeparator}");
            output.AppendIndentedLine($"Culture CurrencySymbol    = {curCulture.NumberFormat.CurrencySymbol}");
            output.AppendIndentedLine($"Culture ShortDatePattern  = {curCulture.DateTimeFormat.ShortDatePattern}");
        }

        private static OSInfo GetOSInfo()
        {

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var result = new OSInfo();
            foreach (ManagementObject os in searcher.Get())
            {
                result.Name = os["Caption"].ToString();
                result.Version = Version.Parse(os["Version"].ToString());
                result.Architecture = "32 bit";
                if (result.Version.Major > 5) result.Architecture = os["OSArchitecture"].ToString();
                continue;
            }

            searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                result.TotalVisibleMemory = long.Parse(os["TotalVisibleMemorySize"].ToString()).KbToGb();
                result.TotalFreeMemory = long.Parse(os["FreePhysicalMemory"].ToString()).KbToGb();

            }

            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "")?.ToString();
            if (string.IsNullOrEmpty(releaseId)) releaseId = "<Unknown>";
            result.Release = releaseId;


            return result;
        }

        private struct OSInfo
        {
            public string Name;
            public Version Version;
            public string Architecture;
            public decimal TotalVisibleMemory;
            public decimal TotalFreeMemory;
            public string Release;
        }

        public static decimal KbToGb(this long bytes)
        {
            return (decimal)bytes / (1024 * 1024);
        }
    }

}
