using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxStudio.UI.Model;
using DaxStudio.UI.Interfaces;

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
