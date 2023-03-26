using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Loxifi.Interfaces
{
	public interface IAppDomainIntegrator
	{
		//
		// Summary:
		//     Occurs when an assembly is loaded.
		event AssemblyLoadEventHandler? AssemblyLoad;

		Assembly[] GetCurrentAssemblies();
	}
}
