using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using System.Management;
using System.Threading;
using System.Reflection;
using Microsoft.Win32;
using System.Globalization;
using DaxStudio.Common;

namespace DaxStudio.UI.Utils
{
    public static class SystemInfo
    {
        private static OSInfo osInfo;
        private static Version version;
        private static CultureInfo curCulture;
        public static void WriteToLog()
        {
            if (!Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Information)) return;
            try
            {
                PopulateInfo();
                Log.Information("DAX STUDIO VERSION: {version}", version);
                Log.Information("System Info: {setting} = {value}", "OSCaption", osInfo.Name);
                Log.Information("System Info: {setting} = {value}", "OSRelease", osInfo.Release);
                Log.Information("System Info: {setting} = {value}", "OSVersion", osInfo.Version.ToString());
                Log.Information("System Info: {setting} = {value}", "OSArchitecture", osInfo.Architecture);
                Log.Information("System Info: {setting} = {value}", "VisibleMemoryGB", osInfo.TotalVisibleMemory.ToString("n2"));
                Log.Information("System Info: {setting} = {value}", "FreeMemoryGB", osInfo.TotalFreeMemory.ToString("n2"));
                Log.Information("Culture Info: {setting} = {value}", "Name", curCulture.Name);
                Log.Information("Culture Info: {setting} = {value}", "DisplayName", curCulture.DisplayName);
                Log.Information("Culture Info: {setting} = {value}", "EnglishName", curCulture.EnglishName);
                Log.Information("Culture Info: {setting} = {value}", "2-Letter ISO Name", curCulture.TwoLetterISOLanguageName);
                Log.Information("Culture Info: {setting} = {value}", "DecimalSeparator", curCulture.NumberFormat.NumberDecimalSeparator);
                Log.Information("Culture Info: {setting} = {value}", "GroupSeparator", curCulture.NumberFormat.NumberGroupSeparator);
                Log.Information("Culture Info: {setting} = {value}", "CurrencySymbol", curCulture.NumberFormat.CurrencySymbol);
                Log.Information("Culture Info: {setting} = {value}", "ShortDatePattern", curCulture.DateTimeFormat.ShortDatePattern);
            }
            catch(Exception ex)
            {
                // just log any error and continue
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(SystemInfo), nameof(WriteToLog), $"Error writing out system info to log: {ex.Message}");
            }
        }

        private static void PopulateInfo()
        {
            if (version != null) return; // exit here if this info is already populated
            osInfo = GetOSInfo();
            version = Assembly.GetExecutingAssembly().GetName().Version;
            curCulture = Thread.CurrentThread.CurrentCulture;
        }

        private static OSInfo GetOSInfo()
        {
            
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var result = new OSInfo();
            try
            {
                foreach (ManagementObject os in searcher.Get())
                {
                    result.Name = os["Caption"].ToString();
                    result.Version = Version.Parse(os["Version"].ToString());
                    result.Architecture = "32 bit";
                    if (result.Version.Major > 5) result.Architecture = os["OSArchitecture"].ToString();
                    continue;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(SystemInfo), nameof(GetOSInfo), $"Error getting OS name and version Info: {ex.Message}");
            }

            try
            {
                searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (ManagementObject os in searcher.Get())
                {
                    result.TotalVisibleMemory = long.Parse(os["TotalVisibleMemorySize"].ToString()).KbToGb();
                    result.TotalFreeMemory = long.Parse(os["FreePhysicalMemory"].ToString()).KbToGb();

                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(SystemInfo), nameof(GetOSInfo), $"Error getting OS memory Info: {ex.Message}");
            }

            string releaseId = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId", "")?.ToString();
            if (string.IsNullOrEmpty(releaseId)) releaseId = "<Unknown>";
            result.Release = releaseId;

            return result;
        
        }

        private class OSInfo
        {
            public string Name;
            public Version Version = new Version();
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
