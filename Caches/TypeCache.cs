using Loxifi.Collections;
using Loxifi.Interfaces;
using System.Diagnostics;
using System.Reflection;

namespace Loxifi.Caches
{
    /// <summary>
    /// For loading/caching loaded types
    /// </summary>
    public class TypeCache : ITypeCache
    {
        private readonly Dictionary<Assembly, CachedTypeCollection> _backingData = new();

        private readonly Dictionary<Assembly, Dictionary<Type, CachedTypeCollection>> _derivedTypes = new();

        public CachedTypeCollection GetDerivedTypes(Assembly a, Type type, int timeoutMs)
        {
            if (!this._derivedTypes.TryGetValue(a, out Dictionary<Type, CachedTypeCollection> derived))
            {
                derived = new Dictionary<Type, CachedTypeCollection>();
                this._derivedTypes.Add(a, derived);
            }

            if (!derived.TryGetValue(type, out CachedTypeCollection derivedList))
            {
                derivedList = new CachedTypeCollection();

                foreach (Type t in this.GetTypes(a, timeoutMs))
                {
                    if (t.IsAssignableFrom(type))
                    {
                        derivedList.Add(t);
                    }
                }

                derived.Add(type, derivedList);
            }

            return derivedList;
        }

        /// <summary>
        /// Gets/Caches a collection of all types in the provided assembly
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public CachedTypeCollection GetTypes(Assembly assembly, int timeoutMs)
        {
            if (this._backingData.TryGetValue(assembly, out CachedTypeCollection? list))
            {
                return list;
            }

            return Dispatcher.Current.Invoke(() => this.CacheTypes(assembly, timeoutMs));
        }

        private CachedTypeCollection CacheTypes(Assembly assembly, int timeoutMs)
        {
            CachedTypeCollection types = new();

            this._backingData.Add(assembly, types);

            try
            {
                ManualResetEvent completionEvent = new(false);

                Thread thread = new(() =>
                {
                    try
                    {
                        Type[] foundTypes = assembly.GetTypes();
                        types.AddRange(foundTypes);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }

                    completionEvent.Set();
                });

                thread.Start();

                if (!completionEvent.WaitOne(timeoutMs))
                {
                    throw new TimeoutException();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception has occurred loading {assembly.FullName}");
                Debug.WriteLine(ex);
            }

            return types;
        }
    }
}