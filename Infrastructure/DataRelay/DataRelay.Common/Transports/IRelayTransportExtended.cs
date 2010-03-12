using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;

namespace MySpace.DataRelay.Transports
{
	public interface IRelayTransportExtended
	{
		void SendSyncMessage(RelayMessage message);

		void SendSyncMessageList(List<RelayMessage> messages);
	
	}
}
