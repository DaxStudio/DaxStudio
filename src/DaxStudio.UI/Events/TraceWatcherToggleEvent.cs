using DaxStudio.QueryTrace.Interfaces;

namespace DaxStudio.UI.Events
{
    public class TraceWatcherToggleEvent
    {
        public TraceWatcherToggleEvent(ITraceWatcher watcher, bool isActive)
        {
            TraceWatcher = watcher;
            IsActive = isActive;
        }
        public ITraceWatcher TraceWatcher { get; set; }
        public bool IsActive { get; set; }
    }
}
