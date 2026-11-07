using System;
using System.Collections.Generic;
using System.Linq;
using DaxStudio.Common;
using Serilog;

namespace DaxStudio.Core.Utils
{
    internal class PowerBIInstanceCache
    {
        private readonly object _scanLock = new object();
        private readonly Func<bool, List<PowerBIInstance>> _scan;
        private readonly Func<DateTime> _utcNow;
        private List<PowerBIInstance> _instances;
        private DateTime _lastScanUtc;
        private bool _lastScanIncludedPBIRS;
        // Coalesce the connection dialog's constructor and application-activation scans.
        private static readonly TimeSpan ScanThrottle = TimeSpan.FromSeconds(2);

        internal PowerBIInstanceCache(Func<bool, List<PowerBIInstance>> scan, Func<DateTime> utcNow = null)
        {
            _scan = scan;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        internal List<PowerBIInstance> GetInstances(bool includePBIRS, bool refreshList, bool bypassThrottle = false)
        {
            lock (_scanLock)
            {
                if (_instances != null && _lastScanIncludedPBIRS == includePBIRS && !bypassThrottle)
                {
                    if (!refreshList)
                    {
                        Log.Debug(Constants.LogMessageTemplate, nameof(PowerBIInstanceCache), nameof(GetInstances), "Returning cached PowerBI instances");
                        return new List<PowerBIInstance>(_instances);
                    }

                    if ((_utcNow() - _lastScanUtc) < ScanThrottle)
                    {
                        Log.Debug(Constants.LogMessageTemplate, nameof(PowerBIInstanceCache), nameof(GetInstances), "Returning recently scanned PowerBI instances (throttled)");
                        return new List<PowerBIInstance>(_instances);
                    }
                }

                var instances = new List<PowerBIInstance>(_scan(includePBIRS));
                instances.Sort();
                _instances = instances;
                _lastScanUtc = _utcNow();
                _lastScanIncludedPBIRS = includePBIRS;
                Log.Debug(Constants.LogMessageTemplate + " Ports: {ports}", nameof(PowerBIInstanceCache), nameof(GetInstances), "Published fresh PowerBI instances", string.Join(", ", instances.Select(i => i.Port)));
                return new List<PowerBIInstance>(_instances);
            }
        }

        internal PowerBIInstance FindByPort(int port)
        {
            var instances = GetInstances(false, false);
            var instance = instances.FirstOrDefault(i => i.Port == port);
            if (instance != null) return instance;

            Log.Debug(Constants.LogMessageTemplate + " Requested port: {port}; discovered ports: {ports}", nameof(PowerBIInstanceCache), nameof(FindByPort), "PowerBI port missing from initial lookup; forcing fresh discovery", port, string.Join(", ", instances.Select(i => i.Port)));
            instances = GetInstances(false, true, true);
            instance = instances.FirstOrDefault(i => i.Port == port);
            if (instance == null)
            {
                Log.Warning(Constants.LogMessageTemplate + " Requested port: {port}; discovered ports: {ports}", nameof(PowerBIInstanceCache), nameof(FindByPort), "PowerBI filename unavailable after fresh discovery; connection can continue without it", port, string.Join(", ", instances.Select(i => i.Port)));
            }

            return instance;
        }
    }
}
