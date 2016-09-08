using System;
using System.Threading;
using NUnit.Framework;
using PeanutButter.TestUtils.Generic;
using splunk4net.Timers;

namespace splunk4net.Tests.Timers
{
    [TestFixture]
    public class TestTimerFactory
    {
        [Test]
        public void Construct_ShouldNotThrow()
        {
            //---------------Set up test pack-------------------

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            // ReSharper disable once ObjectCreationAsStatement
            Assert.DoesNotThrow(() => new TimerFactory());

            //---------------Test Result -----------------------
        }

        [Test]
        public void Type_ShouldImplement_ITimerFactory()
        {
            //---------------Set up test pack-------------------
            var sut = typeof (TimerFactory);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.ShouldImplement<ITimerFactory>();

            //---------------Test Result -----------------------
        }

        [Test]
        public void CreateFor_GivenActionAndNoInterval_ShouldRunActionOnceInASecond()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var barrier1 = new Barrier(2);
            var barrier2 = new Barrier(2);
            var runs = 0;
            //---------------Assert Precondition----------------
            //---------------Execute Test ----------------------
            sut.CreateFor(() =>
            {
                if (runs == 0)
                    barrier1.SignalAndWait();
                if (++runs == 2)
                    barrier2.SignalAndWait();
                
            });
            barrier1.SignalAndWait();
            Thread.Sleep(1000);
            var start = DateTime.Now;
            barrier2.SignalAndWait();
            var end = DateTime.Now;
            //---------------Test Result -----------------------
            Assert.That(end - start, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(500)));
        }

        [Test]
        public void CreateFor_GivenActionAnd100MsIntervalAndState_ShouldRunActionOnceInASecond()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var barrier1 = new Barrier(2);
            var barrier2 = new Barrier(2);
            var runs = 0;
            //---------------Assert Precondition----------------
            //---------------Execute Test ----------------------
            sut.CreateFor(state =>
            {
                Console.WriteLine("runs: " + runs);
                if (runs == 0)
                    barrier1.SignalAndWait();
                if (++runs == 2)
                    barrier2.SignalAndWait();
                
            }, 1000, new object());
            barrier1.SignalAndWait();
            Thread.Sleep(1000);
            var start = DateTime.Now;
            barrier2.SignalAndWait();
            var end = DateTime.Now;
            //---------------Test Result -----------------------
            Assert.That(end - start, Is.LessThanOrEqualTo(TimeSpan.FromMilliseconds(500)));
        }


        private ITimerFactory Create()
        {
            return new TimerFactory();
        }
    }
}
