namespace Loxifi
{
	internal class Dispatcher
	{
		public static Dispatcher Current = new();

		private readonly object _lock = new();

		public void Invoke(Action toInvoke)
		{
			Monitor.Enter(this._lock);

			try
			{
				Monitor.Enter(this._lock);

				toInvoke.Invoke();
			}
			finally
			{
				Monitor.Exit(this._lock);
			}
		}

		public T Invoke<T>(Func<T> toInvoke)
		{
			Monitor.Enter(this._lock);

			try
			{
				Monitor.Enter(this._lock);

				return toInvoke.Invoke();
			}
			finally
			{
				Monitor.Exit(this._lock);
			}
		}
	}
}
