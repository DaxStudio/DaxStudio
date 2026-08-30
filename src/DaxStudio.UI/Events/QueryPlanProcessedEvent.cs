using DaxStudio.Core.Interfaces;

namespace DaxStudio.UI.Events
{
    public sealed class QueryPlanProcessedEvent
    {
        public QueryPlanProcessedEvent(ITraceWatcher trace)
        {
            Trace = trace;
        }

        public ITraceWatcher Trace { get; }
    }
}
