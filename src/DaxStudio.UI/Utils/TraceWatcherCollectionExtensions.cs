using Caliburn.Micro;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Utils
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
            foreach (var tw in traceWatchers)
            {
                tw.IsEnabled = true;
            }
        }
    }
}
