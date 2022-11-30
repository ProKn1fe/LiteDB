using System;
using System.IO;

using Xunit;

namespace LiteDB.Tests.Issues
{
    public class Issue2254_Tests
    {
        [Fact]
        public void SelectTest()
        {
            var ms = new MemoryStream();
            var litedb = new LiteDatabase(ms);

            var collection = litedb.GetCollection<TestData>();
            collection.Insert(new TestData()
            {
                Guid = Guid.NewGuid(),
                Name = "DC00001"
            });

            Assert.NotNull(collection.FindOne(a => a.Name.Contains('0'))); // Work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains('1'))); // Work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("01"))); // Not work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("001"))); // Not work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("0001"))); // Not work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("00001"))); // Work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("C00001"))); // Work
            Assert.NotNull(collection.FindOne(a => a.Name.Contains("DC00001"))); // Work
        }

        private class TestData
        {
            public Guid Guid { get; set; }
            public string Name { get; set; }
        }
    }
}
