using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class TaskExtensions
    {
        public static async void FireAndForget(this Task task)
        {
            await task.ConfigureAwait(false);
        }
    }
}