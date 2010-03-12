using System;
using System.Collections.Generic;

namespace MySpace.DataRelay.Transports
{
	/// <summary>
	/// 	<para>Optional asynchronous contract for <see cref="IRelayTransport"/> implementations.</para>
	/// </summary>
	public interface IAsyncRelayTransport : IRelayTransport
	{
		/// <summary>
		/// Begins sending the specified relay message.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <param name="forceRoundTrip">Specify <see langword="true"/> to force a round trip for one-way messages; <see langword="false"/> otherwise.</param>
		/// <param name="callback">A callback that will be invoked when the operation completes.</param>
		/// <param name="state">A consumer-defined state object; This value will be accessible via <see cref="IAsyncResult.AsyncState"/>.</param>
		/// <returns>
		///	<para>An <see cref="IAsyncResult"/> object that must be passed to <see cref="EndSendMessage"/> to complete the operation.</para>
		/// </returns>
		IAsyncResult BeginSendMessage(RelayMessage message, bool forceRoundTrip, AsyncCallback callback, object state);

		/// <summary>
		/// Begins sending the specified relay message.
		/// </summary>
		/// <param name="message">The message to send.</param>
		/// <param name="callback">A callback that will be invoked when the operation completes.</param>
		/// <param name="state">A consumer-defined state object; This value will be accessible via <see cref="IAsyncResult.AsyncState"/>.</param>
		/// <returns>
		///	<para>An <see cref="IAsyncResult"/> object that must be passed to <see cref="EndSendMessage"/> to complete the operation.</para>
		/// </returns>
		IAsyncResult BeginSendMessage(SerializedRelayMessage message, AsyncCallback callback, object state);

		/// <summary>
		/// Ends sending a relay message.
		/// </summary>
		/// <param name="result">
		///	<para>The result object that was returned from <see cref="BeginSendMessage(RelayMessage, bool, AsyncCallback, object)"/>
		///	or <see cref="BeginSendMessage(SerializedRelayMessage, AsyncCallback, object)"/>.</para>
		/// </param>
		void EndSendMessage(IAsyncResult result);

		/// <summary>
		/// Begins sending an array of serialized relay messages.
		/// </summary>
		/// <param name="messages">The messages to send.</param>
		/// <param name="callback">A callback that will be invoked when the operation completes.</param>
		/// <param name="state">A consumer-defined state object; This value will be accessible via <see cref="IAsyncResult.AsyncState"/>.</param>
		/// <returns>
		///	<para>An <see cref="IAsyncResult"/> object that must be passed to <see cref="EndSendMessageList"/> to complete the operation.</para>
		/// </returns>
		IAsyncResult BeginSendInMessageList(SerializedRelayMessage[] messages, AsyncCallback callback, object state);

		/// <summary>
		/// Begins sending a list of serialized relay messages.
		/// </summary>
		/// <param name="messages">The messages to send.</param>
		/// <param name="callback">A callback that will be invoked when the operation completes.</param>
		/// <param name="state">A consumer-defined state object; This value will be accessible via <see cref="IAsyncResult.AsyncState"/>.</param>
		/// <returns>
		///	<para>An <see cref="IAsyncResult"/> object that must be passed to <see cref="EndSendMessageList"/> to complete the operation.</para>
		/// </returns>
		IAsyncResult BeginSendInMessageList(List<SerializedRelayMessage> messages, AsyncCallback callback, object state);

		/// <summary>
		/// Ends sending the messages.
		/// </summary>
		/// <param name="result">
		///	<para>The <see cref="IAsyncResult"/> object that was returned from
		///	<see cref="BeginSendInMessageList(SerializedRelayMessage[], AsyncCallback, object)"/>
		///	or <see cref="BeginSendInMessageList(List{SerializedRelayMessage}, AsyncCallback, object)"/>.</para>
		/// </param>
		void EndSendInMessageList(IAsyncResult result);

		/// <summary>
		/// Begins sending a list of relay messages.
		/// </summary>
		/// <param name="messages">The messages to send.</param>
		/// <param name="callback">A callback that will be invoked when the operation completes.</param>
		/// <param name="state">A consumer-defined state object; This value will be accessible via <see cref="IAsyncResult.AsyncState"/>.</param>
		/// <returns>
		///	<para>An <see cref="IAsyncResult"/> object that must be passed to <see cref="EndSendMessageList"/> to complete the operation.</para>
		/// </returns>
		IAsyncResult BeginSendMessageList(List<RelayMessage> messages, AsyncCallback callback, object state);

		/// <summary>
		/// Ends sending the messages.
		/// </summary>
		/// <param name="result">
		///	<para>The <see cref="IAsyncResult"/> object that was returned from <see cref="BeginSendMessageList"/>.</para>
		/// </param>
		void EndSendMessageList(IAsyncResult result);
	}
}
