using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using System.Management;
using System.Threading;
using System.Reflection;

namespace DaxStudio.UI.Utils
{
    public static class SystemInfo
    {
        public static void WriteToLog()
        {
            if (!Serilog.Log.IsEnabled(Serilog.Events.LogEventLevel.Information)) return;
            var osInfo = GetOSInfo();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            var curCulture = Thread.CurrentThread.CurrentCulture;
            Log.Information("DAX STUDIO VERSION: {version}", version);
            Log.Information("System Info: {setting} = {value}", "OSCaption",osInfo.Name  );
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

        private static OSInfo GetOSInfo()
        {
            
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            var result = new OSInfo();
            foreach (ManagementObject os in searcher.Get())
            {
                result.Name = os["Caption"].ToString();
                result.Version = Version.Parse(os["Version"].ToString());
                result.Architecture = "32 bit";
                if (result.Version.Major > 5)  result.Architecture = os["OSArchitecture"].ToString();
                continue;
            }

            searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (ManagementObject os in searcher.Get())
            {
                result.TotalVisibleMemory = long.Parse(os["TotalVisibleMemorySize"].ToString()).KbToGb();
                result.TotalFreeMemory = long.Parse(os["FreePhysicalMemory"].ToString()).KbToGb();

            }
            return result;
        
        }

        private struct OSInfo
        {
            public string Name;
            public Version Version;
            public string Architecture;
            public decimal TotalVisibleMemory;
            public decimal TotalFreeMemory;
        }

        public static decimal KbToGb(this long bytes)
        {
            return (decimal)bytes / (1024 * 1024);
        }
    }
}
