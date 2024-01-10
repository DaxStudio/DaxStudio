using Caliburn.Micro;
using DaxStudio.UI.Enums;
using DaxStudio.UI.Interfaces;
using Microsoft.AnalysisServices.AdomdClient;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DaxStudio.UI.Model
{
    [Serializable]
    [JsonObject(MemberSerialization.OptIn)]
    public class QueryEvent: PropertyChangedBase, ITraceDiagnostics, IQueryTextProvider
    {
        
        private long _duration;
        [JsonProperty]
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
        [JsonProperty]
        public string Query { get; set; }
        [JsonProperty]
        public DateTime StartTime { get; set; }
        private DateTime _endTime;

        [JsonProperty]
        public DateTime EndTime { get => _endTime; 
            set
            {
                _endTime = value;
                NotifyOfPropertyChange();
            } 
        }
        [JsonProperty]
        public string Username { get; set; }
        [JsonProperty]
        public string DatabaseName { get; set; }
        [JsonProperty]
        public string QueryType { get; set; }
        [JsonProperty]
        public string RequestID { get; set; }
        private int _aggregationMatchCount;
        [JsonProperty]
        public int AggregationMatchCount { get => _aggregationMatchCount; 
            set { 
                _aggregationMatchCount = value;
                NotifyOfPropertyChange(nameof(_aggregationMatchCount));
                NotifyOfPropertyChange(nameof(AggregationStatusImage));
            } 
        }
        private int _aggregationMissCount;
        [JsonProperty]
        public int AggregationMissCount { get => _aggregationMissCount; 
            set { 
                _aggregationMissCount = value;
                NotifyOfPropertyChange(nameof(AggregationMissCount));
                NotifyOfPropertyChange(nameof(AggregationStatusImage));
            }
        }
        
        [JsonProperty]
        public string RequestProperties { get; set; }
        [JsonProperty]
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
        [JsonProperty]
        public string ActivityID { get; set; }

        
        public DateTime StartDatetime => StartTime;

        
        public string CommandText { get => Query; 
            set { 
                // do nothing
            } 
        }
        
        public string Parameters { get => RequestParameters; 
            set { 
                // do nothing
            } 
        }

        #region IQueryTextProvider
        string IQueryTextProvider.EditorText => this.Query;

        string IQueryTextProvider.QueryText => this.Query;

        List<AdomdParameter> IQueryTextProvider.ParameterCollection { get; } = new List<AdomdParameter>();

        QueryInfo IQueryTextProvider.QueryInfo { get; set; }
        #endregion
    }
}