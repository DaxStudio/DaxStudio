using System;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
{
    internal class SemaphoreSlimLock: IDisposable
    {
        private SemaphoreSlim _semaphore;
        public SemaphoreSlimLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
            _semaphore.Wait();
        }

        public void Dispose()
        {
            _semaphore.Release();
        }
    }
}
