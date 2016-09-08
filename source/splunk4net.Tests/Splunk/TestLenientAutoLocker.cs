using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PeanutButter.TestUtils.Generic;

namespace splunk4net.Tests.Splunk
{
    [TestFixture]
    public class TestLenientAutoLocker
    {
        [Test]
        public void Type_ShouldImplement_IDisposable()
        {
            //---------------Set up test pack-------------------
            var sut = typeof(LenientAutoLocker);
            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.ShouldImplement<IDisposable>();

            //---------------Test Result -----------------------
        }

        [Test]
        public void Construct_GivenSemaphoreSlim_ShouldLockIt()
        {
            //---------------Set up test pack-------------------
            using (var semaphore = new SemaphoreSlim(1))
            {
                bool? gotLock = null;
                //---------------Assert Precondition----------------
                using (new LenientAutoLocker(semaphore))
                {
                    var barrier = new Barrier(2);
                    Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        Thread.Sleep(1000);
                        // ReSharper disable once AccessToDisposedClosure
                        gotLock = semaphore.Wait(100);
                    });
                    //---------------Execute Test ----------------------
                    barrier.SignalAndWait();
                    Thread.Sleep(2000);

                    //---------------Test Result -----------------------
                    Assert.IsNotNull(gotLock);
                    Assert.IsFalse(gotLock.Value);
                }
            }
        }

        [Test]
        public void Dispose_ShouldUnlockTheSemaphore()
        {
            //---------------Set up test pack-------------------
            using (var semaphore = new SemaphoreSlim(1))
            {

                //---------------Assert Precondition----------------

                //---------------Execute Test ----------------------
                using (new LenientAutoLocker(semaphore))
                {
                }

                //---------------Test Result -----------------------
                Assert.IsTrue(semaphore.Wait(1));
            }
        }

        [Test]
        public void Dispose_WhenCalledAgain_ShouldNotAttemptToUnlock()
        {
            //---------------Set up test pack-------------------
            using (var semaphore = new SemaphoreSlim(1))
            {
                //---------------Assert Precondition----------------
                var sut = new LenientAutoLocker(semaphore);
                //---------------Execute Test ----------------------
                sut.Dispose();
                sut.Dispose();
                //---------------Test Result -----------------------
                Assert.IsTrue(semaphore.Wait(1));
            }
        }


        [Test]
        public void Construct_And_Dispose_GivenNullSemaphore_ShouldNotCare()
        {
            //---------------Set up test pack-------------------

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            Assert.DoesNotThrow(() =>
            {
                using (new LenientAutoLocker(null))
                {
                }
            });

            //---------------Test Result -----------------------
        }


    }
}