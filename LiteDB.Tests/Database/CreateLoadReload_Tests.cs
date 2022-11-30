﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;
using Xunit.Extensions.Ordering;

[assembly: TestCaseOrderer("Xunit.Extensions.Ordering.TestCaseOrderer", "Xunit.Extensions.Ordering")]
[assembly: TestCollectionOrderer("Xunit.Extensions.Ordering.CollectionOrderer", "Xunit.Extensions.Ordering")]

namespace LiteDB.Tests.Database
{
    public class CreateLoadReload_Tests
    {
        public byte[] Data { get; set; } = new byte[8192 * 8192];
        public List<TestModel> TestModels { get; set; }

        public CreateLoadReload_Tests()
        {
            TestModels = new List<TestModel>(Random.Shared.Next(50, 300));
            for (var a = 0; a < TestModels.Capacity; ++a)
            {
                TestModels.Add(new TestModel
                {
                    Name = Random.Shared.Next().ToString(),
                    Age = Random.Shared.Next()
                });
            }
        }

        [Fact, Order(1)]
        public void CreateAndFillDatabase()
        {
            using var ms = new MemoryStream();
            using var litedb = new LiteDatabase(ms);

            var collection = litedb.GetCollection<TestModel>();
            foreach (var model in TestModels)
                collection.Insert(model);

            Assert.Equal(collection.Count(), TestModels.Count);

            litedb.Dispose();
            Data = ms.ToArray();
        }

        [Fact, Order(2)]
        public void CheckDatabaseFill()
        {
            CreateAndFillDatabase();

            using var ms = new MemoryStream(Data);
            using var litedb = new LiteDatabase(ms);

            var collection = litedb.GetCollection<TestModel>();
            Assert.Equal(collection.Count(), TestModels.Count);

            foreach (var item in collection.FindAll())
            {
                var itemFromList = TestModels.Find(a => a.Name == item.Name);
                Assert.True(itemFromList != null);
            }

            litedb.Dispose();
            Data = ms.ToArray();
        }

        //[Fact, Order(3)]
        //public void CleanDatabase()
        //{
        //    CheckDatabaseFill();

        //    using var ms = new MemoryStream(Data);
        //    using var litedb = new LiteDatabase(ms);

        //    var collection = litedb.GetCollection<TestModel>();
        //    Assert.Equal(collection.Count(), TestModels.Count);

        //    var removed = collection.DeleteAll();
        //    Assert.Equal(removed, TestModels.Count);

        //    Assert.Equal(0, collection.Count());
        //}
    }

    public class TestModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
    }
}
