using System;
using System.Linq;
using DaxStudio.Interfaces;
using Newtonsoft.Json;

namespace DaxStudio.UI.Events
{
    public class QueryHistoryEvent:IQueryHistoryEvent
    {
        private double _lineHeight = 18;
        private double _padding = 3;

        [JsonConstructor]
        public QueryHistoryEvent( string queryText
            , string parameters
            , DateTime startTime
            , long clientDurationMs 
            , long serverDurationMs
            , long seDurationMs 
            , long feDurationMs 
            , string serverName 
            , string databaseName 
            , string fileName
            , string queryBuilderJson)
        {
            QueryText = queryText.Trim();
            Parameters = parameters;
            QueryTextLines = queryText.Split('\n').Count();
            StartTime = startTime;
            ClientDurationMs = clientDurationMs;
            ServerDurationMs = serverDurationMs;
            SEDurationMs = seDurationMs;
            FEDurationMs = feDurationMs;
            ServerName = serverName.Trim().ToLower();
            DatabaseName = databaseName.Trim();
            FileName = fileName;
            QueryBuilderJson = queryBuilderJson;

        }

        public QueryHistoryEvent(
            string json
            , string queryText
            , string parameters
            , DateTime startTime
            , string serverName 
            , string databaseName 
            , string fileName): this(queryText, parameters,startTime,-1,-1,-1,-1,serverName,databaseName,fileName ,json)
        {   }

        public QueryHistoryEvent(
              string queryText
            , string parameters
            , DateTime startTime
            , string serverName
            , string databaseName
            , string fileName) : this(queryText, parameters, startTime, -1, -1, -1, -1, serverName, databaseName, fileName, string.Empty)
        { }

        public string QueryBuilderJson { get; }
        public string Parameters { get; }
        public string QueryText { get; private set; }
        public DateTime StartTime { get; private set; }
        public long ClientDurationMs { get; set; }
        public long ServerDurationMs { get;  set; }
        public long SEDurationMs { get;  set; }
        public long FEDurationMs { get;  set; }
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public string FileName { get; private set; }
        public string RowCount { get; set; }
        [JsonIgnore]
        public QueryStatus Status { get; set; }
        [JsonIgnore]
        public double QueryTextLines { get; private set; }

        [JsonIgnore]
        public double QueryTextHeight {
            get { 
                if (QueryTextLines > 3) {
                    return 3 * _lineHeight + _padding;
                }
                else
                {
                    return QueryTextLines * _lineHeight + _padding;
                }
            }
        }

        [JsonIgnore]
        public string TypeIcon => string.IsNullOrEmpty(QueryBuilderJson) ? "Edit" : "Wrench" ;

        [JsonIgnore]
        public string TypeTooltip => string.IsNullOrEmpty(QueryBuilderJson) ? "Query Text" : "Query Builder Query";

    }
}
