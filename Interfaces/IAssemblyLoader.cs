using System.Reflection;

namespace Loxifi.Interfaces
{
	public interface IAssemblyLoader
	{
		Assembly Load(string path);
		IEnumerable<string> ValidAssemblyPaths { get; }
	}
}