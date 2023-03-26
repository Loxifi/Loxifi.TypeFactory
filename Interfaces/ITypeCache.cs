using System.Reflection;

namespace Loxifi.Interfaces
{
	public interface ITypeCache
	{
		List<Type> GetDerivedTypes(Assembly a, Type type);

		IReadOnlyList<Type> GetTypes(Assembly assembly);
	}
}