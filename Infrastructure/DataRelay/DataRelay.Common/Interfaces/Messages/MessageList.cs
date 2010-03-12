using System.Collections.Generic;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	public class MessageList
	{		
		public List<RelayMessage> OutMessages;
		public List<RelayMessage> InMessages;

		public MessageList()
		{
		}

		public MessageList(IList<RelayMessage> messages)
		{
			for (int i = 0; i < messages.Count; i++)
			{
				Add(messages[i]);
			}
		}

		public int OutMessageCount
		{
			get
			{
				if (OutMessages == null)
				{
					return 0;
				}
				return OutMessages.Count;
			}
		}

		public int InMessageCount
		{
			get
			{
				if (InMessages == null)
				{
					return 0;
				}
				return InMessages.Count;
			}
		}

		

		public void Add(RelayMessage message)
		{
		    if(message.IsTwoWayMessage)
			{
	            if(OutMessages == null)
	            {
		            OutMessages = new List<RelayMessage>();
	            }				
	            OutMessages.Add(message);
			}
			else
			{
		        if (InMessages == null)
		        {
			        InMessages = new List<RelayMessage>();
		        }
		        InMessages.Add(message);
		    }
		}

		public override string ToString()
		{
			return string.Format("Relay Message List with {0} In Messages and {1} Out Messages",InMessageCount,OutMessageCount);
		}

		
	}

	
}
