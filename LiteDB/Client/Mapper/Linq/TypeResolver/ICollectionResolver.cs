using System.Reflection;

namespace LiteDB
{
    internal class ICollectionResolver : EnumerableResolver
    {
        public override string ResolveMethod(MethodInfo method)
        {
            // special Contains method
            switch (method.Name)
            {
                case "Contains": return "# ANY = @0";
            };

            return base.ResolveMethod(method);
        }
    }
}