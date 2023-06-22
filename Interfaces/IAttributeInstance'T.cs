using System.Reflection;

namespace Loxifi.Interfaces
{
    public interface IAttributeInstance<out T> where T : Attribute
    {
        MemberInfo DeclaringMember { get; }

        T Instance { get; }

        bool IsInherited { get; }
    }
}