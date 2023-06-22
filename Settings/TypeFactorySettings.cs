using Loxifi.Caches;
using Loxifi.Interfaces;

namespace Loxifi
{
    public class TypeFactorySettings
    {
        public TypeFactorySettings(IAttributeCache attributeCache = null, IPropertyCache propertyCache = null, ITypeCache typeCache = null, IAssemblyCache assemblyCache = null)
        {
            this.AttributeCache = attributeCache ?? new AttributeCache();
            this.PropertyCache = propertyCache ?? new PropertyCache();
            this.TypeCache = typeCache ?? new TypeCache();
            this.AssemblyCache = assemblyCache ?? new AssemblyCache();
        }

        public IAssemblyCache AssemblyCache { get; set; }

        public IAttributeCache AttributeCache { get; set; }

        /// <summary>
        /// A list of assembly names to skip while loading
        /// </summary>
        public List<string> Blacklist { get; set; }

        /// <summary>
        /// If true, the type factory will load all assemblies in the current directory
        /// into the app domain in order to find types
        /// </summary>
        public bool LoadUnloadedAssemblies { get; set; } = true;

        public IPropertyCache PropertyCache { get; set; }

        public ITypeCache TypeCache { get; set; }

        public int TypeLoadTimeoutMs { get; set; } = 1000;
    }
}