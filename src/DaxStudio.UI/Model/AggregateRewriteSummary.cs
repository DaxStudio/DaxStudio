using Newtonsoft.Json.Linq;
using System;

namespace DaxStudio.UI.Model
{
    public struct AggregateRewriteSummary
    {
        public string RequestID;
        public DateTime UtcCurrentTime;
        public int MatchCount;
        public int MissCount;

        public AggregateRewriteSummary(string requestId, string textData)
        {
            RequestID = requestId;
            UtcCurrentTime = DateTime.UtcNow;
            JObject rewriteResult = JObject.Parse(textData);
            var matchingResult = (string)rewriteResult["matchingResult"];
            MatchCount = matchingResult == "matchFound" ? 1 : 0;
            MissCount = matchingResult == "matchFound" ? 0 : 1;
        }
    }
}
