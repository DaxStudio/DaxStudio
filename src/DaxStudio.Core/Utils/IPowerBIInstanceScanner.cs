using System.Collections.Generic;

namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// Abstraction over the (platform-specific) discovery of locally running Power BI Desktop /
    /// SSDT Analysis Services instances. The real implementation is Windows-only (it relies on
    /// WMI, the Win32 extended TCP table and window-title APIs); non-Windows targets use a stub
    /// that returns an empty list. This keeps the Windows dependencies out of the cross-platform
    /// binary while leaving <see cref="PowerBIHelper"/> and its callers platform-agnostic.
    /// </summary>
    public interface IPowerBIInstanceScanner
    {
        /// <summary>
        /// Performs the raw scan for running local instances and returns the results.
        /// Caching / throttling is handled by <see cref="PowerBIHelper"/>.
        /// </summary>
        /// <param name="includePBIRS">When true, Power BI Report Server instances are included (admin only).</param>
        List<PowerBIInstance> Scan(bool includePBIRS);
    }
}
