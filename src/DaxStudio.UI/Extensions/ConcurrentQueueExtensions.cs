using System.Collections.Concurrent;

namespace DaxStudio.UI.Extensions
{
    public static class ConcurrentQueueExtensions
    {
        public static void Clear<T>(this ConcurrentQueue<T> queue)
        {
            while(!queue.IsEmpty)
            {
                queue.TryDequeue(out var _);
            }
        }
    }
}
