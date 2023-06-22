using System.Reflection;

namespace Loxifi.Interfaces
{
    public interface IAssemblyCache
    {
        IEnumerable<Assembly> Loaded { get; }

        IEnumerable<Assembly> LoadedAndUnloaded { get; }

        IEnumerable<string> Unloaded { get; }

        Assembly GetByName(string name);

        bool TryGetOrLoad(string assemblyPath, out Assembly? result);
    }
}