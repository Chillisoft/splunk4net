using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using PeanutButter.TestUtils.Generic;
using splunk4net.Buffering;

namespace splunk4net.Tests.Buffering
{
    [TestFixture]
    public class TestLogBufferItemRepositoryInMemory
    {
        [Test]
        public void Type_ShouldImplement_ILogBufferItemRepository()
        {
            //---------------Set up test pack-------------------
            var sut = typeof(LogBufferItemRepositoryInMemory);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.ShouldImplement<ILogBufferItemRepository>();

            //---------------Test Result -----------------------
        }

        [Test]
        public void Buffer_ShouldBufferTheItem()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var expected = RandomValueGen.GetRandomString(10, 20);
            //---------------Assert Precondition----------------
            CollectionAssert.IsEmpty(sut.ListBufferedLogItems());

            //---------------Execute Test ----------------------
            var beforeTest = DateTime.Now;
            sut.Buffer(expected);
            var afterTest = DateTime.Now;
            var result = sut.ListBufferedLogItems();

            //---------------Test Result -----------------------
            CollectionAssert.IsNotEmpty(result);
            Assert.AreEqual(1, result.Count());
            var first = result.First();
            Assert.AreEqual(expected, first.Data);
            Assert.AreEqual(1, first.Id);
            Assert.That(beforeTest, Is.LessThanOrEqualTo(first.Created));
            Assert.That(afterTest, Is.GreaterThanOrEqualTo(first.Created));
        }

        [Test]
        public void UnBuffer_ShouldUnbufferTheItem()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var data = RandomValueGen.GetRandomString(10, 20);
            var toRemove = sut.Buffer(RandomValueGen.GetRandomString(5, 15));
            var toKeep = sut.Buffer(data);

            //---------------Assert Precondition----------------
            Assert.AreEqual(1, toRemove);
            Assert.AreEqual(2, sut.ListBufferedLogItems().Count());

            //---------------Execute Test ----------------------
            sut.Unbuffer(toRemove);

            //---------------Test Result -----------------------
            var remaining = sut.ListBufferedLogItems();
            Assert.AreEqual(1, remaining.Count());
            var item = remaining.First();
            Assert.AreEqual(toKeep, item.Id);
        }

        [Test]
        public void Trim_ShouldTrimTheCollectionToTheDesiredSize_EvictingOldestFirst()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            sut.Buffer(RandomValueGen.GetRandomString(1, 10));
            var toKeep = sut.Buffer(RandomValueGen.GetRandomString(1, 10));

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            sut.Trim(1);

            //---------------Test Result -----------------------
            var remaining = sut.ListBufferedLogItems();
            Assert.AreEqual(1, remaining.Count());
            var item = remaining.First();
            Assert.AreEqual(toKeep, item.Id);
        }



        private LogBufferItemRepositoryInMemory Create()
        {
            return new LogBufferItemRepositoryInMemory();
        }
    }
}
