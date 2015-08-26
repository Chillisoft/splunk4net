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
            if (_semaphore != null)
                _semaphore.Wait();
        }

        public void Dispose()
        {
            try
            {
                if (_semaphore != null)
                    _semaphore.Release();
                _semaphore = null;
            }
            finally { }
        }
    }
}