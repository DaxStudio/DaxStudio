using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Events
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
