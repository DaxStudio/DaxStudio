using Caliburn.Micro;
using DaxStudio.UI.Enums;
using System;



namespace DaxStudio.UI.Model
{
    public class QueryEvent: PropertyChangedBase
    {
        private long _duration;
        public long Duration { 
            get => _duration; 
            set
            {
                _duration = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(IsQueryRunning));
            }
        }

        public bool IsQueryRunning => _duration < 0;
        public string Query { get; set; }
        public DateTime StartTime { get; set; }
        private DateTime _endTime;
        public DateTime EndTime { get => _endTime; 
            set
            {
                _endTime = value;
                NotifyOfPropertyChange();
            } 
        }
        public string Username { get; set; }
        public string DatabaseName { get; internal set; }
        public string QueryType { get; set; }
        public string RequestID { get; set; }
        public int AggregationMatchCount { get; set; }
        public int AggregationMissCount { get; set; }
        public string RequestProperties { get; set; }
        public string RequestParameters { get; set; }
        public string AggregationStatus { set { }
            get {
                if (AggregationMatchCount > 0 && AggregationMissCount > 0) return "Partial";
                if (AggregationMatchCount > 0 && AggregationMissCount == 0) return "Match";
                if (AggregationMatchCount == 0 && AggregationMissCount > 0) return "Miss";
                return "n/a";
            }
        }
    }
}