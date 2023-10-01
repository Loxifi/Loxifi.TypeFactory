using Loxifi.Collections;
using System.Reflection;

namespace Loxifi.Interfaces
{
    public interface ITypeCache
    {
        CachedTypeCollection GetDerivedTypes(Assembly a, Type type, int timeoutMs);

        CachedTypeCollection GetTypes(Assembly assembly, int timeoutMs);
    }
}