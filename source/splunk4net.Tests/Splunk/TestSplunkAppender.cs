using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net.Core;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using splunk4net.Buffering;
using splunk4net.Splunk;
using splunk4net.TaskHelpers;
using splunk4net.Timers;

namespace splunk4net.Tests
{
    [TestFixture]
    public class TestSplunkAppender
    {
        private string _dbPath;

        [TestFixtureSetUp]
        public void TestFixtureSetup()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".db");
        }

        [TestFixtureTearDown]
        public void TestFixtureTeardown()
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
            }
        }

        [Test]
        public void Append_ShouldBufferLog()
        {
            //---------------Set up test pack-------------------
            var db = Substitute.For<ILogBufferDatabase>();
            var sut = Create(bufferDatabase: db);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var loggingEvent = CreateRandomLoggingEvent();
            sut.DoAppend(loggingEvent);

            //---------------Test Result -----------------------
            db.Received().Buffer(JsonConvert.SerializeObject(loggingEvent));
        }

        private static LoggingEvent CreateRandomLoggingEvent(string message = null)
        {
            return new LoggingEvent(new LoggingEventData()
            {
                LoggerName = RandomValueGen.GetRandomString(1, 10),
                Message = message ?? RandomValueGen.GetRandomString(1, 10),
                Level = Level.Info
            });
        }

        [Test]
        public void Append_GivenLoggingEvent_WhenIsFirstEvent_ShouldKickOffTaskToLogToSplunk()
        {
            //---------------Set up test pack-------------------
            var factory = Substitute.For<ISplunkWriterFactory>();
            var taskRunner = Substitute.For<ITaskRunner>();
            var db = Substitute.For<ILogBufferDatabase>();
            Task<AsyncLogResult> logTask = null;
            var firstBarrier = new Barrier(2);
            var secondBarrier = new Barrier(2);
            taskRunner.Run(Arg.Any<Func<AsyncLogResult>>())
                .ReturnsForAnyArgs(ci =>
                {
                    logTask = new Task<AsyncLogResult>(() =>
                        {
                            var result = ci.Arg<Func<AsyncLogResult>>()();
                            firstBarrier.SignalAndWait();
                            secondBarrier.SignalAndWait();    // don't let this exit here to prove unbuffer didn't happen
                            return result;
                        });
                    return logTask;
                });
                
            var sut = Create(taskRunner, factory, db);
            sut.Name = RandomValueGen.GetRandomString(1, 10);
            var logEvent = CreateRandomLoggingEvent();

            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor<bool>(() => true));

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.DoAppend(logEvent);

            //---------------Test Result -----------------------
            // not called just yet...
            factory.Received().CreateFor(sut.Name);
            writer.DidNotReceive().Log(Arg.Any<string>());
            logTask.Start();
            firstBarrier.SignalAndWait();
            writer.Received().Log(JsonConvert.SerializeObject(logEvent));
            db.DidNotReceive().Unbuffer(Arg.Any<long>());
        }

        [Test]
        public void Append_GivenLoggingEvent_WhenLogIsSuccessful_ShouldUnbuffer()
        {
            //---------------Set up test pack-------------------
            var factory = Substitute.For<ISplunkWriterFactory>();
            var taskRunner = ImmediateTaskRunnerBuilder.Create()
                .WithSupportForTaskOfType<AsyncLogResult>()
                .WithSupportForContinuationOfType<AsyncLogResult, AsyncLogResult>()
                .Build();
            var db = Substitute.For<ILogBufferDatabase>();

            var sut = Create(taskRunner, factory, db);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor<bool>(() => true));
            var logEvent = CreateRandomLoggingEvent();
            var json = JsonConvert.SerializeObject(logEvent);
            var bufferId = RandomValueGen.GetRandomLong(100, 200);
            db.Buffer(json).Returns(bufferId);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.DoAppend(logEvent);

            //---------------Test Result -----------------------
            Received.InOrder(() =>
            {
                db.Buffer(json);
                factory.CreateFor(sut.Name);
                writer.Log(json);
                db.Unbuffer(bufferId);
            });
        }

        [Test]
        public void Append_GivenLoggingEvent_WhenLogIsNotSuccessful_ShouldNotUnbuffer()
        {
            //---------------Set up test pack-------------------
            var factory = Substitute.For<ISplunkWriterFactory>();
            var taskRunner = ImmediateTaskRunnerBuilder.Create()
                .WithSupportForTaskOfType<AsyncLogResult>()
                .WithSupportForContinuationOfType<AsyncLogResult, AsyncLogResult>()
                .Build();
            var db = Substitute.For<ILogBufferDatabase>();

            var sut = Create(taskRunner, factory, db);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor<bool>(() => false));
            var logEvent = CreateRandomLoggingEvent();
            var json = JsonConvert.SerializeObject(logEvent);
            var bufferId = RandomValueGen.GetRandomLong(100, 200);
            db.Buffer(json).Returns(bufferId);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.DoAppend(logEvent);

            //---------------Test Result -----------------------
            Received.InOrder(() =>
            {
                db.Buffer(json);
                factory.CreateFor(sut.Name);
                writer.Log(json);
            });
            taskRunner.Received().Run(Arg.Any<Func<AsyncLogResult>>());
            taskRunner.Received().Continue(Arg.Any<Task<AsyncLogResult>>());
            db.DidNotReceive().Unbuffer(Arg.Any<long>());
        }

        [Test]
        public void Append_GivenSequenceOfLoggingEvents_ShouldLogThemInOrder()
        {
            //---------------Set up test pack-------------------
            var factory = Substitute.For<ISplunkWriterFactory>();
            var taskRunner = ImmediateTaskRunnerBuilder.Create()
                .WithSupportForTaskOfType<AsyncLogResult>()
                .WithSupportForContinuationOfType<AsyncLogResult, AsyncLogResult>()
                .Build();
            var db = Substitute.For<ILogBufferDatabase>();

            var sut = Create(taskRunner, factory, db);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor<bool>(() => true));
            var firstLogEvent = CreateRandomLoggingEvent("log1");
            var secondLogEvent = CreateRandomLoggingEvent("log2");
            var json1 = JsonConvert.SerializeObject(firstLogEvent);
            var json2 = JsonConvert.SerializeObject(secondLogEvent);
            db.Buffer(Arg.Any<string>()).ReturnsForAnyArgs(ci =>
            {
                var data = ci.Arg<string>();
                return data == json1 ? 1 : 2;
            });

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.DoAppend(firstLogEvent);
            sut.DoAppend(secondLogEvent);

            //---------------Test Result -----------------------
            Received.InOrder(() =>
            {
                db.Buffer(json1);
                factory.CreateFor(sut.Name);
                writer.Log(json1);
                db.Unbuffer(1);
                // immediate task runner causes all tasks to be run sequentially, one after the other
                db.Buffer(json2);
                factory.CreateFor(sut.Name);
                writer.Log(json2);
                db.Unbuffer(2);
            });
        }

        [Test]
        public void FirstAppend_ShouldInstructTimerFactoryToCreateTimerToFlushBufferedLogsEvery10Minutes()
        {
            //---------------Set up test pack-------------------
            var timerFactory = Substitute.For<ITimerFactory>();
            var factory = Substitute.For<ISplunkWriterFactory>();
            var writer = Substitute.For<ISplunkWriter>();
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor<bool>(() => true));
            var timer = Substitute.For<ITimer>();
            var configuredInterval = 0;
            Action configuredAction = null;

            timerFactory.CreateFor(Arg.Any<Action>(), Arg.Any<int>())
                .ReturnsForAnyArgs(ci =>
                {
                    configuredInterval = ci.Arg<int>();
                    configuredAction = ci.Arg<Action>();
                    return timer;
                });

            var someItems = GetSomeRandomLogBufferItems();
            var logBufferDatabase = Substitute.For<ILogBufferDatabase>();
            logBufferDatabase.ListBufferedLogItems().Returns(someItems);


            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = Create(timerFactory: timerFactory, bufferDatabase: logBufferDatabase, factory:factory);
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);

            Assert.IsNull(configuredAction);
            sut.DoAppend(CreateRandomLoggingEvent("log1"));

            //---------------Test Result -----------------------
            Assert.IsNotNull(configuredAction);
            Assert.AreEqual(600000, configuredInterval);
            configuredAction();
            var firstItem = someItems.First();
            var secondItem = someItems.Last();
            Received.InOrder(() =>
            {
                factory.CreateFor(sut.Name);    // first append does this; task runner should prevent cascading of actual log

                logBufferDatabase.ListBufferedLogItems();
                factory.CreateFor(sut.Name);
                writer.Log(firstItem.Data);
                logBufferDatabase.Unbuffer(firstItem.Id);
                
                factory.CreateFor(sut.Name);
                writer.Log(secondItem.Data);
                logBufferDatabase.Unbuffer(secondItem.Id);
            });
        }

        private static List<LogBufferItem> GetSomeRandomLogBufferItems()
        {
            var someItems = new List<LogBufferItem>(new[]
            {
                CreateRandomLogBufferItem(),
                CreateRandomLogBufferItem()
            });
            while (someItems[0].Id == someItems[1].Id)
                someItems[1] = CreateRandomLogBufferItem();
            return someItems;
        }

        private static LogBufferItem CreateRandomLogBufferItem()
        {
            return new LogBufferItem(RandomValueGen.GetRandomInt(1, 100), RandomValueGen.GetRandomString(10, 20),
                RandomValueGen.GetRandomDate());
        }


        private SplunkAppender Create(ITaskRunner taskRunner = null,
                                        ISplunkWriterFactory factory = null,
                                        ILogBufferDatabase bufferDatabase = null,
                                        ITimerFactory timerFactory = null)
        {
            var splunkAppender = new SplunkAppender(taskRunner ?? CreateImmediateTaskRunner(),
                factory ?? Substitute.For<ISplunkWriterFactory>(),
                bufferDatabase ?? Substitute.For<ILogBufferDatabase>(),
                timerFactory ?? Substitute.For<ITimerFactory>());
            splunkAppender.Name = RandomValueGen.GetRandomString(10, 20);
            return splunkAppender;
        }

        private static ITaskRunner CreateImmediateTaskRunner()
        {
            return ImmediateTaskRunnerBuilder.Create()
                .WithSupportForTaskOfType<AsyncLogResult>()
                .WithSupportForContinuationOfType<AsyncLogResult, AsyncLogResult>()
                .Build();
        }
    }

}
