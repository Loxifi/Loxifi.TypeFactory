using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Loxifi
{
    internal class AssemblyDefinition
    {
        public Assembly? ContainingAssembly { get; set; }

        public List<Type> LoadedTypes { get; set; } = new List<Type>();
    }
}
