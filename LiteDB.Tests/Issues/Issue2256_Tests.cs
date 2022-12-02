using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

namespace LiteDB.Tests.Issues
{
    public class Issue2256_Tests
    {
        [Fact]
        public void TestLinqMax()
        {
#if NET5_0_OR_GREATER
            var random = Random.Shared;
#else
            var random = new Random();
#endif

            using var ms = new MemoryStream();
            using var litedb = new LiteDatabase(ms);

            var list = new List<TestModel>();
            for (var a = 0; a < 100; ++a)
            {
                list.Add(new TestModel() { Count = random.Next() });
            }
            var max = list.Max(a => a.Count);

            var collection = litedb.GetCollection<TestModel>();
            collection.InsertBulk(list);

            var maxFromCollection = collection.Max(a => a.Count);
            Assert.Equal(max, maxFromCollection);
        }

        private class TestModel
        {
            public int Count { get; set; }
        }
    }
}