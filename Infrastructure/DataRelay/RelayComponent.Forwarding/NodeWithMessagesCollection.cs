using System.Collections.ObjectModel;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;


namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Collection of messages, keyed by node used for processing
	/// </summary>
	/// <remarks>
	/// cbrown:
	/// Changed from
	///		:KeyedCollection&lt;Node, NodeWithMessages&gt; 
	///			to
	///		:KeyedCollection&lt;NodeWithInfo,NodeWithMessages&gt;
	///		
	/// Supports carry of typesettings along with node for the purpose of
	/// sync messages with throwOnError set
	/// A single node may handle multiple types of messages, each with its
	/// own Sync/Throw settings.  These will be considered the same if 
	///		A) Node is the same
	///		B) SyncInMessages is the same
	///		C) ThrowOnSyncFailure is the same
	///		
	/// NOTE: The nodes will be considered the same, even for different message types
	/// when the above conditions are met.  Thus, multiple message types can be conained in
	/// a single NodeWithMessages structure	
	/// </remarks>
	internal class NodeWithMessagesCollection : KeyedCollection<NodeWithInfo, NodeWithMessages>
	{
		internal void Add(RelayMessage message, SimpleLinkedList<Node> nodes)
		{
			TypeSetting typeSetting = NodeManager.Instance.Config.TypeSettings.TypeSettingCollection[message.TypeId];

			Node node;
			while (nodes.Pop(out node))
			{
				bool typesettingThrowOnSyncFailure = false;
				bool typesettingSyncInMessages = false;
				if (null != typeSetting && !node.NodeCluster.MeInThisCluster)
				{
					typesettingSyncInMessages = typeSetting.SyncInMessages;
					typesettingThrowOnSyncFailure = typeSetting.ThrowOnSyncFailure;
				}

				// Contains no longer works now that the NodeWithMessages structure has
				// been added, loop through and check the settings
				bool bAdded = false;
				foreach (NodeWithMessages nwm in this)
				{
					NodeWithInfo nwi = GetKeyForItem(nwm);
					if ((nwi.Node == node) &&
						(nwi.SyncInMessages == typesettingSyncInMessages) &&
						(nwi.SkipErrorQueueForSync == typesettingThrowOnSyncFailure))
					{
						bAdded = true;
						this[nwi].Messages.Add(message);
						break;
					}
				}
				if (!bAdded)
				{
					NodeWithInfo newNWI = new NodeWithInfo(node, typesettingSyncInMessages, typesettingThrowOnSyncFailure);
					NodeWithMessages newNode = new NodeWithMessages(newNWI);
					Add(newNode);
					this[newNWI].Messages.Add(message);
				}

			}
		}

		protected override NodeWithInfo GetKeyForItem(NodeWithMessages item)
		{
			return item.NodeWithInfo;
		}
	}
}