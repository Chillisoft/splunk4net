using System;
using System.Collections.Generic;

namespace splunk4net
{
    // mimic of PeanutButter.Utils.AutoDisposer
    //  I just don't want to add a package dependency for one class
    public class AutoDisposer: IDisposable
    {
        private readonly object _lock = new object();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        public void Dispose()
        {
            Action<IDisposable> tryDispose = toDispose =>
            {
                try { toDispose.Dispose(); } catch { /* do nothing, on purpose */ }
            };
            lock(_lock)
            {

                _disposables.Reverse();
                _disposables.ForEach(tryDispose);
                _disposables.Clear();
            }
        }

        public T Add<T>(T disposable) where T: IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }
    }
}
