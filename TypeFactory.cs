using Loxifi.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text.RegularExpressions;

namespace Loxifi
{
	/// <summary>
	/// A  class used to perform all kinds of type based reflections.Used for finding and resolving many kinds of queries
	/// </summary>
	public class TypeFactory
	{
		public EventHandler<AssemblyLoadFailure>? FailedAssemblyLoad { get; set; }

		private static readonly ConcurrentDictionary<string, Assembly> _assembliesByName = new();

		private static readonly ConcurrentDictionary<string, List<Assembly>> _assembliesThatReference = new();

		private static readonly ConcurrentDictionary<string, AssemblyDefinition> _assemblyTypes = new();

		private static readonly HashSet<string> _currentlyLoadedAssemblies = new();

		private static readonly ConcurrentDictionary<Type, ICollection<Type>> _derivedTypes = new();

		private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Type>> _typeMapping = new();

		private readonly TypeFactorySettings _settings;

		/// <summary>
		/// Since everything is cached, we need to make sure ALL potential assemblies are loaded or we might end up missing classes because
		/// the assembly hasn't been loaded yet. Consider only loading whitelisted references if this is slow
		/// </summary>
		public TypeFactory(TypeFactorySettings settings)
		{
			this._settings = settings;

			Dictionary<string, Assembly> loadedPaths = new();

			//Map out the loaded assemblies so we can find them by path
			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (!a.IsDynamic)
				{
					if (!loadedPaths.ContainsKey(a.Location))
					{
						loadedPaths.Add(a.Location, a);
					}
				}
			}

			List<string> referencedPaths = new();

			List<string> searchPaths = new()
			{
				AppDomain.CurrentDomain.BaseDirectory
			};

			if (AppDomain.CurrentDomain.RelativeSearchPath != null)
			{
				searchPaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.RelativeSearchPath));
			}

			//We're going to add the paths to the loaded assemblies here so we can double
			//back and ensure we're building the dependencies for the loaded assemblies that
			//do NOT reside in the EXE/Bin directories
			HashSet<string> searchedPaths = new();

			foreach (string searchPath in searchPaths)
			{
				referencedPaths.AddRange(Directory.GetFiles(searchPath, "*.dll"));

				referencedPaths.AddRange(Directory.GetFiles(searchPath, "*.exe"));

				foreach (string loadPath in referencedPaths)
				{
					_ = searchedPaths.Add(loadPath);

					//If we're not already loaded
					if (!loadedPaths.TryGetValue(loadPath, out Assembly a))
					{
						//Check for blacklist
						string matchingLine = this._settings.Blacklist.FirstOrDefault(b => Regex.IsMatch(Path.GetFileName(loadPath), b));
						if (!string.IsNullOrWhiteSpace(matchingLine))
						{
							continue;
						}

						try
						{
							AssemblyName an = AssemblyName.GetAssemblyName(loadPath);

							a = LoadAssembly(loadPath, true);

							_ = _assembliesByName.TryAdd(an.Name, a);
						}
						catch (Exception ex)
						{
							this.FailedAssemblyLoad?.Invoke(this, new AssemblyLoadFailure(loadPath, ex));
						}
					}

					if (a is not null)
					{
						AddReferenceInformation(a);
					}
				}
			}

			//And now we double check to make sure we're not missing anything in the loaded
			//assemblies that were not found in our path discovery
			foreach (KeyValuePair<string, Assembly> kvp in loadedPaths)
			{
				if (!searchedPaths.Contains(kvp.Key))
				{
					AddReferenceInformation(kvp.Value);
				}
			}

			try
			{
				AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;

				List<Assembly> currentlyLoadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().ToList();

				foreach (Assembly assembly in currentlyLoadedAssemblies)
				{
					if (!assembly.IsDynamic)
					{
						CheckLoadingPath(assembly.Location);
					}
				}
			}
			catch (SecurityException ex)
			{
				Debug.WriteLine(ex);
			}
		}

		private ConcurrentDictionary<MemberInfo, AttributeInstance[]> Attributes { get; set; } = new ConcurrentDictionary<MemberInfo, AttributeInstance[]>();

		private ConcurrentDictionary<Type, PropertyInfo[]> Properties { get; set; } = new ConcurrentDictionary<Type, PropertyInfo[]>();

		/// <summary>
		/// Gets all types in whitelisted assemblies that implement a given interface
		/// </summary>
		/// <typeparam name="T">The interface to check for</typeparam>
		/// <param name="includeAbstract">If true, the result set will include abstract types</param>
		/// <returns>All of the aforementioned types</returns>
		public static IEnumerable<Type> GetAllImplementations<T>(bool includeAbstract = false) => GetAllImplementations(typeof(T), includeAbstract);

		/// <summary>
		/// Gets all types in whitelisted assemblies that implement a given interface
		/// </summary>
		/// <param name="interfaceType">The interface to check for, will also search for implementations of open generics</param>
		/// <param name="includeAbstract">If true, the result set will include abstract types</param>
		/// <returns>All of the aforementioned types</returns>
		public static IEnumerable<Type> GetAllImplementations(Type interfaceType, bool includeAbstract = false)
		{
			if (interfaceType is null)
			{
				throw new ArgumentNullException(nameof(interfaceType));
			}

			IEnumerable<Type> candidates = GetAssemblyTypes(GetDependentAssemblies(interfaceType)).Distinct();

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
		public static IEnumerable<Type> GetAllTypes()
		{
			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				foreach (Type t in GetAssemblyTypes(a))
				{
					yield return t;
				}
			}
		}

		/// <summary>
		/// Gets an assembly by its AssemblyName
		/// </summary>
		/// <param name="name">The AssemblyName</param>
		/// <returns>The matching Assembly</returns>
		public static Assembly GetAssemblyByName(AssemblyName name)
		{
			if (name is null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			return GetAssemblyByName(name.Name);
		}

		/// <summary>
		/// Gets an assembly by its Assembly Name
		/// </summary>
		/// <param name="name">The AssemblyName.Name</param>
		/// <returns>The matching Assembly</returns>
		public static Assembly GetAssemblyByName(string name)
		{
			if (!_assembliesByName.TryGetValue(name, out Assembly a))
			{
				a = AppDomain.CurrentDomain.GetAssemblies().First(aa => aa.GetName().Name == name);
				_ = _assembliesByName.TryAdd(name, a);
			}

			return a;
		}

		/// <summary>
		/// Gets all types in the specified assembly (where not compiler generated)
		/// </summary>
		/// <param name="a">The assembly to check</param>
		/// <returns>All the types in the assembly</returns>
		public static IEnumerable<Type> GetAssemblyTypes(Assembly a)
		{
			if (a is null)
			{
				throw new ArgumentNullException(nameof(a));
			}

			if (!_assemblyTypes.TryGetValue(a.FullName, out AssemblyDefinition b))
			{
				List<Type>? types = null;

				try
				{
					types = a.GetTypes().Where(t => !Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), true)).Distinct().ToList();
				}
				catch (ReflectionTypeLoadException ex)
				{
					try
					{
						types = new List<Type>();

						foreach (Type t in ex.Types)
						{
							try
							{
								if (t != null && !Attribute.IsDefined(t, typeof(CompilerGeneratedAttribute), true))
								{
									types.Add(t);
								}
							}
							catch (Exception exxx)
							{
								Debug.WriteLine(exxx);
							}
						}
					}
					catch (Exception exx)
					{
						Debug.WriteLine(exx);
					}

					types = types.Distinct().ToList();
				}
				catch (Exception)
				{
					types = new List<Type>();
				}

				_ = _assemblyTypes.TryAdd(a.FullName, new AssemblyDefinition() { ContainingAssembly = a, LoadedTypes = types });

				return types.ToList();
			}
			else
			{
				return b.LoadedTypes;
			}
		}

		/// <summary>
		/// Gets all types from all assemblies in the list
		/// </summary>
		/// <param name="assemblies">The source assemblies to search</param>
		/// <returns>The types found in the assemblies</returns>
		public static IEnumerable<Type> GetAssemblyTypes(IEnumerable<Assembly> assemblies)
		{
			if (assemblies is null)
			{
				throw new ArgumentNullException(nameof(assemblies));
			}

			foreach (Assembly a in assemblies)
			{
				foreach (Type t in GetAssemblyTypes(a))
				{
					yield return t;
				}
			}
		}

		/// <summary>
		/// Gets all assemblies that recursively reference the one containing the given type
		/// </summary>
		/// <param name="t">A type in the root assembly to search for </param>
		/// <returns>all assemblies that recursively reference the one containing the given type</returns>
		public static IEnumerable<Assembly> GetDependentAssemblies(Type t)
		{
			if (t is null)
			{
				throw new ArgumentNullException(nameof(t));
			}

			Assembly root = t.Assembly;

			foreach (Assembly a in GetDependentAssemblies(root))
			{
				yield return a;
			}
		}

		/// <summary>
		/// Gets all assemblies that recursively reference the given one
		/// </summary>
		/// <param name="a">The root assembly to search for </param>
		/// <returns>all assemblies that recursively reference the one containing the given type</returns>
		public static IEnumerable<Assembly> GetDependentAssemblies(Assembly a) => GetDependentAssemblies(a, new HashSet<Assembly>());

		/// <summary>
		/// Gets a list of all types derived from the current type
		/// </summary>
		/// <param name="t">The root type to check for</param>
		/// <returns>All of the derived types</returns>
		public static IEnumerable<Type> GetDerivedTypes(Type t)
		{
			if (t is null)
			{
				throw new ArgumentNullException(nameof(t));
			}

			if (t.IsInterface)
			{
				throw new ArgumentException($"Type to check for can not be interface as this method uses 'IsSubclassOf'. To search for interfaces use {nameof(GetAllImplementations)}");
			}

			if (_derivedTypes.TryGetValue(t, out ICollection<Type> value))
			{
				foreach (Type toReturn in value)
				{
					yield return toReturn;
				}
			}
			else
			{
				List<Type> typesToReturn = new();

				foreach (Type type in GetAssemblyTypes(GetDependentAssemblies(t)))
				{
					if (type.IsSubclassOf(t) && type.Module.ScopeName != "EntityProxyModule")
					{
						typesToReturn.Add(type);
					}
				}

				_ = _derivedTypes.TryAdd(t, typesToReturn.ToList());

				foreach (Type toReturn in typesToReturn)
				{
					yield return toReturn;
				}
			}
		}

		/// <summary>
		/// Gets the most derived type of the specified type. For use when inheritence is used to determine
		/// the proper type to return
		/// </summary>
		/// <param name="t">The base type to check for (Ex DbContext)</param>
		/// <returns>The most derived type, or error if branching tree</returns>
		public static Type GetMostDerivedType(Type t) => t is null ? throw new ArgumentNullException(nameof(t)) : GetMostDerivedType(GetDerivedTypes(t).ToList(), t);

		/// <summary>
		/// Gets the most derived type matching the base type, from a custom list of types
		/// </summary>
		/// <param name="types">The list of types to check</param>
		/// <param name="t">The base type to check for</param>
		/// <returns>The most derived type out of the list</returns>
		public static Type GetMostDerivedType(IEnumerable<Type> types, Type t) => types is null ? throw new ArgumentNullException(nameof(types)) : GetMostDerivedType(types.ToList(), t);

		/// <summary>
		/// Gets the most derived type matching the base type, from a custom list of types
		/// </summary>
		/// <param name="types">The list of types to check</param>
		/// <param name="t">The base type to check for</param>
		/// <returns>The most derived type out of the list</returns>
		public static Type GetMostDerivedType(List<Type> types, Type t)
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
		public static PropertyInfo[] GetProperties(object o) => GetProperties(GetRealType(o));

		/// <summary>
		/// Gets all assemblies that are referenced recursively by the assembly containing the given type
		/// </summary>
		/// <param name="t">A type in the root assembly to search for </param>
		/// <returns>all assemblies that are referenced recursively by the assembly containing the given type</returns>
		public static IEnumerable<Assembly> GetReferencedAssemblies(Type t)
		{
			if (t is null)
			{
				throw new ArgumentNullException(nameof(t));
			}

			Assembly root = t.Assembly;

			foreach (Assembly a in GetDependentAssemblies(root))
			{
				yield return a;
			}
		}

		/// <summary>
		/// Gets all assemblies that are referenced recursively by the assembly one
		/// </summary>
		/// <param name="a">The root assembly to search for </param>
		/// <returns>all assemblies that are referenced recursively by the assembly one</returns>
		public static IEnumerable<Assembly> GetReferencedAssemblies(Assembly a) => GetReferencedAssemblies(a, new HashSet<AssemblyName>());

		/// <summary>
		/// Gets the type of the object. Currently strips off EntityProxy type to expose the underlying type.
		/// Should be altered to use a func system for custom resolutions
		/// </summary>
		/// <param name="o">The object to get the type of </param>
		/// <returns>The objects type</returns>
		public static Type? GetRealType(object o)
		{
			//TODO: Make this strip off all runtime generated types
			if (o is null)
			{
				return null;
			}

			Type? toReturn = new List<string>() { "EntityProxyModule", "RefEmit_InMemoryManifestModule" }.Contains(o.GetType().Module.ScopeName) ? o.GetType().BaseType : o.GetType();

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
		public static Type GetTypeByFullName(string name, Type? baseType = null, bool includeDerived = false, string targetNamespace = "")
		{
			bool nameIsCached = _typeMapping.ContainsKey(name);
			bool namespaceIsCached = nameIsCached && _typeMapping[name].ContainsKey(targetNamespace);

			if (!nameIsCached || !namespaceIsCached)
			{
				List<Type> matching = GetTypeByFullName(name).ToList();

				if (includeDerived)
				{
					List<Type> derivedTypes = new();

					derivedTypes.AddRange(matching.Select(GetDerivedTypes).SelectMany(m => m));

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

				if (nameIsCached)
				{
					_ = _typeMapping[name].TryAdd(targetNamespace, targetType);
				}
				else
				{
					ConcurrentDictionary<string, Type> namespaceDictionary = new();
					_ = namespaceDictionary.TryAdd(targetNamespace, targetType);

					_ = _typeMapping.TryAdd(name, namespaceDictionary);
				}
			}

			return _typeMapping[name][targetNamespace];
		}

		/// <summary>
		/// Checks if an object has an attribute declared on its type
		/// </summary>
		/// <param name="o">The object to check</param>
		/// <param name="attribute">The attribute to check for</param>
		/// <returns>Whether or not the attribute is declared on the object type</returns>
		public static bool HasAttribute(object o, Type attribute) => HasAttribute(GetRealType(o), attribute);

		/// <summary>
		/// Gets a list of all custom attributes on the member
		/// </summary>
		/// <typeparam name="T">The base type of the attributes to get</typeparam>
		/// <param name="toCheck">The member to retrieve the information for</param>
		/// <returns>all custom attributes</returns>
		public static List<T> RetrieveAttributes<T>(MemberInfo toCheck) where T : Attribute => toCheck.GetCustomAttributes<T>().ToList();

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
		public PropertyInfo[] GetProperties(Type t)
		{
			if (t is null)
			{
				throw new ArgumentNullException(nameof(t));
			}

			if (!this.Properties.TryGetValue(t, out PropertyInfo[] properties))
			{
				properties = t.GetProperties();
				_ = this.Properties.TryAdd(t, properties);
			}

			return properties;
		}

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
		public AttributeInstance[] GetCustomAttributes(MemberInfo p)
		{
			if (p is null)
			{
				throw new ArgumentNullException(nameof(p));
			}

			if (this.Attributes.TryGetValue(p, out AttributeInstance[]? cachedAttributes))
			{
				return cachedAttributes;
			}

			List<AttributeInstance> attributes = new();

			if (p is PropertyInfo pToCheck)
			{
				bool inherited = false;

				do
				{
					foreach (Attribute instance in pToCheck.GetCustomAttributes(false).OfType<Attribute>())
					{
						attributes.Add(new AttributeInstance(pToCheck, instance, inherited));
					}

					inherited = true;
				} while ((pToCheck = pToCheck.DeclaringType.BaseType?.GetProperty(pToCheck.Name)) != null);
			}
			else if (p is Type tToCheck)
			{
				do
				{
					//This can be recursive to leverage the cache, if it needs to be,
					//however the logic for "isinherited" would need to be changed to reflect that
					foreach (Attribute instance in tToCheck.GetCustomAttributes(false).OfType<Attribute>())
					{
						attributes.Add(new AttributeInstance(tToCheck, instance, p != tToCheck));
					}

					tToCheck = tToCheck.BaseType;
				} while (tToCheck != null);
			}
			else
			{
				throw new NotImplementedException($"Unsupported MemberInfo type {p.GetType()}");
			}

			AttributeInstance[] toReturn = attributes.ToArray();

			_ = this.Attributes.TryAdd(p, toReturn);

			return toReturn;
		}

		private static void AddReferenceInformation(Assembly a)
		{
			foreach (AssemblyName ani in a.GetReferencedAssemblies())
			{
				string aniName = ani.Name;
				if (_assembliesThatReference.TryGetValue(aniName, out List<Assembly> matches))
				{
					matches.Add(a);
				}
				else
				{
					_ = _assembliesThatReference.TryAdd(aniName, new List<Assembly> { a });
				}
			}
		}

		private static void CheckLoadingPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			if (!_currentlyLoadedAssemblies.Contains(path))
			{
				_ = _currentlyLoadedAssemblies.Add(path);
			}
			else
			{
				try
				{
					throw new Exception($"The assembly found at {path} is being loaded, however it appears to have already been loaded. Loading the same assembly more than once causes type resolution issues and is a fatal error");
				}
				catch (Exception)
				{
				}
			}
		}

		private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
		{
			if (!args.LoadedAssembly.IsDynamic)
			{
				CheckLoadingPath(args.LoadedAssembly.Location);
			}
		}

		private static IEnumerable<Assembly> GetDependentAssemblies(Assembly a, HashSet<Assembly> checkedAssemblies, string prefix = "")
		{
			yield return a;

			if (_assembliesThatReference.TryGetValue(a.GetName().Name, out List<Assembly> referencedBy))
			{
				foreach (Assembly ai in referencedBy)
				{
					if (checkedAssemblies.Contains(ai))
					{
						continue;
					}

					_ = checkedAssemblies.Add(ai);

					foreach (Assembly aii in GetDependentAssemblies(ai, checkedAssemblies, "----" + prefix))
					{
						yield return aii;
					}
				}
			}
		}

		private static IEnumerable<Assembly> GetReferencedAssemblies(Assembly a, HashSet<AssemblyName> checkedNames)
		{
			yield return a;

			foreach (AssemblyName an in a.GetReferencedAssemblies())
			{
				if (checkedNames.Contains(an))
				{
					continue;
				}

				_ = checkedNames.Add(an);

				foreach (Assembly ai in GetReferencedAssemblies(GetAssemblyByName(an), checkedNames))
				{
					yield return ai;
				}
			}
		}

		private static Type[] GetTypeByFullName(string className)
		{
			List<Type> returnVal = new();

			foreach (Type t in GetAllTypes())
			{
				if (string.Equals(t.FullName, className, StringComparison.OrdinalIgnoreCase))
				{
					returnVal.Add(t);
				}
			}

			return returnVal.ToArray();
		}

		private static Assembly LoadAssembly(string path, bool skipDuplicateCheck = false)
		{
			if (!skipDuplicateCheck)
			{
				CheckLoadingPath(path);
			}
#if NETCOREAPP3_0_OR_GREATER
			return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
#endif

#if NETSTANDARD
			return Assembly.LoadFrom(path);
#endif
		}
	}
}