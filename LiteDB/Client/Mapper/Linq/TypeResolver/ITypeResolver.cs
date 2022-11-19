using System.Reflection;

namespace LiteDB
{
    internal interface ITypeResolver
    {
        string ResolveMethod(MethodInfo method);

        string ResolveMember(MemberInfo member);

        string ResolveCtor(ConstructorInfo ctor);
    }
}