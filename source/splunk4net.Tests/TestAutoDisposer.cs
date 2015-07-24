using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using PeanutButter.TestUtils.Generic;

namespace splunk4net.Tests
{
    [TestFixture]
    public class TestAutoDisposer
    {
        [Test]
        public void Type_ShouldImplement_IDisposable()
        {
            //---------------Set up test pack-------------------
            var sut = typeof (AutoDisposer);
            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.ShouldImplement<IDisposable>();

            //---------------Test Result -----------------------
        }

        [Test]
        public void Add_ShouldTrackDisposableWhichIsDisposedAsAutoDisposerIsDisposed()
        {
            //---------------Set up test pack-------------------
            var disposable = Substitute.For<IDisposable>();
            var sut = Create();

            //---------------Assert Precondition----------------
            disposable.DidNotReceive().Dispose();

            //---------------Execute Test ----------------------
            var result = sut.Add(disposable);
            Assert.AreEqual(result, disposable);
            disposable.DidNotReceive().Dispose();
            sut.Dispose();
            sut.Dispose();
            //---------------Test Result -----------------------
            disposable.Received(1).Dispose();
        }

        [Test]
        public void Dispose_ShouldDisposeInReverseOrderOfAddCalls()
        {
            //---------------Set up test pack-------------------
            var d1 = Substitute.For<IDisposable>();
            var d2 = Substitute.For<IDisposable>();
            var d3 = Substitute.For<IDisposable>();
            var sut = Create();

            //---------------Assert Precondition----------------
            d1.DidNotReceive().Dispose();

            //---------------Execute Test ----------------------
            sut.Add(d1);
            sut.Add(d2);
            sut.Add(d3);

            d1.DidNotReceive().Dispose();
            sut.Dispose();
            sut.Dispose();
            //---------------Test Result -----------------------
            Received.InOrder(() =>
            {
                d3.Dispose();
                d2.Dispose();
                d1.Dispose();
            });
        }

        private AutoDisposer Create()
        {
            return new AutoDisposer();
        }
    }
}
