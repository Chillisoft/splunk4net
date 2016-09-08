using System;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using splunk4net.Buffering;

namespace splunk4net.Tests.Buffering
{
    [TestFixture]
    public class TestLogBufferItem
    {
        [Test]
        public void Construct_ShouldCopyParametersToProperties()
        {
            //---------------Set up test pack-------------------
            var id = RandomValueGen.GetRandomInt();
            var data = RandomValueGen.GetRandomString();
            var created = RandomValueGen.GetRandomDate();

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = new LogBufferItem(id, data, created);

            //---------------Test Result -----------------------
            Assert.AreEqual(id, sut.Id);
            Assert.AreEqual(data, sut.Data);
            Assert.AreEqual(created, sut.Created);
            Assert.IsNull(sut.Retries);
        }

        [Test]
        public void Construct_GivenOnlyData_ShouldSetPropertiesCorrectly()
        {
            //---------------Set up test pack-------------------
            var start = DateTime.UtcNow;
            var data = RandomValueGen.GetRandomString(1, 10);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut = Create(data);

            //---------------Test Result -----------------------
            Assert.AreEqual(0, sut.Id);
            Assert.AreEqual(data, sut.Data);
            sut.Created.ShouldBeGreaterThanOrEqualTo(start);
            sut.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        private static LogBufferItem Create(string data)
        {
            return new LogBufferItem(data);
        }

        [Test]
        public void AsInsertSql_ShouldReturnValidQuotedInsertSql()
        {
            //---------------Set up test pack-------------------
            var data = RandomValueGen.GetRandomString(1, 10) + "'" + RandomValueGen.GetRandomString(4, 5);
            var expected = "insert into LogBuffer (data) values ('" + data.Replace("'", "''") + "');";
            var sut = Create(data);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var result = sut.AsInsertSql();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            Assert.AreEqual(expected, result);
        }

    }


}
