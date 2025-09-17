using CsvHelper.Configuration.Attributes;
using System;

namespace DaxStudio.UI.Interfaces
{
    internal interface IPowerBIPerformanceData
    {
        public int Sequence { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Component { get; set; }
        public string Name { get; set; }

        public string VisualName { get; set; }
        public string QueryText { get; set; }
        public long RowCount { get; set; }
        public bool? Error { get; set; }
        public DateTime QueryStartTime { get; set; }
        public DateTime? QueryEndTime { get; set; }
        public DateTime RenderStartTime { get; set; }
        public DateTime? RenderEndTime { get; set; }


        public double QueryDuration {get;set;}
        public double RenderDuration { get;  set; }
        public double TotalDuration { get; }

        // copy/paste from the data grid converts the data to tab delimited format
        // so we replace any embedded tabs with 4 spaces 
        [Ignore]
        public string QueryTextQuoted { get; }
    }
}
