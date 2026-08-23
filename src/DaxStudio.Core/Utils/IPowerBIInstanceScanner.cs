using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Abstraction over the (platform-specific) discovery of locally running Power BI Desktop /
    /// SSDT Analysis Services instances. The real implementation is Windows-only (it relies on
    /// native process APIs, the Win32 extended TCP table and window-title APIs); non-Windows
    /// targets use a stub that returns an empty list. This keeps the Windows dependencies out of
    /// the cross-platform binary while leaving <see cref="PowerBIHelper"/> and its callers
    /// platform-agnostic.
    /// </summary>
    public interface IPowerBIInstanceScanner
    {
        /// <summary>
        /// Performs the raw scan for running local instances and returns the results.
        /// Caching / throttling is handled by <see cref="PowerBIHelper"/>.
        /// </summary>
        /// <param name="includePBIRS">When true, Power BI Report Server instances are included (admin only).</param>
        /// <remarks>
        /// Prefer <see cref="ScanAsync"/>. This synchronous overload blocks on the async
        /// implementation and must not be called on the UI thread.
        /// </remarks>
        List<PowerBIInstance> Scan(bool includePBIRS);

        /// <summary>
        /// Asynchronous version of <see cref="Scan"/>. Preferred by all callers as it avoids
        /// blocking a thread (and the sync-over-async deadlock risk) while the scan runs.
        /// </summary>
        Task<List<PowerBIInstance>> ScanAsync(bool includePBIRS, CancellationToken cancellationToken);
    }
}
