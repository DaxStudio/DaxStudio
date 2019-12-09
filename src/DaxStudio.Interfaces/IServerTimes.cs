namespace DaxStudio.Interfaces
{
    public interface IServerTimes
    {
        long FormulaEngineDuration { get; }
        long StorageEngineCpu { get; }
        double StorageEngineCpuFactor { get; }
        long StorageEngineDuration { get; }
        long StorageEngineQueryCount { get; }
        long TotalCpuDuration { get;  }
        double TotalCpuFactor { get; }
        long TotalDuration { get; }
        int VertipaqCacheMatches { get;  }
    }
}