using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.DataRelay
{
	public class HydrationResultInfo
	{
		internal HydrationResultInfo(MessageType originalMessageType, RelayPayload originalPayload)
		{
			OriginalMessageType = originalMessageType;
			OriginalPayload = originalPayload;
		}

		internal HydrationResultInfo(RelayMessage originalMessage)
		{
			OriginalMessageType = originalMessage.MessageType;
			OriginalPayload = originalMessage.Payload;
		}

		public MessageType OriginalMessageType { get; private set; }
		public RelayPayload OriginalPayload { get; private set; }
	}
}
