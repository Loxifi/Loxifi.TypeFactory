using System.Reflection;

namespace Loxifi.Interfaces
{
	public interface IAttributeCache
	{
		T? GetCustomAttribute<T>(MemberInfo m, bool inherited = true) where T : Attribute;

		TOut? GetCustomAttribute<TIn, TOut>() where TOut : Attribute;

		IEnumerable<IAttributeInstance<T>> GetCustomAttributeInstances<T>(MemberInfo m, bool inherited = true) where T : Attribute;

		IEnumerable<T?> GetCustomAttributes<T>(MemberInfo m, bool inherited = true) where T : Attribute;

		bool HasCustomAttribute<T>(MemberInfo m, bool inherited = true) where T : Attribute;
	}
}