using DaxStudio.Core.Interfaces;

namespace DaxStudio.Core.Events
{
    public class CloseTraceWindowEvent
    {
        public CloseTraceWindowEvent(ITraceWatcher watcher)
        {
            TraceWatcher = watcher;
        }
        public ITraceWatcher TraceWatcher { get; private set; }
    }
}
