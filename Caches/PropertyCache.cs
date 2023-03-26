using Loxifi.Interfaces;
using System.Collections.Concurrent;
using System.Reflection;

namespace Loxifi.Caches
{
	public class PropertyCache : IPropertyCache
	{
		private readonly ConcurrentDictionary<Type, PropertyInfo[]> _backingData = new();

		public PropertyInfo[] GetProperties(Type t)
		{
			if (_backingData.TryGetValue(t, out PropertyInfo[] result))
			{
				return result.ToArray();
			}

			return Dispatcher.Current.Invoke(() =>
			{
				PropertyInfo[] toCache = t.GetProperties();

				_ = _backingData.TryAdd(t, toCache);

				return toCache;
			});
		}
	}
}