using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MySpace.Common
{
	[DebuggerDisplay("Value={DebuggerView}")]
	public class LazyInitializer<T>
	{
		public static implicit operator T(LazyInitializer<T> value)
		{
			ArgumentAssert.IsNotNull(value, "value");
			return value.Value;
		}

		private readonly object _syncRoot = new object();
		private Func<T> _initializer;
		private Boxed _value;

		public LazyInitializer(Func<T> initializer)
		{
			_initializer = initializer;
		}

		[DebuggerBrowsable(DebuggerBrowsableState.Never)]
		public T Value
		{
			get
			{
				if (_value == null)
				{
					lock (_syncRoot)
					{
						if (_value == null)
						{
							var boxed = new Boxed { Value = _initializer() };
							Thread.MemoryBarrier();
							_value = boxed;
							_initializer = null;
						}
					}
				}
				return _value.Value;
			}
		}

		internal string DebuggerView
		{
			get
			{
				var value = _value;
				if (value == null)
				{
					return "(Not Initialized)";
				}
				return value.Value.ToString();
			}
		}

		private class Boxed
		{
			public T Value;
		}
	}
}
