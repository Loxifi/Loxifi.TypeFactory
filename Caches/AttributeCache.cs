using Loxifi.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace Loxifi.Caches
{
	/// <summary>
	/// For getting/caching attributes
	/// </summary>
	public static class AttributeCache
	{
		private static readonly ConcurrentDictionary<MemberInfo, List<Attribute>> _cachedAttributes = new();

		/// <summary>
		/// Gets a custom attribute for the member
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="m"></param>
		/// <param name="inherited"></param>
		/// <returns></returns>
		public static T? GetCustomAttribute<T>(MemberInfo m, bool inherited = true) where T : Attribute => GetCustomAttributes<T>(m, inherited).SingleOrDefault();

		/// <summary>
		/// Gets a custom attribute for the member
		/// </summary>
		/// <typeparam name="TIn"></typeparam>
		/// <typeparam name="TOut"></typeparam>
		/// <returns></returns>
		public static TOut? GetCustomAttribute<TIn, TOut>() where TOut : Attribute => GetCustomAttribute<TOut>(typeof(TIn));

		/// <summary>
		/// Gets all custom attributes for the member
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="m"></param>
		/// <param name="inherited"></param>
		/// <returns></returns>
		public static IEnumerable<IAttributeInstance<T>> GetCustomAttributeInstances<T>(MemberInfo m, bool inherited = true) where T : Attribute
		{
			MemberInfo? check = m;

			do
			{
				foreach (T t in GetCustomAttributes<T>(check, false).OfType<T>())
				{
					yield return new AttributeInstance<T>(check, t, m == check);
				}
			} while (inherited && (check = GetBase(m)) is not null);
		}

		/// <summary>
		/// Gets all custom attributes for the member
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="m"></param>
		/// <param name="inherited"></param>
		/// <returns></returns>
		public static IEnumerable<T?> GetCustomAttributes<T>(MemberInfo m, bool inherited = true) where T : Attribute
		{
			IEnumerable<Attribute> toReturn = null;

			if (_cachedAttributes.TryGetValue(m, out List<Attribute> attributes))
			{
				toReturn = attributes;
			}
			else
			{
				toReturn = Dispatcher.Current.Invoke(() =>
				{
					List<Attribute> attributes = new();

					_ = _cachedAttributes.TryAdd(m, attributes);

					return attributes;
				});
			}

			foreach (T t in toReturn.OfType<T>())
			{
				yield return t;
			}

			if (inherited && GetBase(m) is MemberInfo child)
			{
				foreach (T ta in GetCustomAttributes<T>(child, inherited).OfType<T>())
				{
					yield return ta;
				}
			}
		}

		private static MemberInfo? GetBase(MemberInfo m)
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