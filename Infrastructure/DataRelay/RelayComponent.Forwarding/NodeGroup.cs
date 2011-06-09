using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Configuration;
using System.Net.Sockets;
using MySpace.DataRelay.Common.Schemas;
using Microsoft.Ccr.Core;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class NodeGroup
	{
		internal RelayNodeGroupDefinition GroupDefinition;
	    internal RelayRetryPolicy RelayRetryPolicy;
		
		internal NodeCluster MyCluster; //the cluster where this is running, if any
		internal List<NodeCluster> Clusters = new List<NodeCluster>(); //all of the clusters, including "MyCluster"
		internal static int MaximumQueuedItems = 750000;
		internal bool Activated;

		private static readonly LogWrapper _log = new LogWrapper();

		private ForwardingConfig _forwardingConfig;
		private bool _clusterByRange;
		private readonly System.Threading.Timer _nodeReselectTimer;
		private readonly System.Threading.TimerCallback _nodeReselectTimerCallback;

		internal string GroupName
		{
			get
			{
				return GroupDefinition.Name;
			}
		}

		internal NodeGroup(RelayNodeGroupDefinition groupDefinition, RelayNodeConfig nodeConfig, ForwardingConfig forwardingConfig)
		{   
			GroupDefinition = groupDefinition;
			Activated = groupDefinition.Activated;
			_clusterByRange = groupDefinition.UseIdRanges;
			_forwardingConfig = forwardingConfig;
			
			RelayNodeClusterDefinition myClusterDefinition = NodeManager.Instance.GetMyNodeClusterDefinition();

			foreach (RelayNodeClusterDefinition clusterDefintion in groupDefinition.RelayNodeClusters)
			{
				NodeCluster nodeCluster = new NodeCluster(clusterDefintion, nodeConfig, this, forwardingConfig);
				if (clusterDefintion == myClusterDefinition)
				{
					MyCluster = nodeCluster;
				}
				Clusters.Add(nodeCluster);
			}

			_nodeReselectTimerCallback = new System.Threading.TimerCallback(NodeReselectTimer_Elapsed);
			if (_nodeReselectTimer == null)
			{
				_nodeReselectTimer = new System.Threading.Timer(_nodeReselectTimerCallback);
			}
			_nodeReselectTimer.Change(NodeReselectIntervalMilliseconds, NodeReselectIntervalMilliseconds);

			QueueTimerCallback = new System.Threading.TimerCallback(QueueTimer_Elapsed);
			if (QueueTimer == null)
			{
				QueueTimer = new System.Threading.Timer(QueueTimerCallback);
			}
			QueueTimer.Change(DequeueIntervalMilliseconds, DequeueIntervalMilliseconds);
		}

		internal void ReloadMapping(RelayNodeGroupDefinition groupDefinition, RelayNodeConfig newConfig, ForwardingConfig newForwardingConfig)
		{
			RelayNodeClusterDefinition myClusterDefinition = newConfig.GetMyCluster();
			Activated = groupDefinition.Activated;
			GroupDefinition = groupDefinition;
			_clusterByRange = groupDefinition.UseIdRanges;
			_forwardingConfig = newForwardingConfig;
			
			if (groupDefinition.RelayNodeClusters.Length == Clusters.Count)
			{
				//same number of clusters, just let the clusters rebuild themselves. the clusters will entirely rebuild, so shuffinling around servers should be okay
				if (_log.IsDebugEnabled)
					_log.DebugFormat("Rebuilding existing clusters in group {0}.", groupDefinition.Name);
				for (int i = 0; i < groupDefinition.RelayNodeClusters.Length; i++)
				{
					Clusters[i].ReloadMapping(groupDefinition.RelayNodeClusters[i], newConfig, newForwardingConfig);
					if (groupDefinition.RelayNodeClusters[i] == myClusterDefinition)
					{
						MyCluster = Clusters[i];
					}
				}
				if (myClusterDefinition == null && MyCluster != null)
				{
					if (_log.IsInfoEnabled)
						_log.InfoFormat("Group {0} no longer contains this server. Removing.", GroupName);
					//this group no longer contains "me". If it DID contain "me", it would've been set above.
					MyCluster = null;
				}
			}
			else
			{
				//build new clusters and replace the existing ones with them
				if (_log.IsInfoEnabled)
					_log.InfoFormat("Number of clusters in group {0} changed, rebuilding.", groupDefinition.Name);
				NodeCluster myCluster = null;
				List<NodeCluster> newClusters = new List<NodeCluster>();
				foreach (RelayNodeClusterDefinition clusterDefintion in groupDefinition.RelayNodeClusters)
				{
					NodeCluster nodeCluster = new NodeCluster(clusterDefintion, newConfig, this, newForwardingConfig);
					if (clusterDefintion == myClusterDefinition)
					{
						myCluster = nodeCluster;
					}
					newClusters.Add(nodeCluster);
				}
				Clusters = newClusters;
				MyCluster = myCluster;
			}
			_nodeReselectTimer.Change(NodeReselectIntervalMilliseconds, NodeReselectIntervalMilliseconds);
		}

		private void NodeReselectTimer_Elapsed(object state)
		{
			ReselectNodes();
		}

		internal int NodeReselectIntervalMilliseconds
		{
			get
			{
				if (GroupDefinition != null)
				{
					if (GroupDefinition.NodeReselectMinutes <= 0)
					{
						return System.Threading.Timeout.Infinite;
					}
					return GroupDefinition.NodeReselectMinutes * 60 * 1000;
				}
				return System.Threading.Timeout.Infinite;
			}
		}

		private void ReselectNodes()
		{
			for (int i = 0; i < Clusters.Count; i++)
			{
				Clusters[i].ReselectNode();
			}        
		}

		internal NodeCluster GetClusterForId(int objectId, bool interClusterMessage)
		{
			if (MyCluster != null && !interClusterMessage)
			{
				return MyCluster;
			}
			
			int clusterIndex = GroupDefinition.GetClusterIndexFor(objectId);
			
			
			if (clusterIndex >= 0)
			{
				return Clusters[clusterIndex];
			}
			return null;
		}

		private int GetModdedIndex(int objectId)
		{
			if (objectId == Int32.MinValue) return 0; //cause Math.Abs(Int32.MinValue) throws
			return Math.Abs(objectId) % Clusters.Count;
		}

		/// <summary>
		/// Returns a jagged array of caching server indices and items that are assigned to them.
		/// </summary>
		/// <param name="objectIdList"></param>
		/// <returns></returns>
		public List<int>[] GetModdedIndexLists(int[] objectIdList)
		{
			List<int>[] lists = new List<int>[Clusters.Count];			

			for (int i = 0; i < objectIdList.Length; i++)
			{
				int itemId = objectIdList[i];
				int clusterIndex = GetModdedIndex(itemId);

				if (lists[clusterIndex] == null)
				{
					lists[clusterIndex] = new List<int>(objectIdList.Length);
				}
				lists[clusterIndex].Add(itemId);				
			}

			return lists;
		}

		public List<RelayMessage>[] GetModdedMessageLists(RelayMessage[] messages)
		{
			List<RelayMessage>[] lists = new List<RelayMessage>[Clusters.Count];

			for (int i = 0; i < messages.Length; i++)
			{
				int itemId = messages[i].Id;
				
				int clusterIndex = GetModdedIndex(itemId);
				
				if (lists[clusterIndex] == null)
				{
					lists[clusterIndex] = new List<RelayMessage>(messages.Length);
				}
				lists[clusterIndex].Add(messages[i]);
			}

			return lists;
		}
		
		internal static void LogNodeException(RelayMessage message, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} for node {2}.", sex.SocketErrorCode, message, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} for node {1}: {2}", message, node.ToExtendedString(), ex);
				}
			}
		}

		internal static void LogNodeException(SerializedRelayMessage message, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} for node {2}", sex.SocketErrorCode, message, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} for node {1}: {2}", message, node, ex);
				}
			}
		}

		internal static void LogNodeException(MessageList messages, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} for node {2}", sex.SocketErrorCode, messages, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} for node {1}: {2}", messages, node.ToExtendedString(), ex.ToString());
				}
			}
		}

		internal static void LogNodeOutMessageException(List<RelayMessage> messages, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} OUT messages for node {2}", sex.SocketErrorCode, messages.Count, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} OUT messages for node {1}: {2}", messages.Count, node.ToExtendedString(), ex.ToString());
				}
			}
		}

		internal static void LogNodeInMessageException(List<SerializedRelayMessage> messages, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} IN messages for node {2}", sex.SocketErrorCode, messages.Count, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} IN messages for node {1}: {2}", messages.Count, node.ToExtendedString(), ex);
				}
			}
		}

		internal static void LogNodeInMessageException(SerializedRelayMessage[] messages, Node node, Exception ex)
		{
			if (_log.IsErrorEnabled)
			{
				if (ex is SocketException)
				{
					SocketException sex = (SocketException)ex;
					_log.ErrorFormat("Socket error {0} while handling {1} IN messages for node {2}", sex.SocketErrorCode, messages.Length, node.ToExtendedString());
				}
				else
				{
					_log.ErrorFormat("Error handling {0} IN messages for node {1}: {2}.", messages.Length, node.ToExtendedString(), ex);
				}
			}
		}

		internal LinkedListStack<Node> GetNodesForMessage(RelayMessage message)
		{
			if (!Activated)
			{
				return new LinkedListStack<Node>();
			}
			
			NodeCluster cluster;
			LinkedListStack<Node> nodes;
			
			//messages that, from out of system, go to each cluster
			if(message.IsClusterBroadcastMessage)
			{
				if (MyCluster == null) //out of system, each cluster
				{
					nodes = new LinkedListStack<Node>();
					for (int clusterIndex = 0; clusterIndex < Clusters.Count; clusterIndex++)
					{
						nodes.Push(Clusters[clusterIndex].GetNodesForMessage(message));							
					}
				}
				else //in system, my cluster
				{
					nodes = MyCluster.GetNodesForMessage(message);
				}
			}
			else
			{
				//messages that route to one modded cluster
				//to modded cluster in group
				cluster = GetClusterForId(message.Id, message.IsInterClusterMsg);
				if (cluster != null)
				{
					if (message.IsInterClusterMsg && cluster.MeInThisCluster)
					{
						nodes = new LinkedListStack<Node>();
						nodes.Push(cluster.Me);
					}
					else
					{
						nodes = cluster.GetNodesForMessage(message);
					}
				}
				else
				{
					nodes = new LinkedListStack<Node>();
				}
			}
			return nodes;
		}

		#region HtmlStatus

	  internal void GetHtmlStatus(StringBuilder statusBuilder, TypeSettingCollection typeSettingCollection)
		{
			statusBuilder.Append("<table class=\"nodeGroupBox\">" + Environment.NewLine);
			AddHeaderLine(statusBuilder, "Group " + GroupName,1);

		 // display the the type setting information of the group
		 if (typeSettingCollection != null)
		 {
			 statusBuilder.Append(@"<tr><td>" + Environment.NewLine);
			 statusBuilder.Append("<table class=\"nodeGroupTypeIDBox\">" + Environment.NewLine);
			 statusBuilder.Append("<tr><th align=\"left\">Type Infomation</th></tr>");

			 foreach (TypeSetting ts in typeSettingCollection)
			 {
				 if (ts.GroupName != null && ts.GroupName.ToUpperInvariant() == GroupName.ToUpperInvariant())
				 {
					 statusBuilder.Append(@"<tr>");
					 statusBuilder.Append(string.Format("<td align=\"left\"> {0} </td>", ts));
					 statusBuilder.Append(@"</tr>");
				 }
			 }

			 statusBuilder.Append(@"</table>" + Environment.NewLine);
			 statusBuilder.Append(@"</td></tr>" + Environment.NewLine);
		 }

			foreach (NodeCluster cluster in Clusters)
			{
				statusBuilder.Append(@"<tr><td>" + Environment.NewLine);
				cluster.GetHtmlStatus(statusBuilder);
				statusBuilder.Append(@"</td></tr>" + Environment.NewLine);
			}
			statusBuilder.Append(@"</table>" + Environment.NewLine);
		}
		 
		public NodeGroupStatus GetNodeGroupStatus(TypeSettingCollection typeSettingCollection)
		{
		 	NodeGroupStatus nodeGroupStatus = new NodeGroupStatus();

			nodeGroupStatus.GroupName = this.GroupName;

			if (typeSettingCollection != null)
			{
				foreach (TypeSetting ts in typeSettingCollection)
				{
					if (ts.GroupName != null && ts.GroupName.ToUpperInvariant() == GroupName.ToUpperInvariant())
					{
						TypeSettingStatus tss = TypeSpecificStatisticsManager.Instance.GetStatus(ts.TypeId);
						if(tss == null)//should not be null
						{
							_log.WarnFormat("NodeGroup:GetNodeGroupStatus " +
								"TypeSettingStatus is null for typeId:{0}",
								ts.TypeId);
							tss = new TypeSettingStatus();//add empty one to hold place and show error
						}
						nodeGroupStatus.TypeSettingStatuses.Add(tss);				
					}
				}
			}

			foreach (NodeCluster cluster in Clusters)
			{
				nodeGroupStatus.NodeClusterStatuses.Add(cluster.GetNodeClusterStatus());
			}

			return nodeGroupStatus;
		}
		internal static void AddPropertyLine(StringBuilder statusBuilder, string propName, double propValue, int precision)
		{
			if (propValue.ToString() == "0")
			{
			}
			else
			{
				statusBuilder.AppendFormat("<tr><td valign=top><b>{0}</b>:</td> <td valign=top>{1}</td></tr>" + Environment.NewLine, propName, propValue.ToString("N" + precision));
			}
		}

		internal static void AddPropertyLine(StringBuilder statusBuilder, string propName, string propValue)
		{
			statusBuilder.AppendFormat("<tr><td valign=top><b>{0}</b>:</td> <td valign=top>{1}</td></tr>" + Environment.NewLine, propName, propValue);
		}

		internal static void AddHeaderLine(StringBuilder statusBuilder, object propValue)
		{
			AddHeaderLine(statusBuilder, propValue, 2);
		}
		
		internal static void AddHeaderLine(StringBuilder statusBuilder, object propValue, int colSpan)
		{
			statusBuilder.AppendFormat("<tr><td colspan=\"{0}\" valign=top><b>{1}</b>:</td></tr>" + Environment.NewLine, colSpan, propValue);
		}

		#endregion

		internal void ProcessQueues()
		{
			for (int i = 0; i < Clusters.Count; i++)
			{
				Clusters[i].ProcessQueues();
			}
		}

		internal void PopulateQueues(Dictionary<string, ErrorQueue> errorQueues, bool incrementCounter)
		{
			for (int i = 0; i < Clusters.Count; i++)
			{
				Clusters[i].PopulateQueues(errorQueues,incrementCounter);
			}
		}

		internal void AggregateCounterTick()
		{
			for (int i = 0; i < Clusters.Count; i++)
			{
				Clusters[i].AggregateCounterTicker();
			}
		}

		internal void SetNewDispatchers(Dispatcher newInDispatcher, Dispatcher newOutDispatcher)
		{
			for (int i = 0; i < Clusters.Count; i++)
			{
				Clusters[i].SetNewDispatchers(newInDispatcher, newOutDispatcher);
			}
		}

		internal int GetClusterQueueDepth()
		{
			return (MaximumQueuedItems / GroupDefinition.RelayNodeClusters.Length);
		}

		internal QueueConfig GetQueueConfig()
		{
			if (GroupDefinition.QueueConfig != null)
			{
				return GroupDefinition.QueueConfig;
			}
			if (_forwardingConfig != null)
			{
				return _forwardingConfig.QueueConfig;
			}
			
			return null;
		}

		#region Error Queue Processing 
		private readonly System.Threading.Timer QueueTimer;
		private readonly System.Threading.TimerCallback QueueTimerCallback;

		internal int DequeueIntervalMilliseconds
		{
			get
			{
				QueueConfig config = GetQueueConfig();
				if(config == null || !config.Enabled || config.DequeueIntervalSeconds < 1)				
				{
					return System.Threading.Timeout.Infinite;
				}
				return config.DequeueIntervalSeconds * 1000;
			}
		}

		void QueueTimer_Elapsed(object state)
		{
			QueueTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);			
			ProcessQueues();
			if (QueueTimer != null)
			{
				QueueTimer.Change(DequeueIntervalMilliseconds, DequeueIntervalMilliseconds);
			}
		}
		#endregion
		
		internal void Shutdown()
		{
			if (_nodeReselectTimer != null)
			{
				_nodeReselectTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
				_nodeReselectTimer.Dispose();
			}
			if (QueueTimer != null)
			{
				QueueTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
				QueueTimer.Dispose();
			}
		}	
	}
}
