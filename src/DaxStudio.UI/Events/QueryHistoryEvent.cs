using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using Newtonsoft.Json;

namespace DaxStudio.UI.Events
{
    public class QueryHistoryEvent:IQueryHistoryEvent
    {
        private double _lineHeight = 18;
        private double _padding = 3;

        [JsonConstructor]
        public QueryHistoryEvent( string queryText
        , DateTime startTime
        , long clientDurationMs 
        , long serverDurationMs
        , long seDurationMs 
        , long feDurationMs 
        , string serverName 
        , string databaseName 
        , string fileName)
        {
            QueryText = queryText.Trim();
            QueryTextLines = QueryText.Split('\n').Count();
            StartTime = startTime;
            ClientDurationMs = clientDurationMs;
            ServerDurationMs = serverDurationMs;
            SEDurationMs = seDurationMs;
            FEDurationMs = feDurationMs;
            ServerName = serverName.Trim().ToLower();
            DatabaseName = databaseName.Trim();
            FileName = fileName;
        }

        public QueryHistoryEvent( string queryText
        , DateTime startTime
        , string serverName 
        , string databaseName 
        , string fileName): this(queryText,startTime,-1,-1,-1,-1,serverName,databaseName,fileName )
        {   }

        public string QueryText { get; private set; }
        public DateTime StartTime { get; private set; }
        public long ClientDurationMs { get; set; }
        public long ServerDurationMs { get;  set; }
        public long SEDurationMs { get;  set; }
        public long FEDurationMs { get;  set; }
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public string FileName { get; private set; }
        public int RowCount { get; set; }
        [JsonIgnore]
        public QueryStatus Status { get; set; }
        [JsonIgnore]
        public double QueryTextLines { get; private set; }
        public double QueryTextHeight { get { 
            if (QueryTextLines > 3) {
                return 3 * _lineHeight + _padding;
            }
            else
            {
                return QueryTextLines * _lineHeight + _padding;
            }
        }
        }

    }
}
