using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Loxifi.Caches
{
    [DebuggerDisplay("{Path,nq}")]
    internal class CachedAssembly
    {
        private CachedAssembly()
        {
        }

        public override int GetHashCode()
        {
            if (this.HasPath)
            {
                return this.Path!.GetHashCode();
            }

            if (this.HasName)
            {
                return this.Name!.GetHashCode();
            }

            throw new Exception("How did we get here?");
        }

        public static bool operator ==(CachedAssembly p1, CachedAssembly p2)
        {
            // If both are null, or both are the same instance, return true.
            if (ReferenceEquals(p1, p2))
            {
                return true;
            }

            // If one is null, but not both, return false.
            if (p1 is null || p2 is null)
            {
                return false;
            }

            // Return true if the fields match:
            return p1.Equals(p2);
        }

        public static bool operator !=(CachedAssembly p1, CachedAssembly p2) => !(p1 == p2);

        public override bool Equals(object? obj)
        {
            if (obj is CachedAssembly ca)
            {
                if (this.HasName && ca.HasName)
                {
                    return ca.Name == this.Name;
                }

                if(this.HasPath && ca.HasPath)
                {
                    return ca.Path == this.Path;
                } 
                
                //The only known way to fall through here is if one 
                //assembly is Dynamic and the other is an unloaded
                //on-disk assembly, in which case they are definitely
                //not equal
            }

            return false;
        }

        public bool HasPath => !string.IsNullOrWhiteSpace(this.Path);

        public bool HasName => !string.IsNullOrWhiteSpace(this.Name);

        public static CachedAssembly FromPath(string path, Assembly? a = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException($"'{nameof(path)}' cannot be null or whitespace.", nameof(path));
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Assembly not found at path", path);
            }

            CachedAssembly toReturn = new ()
            {
                Path = path,
                Assembly = a
            };

            return toReturn;
        }

        public static CachedAssembly FromAssembly(Assembly a)
        {
            if (a is null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            string fullName = a.FullName;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException($"'Assembly Name cannot be null or whitespace.");
            }

            CachedAssembly toReturn = new()
            {
                Assembly = a
            };

            if(!a.IsDynamic)
            {
                toReturn.Path = a.Location;
            }

            return toReturn;
        }

        public Assembly? Assembly { get; set; }

        public bool IsLoaded => this.Assembly != null;

        public bool LoadFailed { get; set; }

        public string? Name => this.Assembly?.FullName;

        public string? Path { get; private set; }
    }
}