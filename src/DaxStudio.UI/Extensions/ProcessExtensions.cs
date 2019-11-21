using Serilog;
using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace DaxStudio.UI.Extensions
{
    public static class ProcessExtensions
    {
        public static Process GetParent(this Process process)
        {
            try
            {
                using (var query = new ManagementObjectSearcher(
                  "SELECT ParentProcessId " +
                  "FROM Win32_Process " +
                  "WHERE ProcessId=" + process.Id))
                {
                    return query
                      .Get()
                      .OfType<ManagementObject>()
                      .Select(p => Process.GetProcessById((int)(uint)p["ParentProcessId"]))
                      .FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", nameof(ProcessExtensions), nameof(GetParent), $"Error getting parent processid via WMI: {ex.Message}");
                return null;
            }
        }
    }
}
