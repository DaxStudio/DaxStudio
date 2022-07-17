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

    }
}
