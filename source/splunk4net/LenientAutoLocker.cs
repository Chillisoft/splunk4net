using System;
using System.Threading;

namespace splunk4net
{
    public class LenientAutoLocker : IDisposable
    {
        private SemaphoreSlim _semaphore;

        public LenientAutoLocker(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
            _semaphore?.Wait();
        }

        public void Dispose()
        {
            try
            {
                _semaphore?.Release();
                _semaphore = null;
            }
            catch { /* do nothing, do not throw, on purpose, ever */ }
        }
    }
}