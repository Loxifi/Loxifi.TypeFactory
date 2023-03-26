using Loxifi.Interfaces.Settings;

namespace Loxifi.Settings
{
	public class AssemblyLoaderSettings : IAssemblyLoaderSettings
	{
		/// <summary>
		/// All extensions that will be loaded as assemblies
		/// </summary>
		public IEnumerable<string> AssemblyExtensions { get; set; } = new List<string>()
		{
			".exe",
			".dll"
		};

		/// <summary>
		/// All paths that assemblies are potentially found in
		/// </summary>
		public IEnumerable<string> AssemblyLoadDirectories { get; } = GetAssemblyLoadPaths().ToList();

		/// <summary>
		/// Gets the paths that assemblies are potentially found in
		/// </summary>
		/// <returns></returns>
		private static IEnumerable<string> GetAssemblyLoadPaths()
		{
			yield return AppDomain.CurrentDomain.BaseDirectory;

			if (AppDomain.CurrentDomain.RelativeSearchPath != null)
			{
				yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.RelativeSearchPath);
			}
		}
	}
}