using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio.Core.Events
{
    public class TraceChangingEvent
    {
        public TraceChangingEvent(object sender, QueryTraceStatus traceStatus)
        {
            TraceStatus = traceStatus;
            Sender = sender;
        }
        public QueryTraceStatus TraceStatus { get; }
        public object Sender { get; }
    }
}