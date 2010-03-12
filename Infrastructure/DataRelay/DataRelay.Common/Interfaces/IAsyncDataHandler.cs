using System;
using System.Collections.Generic;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Implement for asynchronous data handling of <see cref="RelayMessage"/>.
	/// </summary>
	public interface IAsyncDataHandler
	{
		/// <summary>
		/// Begins asynchronous processing of a single <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="message">The <see cref="RelayMessage"/>.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>Returns an <see cref="IAsyncResult"/>.</returns>
		IAsyncResult BeginHandleMessage(RelayMessage message, object state, AsyncCallback callback);
		
		/// <summary>
		/// Ends asynchronous processing of a single <see cref="RelayMessage"/>.
		/// </summary>
		/// <param name="asyncResult">The <see cref="IAsyncResult"/> from <see cref="BeginHandleMessage"/></param>
		void EndHandleMessage(IAsyncResult asyncResult);

		/// <summary>
		/// Begins asynchronous processing of a <see cref="List{T}"/> of <see cref="RelayMessage"/>s.
		/// </summary>
		/// <param name="messages">The list of <see cref="RelayMessage"/>s.</param>
		/// <param name="state">Callers can put any state they like here.</param>
		/// <param name="callback">The method to call upon completion.</param>
		/// <returns>Returns an <see cref="IAsyncResult"/>.</returns>
		IAsyncResult BeginHandleMessages(IList<RelayMessage> messages, object state, AsyncCallback callback);
		
		/// <summary>
		/// Ends asynchronous processing of a <see cref="List{T}"/> of <see cref="RelayMessage"/>s.
		/// </summary>
		/// <param name="asyncResult">The <see cref="IAsyncResult"/> from <see cref="BeginHandleMessages"/></param>
		void EndHandleMessages(IAsyncResult asyncResult);

	}
}
