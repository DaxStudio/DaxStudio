using DaxStudio.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Events
{
    public class ServerTimingsEvent : IServerTimes
    {
        public ServerTimingsEvent(IServerTimes timings)
        {
            FormulaEngineDuration = timings.FormulaEngineDuration;
            StorageEngineCpu = timings.StorageEngineCpu;
            StorageEngineCpuFactor = timings.StorageEngineCpuFactor;
            StorageEngineDuration = timings.StorageEngineDuration;
            StorageEngineQueryCount = timings.StorageEngineQueryCount;
            TotalCpuDuration = timings.TotalCpuDuration;
            TotalCpuFactor = timings.TotalCpuFactor;
            TotalDuration = timings.TotalDuration;
            VertipaqCacheMatches = timings.VertipaqCacheMatches;
        }

        public long FormulaEngineDuration { get; }
        public long StorageEngineCpu { get; }
        public double StorageEngineCpuFactor { get; }
        public long StorageEngineDuration { get; }
        public long StorageEngineQueryCount { get; }
        public long TotalCpuDuration { get; }
        public double TotalCpuFactor { get; }
        public long TotalDuration {get;}

        public int VertipaqCacheMatches { get; }

    }
}
