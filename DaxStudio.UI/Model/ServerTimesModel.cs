using Caliburn.Micro;
using DaxStudio.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    public class ServerTimesModel
    {
        public long StorageEngineDuration {get;set;}
        public double  StorageEngineDurationPercentage {get;set;}
        public long FormulaEngineDuration {get;set;}
        public double FormulaEngineDurationPercentage { get; set; }
        public BindableCollection<TraceStorageEngineEvent> StoreageEngineEvents { get; set; }
        public long StorageEngineCpu { get; set; }
        public long TotalDuration { get; set; }
        public long StorageEngineQueryCount { get; set; }
        public int VertipaqCacheMatches { get; set; }
        public long TotalCpuDuration { get; set; }
    }
}
