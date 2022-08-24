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
        private int _aggregationMatchCount;
        public int AggregationMatchCount { get => _aggregationMatchCount; set { 
                _aggregationMatchCount = value;
                NotifyOfPropertyChange(nameof(_aggregationMatchCount));
                NotifyOfPropertyChange(nameof(AggregationStatusImage));
            } 
        }
        private int _aggregationMissCount;
        public int AggregationMissCount { get => _aggregationMissCount; 
            set { 
                _aggregationMissCount = value;
                NotifyOfPropertyChange(nameof(AggregationMissCount));
                NotifyOfPropertyChange(nameof(AggregationStatusImage));
            }
        }
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

        public string AggregationStatusImage { get {
                if (AggregationMatchCount > 0 && AggregationMissCount > 0) return "agg_partialDrawingImage";
                if (AggregationMatchCount > 0 && AggregationMissCount == 0) return "agg_matchDrawingImage";
                if (AggregationMatchCount == 0 && AggregationMissCount > 0) return "agg_missDrawingImage";
                return string.Empty;
            } 
        
        }
    }
}