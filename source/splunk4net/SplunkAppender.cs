using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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
                                        new LogBufferItemRepository(GetBufferDatabasePathForApplication()),
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

        private object _lock = new object();
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
                                ILogBufferItemRepository logBufferItemRepository,
                                ITimerFactory timerFactory)
        {
            StoreForward = true;
            MaxStore = 1024;
            _taskRunner = taskRunner;
            _splunkWriterFactory = splunkWriterFactory;
            _bufferItemRepository = logBufferItemRepository;
            _timerFactory = timerFactory;
        }

        protected override void OnClose()
        {
            lock(_lock)
            {
                if (_timer != null)
                    _timer.Dispose();
                _timer = null;
            }
        }

        public class MyResolver : DefaultContractResolver, IContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return type.GetProperties()
                    .Select(pi => new JsonProperty()
                    {
                        PropertyName = pi.Name,
                        PropertyType = pi.PropertyType,
                        Readable = true,
                        Writable = true,
                        ValueProvider = base.CreateMemberValueProvider(type.GetMember(pi.Name).First())
                    }).ToList();
            }

            protected override JsonISerializableContract CreateISerializableContract(Type objectType)
            {
                var jsonISerializableContract = base.CreateISerializableContract(objectType);
                return jsonISerializableContract;
            }
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
            return JsonConvert.SerializeObject(loggingEvent, Formatting.None, new JsonConverterWhichTriesHarderOnMessageObjects());
        }

        private void DoFirstTimeInitializations()
        {
            DoFirstTimeSplunkConfigurationRegistration();
            DoFirstTimeSendUnsent();
        }

        private void DoFirstTimeSendUnsent()
        {
            lock(_lock)
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
            lock(_lock)
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
                lock (_lock)
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
            lock(_lock)
            {
                var logResult = new AsyncLogResult() {BufferId = id};
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
    }
}
