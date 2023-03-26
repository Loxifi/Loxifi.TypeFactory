using Loxifi.Caches;
using Loxifi.Interfaces;
using Loxifi.Interfaces.Settings;
using Loxifi.Settings;
using System.Reflection;

namespace Loxifi.Tests
{
	[TestClass]
	public class AssemblyCacheTests
	{
		private class MockFailAssemblyLoader : IAssemblyLoader
		{
			public IEnumerable<string> ValidAssemblyPaths
			{
				get
				{
					yield return "NonExistantAssembly.dll";
				}
			}
			public Assembly Load(string path) => throw new BadImageFormatException();
		}

		[TestMethod]
		public void OnlyFailOnce()
		{
			int tries = 5;
			int fails = 0;
			int success = 0;
			IAssemblyCacheSettings assemblyCacheSettings = new AssemblyCacheSettings(new MockFailAssemblyLoader())
			{
				OnAssemblyLoadException = (e) => fails++
			};

			AssemblyCache assemblyCache = new(assemblyCacheSettings);

			for (int i = 0; i < tries; i++)
			{
				if(assemblyCache.TryGetOrLoad("doesnt matter", out _))
				{
					success++;
				}
			}

			Assert.AreEqual(1, fails);
		}
	}
}
