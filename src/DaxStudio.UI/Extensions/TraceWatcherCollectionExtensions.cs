using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using System.Linq;

namespace DaxStudio.UI.Extensions
{
    public static class TraceWatcherCollectionExtensions
    {
        public static void DisableAll(this IObservableCollection<ITraceWatcher> traceWatchers)
        {
            foreach (var tw in traceWatchers)
            {
                tw.IsEnabled = false;
            }
        }

        public static void EnableAll(this BindableCollection<ITraceWatcher> traceWatchers)
        {
            var activeTrace = traceWatchers.FirstOrDefault(t => t.IsChecked);
            foreach (var tw in traceWatchers)
            {
                tw.IsEnabled = activeTrace == null || activeTrace.FilterForCurrentSession == tw.FilterForCurrentSession ;
            }
        }
    }
}
