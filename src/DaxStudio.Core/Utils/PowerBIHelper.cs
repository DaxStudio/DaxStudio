using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace DaxStudio.Core.Utils
{
    public static class PowerBIHelper
    {
        private static readonly List<PowerBIInstance> _instances = new List<PowerBIInstance>();
        private static bool instancesLoaded = false;
        // SemaphoreSlim rather than lock() because the async path awaits the scan while holding it
        private static readonly SemaphoreSlim _scanLock = new SemaphoreSlim(1, 1);
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

        /// <summary>
        /// Returns the locally running Power BI Desktop / SSDT instances.
        /// </summary>
        /// <remarks>
        /// Blocks on the async implementation, so it must not be called from the UI thread -
        /// prefer <see cref="GetLocalInstancesAsync"/>.
        /// The returned list is a snapshot; callers may freely mutate it without corrupting the
        /// shared cache.
        /// </remarks>
        public static List<PowerBIInstance> GetLocalInstances(bool includePBIRS, bool refreshList)
        {
            return GetLocalInstancesAsync(includePBIRS, refreshList, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns the locally running Power BI Desktop / SSDT instances, scanning for them if the
        /// cache is stale (or <paramref name="refreshList"/> is set).
        /// </summary>
        /// <remarks>
        /// The returned list is a snapshot; callers may freely mutate it without corrupting the
        /// shared cache.
        /// </remarks>
        public static async Task<List<PowerBIInstance>> GetLocalInstancesAsync(bool includePBIRS, bool refreshList, CancellationToken cancellationToken)
        {
            if (!refreshList && instancesLoaded)
            {
                Log.Debug("{class} {method} Returning cached PowerBI instances", nameof(PowerBIHelper), nameof(GetLocalInstances));
                return Snapshot();
            }

            await _scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Another thread may have completed a scan while we were waiting for the lock.
                // Coalesce near-simultaneous force-refreshes (same scope) so we only scan the
                // running msmdsrv processes once.
                if (instancesLoaded
                    && _lastScanIncludedPBIRS == includePBIRS
                    && (DateTime.UtcNow - _lastScanUtc) < ScanThrottle)
                {
                    Log.Debug("{class} {method} Returning recently scanned PowerBI instances (throttled)", nameof(PowerBIHelper), nameof(GetLocalInstances));
                    return Snapshot();
                }

                var scanned = await Scanner.ScanAsync(includePBIRS, cancellationToken).ConfigureAwait(false);
                scanned.Sort(); // order by name

                lock (_instances)
                {
                    _instances.Clear();
                    _instances.AddRange(scanned);
                }

                instancesLoaded = true;
                _lastScanUtc = DateTime.UtcNow;
                _lastScanIncludedPBIRS = includePBIRS;

                return Snapshot();
            }
            finally
            {
                _scanLock.Release();
            }
        }

        // Hand back a copy so that callers cannot mutate (or observe a concurrent rewrite of) the
        // cached list - the connection dialog appends a placeholder "none detected" entry to what
        // it gets back, which previously poisoned the cache for every subsequent caller.
        private static List<PowerBIInstance> Snapshot()
        {
            lock (_instances)
            {
                return new List<PowerBIInstance>(_instances);
            }
        }
    }
}
