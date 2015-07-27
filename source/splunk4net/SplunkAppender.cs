﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
        public bool StoreForward { get; set; }

        public SplunkAppender(): this(new TaskRunner(),
                                        new SplunkWriterFactory(), 
                                        new LogBufferDatabase(GetBufferDatabasePathForApplication()),
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
        private ILogBufferDatabase _bufferDatabase;
        private ITimer _timer;
        private bool _splunkConfigured;
        private ITimerFactory _timerFactory;
        private const int TEN_MINUTES = 600000;

        internal SplunkAppender(ITaskRunner taskRunner,
                                ISplunkWriterFactory splunkWriterFactory, 
                                ILogBufferDatabase logBufferDatabase,
                                ITimerFactory timerFactory)
        {
            StoreForward = true;
            _taskRunner = taskRunner;
            _splunkWriterFactory = splunkWriterFactory;
            _bufferDatabase = logBufferDatabase;
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

        protected override void Append(LoggingEvent loggingEvent)
        {
            DoFirstTimeSplunkConfigurationRegistration();
            DoFirstTimeSendUnsent();
            ActualizeLogEventPropertyData(loggingEvent);
            var serialized = JsonConvert.SerializeObject(loggingEvent);
            var id = BufferIfAllowed(serialized);
            ScheduleSplunkLogFor(id, serialized);
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
            return StoreForward ? _bufferDatabase.Buffer(serialized) : -1;
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
            var unsent = _bufferDatabase.ListBufferedLogItems();
            unsent.ForEach(u =>
            {
                lock (_lock)
                {
                    var writer = _splunkWriterFactory.CreateFor(Name);
                    AttemptSplunkLog(u.Data, writer, new AsyncLogResult() { BufferId = u.Id });
                }
            });
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
                _bufferDatabase.Unbuffer(taskResult.BufferId);
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