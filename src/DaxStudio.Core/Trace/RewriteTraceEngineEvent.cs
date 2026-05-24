using System.Collections.Generic;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using Newtonsoft.Json.Linq;

namespace DaxStudio.Core.Trace
{
    public class RewriteTraceEngineEvent : TraceStorageEngineEvent
    {

        public RewriteTraceEngineEvent() { }

        public RewriteTraceEngineEvent(DaxStudioTraceEventArgs ev, int rowNumber, IGlobalOptions options, Dictionary<string, string> remapColumns, Dictionary<string, string> remapTables, HashSet<string> dateColumnIds = null) 
            : base(ev, rowNumber, options, remapColumns, remapTables, dateColumnIds) {
            TextData = ev.TextData;
        }
        
        public string Table { get; set; }
        public string MatchingResult { get; set; }
        public string Mapping { get; set; }
        private string _textData;
        public override string TextData { get { return _textData; } set {
                _textData = value;
                if (_textData == null) return;
                JObject rewriteResult = JObject.Parse(_textData);
                Table = (string)rewriteResult["table"];
                MatchingResult = (string)rewriteResult["matchingResult"];
                var mapping = rewriteResult["mapping"];
                if (mapping != null) {
                    if (mapping.HasValues) {
                        Mapping = (string)rewriteResult["mapping"]["table"];
                    }
                }
                Query = $"<{MatchingResult}>";
            }
        }
        public new string Query { get; set; } = "";
        public bool MatchFound { get { return MatchingResult == "matchFound"; } }

        public override bool ShowTimelineForRow => false;
    }
}
