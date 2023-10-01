using Loxifi.Caches;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace Loxifi.Collections
{
    internal class CachedAssemblyCollection : IEnumerable<CachedAssembly>
    {
        public CachedAssembly this[int index]
        {
            get
            {
                //I don't even know if this is atomic but its probably not
                lock (this._lock)
                {
                    return this._cachedAssemblies[index];
                }
            }
        }

        private readonly List<CachedAssembly> _cachedAssemblies = new();

        private readonly Dictionary<string, string> _cachedHashes = new();

        private readonly object _lock = new();

        private readonly HashSet<string> _loadedHashes = new();

        public int Count => this._cachedAssemblies.Count;

        public void Add(CachedAssembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            lock (this._lock)
            {
                string hash = this.GetAssemblyHash(assembly);

                if (_loadedHashes.Add(hash))
                {
                    this._cachedAssemblies.Add(assembly);
                }
            }
        }

        public IEnumerator<CachedAssembly> GetEnumerator()
        {
            for (int i = 0; i < this._cachedAssemblies.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        private static string HashFromFile(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);
            using SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        private string GetAssemblyHash(CachedAssembly assembly)
        {
            if (assembly.HasPath)
            {
                if (!_cachedHashes.TryGetValue(assembly.Path!, out string? hash))
                {
                    hash = HashFromFile(assembly.Path!);
                    _cachedHashes[assembly.Path!] = hash;
                }

                return hash;
            }

            if(assembly.HasName)
            {
                return assembly.Name;
            }

            throw new ArgumentException("Can not get assembly hash for assembly with no path or name");
        }
    }
}