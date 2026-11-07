using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.Utils
{
    public static class PowerBIHelper
    {
        private static readonly PowerBIInstanceCache _instanceCache = new PowerBIInstanceCache(
            (includePBIRS, cancellationToken) => Scanner.ScanAsync(includePBIRS, cancellationToken));

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
        public static Task<List<PowerBIInstance>> GetLocalInstancesAsync(bool includePBIRS, bool refreshList, CancellationToken cancellationToken)
        {
            return _instanceCache.GetInstancesAsync(includePBIRS, refreshList, cancellationToken);
        }

        /// <summary>
        /// Finds a local instance by port, bypassing the refresh throttle once if it is not cached.
        /// Must not be called from the UI thread; prefer <see cref="GetLocalInstanceAsync"/>.
        /// </summary>
        public static PowerBIInstance GetLocalInstance(int port)
        {
            return GetLocalInstanceAsync(port, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Finds a local instance by port, bypassing the refresh throttle once if it is not cached.
        /// Returns null if fresh discovery still cannot find it; the connection can continue.
        /// </summary>
        public static Task<PowerBIInstance> GetLocalInstanceAsync(int port, CancellationToken cancellationToken)
        {
            return _instanceCache.FindByPortAsync(port, cancellationToken);
        }
    }
}
