using System.Reflection;

namespace Loxifi.Interfaces
{
    public interface ITypeCache
    {
        List<Type> GetDerivedTypes(Assembly a, Type type, int timeoutMs);

        IReadOnlyList<Type> GetTypes(Assembly assembly, int timeoutMs);
    }
}