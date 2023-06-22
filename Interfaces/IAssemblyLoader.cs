using System.Reflection;

namespace Loxifi.Interfaces
{
    public interface IAssemblyLoader
    {
        IEnumerable<string> ValidAssemblyPaths { get; }

        Assembly Load(string path);
    }
}