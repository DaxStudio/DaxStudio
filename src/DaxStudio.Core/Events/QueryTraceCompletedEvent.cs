using DaxStudio.Core.Interfaces;

namespace DaxStudio.Core.Events
{
    public class QueryTraceCompletedEvent
    {
        public QueryTraceCompletedEvent(ITraceWatcher trace)
        {
            Trace = trace;
        }
        public ITraceWatcher Trace { get; }
    }
}
