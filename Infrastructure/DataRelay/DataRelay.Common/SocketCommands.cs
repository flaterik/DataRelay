namespace MySpace.DataRelay.SocketTransport
{
	//Add any new commands AT THE END! DO NOT REORDER!
	public enum SocketCommand
	{
		Unknown,
		HandleOneWayMessage,
		HandleOneWayMessages,
		HandleSyncMessage,
		HandleSyncMessages,
		GetRuntimeInfo
	}

}