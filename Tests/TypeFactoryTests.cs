using Loxifi.Tests.LocalTypes;
using System.Reflection;

namespace Loxifi.Tests
{
	[TestClass]
	public class TypeFactoryTests
	{
		[TestMethod]
		public void GetAttributesClass()
		{
			List<TestAttribute> attributes = TypeFactory.Default.RetrieveAttributes<TestAttribute>(typeof(LocalType));

			Assert.AreEqual(1, attributes.Count);
		}

		[TestMethod]
		public void GetAttributesPropertyInfo()
		{
			List<TestAttribute> attributes = TypeFactory.Default.RetrieveAttributes<TestAttribute>(typeof(LocalType).GetProperty(nameof(LocalType.PropertyA))!);

			Assert.AreEqual(1, attributes.Count);
		}

		[TestMethod]
		public void GetMostDerivedType()
		{
			string name = $"TestBinaries.BaseClass";

			Type? t = TypeFactory.Default.GetTypeByFullName(name);

			Type derived = TypeFactory.Default.GetMostDerivedType(t);

			string? fullName = derived?.FullName;

			Assert.AreEqual(fullName, "TestBinaries.DerivedClass");
		}

		[TestMethod]
		public void GetProperties()
		{
			PropertyInfo[] properties = TypeFactory.Default.GetProperties<LocalType>();

			Assert.AreEqual(3, properties.Length);
		}

		[TestMethod]
		public void GetTypeByFullName()
		{
			string name = $"TestBinaries.BaseClass";

			Type? t = TypeFactory.Default.GetTypeByFullName(name);

			Assert.IsNotNull(t);
		}

		[TestMethod]
		public void LoadAllTypes()
		{
			int i = 0;

			foreach (Type t in TypeFactory.Default.GetAllTypes(true))
			{
				string ns = t.Namespace;

				if (ns is null)
				{
					continue;
				}

				if (ns.StartsWith("TestBinaries.") || ns == "TestBinaries")
				{
					i++;
				}
			}

			Assert.AreEqual(5, i);
		}
	}
}