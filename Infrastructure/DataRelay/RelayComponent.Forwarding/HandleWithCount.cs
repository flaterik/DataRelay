using System;
using System.Threading;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{

	internal class HandleWithCount
	{
		private readonly AutoResetEvent _handle;
		private int _count;

		internal HandleWithCount(AutoResetEvent handle, int initialCount)
		{
			if (handle == null)
			{
				throw new ArgumentNullException("handle");
			}

			_handle = handle;
			_count = initialCount;

		}

		internal void Decrement()
		{
			if (Interlocked.Decrement(ref _count) == 0)
			{
				_handle.Set();
			}
		}

	}
}