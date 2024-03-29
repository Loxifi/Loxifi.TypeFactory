﻿using Loxifi.Exceptions;

namespace Loxifi.Interfaces.Settings
{
    public interface IAssemblyCacheSettings
    {
        IAppDomainIntegrator AppDomainIntegrator { get; }

        IAssemblyLoader AssemblyLoader { get; }

        bool CacheDynamic { get; }

        Action<AssemblyLoadException>? OnAssemblyLoadException { get; }
    }
}