namespace Loxifi.Interfaces.Settings
{
    public interface IAssemblyLoaderSettings
    {
        IEnumerable<string> AssemblyExtensions { get; }

        IEnumerable<string> AssemblyLoadDirectories { get; }
    }
}