using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;

namespace MySpace.DataRelay.Transports
{
	public interface IRelayTransport
	{
		void SendMessage(RelayMessage message);

		void SendMessage(SerializedRelayMessage message);

		void SendInMessageList(SerializedRelayMessage[] messages);

		void SendInMessageList(List<SerializedRelayMessage> messages);

		void SendOutMessageList(List<RelayMessage> messages);

		void GetConnectionStats(out int openConnections, out int activeConnections);
	}

	
}
