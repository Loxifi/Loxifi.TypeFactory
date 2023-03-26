using Loxifi.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace Loxifi.Caches
{
    /// <summary>
    /// For getting/caching attributes
    /// </summary>
    public class AttributeCache : IAttributeCache
    {
        private readonly ConcurrentDictionary<MemberInfo, Attribute[]> _cachedAttributes = new();

        private readonly ConcurrentDictionary<MemberInfo, Attribute[]> _cachedInheritedAttributes = new();

        /// <summary>
        /// Gets a custom attribute for the member
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="inherited"></param>
        /// <returns></returns>
        public T? GetCustomAttribute<T>(MemberInfo m, bool inherited = true) where T : Attribute => this.GetCustomAttributes<T>(m, inherited).SingleOrDefault();

        /// <summary>
        /// Gets a custom attribute for the member
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <returns></returns>
        public TOut? GetCustomAttribute<TIn, TOut>() where TOut : Attribute => this.GetCustomAttribute<TOut>(typeof(TIn));

        /// <summary>
        /// Gets all custom attributes for the member
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="inherited"></param>
        /// <returns></returns>
        public IEnumerable<IAttributeInstance<T>> GetCustomAttributeInstances<T>(MemberInfo m, bool inherited = true) where T : Attribute
        {
            MemberInfo? check = m;

            do
            {
                foreach (T t in this.GetCustomAttributes<T>(check, false).OfType<T>())
                {
                    yield return new AttributeInstance<T>(check, t, m == check);
                }
            } while (inherited && (check = this.GetBase(m)) is not null);
        }

        /// <summary>
        /// Gets all custom attributes for the member
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="inherited"></param>
        /// <returns></returns>
        public IEnumerable<T> GetCustomAttributes<T>(MemberInfo m, bool inherited = true) where T : Attribute
        {
            if (!this._cachedAttributes.TryGetValue(m, out Attribute[] attributes))
            {
                List<Attribute> attributeList = new();

                try
                {
                    IEnumerator<Attribute> attributeEnumerator = m.GetCustomAttributes().GetEnumerator();

                    while (attributeEnumerator.MoveNext())
                    {
                        attributeList.Add(attributeEnumerator.Current);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                attributes = attributeList.ToArray();

                _ = this._cachedAttributes.TryAdd(m, attributes);
            }

            foreach (T t in attributes.OfType<T>())
            {
                yield return t;
            }

            if (inherited)
            {
                if (!this._cachedInheritedAttributes.TryGetValue(m, out Attribute[] inheritedAttributes))
                {
                    if (this.GetBase(m) is MemberInfo child)
                    {
                        inheritedAttributes = this.GetCustomAttributes<Attribute>(child, inherited).ToArray();
                    }
                    else
                    {
                        inheritedAttributes = Array.Empty<Attribute>();
                    }

                    _ = this._cachedInheritedAttributes.TryAdd(m, inheritedAttributes);
                }

                foreach (T ta in inheritedAttributes.OfType<T>())
                {
                    yield return ta;
                }
            }
        }

        /// <summary>
        /// Returns true if the member has the attribute
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="member"></param>
        /// <param name="inherited"></param>
        /// <returns></returns>
        public bool HasCustomAttribute<T>(MemberInfo member, bool inherited = true) where T : Attribute => this.GetCustomAttributes<T>(member, inherited).Any();

        private MemberInfo? GetBase(MemberInfo m)
        {
            if (m is Type t)
            {
                return t.BaseType;
            }
            else if (m is MethodInfo mi)
            {
                return mi.GetBaseDefinition();
            }

            return null;
        }
    }
}