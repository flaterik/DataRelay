using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Represents a <see cref="IAsyncResult"/> for <see cref="RelayNode"/> 
	/// and <see cref="IAsyncDataHandler"/> for a list of <see cref="RelayMessage"/>.
	/// </summary>
	internal class RelayMessageListAsyncResult: BaseAsyncResult
	{
		private readonly IList<RelayMessage> _mesages; 

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="RelayMessageAsyncResult"/> class.</para>
		/// </summary>
		/// <param name="messages">The list of <see cref="RelayMessage"/>. Never <see langword="null"/>.</param>
		/// <param name="state">The caller's state.</param>
		/// <param name="callback">The callback. Never <see langword="null"/>.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="messages"/> 
		/// or <paramref name="callback"/> is null.</exception>
		public RelayMessageListAsyncResult(IList<RelayMessage> messages, object state, AsyncCallback callback)
			: base(state, callback)
		{
			if (messages == null)
			{
				throw new ArgumentNullException("messages");
			}
			_mesages = messages;
		}

		/// <summary>
		/// Gets the <see cref="RelayMessage"/> to process.
		/// </summary>
		/// <value>The message; can't be null.</value>
		public IList<RelayMessage> Messages { get { return _mesages; } }
	}
}
