using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace splunk4net.Tests.Splunk
{
    [TestFixture]
    public class TestSplunkAppender
    {
        private string _dbPath;

        [OneTimeSetUp]
        public void TestFixtureSetup()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), ".db");
        }

        [OneTimeTearDown]
        public void TestFixtureTeardown()
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                /* do nothing, on purpose */
            }
        }

        private ILogBufferItemRepositoryFactory CreateSubstituteBufferFactoryWith(ILogBufferItemRepository repository = null)
        {
            var result = Substitute.For<ILogBufferItemRepositoryFactory>();
            result.CreateRepository().Returns(repository ?? Substitute.For<ILogBufferItemRepository>());
            return result;
        }

        [Test]
        public void Append_ShouldBufferLog()
        {
            //---------------Set up test pack-------------------
            var db = Substitute.For<ILogBufferItemRepository>();
            var factory = CreateSubstituteBufferFactoryWith(db);
            var sut = Create(bufferItemRepositoryFactory: factory);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var loggingEvent = CreateRandomLoggingEvent();
            sut.DoAppend(loggingEvent);

            //---------------Test Result -----------------------
            db.Received().Buffer(JsonConvert.SerializeObject(loggingEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects()));
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
            var db = Substitute.For<ILogBufferItemRepository>();
            var dbFactory = CreateSubstituteBufferFactoryWith(db);
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
                
            var sut = Create(taskRunner, factory, dbFactory);
            sut.Name = RandomValueGen.GetRandomString(1, 10);
            var logEvent = CreateRandomLoggingEvent();

            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor(() => true));

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.DoAppend(logEvent);

            //---------------Test Result -----------------------
            // not called just yet...
            factory.Received().CreateFor(sut.Name);
            writer.DidNotReceive().Log(Arg.Any<string>());
            logTask.Start();
            firstBarrier.SignalAndWait();
            writer.Received().Log(JsonConvert.SerializeObject(logEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects()));
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
            var db = Substitute.For<ILogBufferItemRepository>();
            var dbFactory = CreateSubstituteBufferFactoryWith(db);

            var sut = Create(taskRunner, factory, dbFactory);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor(() => true));
            var logEvent = CreateRandomLoggingEvent();
            var json = JsonConvert.SerializeObject(logEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects());
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
            var db = Substitute.For<ILogBufferItemRepository>();
            var dbFactory = CreateSubstituteBufferFactoryWith(db);

            var sut = Create(taskRunner, factory, dbFactory);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor(() => false));
            var logEvent = CreateRandomLoggingEvent();
            var json = JsonConvert.SerializeObject(logEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects());
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
            var db = Substitute.For<ILogBufferItemRepository>();
            var dbFactory = CreateSubstituteBufferFactoryWith(db);

            var sut = Create(taskRunner, factory, dbFactory);
            var writer = Substitute.For<ISplunkWriter>();
            factory.CreateFor(sut.Name).ReturnsForAnyArgs(ci => writer);
            writer.Log(Arg.Any<string>())
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor(() => true));
            var firstLogEvent = CreateRandomLoggingEvent("log1");
            var secondLogEvent = CreateRandomLoggingEvent("log2");
            var json1 = JsonConvert.SerializeObject(firstLogEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects());
            var json2 = JsonConvert.SerializeObject(secondLogEvent, Formatting.None, new JsonConverterWhichProducesHierachicalOutputOnLog4NetMessageObjects());
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
                .ReturnsForAnyArgs(ci => ImmediateTaskRunnerBuilder.CreateImmediateTaskFor(() => true));
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
            var logBufferDatabase = Substitute.For<ILogBufferItemRepository>();
            logBufferDatabase.ListBufferedLogItems().Returns(someItems);
            var dbFactory = CreateSubstituteBufferFactoryWith(logBufferDatabase);


            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = Create(timerFactory: timerFactory, bufferItemRepositoryFactory: dbFactory, factory:factory);
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

                logBufferDatabase.Trim(sut.MaxStore);
            });
        }

        private static List<ILogBufferItem> GetSomeRandomLogBufferItems()
        {
            var someItems = new List<ILogBufferItem>(new[]
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
                                        ILogBufferItemRepositoryFactory bufferItemRepositoryFactory = null,
                                        ITimerFactory timerFactory = null)
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var splunkAppender = new SplunkAppender(taskRunner ?? CreateImmediateTaskRunner(),
                factory ?? Substitute.For<ISplunkWriterFactory>(),
                bufferItemRepositoryFactory ?? Substitute.For<ILogBufferItemRepositoryFactory>(),
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
