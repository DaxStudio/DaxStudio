using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace DaxStudio.Core.Utils
{
    /// <summary>
    /// No-op <see cref="IPowerBIInstanceScanner"/> used on platforms where scanning for local
    /// Power BI Desktop instances is not supported (e.g. non-Windows). There is no Power BI
    /// Desktop on those platforms, so returning an empty list is the correct behaviour.
    /// </summary>
    public sealed class NullPowerBIInstanceScanner : IPowerBIInstanceScanner
    {
        public List<PowerBIInstance> Scan(bool includePBIRS)
        {
            Log.Debug("{class} {method} local Power BI Desktop scan is not supported on this platform", nameof(NullPowerBIInstanceScanner), nameof(Scan));
            return new List<PowerBIInstance>();
        }

        public Task<List<PowerBIInstance>> ScanAsync(bool includePBIRS, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Scan(includePBIRS));
        }
    }
}
