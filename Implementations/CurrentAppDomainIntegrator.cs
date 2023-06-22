using Loxifi.Interfaces;
using System.Reflection;

namespace Loxifi.Implementations
{
    public class CurrentAppDomainIntegrator : IAppDomainIntegrator
    {
        public CurrentAppDomainIntegrator()
        {
            AppDomain.CurrentDomain.AssemblyLoad += (o, e) => AssemblyLoad?.Invoke(o, e);
        }

        public event AssemblyLoadEventHandler? AssemblyLoad;

        public Assembly[] GetCurrentAssemblies() => AppDomain.CurrentDomain.GetAssemblies();
    }
}