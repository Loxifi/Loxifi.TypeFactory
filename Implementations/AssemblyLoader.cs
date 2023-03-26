using Loxifi.Caches;
using Loxifi.Interfaces;
using Loxifi.Interfaces.Settings;
using Loxifi.Settings;
using System.Reflection;

namespace Loxifi.Implementations
{
    public class AssemblyLoader : IAssemblyLoader
    {
        private readonly IAssemblyLoaderSettings _assemblyLoaderSettings;

        public AssemblyLoader(IAssemblyLoaderSettings? assemblyLoaderSettings = null)
        {
            _assemblyLoaderSettings = assemblyLoaderSettings ?? new AssemblyLoaderSettings();
        }

        public IEnumerable<string> ValidAssemblyPaths
        {
            get
            {
                foreach (string assemblyLoadPath in _assemblyLoaderSettings.AssemblyLoadDirectories)
                {
                    foreach (string ext in _assemblyLoaderSettings.AssemblyExtensions)
                    {
                        foreach (string file in Directory.EnumerateFiles(assemblyLoadPath, $"*{ext}"))
                        {
                            yield return file;
                        }
                    }
                }
            }
        }

        public Assembly Load(string path)
        {
#if NETCOREAPP3_0_OR_GREATER
            Assembly loaded = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#endif

#if NETSTANDARD
            Assembly loaded = Assembly.LoadFrom(path);
#endif
            return loaded;
        }
    }
}