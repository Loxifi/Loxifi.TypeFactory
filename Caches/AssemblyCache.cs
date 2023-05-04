using Loxifi.Exceptions;
using Loxifi.Interfaces;
using Loxifi.Interfaces.Settings;
using Loxifi.Settings;
using System.Diagnostics;
using System.Reflection;

namespace Loxifi.Caches
{
	/// <summary>
	/// Manages caching and loading assemblies
	/// </summary>
	public class AssemblyCache : IAssemblyCache
	{
		private readonly IAssemblyCacheSettings _assemblyCacheSettings;
		private readonly IAssemblyLoader _assemblyLoader;
		private readonly IAppDomainIntegrator _appDomainIntegrator;
		private readonly List<CachedAssembly> _cachedAssemblies = new();

		public AssemblyCache(IAssemblyCacheSettings? assemblyCacheSettings = null)
		{
			assemblyCacheSettings ??= new AssemblyCacheSettings();

			this._assemblyCacheSettings = assemblyCacheSettings;
			this._assemblyLoader = assemblyCacheSettings.AssemblyLoader;
			this._appDomainIntegrator = assemblyCacheSettings.AppDomainIntegrator;

			this._appDomainIntegrator.AssemblyLoad += this.AppDomain_AssemblyLoad;

			foreach (string assemblyLoadPath in this._assemblyLoader.ValidAssemblyPaths)
			{
				this._cachedAssemblies.Add(new CachedAssembly(assemblyLoadPath));
			}

			foreach (Assembly a in this._appDomainIntegrator.GetCurrentAssemblies())
			{
				this.MarkAsLoaded(a);
			}
		}

		/// <summary>
		/// A collection of all loaded assemblies
		/// </summary>
		public IEnumerable<Assembly> Loaded
		{
			get
			{
				for (int i = 0; i < this._cachedAssemblies.Count; i++)
				{
					if (this._cachedAssemblies[i].IsLoaded)
					{
						yield return this._cachedAssemblies[i].Assembly;
					}
				}
			}
		}

		/// <summary>
		/// A collection of all assemblies, loaded and unloaded
		/// Loads assemblies as it iterates so all assemblies will
		/// be loaded if enumeration completes
		/// </summary>
		public IEnumerable<Assembly> LoadedAndUnloaded
		{
			get
			{
				HashSet<Assembly> returned = new();
				foreach (Assembly assembly in this.Loaded)
				{
					yield return assembly;
					_ = returned.Add(assembly);
				}

				for (int i = 0; i < this._cachedAssemblies.Count; i++)
				{
					CachedAssembly ca = this._cachedAssemblies[i];

					if (ca.IsLoaded)
					{
						if (returned.Add(ca.Assembly))
						{
							yield return ca.Assembly;
						}
					}
					else
					{
						if (this.TryGetOrLoad(ca.Path, out Assembly loaded))
						{
							yield return loaded;
							_ = returned.Add(loaded);
						}
					}
				}
			}
		}

		/// <summary>
		/// Gets the paths of all detected assemblies that are not loaded
		/// </summary>
		public IEnumerable<string> Unloaded
		{
			get
			{
				for (int i = 0; i < this._cachedAssemblies.Count; i++)
				{
					if (!this._cachedAssemblies[i].IsLoaded)
					{
						yield return this._cachedAssemblies[i].Path;
					}
				}
			}
		}

		/// <summary>
		/// Gets an assembly by its name
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public Assembly GetByName(string name)
		{
			for (int i = 0; i < this._cachedAssemblies.Count; i++)
			{
				if (this._cachedAssemblies[i].IsLoaded && this._cachedAssemblies[i].Name == name)
				{
					return this._cachedAssemblies[i].Assembly;
				}
			}

			_ = this.TryGetOrLoad(name, out Assembly assembly);

			return assembly;
		}

		/// <summary>
		/// Returns true if the assembly referenced failed the 
		/// last loading attempt
		/// </summary>
		/// <param name="assemblyPath"></param>
		/// <returns></returns>
		public bool FailedLoading(string assemblyPath)
		{
			for (int i = 0; i < this._cachedAssemblies.Count; i++)
			{
				if (this._cachedAssemblies[i].Path == assemblyPath)
				{
					return this._cachedAssemblies[i].LoadFailed;
				}
			}

			return false;
		}

		/// <summary>
		/// Gets a loaded instance of the specified assembly path
		/// if the assembly is not loaded, attempts to load it
		/// </summary>
		/// <param name="assemblyPath"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		public bool TryGetOrLoad(string assemblyPath, out Assembly? result)
		{
			if (this.IsLoaded(assemblyPath, out result))
			{
				return true;
			}

			if (this.FailedLoading(assemblyPath))
			{
				result = null;
				return false;
			}

			result = Dispatcher.Current.Invoke(() =>
			{
				try
				{
					Assembly loaded = this._assemblyCacheSettings.AssemblyLoader.Load(assemblyPath);

					foreach (CachedAssembly assembly in this._cachedAssemblies)
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
					this.MarkAsFailedLoading(assemblyPath);
					this._assemblyCacheSettings.OnAssemblyLoadException?.Invoke(new AssemblyLoadException(assemblyPath, ex));
					return null;
				}
			}
			);

			return result != null;
		}

		/// <summary>
		/// Adds the assembly to the cache as having already been loaded
		/// </summary>
		/// <param name="a"></param>
		/// <param name="assemblyPath"></param>
		private void AddAsLoaded(Assembly a, string? assemblyPath)
		{
			assemblyPath ??= a.Location;

			Dispatcher.Current.Invoke(() =>
			{
				this._cachedAssemblies.Add(new CachedAssembly(assemblyPath)
				{
					Assembly = a,
					IsLoaded = true
				});
			});
		}

		/// <summary>
		/// Adds the assembly to the cache as having failed loading.
		/// For when assemblies are being loaded but the metadata 
		/// isn't from the cache
		/// </summary>
		/// <param name="assemblyPath"></param>
		private void AddAsFailed(string assemblyPath)
		{
			Dispatcher.Current.Invoke(() =>
			{
				this._cachedAssemblies.Add(new CachedAssembly(assemblyPath)
				{
					IsLoaded = false,
					LoadFailed = true
				});
			});
		}

		/// <summary>
		/// Occurs on an app domain unload
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void AppDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs args) => this.MarkAsLoaded(args.LoadedAssembly);

		/// <summary>
		/// Returns true if the given assembly is loaded
		/// </summary>
		/// <param name="assemblyPath"></param>
		/// <param name="loaded">The loaded instance of the assembly</param>
		/// <returns></returns>
		private bool IsLoaded(string assemblyPath, out Assembly? loaded)
		{
			loaded = Dispatcher.Current.Invoke(() =>
			{
				foreach (CachedAssembly assembly in this._cachedAssemblies)
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
		/// Finds the assembly in the cache and marks it as failed loading. 
		/// If the assembly isn't in the cache, it will be added 
		/// </summary>
		/// <param name="assemblyPath"></param>
		private void MarkAsFailedLoading(string assemblyPath)
		{
			for (int i = 0; i < this._cachedAssemblies.Count; i++)
			{
				if (this._cachedAssemblies[i].Path == assemblyPath)
				{
					this._cachedAssemblies[i].LoadFailed = true;
					return;
				}
			}

			this.AddAsFailed(assemblyPath);
		}

		/// <summary>
		/// Finds the assembly in the cache and marks it as loaded.
		/// If the assembly isn't already in the cache, it will be added
		/// </summary>
		/// <param name="a"></param>
		private void MarkAsLoaded(Assembly a)
		{
			if (a.IsDynamic && this._assemblyCacheSettings.CacheDynamic)
			{
				return;
			}

			string assemblyPath = a.IsDynamic ? a.FullName : a.Location;

			for (int i = 0; i < this._cachedAssemblies.Count; i++)
			{
				if (this._cachedAssemblies[i].Path == assemblyPath)
				{
					this._cachedAssemblies[i].IsLoaded = true;
					this._cachedAssemblies[i].Assembly = a;
					this._cachedAssemblies[i].Name = a.FullName;
					return;
				}
			}

			this.AddAsLoaded(a, assemblyPath);
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

		public string Name { get; set; }

		public string Path { get; private set; }
	}
}