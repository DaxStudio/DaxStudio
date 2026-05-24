using DaxStudio.UI.Interfaces;
using DaxStudio.Core.Interfaces;

namespace DaxStudio.UI.Events
{
    public class ShowTraceWindowEvent
    {
        public ShowTraceWindowEvent(ITraceWatcher watcher)
        {
            TraceWatcher = watcher;
        }

        public ITraceWatcher TraceWatcher { get; private set; }
    }
}
