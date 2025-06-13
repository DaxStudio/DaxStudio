using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using System;

namespace DaxStudio.UI.Model
{
    public class ServerTimesModel
    {
        // 7 - added ObjectName
        // 8 - added ErrorMessage
        public int FileFormatVersion { get { return 8; } }
        public string ActivityID { get; set; }
        public long StorageEngineDuration {get;set;}
        public long StorageEngineNetParallelDuration { get; set; }
        public double  StorageEngineDurationPercentage { get;set; }
        public long FormulaEngineDuration {get;set;}
        public double FormulaEngineDurationPercentage { get; set; }
        public BindableCollection<TraceStorageEngineEvent> StorageEngineEvents { get; set; }

        // we need the collection with the typo in the name for backward compatibility
        public BindableCollection<TraceStorageEngineEvent> StoreageEngineEvents { get; set; } 
        public long StorageEngineCpu { get; set; }
        public long TotalDuration { get; set; }
        public long StorageEngineQueryCount { get; set; }
        public int VertipaqCacheMatches { get; set; }
        public long TotalDirectQueryDuration { get; set; }
        public long TotalCpuDuration { get; set; }
        public DateTime QueryEndDateTime { get; set; }
        public bool ParallelStorageEngineEventsDetected { get; set; }
        public bool ShowStorageEngineNetParallelDuration { get; set; }
        public DateTime QueryStartDateTime { get; set; }
        public string Parameters { get; set; }
        public string CommandText { get; set; }
        public long TimelineTotalDuration { get; set; }
        public string ErrorMessage { get; set; } // added in v8
    }
}
