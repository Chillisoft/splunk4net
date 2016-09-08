using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using PeanutButter.RandomGenerators;
using splunk4net.Buffering;
// ReSharper disable PossibleMultipleEnumeration

namespace splunk4net.Tests.Buffering
{
    [TestFixture]
    public class TestLogBufferItemRepositorySqlite
    {
        [Test]
        public void Constructor_ShouldCreateDatabase()
        {
            //---------------Set up test pack-------------------

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var sut1 = Create();

            //---------------Test Result -----------------------
            Assert.IsTrue(File.Exists(sut1.BufferDatabasePath));
            using (var connection = new SQLiteConnection($"Data source={sut1.BufferDatabasePath};Version=3"))
            {
                Assert.DoesNotThrow(() => connection.Open());
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "select * from LogBuffer;";
                    Assert.DoesNotThrow(() => cmd.ExecuteNonQuery());
                }
            }
        }

        [Test]
        public void Constructor_WhenGivenTheSameIdentifier_ShouldUseExistingDatabase()
        {
            //---------------Set up test pack-------------------
            var id = Guid.NewGuid().ToString();
            var first = Create(id);

            //---------------Assert Precondition----------------
            Assert.IsTrue(File.Exists(first.BufferDatabasePath));

            //---------------Execute Test ----------------------
            LogBufferItemRepositorySqlite second = null;
            Assert.DoesNotThrow(() => second = Create(id));

            //---------------Test Result -----------------------
            Assert.AreEqual(first.BufferDatabasePath, second.BufferDatabasePath);
        }

        [Test]
        public void Buffer_ShouldAddDataToDatabase()
        {
            //---------------Set up test pack-------------------
            var id = Guid.NewGuid().ToString();
            var sut = Create(id);
            var data = RandomValueGen.GetRandomString(10, 20);

            //---------------Assert Precondition----------------

            //---------------Execute Test ----------------------
            var bufferId = sut.Buffer(data);

            //---------------Test Result -----------------------
            Assert.AreNotEqual(0, bufferId);
            using (var conn = new SQLiteConnection(sut.ConnectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "select * from LogBuffer;";
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        var item = new LogBufferItem(reader);
                        Assert.AreEqual(bufferId, item.Id);
                        Assert.AreEqual(data, item.Data);
                        Assert.IsNull(item.Retries);
                        Console.WriteLine(item.Created);
                    }
                }
            }
        }

        [Test]
        public void Unbuffer_ShouldRemoveBufferedItemFromDatabase()
        {
            //---------------Set up test pack-------------------
            var id = Guid.NewGuid().ToString();
            var sut = Create(id);
            var data1 = RandomValueGen.GetRandomString(10, 20);
            var data2 = RandomValueGen.GetRandomString(10, 20);

            var toKeep = sut.Buffer(data1);
            var toChuck = sut.Buffer(data2);

            //---------------Assert Precondition----------------
            Assert.AreNotEqual(0, toKeep);
            Assert.AreNotEqual(0, toChuck);
            Assert.AreNotEqual(toKeep, toChuck);

            //---------------Execute Test ----------------------
            sut.Unbuffer(toChuck);

            //---------------Test Result -----------------------
            var stillBuffered = GetCurrentlyBufferedItemsIn(sut.ConnectionString);
            Assert.AreEqual(1, stillBuffered.Count);
            Assert.AreEqual(toKeep, stillBuffered.First().Id);
        }

        private List<LogBufferItem> GetCurrentlyBufferedItemsIn(string connectionString, string query = "select * from LogBuffer")
        {
            var result = new List<LogBufferItem>();
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = query;
                    using (var reader = cmd.ExecuteReader())
                    {
                        while(reader.Read())
                            result.Add(new LogBufferItem(reader));
                    }
                }
            }
            return result;
        }

        [Test]
        public void ListBuffered_WhenNothingBuffered_ShouldReturnEmptyCollection()
        {
            //---------------Set up test pack-------------------
            var sut = Create();

            //---------------Assert Precondition----------------
            CollectionAssert.IsEmpty(GetCurrentlyBufferedItemsIn(sut.ConnectionString));

            //---------------Execute Test ----------------------
            var result = sut.ListBufferedLogItems();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            CollectionAssert.IsEmpty(result);
        }

        [Test]
        public void ListBufferedLogItems_WhenOneItemBuffered_ShouldReturnIt()
        {
            //---------------Set up test pack-------------------
            var testStart = DateTime.UtcNow;
            var sut = Create();
            var expectedData = RandomValueGen.GetRandomString(1, 10);
            var expectedId = sut.Buffer(expectedData);

            //---------------Assert Precondition----------------
            CollectionAssert.IsNotEmpty(GetCurrentlyBufferedItemsIn(sut.ConnectionString));

            //---------------Execute Test ----------------------
            var result = sut.ListBufferedLogItems();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            CollectionAssert.IsNotEmpty(result);
            Assert.AreEqual(1, result.Count());
            var item = result.First();
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedId, item.Id);
            Assert.AreEqual(expectedData, item.Data);
            item.Created.ShouldBeGreaterThanOrEqualTo(testStart);
            item.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        }

        [Test]
        public void ListBufferedLogItems_WhenMultipleItemsBuffered_ShouldReturnThemInOrderOfCreation()
        {
            //---------------Set up test pack-------------------
            var testStart = DateTime.UtcNow;
            var sut = Create();
            var expectedData1 = RandomValueGen.GetRandomString(1, 10);
            var expectedId1 = sut.Buffer(expectedData1);
            Thread.Sleep(2000); // really, really lazy way to get logs with different creation times
            var expectedData2 = RandomValueGen.GetRandomString(1, 10);
            var expectedId2 = sut.Buffer(expectedData2);

            //---------------Assert Precondition----------------
            CollectionAssert.IsNotEmpty(GetCurrentlyBufferedItemsIn(sut.ConnectionString));

            //---------------Execute Test ----------------------
            var result = sut.ListBufferedLogItems();

            //---------------Test Result -----------------------
            Assert.IsNotNull(result);
            CollectionAssert.IsNotEmpty(result);
            Assert.AreEqual(2, result.Count());
            var item1 = result.First();
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedId1, item1.Id);
            Assert.AreEqual(expectedData1, item1.Data);
            item1.Created.ShouldBeGreaterThanOrEqualTo(testStart);
            item1.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);

            var item2 = result.Last();
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedId2, item2.Id);
            Assert.AreEqual(expectedData2, item2.Data);
            item2.Created.ShouldBeGreaterThanOrEqualTo(testStart);
            item2.Created.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
            Assert.That(item2.Created, Is.GreaterThan(item1.Created));
        }

        [Test]
        public void Trim_GivenTrimValue_WhenNothingToTrim_ShouldDoNothing()
        {
            //---------------Set up test pack-------------------
            var sut = Create();

            //---------------Assert Precondition----------------
            CollectionAssert.IsEmpty(GetCurrentlyBufferedItemsIn(sut.ConnectionString));

            //---------------Execute Test ----------------------
            Assert.DoesNotThrow(() => sut.Trim(1024));

            //---------------Test Result -----------------------
            CollectionAssert.IsEmpty(GetCurrentlyBufferedItemsIn(sut.ConnectionString));

        }

        [Test]
        public void Trim_GivenTrimValue_WhenTrimValueLargerThanBufferedAmount_ShouldDoNothing()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var howMany = RandomValueGen.GetRandomInt(4, 8);
            for (var i = 0; i < howMany; i++)
                sut.Buffer(RandomValueGen.GetRandomString(10, 10));

            //---------------Assert Precondition----------------
            Assert.AreEqual(howMany, GetCurrentlyBufferedItemsIn(sut.ConnectionString).Count);

            //---------------Execute Test ----------------------
            Assert.DoesNotThrow(() => sut.Trim(10));

            //---------------Test Result -----------------------
            Assert.AreEqual(howMany, GetCurrentlyBufferedItemsIn(sut.ConnectionString).Count);

        }

        [Test]
        public void Trim_GivenTrimValue_WhenTrimValueSmallerThanBufferedAmount_ShouldTrimOldest()
        {
            //---------------Set up test pack-------------------
            var sut = Create();
            var howMany = RandomValueGen.GetRandomInt(4, 8);
            var ids = new List<long>();
            for (var i = 0; i < howMany; i++)
                ids.Add(sut.Buffer(RandomValueGen.GetRandomString(10, 10)));
            ids.Reverse();
            var expected = ids.Take(2).ToArray();
            //---------------Assert Precondition----------------
            Assert.AreEqual(howMany, GetCurrentlyBufferedItemsIn(sut.ConnectionString).Count);

            //---------------Execute Test ----------------------
            sut.Trim(2);

            //---------------Test Result -----------------------
            var remaining = GetCurrentlyBufferedItemsIn(sut.ConnectionString);
            Assert.AreEqual(2, remaining.Count);
            var reaminingIds = remaining.Select(r => r.Id).ToArray();
            CollectionAssert.AreEquivalent(expected, reaminingIds);

        }

        private readonly List<string> _toDelete = new List<string>();
        private LogBufferItemRepositorySqlite Create(string id = null)
        {
            var dbPath = Path.Combine(Path.GetTempPath(), (id ?? Guid.NewGuid() + ".db"));
            _toDelete.Add(dbPath);
            return new LogBufferItemRepositorySqlite(dbPath);
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            _toDelete.ForEach(p =>
            {
                try { File.Delete(p); } catch { /* do nothing, on purpose */ }
            });
        }
    }


    public static class SaneDateExtensions
    {
        // dates which should be coming back equal aren't, according to NUnit
        //  in addition, it appears that sqlite doesn't want to store precision > seconds
        public static void ShouldBeGreaterThanOrEqualTo(this DateTime self, DateTime other)
        {
            Assert.That(self.Year, Is.GreaterThanOrEqualTo(other.Year));
            Assert.That(self.Month, Is.GreaterThanOrEqualTo(other.Month));
            Assert.That(self.Day, Is.GreaterThanOrEqualTo(other.Day));
            Assert.That(self.Hour, Is.GreaterThanOrEqualTo(other.Hour));
            Assert.That(self.Minute, Is.GreaterThanOrEqualTo(other.Minute));
            Assert.That(self.Second, Is.GreaterThanOrEqualTo(other.Second));
        }
        public static void ShouldBeLessThanOrEqualTo(this DateTime self, DateTime other)
        {
            Assert.That(self.Year, Is.LessThanOrEqualTo(other.Year));
            Assert.That(self.Month, Is.LessThanOrEqualTo(other.Month));
            Assert.That(self.Day, Is.LessThanOrEqualTo(other.Day));
            Assert.That(self.Hour, Is.LessThanOrEqualTo(other.Hour));
            Assert.That(self.Minute, Is.LessThanOrEqualTo(other.Minute));
            Assert.That(self.Second, Is.LessThanOrEqualTo(other.Second));
        }
    }
}
