using Loxifi.Caches;
using System.Collections;

namespace Loxifi.Collections
{
    public class CachedTypeCollection : IEnumerable<Type>
    {
        public Type this[int index]
        {
            get
            {
                //I don't even know if this is atomic but its probably not
                lock (this._lock)
                {
                    return this._cachedTypes[index];
                }
            }
        }

        private readonly List<Type> _cachedTypes = new();

        private readonly object _lock = new();

        private readonly HashSet<string> names = new();

        public int Count => this._cachedTypes.Count;

        public void Add(Type type)
        {
            lock (this._lock)
            {
                if (!this.names.Add(type.FullName))
                {
                    throw new ArgumentException("Type has already been added to the cache");
                }

                this._cachedTypes.Add(type);
            }
        }

        public void AddRange(IEnumerable<Type> types)
        {
            lock (this._lock)
            {
                foreach (Type type in types)
                {
                    if (!this.names.Add(type.FullName))
                    {
                        throw new ArgumentException("Type has already been added to the cache");
                    }

                    this._cachedTypes.Add(type);
                }
            }
        }

        public IEnumerator<Type> GetEnumerator()
        {
            for (int i = 0; i < _cachedTypes.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}