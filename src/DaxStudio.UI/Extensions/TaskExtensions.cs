using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class TaskExtensions
    {
        //public static async void FireAndForget(this Task task)
        //{
        //    await task.ConfigureAwait(false);
        //}


        // sourced from https://www.meziantou.net/fire-and-forget-a-task-in-dotnet.htm
        public static void FireAndForget(this Task task)
        {
            // note: this code is inspired by a tweet from Ben Adams: https://twitter.com/ben_a_adams/status/1045060828700037125
            // Only care about tasks that may fault (not completed) or are faulted,
            // so fast-path for SuccessfullyCompleted and Canceled tasks.
            if (!task.IsCompleted || task.IsFaulted)
            {
                // use "_" (Discard operation) to remove the warning IDE0058: Because this call is not awaited, execution of the current method continues before the call is completed
                // https://docs.microsoft.com/en-us/dotnet/csharp/discards#a-standalone-discard
                _ = ForgetAwaited(task);
            }

            
        }

        // Allocate the async/await state machine only when needed for performance reason.
        // More info about the state machine: https://blogs.msdn.microsoft.com/seteplia/2017/11/30/dissecting-the-async-methods-in-c/?WT.mc_id=DT-MVP-5003978
        private async static Task ForgetAwaited(Task task)
        {
            try
            {
                // No need to resume on the original SynchronizationContext, so use ConfigureAwait(false)
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Nothing to do here
            }
        }

        public static async Task<T[]> WhenAll<T>(params Task<T>[] tasks)
        {
            var allTasks = Task.WhenAll(tasks);

            try
            {
                return await allTasks;
            }
            catch (Exception)
            {
                //ignore
            }

            throw allTasks.Exception ??
                  throw new Exception("AggregateException of all tasks was null. What the hell.");

        }

        public static async Task WhenAll(params Task[] tasks)
        {
            var allTasks = Task.WhenAll(tasks);

            try
            {
                await allTasks;
                return;
            }
            catch (Exception)
            {
                //ignore
            }

            throw allTasks.Exception ??
                  throw new Exception("AggregateException of all tasks was null. What the hell.");

        }

        public static async Task ParallelForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> asyncAction, int maxDegreeOfParallelism)
        {
            var throttler = new SemaphoreSlim(initialCount: maxDegreeOfParallelism);
            var tasks = source.Select(async item =>
            {
                await throttler.WaitAsync();
                try
                {
                    await asyncAction(item).ConfigureAwait(false);
                }
                finally
                {
                    throttler.Release();
                }
            });
            await TaskExtensions.WhenAll(tasks.ToArray());
        }
    }
}