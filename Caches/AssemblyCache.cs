using System.Diagnostics;
using System.Reflection;

namespace Loxifi.Caches
{
	/// <summary>
	/// Manages caching and loading assemblies
	/// </summary>
	public static class AssemblyCache
	{
		/// <summary>
		/// All extensions that will be loaded as assemblies
		/// </summary>
		public static IEnumerable<string> AssemblyExtensions
		{
			get
			{
				yield return ".exe";
				yield return ".dll";
			}
		}

		/// <summary>
		/// All paths that assemblies are potentially found in
		/// </summary>
		public static IEnumerable<string> AssemblyLoadPaths
		{
			get
			{
				yield return AppDomain.CurrentDomain.BaseDirectory;

				if (AppDomain.CurrentDomain.RelativeSearchPath != null)
				{
					yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppDomain.CurrentDomain.RelativeSearchPath);
				}
			}
		}

		static AssemblyCache()
		{
			AppDomain.CurrentDomain.AssemblyLoad += AppDomain_AssemblyLoad;

			foreach (string assemblyLoadPath in AssemblyLoadPaths)
			{
				foreach (string ext in AssemblyExtensions)
				{
					foreach (string file in Directory.EnumerateFiles(assemblyLoadPath, $"*{ext}"))
					{
						_cachedAssemblies.Add(new CachedAssembly(file));
					}
				}
			}

			foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
			{
				MarkAsLoaded(a);
			}
		}

		public static Assembly GetByName(string name)
		{
			for (int i = 0; i < _cachedAssemblies.Count; i++)
			{
				if (_cachedAssemblies[i].IsLoaded && _cachedAssemblies[i].Name == name)
				{
					return _cachedAssemblies[i].Assembly;
				}
			}

			_ = TryGetOrLoad(name, out Assembly assembly);

			return assembly;
		}

		/// <summary>
		/// Gets the paths of all detected assemblies that are not loaded
		/// </summary>
		public static IEnumerable<string> Unloaded
		{
			get
			{
				for (int i = 0; i < _cachedAssemblies.Count; i++)
				{
					if (!_cachedAssemblies[i].IsLoaded)
					{
						yield return _cachedAssemblies[i].Path;
					}
				}
			}
		}

		/// <summary>
		/// A collection of all loaded assemblies
		/// </summary>
		public static IEnumerable<Assembly> Loaded
		{
			get
			{
				for (int i = 0; i < _cachedAssemblies.Count; i++)
				{
					if (_cachedAssemblies[i].IsLoaded)
					{
						yield return _cachedAssemblies[i].Assembly;
					}
				}
			}
		}

		private static void AppDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args) => MarkAsLoaded(args.LoadedAssembly);

		private static void AddAsLoaded(Assembly a, string? assemblyPath)
		{
			assemblyPath ??= a.Location;

			Dispatcher.Current.Invoke(() =>
			{
				_cachedAssemblies.Add(new CachedAssembly(assemblyPath)
				{
					Assembly = a,
					IsLoaded = true
				});
			});
		}

		private static void MarkAsLoaded(Assembly a)
		{
			string assemblyPath = a.Location;

			for (int i = 0; i < _cachedAssemblies.Count; i++)
			{
				if (_cachedAssemblies[i].Path == assemblyPath)
				{
					_cachedAssemblies[i].IsLoaded = true;
					_cachedAssemblies[i].Assembly = a;
					_cachedAssemblies[i].Name = a.FullName;
					return;
				}
			}

			AddAsLoaded(a, assemblyPath);
		}

		private static readonly List<CachedAssembly> _cachedAssemblies = new();

		private static bool IsLoaded(string assemblyPath, out Assembly? loaded)
		{
			loaded = Dispatcher.Current.Invoke(() =>
			{
				foreach (CachedAssembly assembly in _cachedAssemblies)
				{
					if (assembly.Path == assemblyPath)
					{
						return assembly.Assembly;
					}
				}

				return null;
			});

			return loaded != null;
		}

		/// <summary>
		/// Gets a loaded instance of the specified assembly path
		/// if the assembly is not loaded, attempts to load it
		/// </summary>
		/// <param name="assemblyPath"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public static bool TryGetOrLoad(string assemblyPath, out Assembly? result)
		{
			if (IsLoaded(assemblyPath, out result))
			{
				return true;
			}

			result = Dispatcher.Current.Invoke(() =>
			{
				try
				{
#if NETCOREAPP3_0_OR_GREATER
					Assembly loaded = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
#endif

#if NETSTANDARD
					Assembly loaded = Assembly.LoadFrom(assemblyPath);
#endif

					foreach (CachedAssembly assembly in _cachedAssemblies)
					{
						if (assembly.Path == assemblyPath)
						{
							assembly.Assembly = loaded;
							assembly.Name = loaded.FullName;
							break;
						}
					}

					return loaded;
				}
				catch (Exception ex)
				{
					return null;
				}
			}
			);

			return result != null;
		}

		public static IEnumerable<Assembly> LoadedAndUnloaded
		{
			get
			{
				HashSet<Assembly> returned = new();
				foreach (Assembly assembly in Loaded)
				{
					yield return assembly;
					_ = returned.Add(assembly);
				}

				for (int i = 0; i < _cachedAssemblies.Count; i++)
				{
					CachedAssembly ca = _cachedAssemblies[i];

					if (ca.IsLoaded)
					{
						if (returned.Add(ca.Assembly))
						{
							yield return ca.Assembly;
						}
					}
					else
					{
						if (TryGetOrLoad(ca.Path, out Assembly loaded))
						{
							yield return loaded;
							_ = returned.Add(loaded);
						}
					}
				}
			}
		}
	}

	[DebuggerDisplay("{path,nq}")]
	internal class CachedAssembly
	{
		public CachedAssembly(string path)
		{
			this.Path = path;
		}

		public Assembly Assembly { get; set; }

		public bool IsLoaded { get; set; }

		public bool LoadFailed { get; set; }

		public string Path { get; private set; }

		public string Name { get; set; }
	}
}