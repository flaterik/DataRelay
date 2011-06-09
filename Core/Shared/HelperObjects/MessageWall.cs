using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace MySpace.Common.HelperObjects
{
	/// <summary>
	/// allows component in any layer to publish "messages" or properties
	/// to any interested component in any other layer
	/// primary use: logging contextual information
	/// </summary>
	/// <example>-trace, -client/ip, -server/ip, -links/context, -referer</example>
	public static class MessageWall
	{
		private static IDictionary<string, Func<object>> _getters =
			new Dictionary<string, Func<object>>(StringComparer.Ordinal);

		private static readonly object SyncRoot = new object();

		/// <summary>
		/// register message header and content getter
		/// multiple handlers for the header are allowed, the first one to return the value is preferred
		/// </summary>
		/// <param name="messageHeader">message header</param>
		/// <param name="getter">message content getter</param>
		public static void Register(string messageHeader, Func<object> getter)
		{
			if (getter == null)
			{
				return;
			}

			if (string.IsNullOrWhiteSpace(messageHeader))
			{
				return;
			}

			lock (SyncRoot)
			{
				var getters = new Dictionary<string, Func<object>>(_getters);

				Func<object> value;
				if (getters.TryGetValue(messageHeader, out value))
				{
					getters[messageHeader] = value + getter;
				}
				else
				{
					getters.Add(messageHeader, getter);
				}

				Thread.MemoryBarrier();
				_getters = getters;
			}
		}

		/// <summary>
		/// get message or defualt value
		/// </summary>
		/// <typeparam name="T">expected type of a message</typeparam>
		/// <param name="messageHeader">message header</param>
		/// <param name="defaultValue">message default value</param>
		/// <returns>value of a message or default</returns>
		public static T Get<T>(string messageHeader, T defaultValue = default(T))
		{
			if (string.IsNullOrEmpty(messageHeader))
			{
				return defaultValue;
			}
			try
			{
				Func<object> getter;

				// get message publisher
				if (false == _getters.TryGetValue(messageHeader, out getter))
				{
					return defaultValue;
				}

				foreach (Func<object> @delegate in getter.GetInvocationList())
				{
					// the publisher should take care of not throwing exceptions
					var value = @delegate();
					if (value is T)
					{
						return (T)@delegate();
					}
				}

				// unexpected message type
				return defaultValue;
			}
			catch
			{
				return defaultValue;
			}
		}

		public static string[] Headlines
		{
			get
			{
				return _getters.Keys.ToArray();
			}
		}
	}
}
