using System.Collections.Generic;
using System;
using System.IO;

using Xunit;
using System.Dynamic;

namespace LiteDB.Tests.Issues
{
    public class Issue2253_Tests
    {
        [Fact]
        public void DictionaryWithObject()
        {
            var ms = new MemoryStream();
            var litedb = new LiteDatabase(ms);

            var customProps = new Dictionary<string, object>
            {
                { "key1", 1 },
                { "key2", "Hello" },
                { "key3", true },
                { "key4", DateTime.Now },
                { "key5", new TimeSpan(1, 20, 0) }
            };
            dynamic exp = new ExpandoObject();
            exp.prop1 = "val1";
            exp.prop2 = "val2";
            customProps.Add("key6", exp);
            var scope = new UserScope
            {
                Policy = new Policy
                {
                    Properties = customProps
                }
            };

            var collection = litedb.GetCollection<UserScope>("test");
            collection.Insert(scope);

            litedb.Dispose();
            ms.Position = 0;
            litedb = new LiteDatabase(ms);
            collection = litedb.GetCollection<UserScope>("test");

            var getCustomPropsBack = collection.FindOne(_ => true);
            Assert.NotEqual(scope, getCustomPropsBack); // They are not equals because of ExpandoObject convert to object.
        }

        private class UserScope
        {
            public object Policy { get; set; }
        }

        private class Policy
        {
            public Dictionary<string, object> Properties { get; set; }
        }
    }
}
