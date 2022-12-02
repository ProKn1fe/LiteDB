using System.Collections.Generic;
using System.Globalization;

using Xunit;

namespace LiteDB.Tests.Issues
{
    public class Issue2255_Tests
    {
        [Fact]
        public void GlobalizationTest()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var connectionString = new ConnectionString("dbfile.db") { Upgrade = true };
            var db = new LiteDatabase(connectionString);
            var table = db.GetCollection<DatabaseRow>("data");
            var row = new DatabaseRow();
            row.Values = new Dictionary<double, double>();
            row.Values[9.9] = 1.23;
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("de-DE");
            row.Values[10.10] = 1.23;
            table.Insert(row);

            db.Dispose();
            db = new LiteDatabase(connectionString);
            table = db.GetCollection<DatabaseRow>("data");

            var rowBack = table.FindOne(_ => true);

            var table1 = db.GetCollection("data");
            var row1 = table1.FindOne(_ => true);

            Assert.Equal(row.Values, rowBack.Values);
        }

        private class DatabaseRow
        {
            public int Id { get; set; }
            public Dictionary<double, double> Values { get; set; }
        }
    }
}
