using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using DaxStudio.Common;
using DaxStudio.Core.Extensions;
using Serilog;

namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Windows implementation of <see cref="IPowerBIInstanceScanner"/>. Discovers locally running
    /// msmdsrv (Power BI Desktop / SSDT) instances using WMI (parent process lookup), the Win32
    /// extended TCP table (<see cref="ManagedIpHelper"/>) and window-title APIs (<see cref="WindowTitle"/>).
    /// This type is only compiled into the Windows target frameworks.
    /// </summary>
    public sealed class WindowsPowerBIInstanceScanner : IPowerBIInstanceScanner
    {
        const int MaxParallelInstanceScans = 5;

        public List<PowerBIInstance> Scan(bool includePBIRS)
        {
            var instances = new List<PowerBIInstance>();

            var dict = ManagedIpHelper.GetExtendedTcpDictionary();
            var msmdsrvProcesses = Process.GetProcessesByName("msmdsrv");
            var isAdmin = IsAdministrator();

            Func<Process, Task> myfunc = async (proc) =>
            {
                var instance = await GetInstanceDetailsAsync(includePBIRS, dict, proc, isAdmin);
                if (instance != null)
                {
                    lock (instances)
                    {
                        instances.Add(instance);
                    }
                }
            };

            msmdsrvProcesses.ParallelForEachAsync(async proc => await myfunc(proc), MaxParallelInstanceScans).Wait();

            return instances;
        }

        private static async Task<PowerBIInstance> GetInstanceDetailsAsync(bool includePBIRS, Dictionary<int, TcpRow> tcpPorts, Process proc, bool isAdmin)
        {
            return await Task.Run<PowerBIInstance>(() => {
                PowerBIInstance instance = null;
                int _port = 0;
                string parentTitle = string.Empty; // $"localhost:{_port}";
                EmbeddedSSASIcon _icon = EmbeddedSSASIcon.PowerBI;
                var parent = proc.GetParent();

                if (parent != null)
                {
                    // exit here if the parent == "services" then this is a SSAS instance
                    if (parent.ProcessName.Equals("services", StringComparison.OrdinalIgnoreCase)) return instance;

                    // exit here if the parent == "RSHostingService" then this is a SSAS instance
                    if (parent.ProcessName.Equals("RSHostingService", StringComparison.OrdinalIgnoreCase))
                    {
                        // only show PBI Report Server if we are running as admin
                        // otherwise we won't have any access to the models
                        if (isAdmin && includePBIRS)
                            _icon = EmbeddedSSASIcon.PowerBIReportServer;
                        else
                            return instance;
                    }

                    // if the process was launched from Visual Studio change the icon
                    if (parent.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase)) _icon = EmbeddedSSASIcon.Devenv;

                    // get the window title so that we can parse out the file name
                    parentTitle = parent.MainWindowTitle;

                    if (parentTitle.Length == 0)
                    {
                        // for minimized windows we need to use some Win32 api calls to get the title
                        //parentTitle = WindowTitle.GetWindowTitleTimeout( parent.Id, 300);
                        parentTitle = WindowTitle.GetWindowTitle(parent.Id);
                    }
                }
                // try and get the tcp port from the Win32 TcpTable API
                try
                {
                    TcpRow tcpRow = null;
                    tcpPorts.TryGetValue(proc.Id, out tcpRow);
                    if (tcpRow != null)
                    {
                        _port = tcpRow.LocalEndPoint.Port;
                        instance = new PowerBIInstance(parentTitle, _port, _icon);
                        Log.Debug("{class} {method} PowerBI found on port: {port}", nameof(WindowsPowerBIInstanceScanner), nameof(Scan), _port);
                    }
                    else
                    {
                        Log.Debug("{class} {method} PowerBI port not found for process: {processName} PID: {pid}", nameof(WindowsPowerBIInstanceScanner), nameof(Scan), proc.ProcessName, proc.Id);
                    }

                }
                catch (Exception ex)
                {
                    Log.Error("{class} {Method} {Error} {StackTrace}", nameof(WindowsPowerBIInstanceScanner), nameof(Scan), ex.Message, ex.StackTrace);
                }

                return instance;
            });
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
