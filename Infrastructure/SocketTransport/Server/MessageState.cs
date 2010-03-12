using System;
using System.IO;
using System.Net;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Responsible for holding the data required to process a message.
	/// </summary>
	public class MessageState
	{
		/// <summary>
		/// Gets the command requested for the <see cref="Message"/>.
		/// </summary>
		public int CommandId { get; internal set; }

		/// <summary>
		/// Gets the <see cref="MemoryStream"/> containing a complete message.
		/// </summary>
		public MemoryStream Message { get; internal set; }

		/// <summary>
		/// Gets the length of the <see cref="Message"/>.
		/// </summary>
		public int Length { get; internal set; }

		/// <summary>
		/// Gets the state object to use for responding to the message.  
		/// </summary>
		/// <remarks>This object is opaque do not count on it being the same datatype
		/// between releases.</remarks>
		public IPEndPoint ClientIP { get; internal set; }
	}
}
