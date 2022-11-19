using System;

namespace LiteDB.Stress
{
    public interface ITestItem
    {
        string Name { get; }
        int TaskCount { get; }
        TimeSpan Sleep { get; }
        BsonValue Execute(LiteDatabase db);
    }
}
