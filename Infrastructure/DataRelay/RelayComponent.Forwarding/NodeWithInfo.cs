using System;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{

	/// <summary>
	///	The key for the NodeWithMessages collection
	///	Implements IEquatable to support the index[] property of KeyedCollection with
	///	a complex type.
	/// </summary>
	internal class NodeWithInfo : IEquatable<NodeWithInfo> {
		internal Node Node;
		/// <summary>
		/// CTOR
		/// </summary>
		/// <param name="node"></param>
		/// <param name="syncInMessages"></param>
		/// <param name="skipErrorQueue"></param>
		/// <summary>
		/// added: cbrown
		/// To allow exceptions to be thrown from certain object types, and the error queue
		/// to be used by others, even when they target the same node, the NodeWithMessages
		/// collection now seperates the Relay Messages that can throw exceptions from the
		/// remainder of the queue.  This class is now the key to the NodeWithMessages collection
		/// class.
		/// </summary>
		internal NodeWithInfo(Node node, bool syncInMessages, bool skipErrorQueue) 
		{
			Node = node;
			SyncInMessages = syncInMessages;
			SkipErrorQueueForSync = skipErrorQueue;
		}
		
		internal bool SyncInMessages { get; set; }
		
		internal bool SkipErrorQueueForSync { get; set; }
	
		#region IEquatable<NodeWithInfo> Members

		public bool Equals(NodeWithInfo other) {
			if ((other.Node == Node) &&  (other.SyncInMessages == SyncInMessages) &&  (other.SkipErrorQueueForSync == SkipErrorQueueForSync)) 
			{
				return true;
			}
			// even if the message types are different, they are handled the same,
			// so consider them equal
			return false;			
		}

		#endregion
	}
}