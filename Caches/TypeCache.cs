using System.Diagnostics;
using System.Reflection;

namespace Loxifi.Caches
{
	/// <summary>
	/// For loading/caching loaded types
	/// </summary>
	public static class TypeCache
	{
		private static readonly Dictionary<Assembly, IReadOnlyList<Type>> _backingData = new();
		private static readonly Dictionary<Assembly, Dictionary<Type, List<Type>>> _derivedTypes = new();

		public static List<Type> GetDerivedTypes(Assembly a, Type type)
		{
			if (!_derivedTypes.TryGetValue(a, out Dictionary<Type, List<Type>> derived))
			{
				derived = new Dictionary<Type, List<Type>>();
				_derivedTypes.Add(a, derived);
			}

			if (!derived.TryGetValue(type, out List<Type> derivedList))
			{
				derivedList = new List<Type>();

				foreach (Type t in GetTypes(a))
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

		private static IReadOnlyList<Type> CacheTypes(Assembly assembly)
		{
			List<Type> types = new();

			_backingData.Add(assembly, types);

			try
			{
				types.AddRange(assembly.GetTypes());
			}
			catch (Exception ex)
			{
				Debug.WriteLine(ex);
			}

			return types;
		}

		/// <summary>
		/// Gets/Caches a collection of all types in the provided assembly
		/// </summary>
		/// <param name="assembly"></param>
		/// <returns></returns>
		public static IReadOnlyList<Type> GetTypes(Assembly assembly)
		{
			if (_backingData.TryGetValue(assembly, out IReadOnlyList<Type>? list))
			{
				return list;
			}

			return Dispatcher.Current.Invoke(() => CacheTypes(assembly));
		}
	}
}
