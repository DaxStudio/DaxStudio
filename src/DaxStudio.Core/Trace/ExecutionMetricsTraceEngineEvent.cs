using System.Collections.Generic;
using System.Data;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DaxStudio.Core.Trace
{
    public class ExecutionMetricsTraceEngineEvent: TraceStorageEngineEvent {
        public ExecutionMetricsTraceEngineEvent() { }

        public ExecutionMetricsTraceEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables, HashSet<string> dateColumnIds = null)
            : base(ev, rowNumber, options, remapColumns, remapTables, dateColumnIds)
        {
            TextData = ev.TextData;
        }

        public override string TextData { get => base.TextData;
            set { base.TextData = value; 
                ParseTextData(value);
                Query = TextData;
            } 
        }

        [JsonIgnore]
        public DataTable Properties { get; set; } 

        private void ParseTextData(string json)
        {
            Properties = new DataTable();
            Properties.Columns.Add("Property", typeof(string));
            Properties.Columns.Add("Value", typeof(string));
            //Properties.Columns.Add("FormatString", typeof(string));
            var data = JObject.Parse(json);
            foreach (var prop in data.Properties())
            {
                var row = Properties.NewRow();
                row["Property"] = prop.Name;
                var formatString = GetFormatString(prop.Name);
                row["Value"] = ParsePropValue(prop.Name, prop.Value.ToString(), formatString);
                //row["FormatString"] = GetFormatString(prop.Name);
                Properties.Rows.Add(row);

            }
        }

        private string GetFormatString(string name)
        {
            if (name.EndsWith("Ms") 
                || name.EndsWith("KB")
                || name.EndsWith("Rows")) return "N0";
            return string.Empty;
        }

        private string ParsePropValue(string name, string value, string formatString)
        {
            switch (name)
            {
                case "commandType":
                    return value;
                case "queryDialect":
                    int i = -1;
                    int.TryParse(value, out i);
                    return  ((QueryEndSubClass)i).ToString();
                default:
                    if( int.TryParse(value, out var i2 ))
                    {  return i2.ToString(formatString); }
                    if (long.TryParse(value, out var lng))
                    { return lng.ToString(formatString); }
                    return value;
            }
        }

        public override bool ShowTimelineForRow => false;
    }
}
