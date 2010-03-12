using System.Collections.Generic;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class MessagesWithLock
	{
		internal MessagesWithLock(List<RelayMessage> messages, HandleWithCount locker)
		{
			Messages = messages;
			Locker = locker;
		}

		internal List<RelayMessage> Messages;
		internal HandleWithCount Locker;
	}
}