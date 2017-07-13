using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Events
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
