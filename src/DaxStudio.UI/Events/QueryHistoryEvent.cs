using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DaxStudio.Interfaces;
using Newtonsoft.Json;

namespace DaxStudio.UI.Events
{
    public class QueryHistoryEvent:IQueryHistoryEvent, INotifyPropertyChanged
    {
        private double _lineHeight = 18;
        private double _padding = 3;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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
            QueryTextLines = queryText.Split('\n').Length;
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
        private long _clientDurationMs;
        public long ClientDurationMs { get => _clientDurationMs;
            set { 
                _clientDurationMs = value;
                NotifyPropertyChanged();
            }
        }
        private long _serverDurationMs;
        public long ServerDurationMs { get => _serverDurationMs;
            set { _serverDurationMs = value;
                NotifyPropertyChanged();
            } 
        }
        private long _seDurationMs;
        public long SEDurationMs { get => _seDurationMs;
            set { _seDurationMs = value;
                NotifyPropertyChanged() ;
            } 
        }
        private long _feDurationMs;
        public long FEDurationMs { get => _feDurationMs;
            set { 
                _feDurationMs = value;
                NotifyPropertyChanged() ;
            } 
        }
        public string ServerName { get; private set; }
        public string DatabaseName { get; private set; }
        public string FileName { get; private set; }
        public string RowCount { get; set; }
        [JsonIgnore]
        private QueryStatus _status = QueryStatus.Running;
        public QueryStatus Status { get => _status;
            set { _status = value;
                NotifyPropertyChanged();
            } 
        }
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
        public string TypeIcon => string.IsNullOrEmpty(QueryBuilderJson) ? "editorDrawingImage" : "query_builder_toolbarDrawingImage" ;

        [JsonIgnore]
        public string TypeTooltip => string.IsNullOrEmpty(QueryBuilderJson) ? "Query Text" : "Query Builder Query";

    }
}
