namespace Loxifi
{
	public class TypeFactorySettings
	{
		/// <summary>
		/// If true, the type factory will load all assemblies in the current directory
		/// into the app domain in order to find types
		/// </summary>
		public bool LoadUnloadedAssemblies { get; set; } = true;

		/// <summary>
		/// A list of assembly names to skip while loading
		/// </summary>
		public List<string> Blacklist { get; set; } = new List<string>();
	}
}
