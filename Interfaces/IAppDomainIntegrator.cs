using System.Reflection;

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