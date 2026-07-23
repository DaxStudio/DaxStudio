using System;
using System.Collections.Generic;
using Serilog;

namespace DaxStudio.Core.Utils
{
    public static class PowerBIHelper
    {
        private static readonly List<PowerBIInstance> _instances = new List<PowerBIInstance>();
        private static bool instancesLoaded = false;
        private static readonly object _scanLock = new object();
        private static DateTime _lastScanUtc = DateTime.MinValue;
        private static bool _lastScanIncludedPBIRS = false;
        // Coalesce force-refresh scans that occur within this window. The connection dialog
        // triggers a scan from its constructor and again when it handles the
        // ApplicationActivatedEvent that fires as the app gains focus at startup - both fire
        // almost simultaneously, so without this we scan (and log) the msmdsrv processes twice.
        private static readonly TimeSpan ScanThrottle = TimeSpan.FromSeconds(2);

        // The platform-specific scanner that performs the actual discovery of running instances.
        // Defaulted at compile time via PowerBIScannerFactory (Windows scanner on Windows targets,
        // a no-op stub on cross-platform builds). Settable so tests can inject a fake scanner.
        public static IPowerBIInstanceScanner Scanner { get; set; } = PowerBIScannerFactory.Create();

        public static List<PowerBIInstance> GetLocalInstances(bool includePBIRS, bool refreshList)
        {
            if (!refreshList && instancesLoaded)
            {
                Log.Debug("{class} {method} Returning cached PowerBI instances", nameof(PowerBIHelper), nameof(GetLocalInstances));
                return _instances;
            }

            lock (_scanLock)
            {
                // Another thread may have completed a scan while we were waiting for the lock.
                // Coalesce near-simultaneous force-refreshes (same scope) so we only scan the
                // running msmdsrv processes once.
                if (instancesLoaded
                    && _lastScanIncludedPBIRS == includePBIRS
                    && (DateTime.UtcNow - _lastScanUtc) < ScanThrottle)
                {
                    Log.Debug("{class} {method} Returning recently scanned PowerBI instances (throttled)", nameof(PowerBIHelper), nameof(GetLocalInstances));
                    return _instances;
                }

                _instances.Clear(); // clear the list before we start

                _instances.AddRange(Scanner.Scan(includePBIRS));

                _instances.Sort(); // order by name

                instancesLoaded = true;
                _lastScanUtc = DateTime.UtcNow;
                _lastScanIncludedPBIRS = includePBIRS;

                return _instances;
            }
        }
    }
}
