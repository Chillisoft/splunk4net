using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Core;
using Newtonsoft.Json;
using splunk4net.Buffering;
using splunk4net.Splunk;
using splunk4net.TaskHelpers;
using splunk4net.Timers;

namespace splunk4net
{
    public class SplunkAppender: AppenderSkeleton
    {

        public string Index { get; set; }
        public string RemoteUrl { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public int MaxStore { get; set; }
        public bool StoreForward { get; set; }

        public SplunkAppender(): this(new TaskRunner(),
                                        new SplunkWriterFactory(), 
                                        new LogBufferItemRepositoryFactory(), 
                                        new TimerFactory())
        {
        }

        private static string GetBufferDatabasePathForApplication()
        {
            // this only works for actual apps: NUnit running through R# will b0rk
            //  as there is no EntryAssembly, apparently
            var entryPath = Assembly.GetEntryAssembly().CodeBase;
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var bufferBase = Path.Combine(appDataPath, "LogBuffer");
            var md5 = MD5.Create();
            var dbName = string.Join("", md5.ComputeHash(Encoding.UTF8.GetBytes(entryPath))
                                .Select(b => b.ToString("X2"))) + ".db";
            return Path.Combine(bufferBase, dbName);
        }

        private SemaphoreSlim _lock = new SemaphoreSlim(1);
        private Task<AsyncLogResult> _lastSplunkTask;
        private readonly ITaskRunner _taskRunner;
        private readonly ISplunkWriterFactory _splunkWriterFactory;
        private ILogBufferItemRepository _bufferItemRepository;
        private ITimer _timer;
        private bool _splunkConfigured;
        private ITimerFactory _timerFactory;
        private const int TEN_MINUTES = 600000;

        internal SplunkAppender(ITaskRunner taskRunner,
                                ISplunkWriterFactory splunkWriterFactory, 
                                ILogBufferItemRepositoryFactory logBufferItemRepositoryFactory,
                                ITimerFactory timerFactory)
        {
            StoreForward = true;
            MaxStore = 1024;
            _taskRunner = taskRunner;
            _splunkWriterFactory = splunkWriterFactory;
            _bufferItemRepository = logBufferItemRepositoryFactory.CreateRepository();
            _timerFactory = timerFactory;
        }

        protected override void OnClose()
        {
            // OnClose is called within the Finalizer for SkeletonAppender (the base of this class)
            //  Finalizers can be called before the object is fully constructed, so I've employed
            //  a strategy very similar to sticking my fingers in my ears and saying "lalalalalala"
            using (GetLock())
            {
                TryDispose(ref _timer);
                TryDispose(ref _lock);
            }
        }

        private void TryDispose<T>(ref T disposable) where T: class, IDisposable
        {
            var myCopy = disposable;
            JustDo(() => myCopy.Dispose());
            disposable = null;
        }

        private void JustDo(Action action)
        {
            try
            {
                action();
            }
            catch { }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            DoFirstTimeInitializations();
            ActualizeLogEventPropertyData(loggingEvent);
            var serialized = ConvertToJsonWithComplexMessageObjectsHandledProperly(loggingEvent);
            var id = BufferIfAllowed(serialized);
            ScheduleSplunkLogFor(id, serialized);
        }

        private static string ConvertToJsonWithComplexMessageObjectsHandledProperly(LoggingEvent loggingEvent)
        {
            return JsonConvert.SerializeObject(loggingEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4netMessageObjects());
        }

        private void DoFirstTimeInitializations()
        {
            DoFirstTimeSplunkConfigurationRegistration();
            DoFirstTimeSendUnsent();
        }

        private void DoFirstTimeSendUnsent()
        {
            using (GetLock())
            {
                if (_timer == null)
                {
                    _timer = _timerFactory.CreateFor(SendUnsent, TEN_MINUTES);
                }
            }
        }

        private static void ActualizeLogEventPropertyData(LoggingEvent loggingEvent)
        {
            loggingEvent.Fix = FixFlags.All;
        }

        private long BufferIfAllowed(string serialized)
        {
            return StoreForward ? _bufferItemRepository.Buffer(serialized) : -1;
        }

        private void DoFirstTimeSplunkConfigurationRegistration()
        {
            using (GetLock())
            {
                if (_splunkConfigured)
                    return;
                RegisterConfiguredSplunkConfig();
                _splunkConfigured = true;
            }
        }

        private void RegisterConfiguredSplunkConfig()
        {
            var canConfigure = new[] {Index, RemoteUrl, Login, Password}
                .Aggregate(true, (state, item) => state && !string.IsNullOrWhiteSpace(item));
            if (!canConfigure)
                return;
            _splunkWriterFactory.RegisterConfigFor(Name, Index, RemoteUrl, Login, Password);
        }

        private void SendUnsent()
        {
            var unsent = _bufferItemRepository.ListBufferedLogItems();
            unsent.ForEach(u =>
            {
                using (GetLock())
                {
                    var writer = _splunkWriterFactory.CreateFor(Name);
                    AttemptSplunkLog(u.Data, writer, new AsyncLogResult() { BufferId = u.Id });
                }
            });
            _bufferItemRepository.Trim(MaxStore);
        }

        private void ScheduleSplunkLogFor(long id, string serialized)
        {
            var writer = _splunkWriterFactory.CreateFor(Name);
            using (GetLock())
            {
                var logResult = new AsyncLogResult() { BufferId = id };
                AttemptSplunkLog(serialized, writer, logResult);
            }
        }

        private void AttemptSplunkLog(string serialized, ISplunkWriter writer, AsyncLogResult logResult)
        {
            _lastSplunkTask = _lastSplunkTask == null
                ? AttemptFirstLogViaSplunk(serialized, writer, logResult)
                : AttemptToContinueLogViaSplunk(serialized, writer, logResult);
        }

        private Task<AsyncLogResult> AttemptFirstLogViaSplunk(string serialized, ISplunkWriter writer, AsyncLogResult logResult)
        {
            return _taskRunner
                .Run(WriteWithWriter(writer, logResult, serialized))
                .Using(_taskRunner)
                .ContinueWith(DebufferOnSuccess);
        }

        private Task<AsyncLogResult> AttemptToContinueLogViaSplunk(string serialized, ISplunkWriter writer, AsyncLogResult logResult)
        {
            return _lastSplunkTask.Using(_taskRunner)
                .ContinueWith<AsyncLogResult>(t => WriteWithWriter(writer, logResult, serialized)())
                .Using(_taskRunner)
                .ContinueWith(DebufferOnSuccess);
        }

        private AsyncLogResult DebufferOnSuccess(Task<AsyncLogResult> lastTask)
        {
            var taskResult = lastTask.Result;
            if (taskResult.Success && taskResult.BufferId > 0)
                _bufferItemRepository.Unbuffer(taskResult.BufferId);
            return taskResult;
        }

        private Func<AsyncLogResult> WriteWithWriter(ISplunkWriter writer, AsyncLogResult lastResult, string data)
        {
            return () =>
            {
                lastResult.Success = writer.Log(data).Result;
                return lastResult;
            };
        }

        private LenientAutoLocker GetLock()
        {
            return new LenientAutoLocker(_lock);
        }
    }
}
