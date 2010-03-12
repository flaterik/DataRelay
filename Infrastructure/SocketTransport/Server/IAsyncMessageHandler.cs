using System;
using System.IO;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Represents an interface to be implemented to handle messages asynchronously 
	/// from the <see cref="SocketServer"/>.
	/// </summary>
	/// <remarks>When a complete message has been retrieved from the network connection,
	/// <see cref="BeginHandleMessage"/> is called to start asynchronous processing
	/// of the message.</remarks>
	public interface IAsyncMessageHandler : IMessageHandler
	{
		/// <summary>
		/// Begins the handling of a complete message from a stream. All references 
		/// to <see cref="MessageState.Message"/> must be released by the end of this method.
		/// </summary>
		/// <param name="message">The complete message that is to be handled.</param>
		/// <param name="callback">The delegate to call when complete.</param>
		/// <returns>Returns an <see cref="IAsyncResult"/>.</returns>
		/// <remarks>
		///		<para>
		///		All implementors must release any references to <see cref="MessageState.Message"/>
		///		by the time that <see cref="BeginHandleMessage"/> returns.
		///		</para>
		/// </remarks>
		IAsyncResult BeginHandleMessage(MessageState message, AsyncCallback callback);

		/// <summary>
		/// Ends the handling of a message and returns the memory stream to send back to the client.
		/// </summary>
		/// <param name="asyncResult">The <see cref="IAsyncResult"/> returned from <see cref="BeginHandleMessage"/>.</param>
		/// <returns>Retuns a <see cref="MemoryStream"/> to send back to the client, if the stream is <see langword="null"/> an empty response is sent.</returns>
		MemoryStream EndHandleMessage(IAsyncResult asyncResult);
	}
}
