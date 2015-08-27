using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace splunk4net.Buffering
{
    public class LogBufferItemRepositoryInMemory: ILogBufferItemRepository
    {
        private List<ILogBufferItem> _buffer;
        private object _lock;
        private int _lastId;

        public LogBufferItemRepositoryInMemory()
        {
            _buffer = new List<ILogBufferItem>();
            _lock = new object();
        }

        public long Buffer(string data)
        {
            lock (_lock)
            {
                _buffer.Add(new LogBufferItem(++_lastId, data, DateTime.Now));
                return _lastId;
            }
        }

        public void Unbuffer(long id)
        {
            lock (_lock)
            {
                var toRemove = _buffer.FirstOrDefault(b => b.Id == id);
                if (toRemove != null)
                    _buffer.Remove(toRemove);
            }
        }

        public IEnumerable<ILogBufferItem> ListBufferedLogItems()
        {
            return _buffer;
        }

        public void Trim(int maxRemaining)
        {
            lock (_lock)
            {
                var toRemove = _buffer.OrderByDescending(b => b.Id).Skip(maxRemaining).ToArray();
                toRemove.ForEach(item => _buffer.Remove(item));
            }
        }
    }
}
