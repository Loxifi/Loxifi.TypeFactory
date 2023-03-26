namespace Loxifi.Exceptions
{
	public class AssemblyLoadException : Exception
	{
		private const string MESSAGE = "An exception has occurred while loading an assembly. See the inner exception for more details";

		public AssemblyLoadException(string path, Exception? innerException) : base(MESSAGE, innerException)
		{
			this.Path = path;
		}

		public string Path { get; set; }
	}
}