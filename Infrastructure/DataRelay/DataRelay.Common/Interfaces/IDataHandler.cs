using System;
using System.Collections.Generic;
using System.Text;


namespace MySpace.DataRelay
{
	public interface IDataHandler
	{
		void HandleMessage(RelayMessage message);
		void HandleMessages(IList<RelayMessage> messages);
	}
}
