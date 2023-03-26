using Loxifi.Exceptions;
using Loxifi.Implementations;
using Loxifi.Interfaces;
using Loxifi.Interfaces.Settings;

namespace Loxifi.Settings
{
	public class AssemblyCacheSettings : IAssemblyCacheSettings
	{
		public AssemblyCacheSettings(IAssemblyLoader? assemblyLoader = null, IAppDomainIntegrator? appDomainIntegrator = null)
		{
			this.AssemblyLoader = assemblyLoader ?? new AssemblyLoader();
			this.AppDomainIntegrator = appDomainIntegrator ?? new CurrentAppDomainIntegrator();
		}

		public IAppDomainIntegrator AppDomainIntegrator { get; }

		/// <summary>
		/// Object used to load assemblies
		/// </summary>
		public IAssemblyLoader AssemblyLoader { get; }

		public bool CacheDynamic { get; set; }

		public Action<AssemblyLoadException>? OnAssemblyLoadException { get; set; }
	}
}