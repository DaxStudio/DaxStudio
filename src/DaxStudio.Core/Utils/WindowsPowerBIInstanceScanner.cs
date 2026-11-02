using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using DaxStudio.Common;
using DaxStudio.Core.Extensions;
using Serilog;

namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Windows implementation of <see cref="IPowerBIInstanceScanner"/>. Discovers locally running
    /// msmdsrv (Power BI Desktop / SSDT) instances using the native process APIs (parent process
    /// lookup), the Win32 extended TCP table (<see cref="ManagedIpHelper"/>) and window-title APIs
    /// (<see cref="WindowTitle"/>).
    /// This type is only compiled into the Windows target frameworks.
    /// </summary>
    public sealed class WindowsPowerBIInstanceScanner : IPowerBIInstanceScanner
    {
        const int MaxParallelInstanceScans = 5;

        public List<PowerBIInstance> Scan(bool includePBIRS)
        {
            // Sync-over-async: only safe off the UI thread. Prefer ScanAsync.
            return ScanAsync(includePBIRS, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task<List<PowerBIInstance>> ScanAsync(bool includePBIRS, CancellationToken cancellationToken)
        {
            var instances = new List<PowerBIInstance>();
            var sw = Stopwatch.StartNew();

            // Enumerating processes and reading the TCP table are synchronous and take on the order
            // of 100-200ms, so keep them off the caller's thread - ScanAsync is awaited directly
            // from the UI thread by the connection dialog.
            var prologue = await Task.Run(() => new
            {
                TcpPorts = ManagedIpHelper.GetExtendedTcpDictionary(),
                Processes = Process.GetProcessesByName("msmdsrv"),
                // evaluated once per scan rather than once per process
                IsAdmin = IsAdministrator()
            }, cancellationToken).ConfigureAwait(false);

            var msmdsrvProcesses = prologue.Processes;
            Log.Debug("{class} {method} Prologue took {elapsed}ms ({processCount} msmdsrv process(es), {portCount} tcp port(s))", nameof(WindowsPowerBIInstanceScanner), nameof(ScanAsync), sw.ElapsedMilliseconds, msmdsrvProcesses.Length, prologue.TcpPorts.Count);

            try
            {
                await msmdsrvProcesses.ParallelForEachAsync(async proc =>
                {
                    var instance = await GetInstanceDetailsAsync(includePBIRS, prologue.TcpPorts, proc, prologue.IsAdmin, cancellationToken).ConfigureAwait(false);
                    if (instance != null)
                    {
                        lock (instances)
                        {
                            instances.Add(instance);
                        }
                    }
                }, MaxParallelInstanceScans, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Process instances hold an OS handle each - the connection dialog rescans on every
                // activation so these must not be left to the finalizer.
                foreach (var proc in msmdsrvProcesses) proc.Dispose();
                Log.Debug("{class} {method} Scan finished in {elapsed}ms, found {instanceCount} Power BI instance(s)", nameof(WindowsPowerBIInstanceScanner), nameof(ScanAsync), sw.ElapsedMilliseconds, instances.Count);
            }

            return instances;
        }

        private static async Task<PowerBIInstance> GetInstanceDetailsAsync(bool includePBIRS, Dictionary<int, TcpRow> tcpPorts, Process proc, bool isAdmin, CancellationToken cancellationToken)
        {
            return await Task.Run<PowerBIInstance>(() => {
                // The entire body is guarded: a Power BI Desktop instance that shuts down while we
                // are inspecting it makes the Process members throw, and an unhandled exception here
                // would fail the whole scan rather than just this one instance.
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    PowerBIInstance instance = null;
                    int _port = 0;
                    string parentTitle = string.Empty; // $"localhost:{_port}";
                    EmbeddedSSASIcon _icon = EmbeddedSSASIcon.PowerBI;

                    using (var parent = proc.GetParent())
                    {
                        if (parent != null)
                        {
                            // exit here if the parent == "services" then this is a SSAS instance
                            if (parent.ProcessName.Equals("services", StringComparison.OrdinalIgnoreCase)) return null;

                            // exit here if the parent == "RSHostingService" then this is a SSAS instance
                            if (parent.ProcessName.Equals("RSHostingService", StringComparison.OrdinalIgnoreCase))
                            {
                                // only show PBI Report Server if we are running as admin
                                // otherwise we won't have any access to the models
                                if (isAdmin && includePBIRS)
                                    _icon = EmbeddedSSASIcon.PowerBIReportServer;
                                else
                                    return null;
                            }

                            // if the process was launched from Visual Studio change the icon
                            if (parent.ProcessName.Equals("devenv", StringComparison.OrdinalIgnoreCase)) _icon = EmbeddedSSASIcon.Devenv;

                            // get the window title so that we can parse out the file name
                            parentTitle = parent.MainWindowTitle;

                            if (parentTitle.Length == 0)
                            {
                                // for minimized windows we need to use some Win32 api calls to get the title
                                parentTitle = WindowTitle.GetWindowTitle(parent.Id);
                            }
                        }
                    }

                    // try and get the tcp port from the Win32 TcpTable API
                    tcpPorts.TryGetValue(proc.Id, out var tcpRow);
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

                    return instance;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Log.Error("{class} {Method} {Error} {StackTrace}", nameof(WindowsPowerBIInstanceScanner), nameof(Scan), ex.Message, ex.StackTrace);
                    return null;
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
