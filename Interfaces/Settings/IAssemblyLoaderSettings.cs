using System;
using System.Collections.Generic;
using System.Text;

namespace Loxifi.Interfaces.Settings
{
	public interface IAssemblyLoaderSettings
	{
		IEnumerable<string> AssemblyLoadDirectories { get; }
		IEnumerable<string> AssemblyExtensions { get; }
	}
}
