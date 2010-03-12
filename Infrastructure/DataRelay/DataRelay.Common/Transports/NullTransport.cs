using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;

namespace MySpace.DataRelay.Transports
{
	/// <summary>
	/// Provides an empty hole in case a node's address is invalid or cannot be resolved.
	/// </summary>
	public class NullTransport : IRelayTransport
	{
		#region IRelayTransport Members

		public void SendMessage(RelayMessage message)
		{

		}

		public void SendMessage(SerializedRelayMessage message)
		{

		}

		public void SendInMessageList(RelayMessage[] messages)
		{		
		}

		public void SendInMessageList(SerializedRelayMessage[] messages)
		{
		}

		public void SendOutMessageList(List<RelayMessage> messages)
		{
		}

		public void SendInMessageList(List<RelayMessage> messages)
		{
		}

		public void SendInMessageList(List<SerializedRelayMessage> messages)
		{
		}

		public void GetConnectionStats(out int openConnections, out int activeConnections)
		{
			openConnections = -1;
			activeConnections = -1;
		}
		#endregion
	}
}
