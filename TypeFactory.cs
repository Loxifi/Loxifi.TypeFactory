﻿using Loxifi.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Loxifi
{
    /// <summary>
    /// A  class used to perform all kinds of type based reflections.Used for finding and resolving many kinds of queries
    /// </summary>
    public class TypeFactory
    {
        private readonly TypeFactorySettings _settings;

        private readonly IAssemblyCache _assemblyCache;

        private readonly ITypeCache _typeCache;

        private readonly IPropertyCache _propertyCache;

        private readonly IAttributeCache _attributeCache;

        public TypeFactory(TypeFactorySettings settings)
        {
            this._settings = settings;
            this._assemblyCache = settings.AssemblyCache;
            this._typeCache = settings.TypeCache;
            this._propertyCache = settings.PropertyCache;
            this._attributeCache = settings.AttributeCache;
        }

        public static TypeFactory Default { get; set; } = new TypeFactory(new TypeFactorySettings());

        /// <summary>
        /// Gets all types in whitelisted assemblies that implement a given interface
        /// </summary>
        /// <typeparam name="T">The interface to check for</typeparam>
        /// <param name="includeAbstract">If true, the result set will include abstract types</param>
        /// <returns>All of the aforementioned types</returns>
        public IEnumerable<Type> GetAllImplementations<T>(bool includeAbstract = false) => this.GetAllImplementations(typeof(T), includeAbstract);

        /// <summary>
        /// Gets all types in whitelisted assemblies that implement a given interface
        /// </summary>
        /// <param name="interfaceType">The interface to check for, will also search for implementations of open generics</param>
        /// <param name="includeAbstract">If true, the result set will include abstract types</param>
        /// <returns>All of the aforementioned types</returns>
        public IEnumerable<Type> GetAllImplementations(Type interfaceType, bool includeAbstract = false, bool loadUnloaded = true)
        {
            if (interfaceType is null)
            {
                throw new ArgumentNullException(nameof(interfaceType));
            }

            IEnumerable<Type> candidates = this.GetAllTypes(loadUnloaded);

            if (interfaceType.IsGenericTypeDefinition)
            {
                foreach (Type t in candidates)
                {
                    if (!includeAbstract && t.IsAbstract)
                    {
                        continue;
                    }

                    bool isValid = t.GetInterfaces().Any(x =>
                      x.IsGenericType &&
                      x.GetGenericTypeDefinition() == interfaceType);

                    if (isValid)
                    {
                        yield return t;
                    }
                }
            }
            else
            {
                foreach (Type t in candidates.Where(p => interfaceType.IsAssignableFrom(p) && (includeAbstract || !p.IsAbstract)))
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Gets all types from all assemblies
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Type> GetAllTypes(bool loadUnloaded)
        {
            IEnumerable<Assembly> allAssemblies;

            if (!loadUnloaded)
            {
                allAssemblies = this._assemblyCache.Loaded;
            }
            else
            {
                allAssemblies = this._assemblyCache.LoadedAndUnloaded;
            }

            ConcurrentBag<Type> types = new();

            Parallel.ForEach(allAssemblies,
            new ParallelOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            a =>
            {
#if DEBUG
                string assemblyName = a.FullName;
#endif
                foreach (Type t in this.GetAssemblyTypes(a))
                {
                    types.Add(t);
                }
            });

            return types;
        }

        /// <summary>
        /// Gets an assembly by its AssemblyName
        /// </summary>
        /// <param name="name">The AssemblyName</param>
        /// <returns>The matching Assembly</returns>
        public Assembly GetAssemblyByName(AssemblyName name)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return this.GetAssemblyByName(name.Name);
        }

        /// <summary>
        /// Gets an assembly by its Assembly Name
        /// </summary>
        /// <param name="name">The AssemblyName.Name</param>
        /// <returns>The matching Assembly</returns>
        public Assembly GetAssemblyByName(string name) => this._assemblyCache.GetByName(name);

        /// <summary>
        /// Gets all types in the specified assembly (where not compiler generated)
        /// </summary>
        /// <param name="a">The assembly to check</param>
        /// <returns>All the types in the assembly</returns>
        public IEnumerable<Type> GetAssemblyTypes(Assembly a)
        {
            if (a is null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            foreach (Type t in this._typeCache.GetTypes(a, this._settings.TypeLoadTimeoutMs))
            {
                if (!this._attributeCache.HasCustomAttribute<CompilerGeneratedAttribute>(t, true))
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Gets all types from all assemblies in the list
        /// </summary>
        /// <param name="assemblies">The source assemblies to search</param>
        /// <returns>The types found in the assemblies</returns>
        public IEnumerable<Type> GetAssemblyTypes(IEnumerable<Assembly> assemblies)
        {
            if (assemblies is null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            foreach (Assembly a in assemblies)
            {
                foreach (Type t in this.GetAssemblyTypes(a))
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Gets a list of all types derived from the current type
        /// </summary>
        /// <param name="t">The root type to check for</param>
        /// <returns>All of the derived types</returns>
        public IEnumerable<Type> GetDerivedTypes(Type t, bool loadUnloaded = true)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            if (t.IsInterface)
            {
                throw new ArgumentException($"Type to check for can not be interface as this method uses 'IsSubclassOf'. To search for interfaces use {nameof(GetAllImplementations)}");
            }

            foreach (Type type in this.GetAllTypes(loadUnloaded))
            {
                if (type.IsSubclassOf(t) && type.Module.ScopeName != "EntityProxyModule")
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Gets the most derived type of the specified type. For use when inheritence is used to determine
        /// the proper type to return
        /// </summary>
        /// <param name="t">The base type to check for (Ex DbContext)</param>
        /// <returns>The most derived type, or error if branching tree</returns>
        public Type GetMostDerivedType(Type t) => t is null ? throw new ArgumentNullException(nameof(t)) : this.GetMostDerivedType(this.GetDerivedTypes(t).ToList(), t);

        /// <summary>
        /// Gets the most derived type matching the base type, from a custom list of types
        /// </summary>
        /// <param name="types">The list of types to check</param>
        /// <param name="t">The base type to check for</param>
        /// <returns>The most derived type out of the list</returns>
        public Type GetMostDerivedType(IEnumerable<Type> types, Type t) => types is null ? throw new ArgumentNullException(nameof(types)) : this.GetMostDerivedType(types.ToList(), t);

        /// <summary>
        /// Gets the most derived type matching the base type, from a custom list of types
        /// </summary>
        /// <param name="types">The list of types to check</param>
        /// <param name="t">The base type to check for</param>
        /// <returns>The most derived type out of the list</returns>
        public Type GetMostDerivedType(List<Type> types, Type t)
        {
            if (types is null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            List<Type> toProcess = types.Where(t.IsAssignableFrom).ToList();

            foreach (Type toCheckA in toProcess.ToList())
            {
                foreach (Type toCheckB in toProcess.ToList())
                {
                    if (toCheckA != toCheckB && toCheckA.IsAssignableFrom(toCheckB))
                    {
                        _ = toProcess.Remove(toCheckA);
                        break;
                    }
                }
            }

            return toProcess.Count > 1
                ? throw new Exception($"More than one terminating type found for base {t.FullName}")
                : toProcess.FirstOrDefault() ?? t;
        }

        /// <summary>
        /// Gets all the properties of the object
        /// </summary>
        /// <param name="o">The object to get the properties of</param>
        /// <returns>All of the properties. All of them.</returns>
        public PropertyInfo[] GetProperties(object o) => this.GetProperties(this.GetRealType(o));

        /// <summary>
        /// Gets all the properties of the object
        /// </summary>
        /// <param name="t">The type to get the properties of</param>
        /// <returns>All of the properties. All of them.</returns>
        public PropertyInfo[] GetProperties<T>() => this.GetProperties(this.GetRealType(typeof(T)));

        /// <summary>
        /// Gets the type of the object. Currently strips off EntityProxy type to expose the underlying type.
        /// Should be altered to use a func system for custom resolutions
        /// </summary>
        /// <param name="o">The object to get the type of </param>
        /// <returns>The objects type</returns>
        public Type? GetRealType(object o)
        {
            //TODO: Make this strip off all runtime generated types
            if (o is null)
            {
                return null;
            }

            return this.GetRealType(o.GetType());
        }

        /// <summary>
        /// Gets the type of the object. Currently strips off EntityProxy type to expose the underlying type.
        /// Should be altered to use a func system for custom resolutions
        /// </summary>
        /// <param name="t">The type to strip </param>
        /// <returns>The objects type</returns>
        public Type GetRealType(Type t)
        {
            if (t is null)
            {
                throw new ArgumentNullException(nameof(t));
            }

            Type? toReturn = new List<string>() { "EntityProxyModule", "RefEmit_InMemoryManifestModule" }.Contains(t.Module.ScopeName) ? t.BaseType! : t;

            return toReturn;
        }

        /// <summary>
        /// Searches all assemblies to find a type with the given full name
        /// </summary>
        /// <param name="name">The full name to check for</param>
        /// <param name="baseType">An optional base type requirement</param>
        /// <param name="includeDerived">Whether or not to include types that inherit from the specified name type</param>
        /// <param name="targetNamespace">An optional restriction on the namespace of the search</param>
        /// <returns>A type matching the full name, or derived type</returns>
        public Type? GetTypeByFullName(string name, Type? baseType = null, bool includeDerived = false, string targetNamespace = "", bool loadUnloaded = true)
        {
            List<Type> matching = this.GetTypeByFullName(name, loadUnloaded).ToList();

            if (includeDerived)
            {
                List<Type> derivedTypes = new();

                foreach (Type m in matching.ToList())
                {
                    derivedTypes.AddRange(this.GetDerivedTypes(m, loadUnloaded));
                }

                matching.AddRange(derivedTypes);
            }

            matching = matching.Where(t => string.IsNullOrEmpty(targetNamespace) || targetNamespace == t.Namespace).Distinct().ToList();

            Type? targetType = null;

            foreach (Type t in matching)
            {
                if (baseType != null && !t.IsSubclassOf(baseType))
                {
                    continue;
                }

                if (targetType == null || targetType.IsSubclassOf(t))
                {
                    targetType = t;
                }
                else if (targetType != null && !targetType.IsSubclassOf(t) && !t.IsSubclassOf(targetType))
                {
                    throw new Exception("Found multiple noninherited types that match name " + name);
                }
            }

            return targetType;
        }

        /// <summary>
        /// Checks if an object has an attribute declared on its type
        /// </summary>
        /// <param name="o">The object to check</param>
        /// <param name="attribute">The attribute to check for</param>
        /// <returns>Whether or not the attribute is declared on the object type</returns>
        public bool HasAttribute(object o, Type attribute) => this.HasAttribute(this.GetRealType(o), attribute);

        /// <summary>
        /// Gets a list of all custom attributes on the member
        /// </summary>
        /// <typeparam name="T">The base type of the attributes to get</typeparam>
        /// <param name="toCheck">The member to retrieve the information for</param>
        /// <returns>all custom attributes</returns>
        public List<T> RetrieveAttributes<T>(MemberInfo toCheck) where T : Attribute => toCheck.GetCustomAttributes<T>().ToList();

        /// <summary>
        /// Gets the first attribute matching the specified type
        /// </summary>
        /// <typeparam name="T">The attribute type</typeparam>
        /// <param name="p">The member source</param>
        /// <returns>The first attribute matching the specified type</returns>
        public T? GetAttribute<T>(MemberInfo p) where T : Attribute => this.GetCustomAttributes(p).First(a => a.Instance.GetType() == typeof(T)).Instance as T;

        /// <summary>
        /// Gets all the properties of the current type (Cached)
        /// </summary>
        /// <param name="t">The type to search</param>
        /// <returns>All of the properties. All of them</returns>
        public PropertyInfo[] GetProperties(Type t) => this._propertyCache.GetProperties(t);

        /// <summary>
        /// Checks to see if the given member contains an attribute of a specified type
        /// </summary>
        /// <typeparam name="T">The type to check for</typeparam>
        /// <param name="p">The member to check</param>
        /// <returns>Does the member declare this attribute?</returns>
        public bool HasAttribute<T>(MemberInfo p) where T : Attribute => this.GetCustomAttributes(p).Any(a => a.Instance.GetType() == typeof(T));

        /// <summary>
        /// Checks to see if the given member contains an attribute of a specified type
        /// </summary>
        /// <param name="p">The member to check</param>
        /// <param name="t">The type to check for</param>
        /// <returns>Does the member declare this attribute?</returns>
        public bool HasAttribute(MemberInfo p, Type t) => this.GetCustomAttributes(p).Any(a => a.Instance.GetType() == t);

        /// <summary>
        /// Gets all attribute instances from the current member
        /// </summary>
        /// <param name="p">The member source</param>
        /// <returns>All attribute instances from the current member</returns>
        public IAttributeInstance<Attribute>[] GetCustomAttributes(MemberInfo p) => this._attributeCache.GetCustomAttributeInstances<Attribute>(p, true).ToArray();

        private IEnumerable<Type> GetTypeByFullName(string className, bool loadUnloaded)
        {
            foreach (Type t in this.GetAllTypes(loadUnloaded))
            {
                if (string.Equals(t.FullName, className, StringComparison.OrdinalIgnoreCase))
                {
                    yield return t;
                }
            }
        }
    }
}