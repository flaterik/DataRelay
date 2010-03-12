using System;
using System.Collections.Generic;
using MySpace.SocketTransport;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Provides an <see cref="IAsyncResult"/> implementation <see cref="SocketServerAsyncMessageHandler"/>
	/// and <see cref="IAsyncMessageHandler"/>.
	/// </summary>
	internal class SocketHandlerAsyncResult: BaseAsyncResult
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SocketHandlerAsyncResult"/> class.</para>
		/// </summary>
		/// <param name="state">The callers state.</param>
		/// <param name="callback">The callback. Never <see langword="null"/>.</param>
		public SocketHandlerAsyncResult(object state, AsyncCallback callback) : base(state, callback)
		{}

		internal RelayMessage ReplyMessage { get; set;}
		internal IList<RelayMessage> ReplyMessages { get; set; }
		internal ComponentRuntimeInfo[] RuntimeInfo { get; set; }
	}
}
