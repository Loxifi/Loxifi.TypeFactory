using System.Reflection;

namespace Loxifi.Interfaces
{
	public interface IPropertyCache
	{
		PropertyInfo[] GetProperties(Type t);
	}
}