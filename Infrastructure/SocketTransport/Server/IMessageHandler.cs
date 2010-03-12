using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Represents an interface to be implemented to handle messages synchronously 
	/// from the <see cref="SocketServer"/>.
	/// </summary>
	public interface IMessageHandler
	{
		/// <summary>Handle a complete message.</summary>
		/// <param name="commandID">The command id of the message.</param>
		/// <param name="messageStream">The stream that contains the message.</param>
		/// <param name="messageLength">The length of the message contained in the stream.</param>
		/// <returns>Returns a stream containing the message to return to the client. If null, sends an empty response.</returns>
		MemoryStream HandleMessage(int commandID, MemoryStream messageStream, int messageLength);
	}
}
