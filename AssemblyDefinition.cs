using System.Reflection;

namespace Loxifi
{
    internal class AssemblyDefinition
    {
        public Assembly? ContainingAssembly { get; set; }

        public List<Type> LoadedTypes { get; set; } = new List<Type>();
    }
}