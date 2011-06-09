using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MySpace.DataRelay
{
	internal class SerializedMessageList
	{		
		public List<RelayMessage> OutMessages;
		public List<SerializedRelayMessage> InMessages;

		public SerializedMessageList()
		{
		}

		public SerializedMessageList(IList<RelayMessage> messages) : this(messages,false)
		{			
		}

		public SerializedMessageList(IList<RelayMessage> messages, bool traceMessageInfo)
		{
			if (traceMessageInfo)
			{				
				for (int i = 0; i < messages.Count; i++)
				{
					Trace.WriteLine(messages[i]);
					Add(messages[i]);
				}
			}
			else
			{
				for (int i = 0; i < messages.Count; i++)
				{
					Add(messages[i]);
				}
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
			        InMessages = new List<SerializedRelayMessage>();
		        }
		        InMessages.Add(new SerializedRelayMessage(message));
			}
		}

		public void Add(SerializedRelayMessage message)
		{
			if (InMessages == null)
			{
				InMessages = new List<SerializedRelayMessage>();
			}
			InMessages.Add(message);
		}

		public override string ToString()
		{
			return "Relay Message List with " + InMessageCount + " In Messages and " + OutMessageCount + " Out Messages";
		}

		
	}

	
}
