namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class NodeWithMessages
	{	
		internal NodeWithMessages(NodeWithInfo node)
		{
			NodeWithInfo = node;
		}
		
		internal NodeWithInfo NodeWithInfo;
		internal SerializedMessageList Messages = new SerializedMessageList();
		
	}

}
