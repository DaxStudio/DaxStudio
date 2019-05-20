using System;

namespace DaxStudio.UI.Model
{
    public class PowerBIPerformanceData
    {
        public int Sequence { get; set; }
        public string Id { get; set; }
        public string ParentId { get; set; }
        public string Component { get; set; }
        public string Name { get; set; }

        public string VisualName { get; set; }
        public string QueryText { get; set; }
        public long RowCount { get; set; }
        public DateTime QueryStartTime { get; set; }
        public DateTime QueryEndTime { get; set; }
        public DateTime RenderStartTime { get; set; }
        public DateTime RenderEndTime { get; set; }

        
        public double QueryDuration { get {
                TimeSpan duration = QueryEndTime - QueryStartTime;
                return duration.TotalMilliseconds;
            }
        }
        public double RenderDuration
        {
            get
            {
                TimeSpan duration = RenderEndTime - RenderStartTime;
                return duration.TotalMilliseconds;
            }
        }
        public double TotalDuration => QueryDuration + RenderDuration;

    }
}
