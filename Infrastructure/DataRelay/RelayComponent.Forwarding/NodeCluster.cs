using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.Common.Schemas;
using Microsoft.Ccr.Core;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class NodeCluster
	{
		internal Node ChosenNode;
		private readonly object _chooseLock = new object();

		internal Node Me; //The server the code is running on

		// Nodes organized by their designated Zones
		internal Dictionary<ushort, Node> ChosenZoneNodes = new Dictionary<ushort, Node>();
		private readonly object _chooseZoneNodeLock = new object();

		internal List<Node> Nodes = new List<Node>(); //All nodes in the cluster EXCEPT "Me"

		private static readonly LogWrapper log = new LogWrapper();

		private readonly NodeGroup _nodeGroup;
		
		private List<Node>[]    _nodesByZone; //Divided using detectedZone, based on the zonedefinition node instead of the per-node zone definition
		private ushort          _localZone; //determined by local ip address and zone definition config, NOT the zone on the node
		private ZoneDefinitionCollection _zoneDefinitions; //kept around just to see if it's changed during a config reload        
		private readonly RelayNodeClusterDefinition _clusterDefinition;
		private int _minimumId, _maximumId;
		private readonly bool _meInThisCluster;
		
		/// <summary>
		/// Returns TRUE if the calling node is contained in the cluster.
		/// This will never be true for replication.
		/// Used by Forwarder to determine replicated messages which should always be
		/// processed Async, regardless of the typeSettings
		/// </summary>
		internal bool MeInThisCluster 
		{
			get { return _meInThisCluster; }
		}

		internal static string GetQueueNameFor(RelayNodeClusterDefinition definition)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("Relay Cluster ");
			for (int i = 0; i < definition.RelayNodes.Length; i++)
			{
				sb.Append(definition.RelayNodes[i].ToString());
				if (i < definition.RelayNodes.Length - 1)
				{
					sb.Append(", ");
				}
			}
			return sb.ToString();
		}

		internal static string GetMessageQueueNameFor(RelayNodeDefinition nodeDefinition)
		{
			return "Relay Node " + nodeDefinition;
		}

		internal NodeCluster(RelayNodeClusterDefinition clusterDefinition, RelayNodeConfig nodeConfig, NodeGroup nodeGroup, ForwardingConfig forwardingConfig)
		{
			_nodeGroup = nodeGroup;
			_clusterDefinition = clusterDefinition;
			_minimumId = clusterDefinition.MinId;
			_maximumId = clusterDefinition.MaxId;
			
			RelayNodeDefinition meDefinition = nodeConfig.GetMyNode();
			_meInThisCluster = false;
			
			_localZone = nodeConfig.GetLocalZone();
			_zoneDefinitions = nodeConfig.RelayNodeMapping.ZoneDefinitions;
			foreach (RelayNodeDefinition nodeDefinition in clusterDefinition.RelayNodes)
			{
				if (meDefinition == nodeDefinition)
				{
					_meInThisCluster = true;
				}
			}
			
			DispatcherQueue nodeInQueue, nodeOutQueue;
			
			if (_meInThisCluster)
			{
				GetMessageQueuesFor(GetMessageQueueNameFor(meDefinition), clusterDefinition,
					NodeManager.Instance.InMessageDispatcher, NodeManager.Instance.OutMessageDispatcher,
					out nodeInQueue, out nodeOutQueue);

				Me = new Node(meDefinition, nodeGroup, this, forwardingConfig,
						nodeInQueue, nodeOutQueue);
			}			

			ushort maxDetectedZone = _localZone;
			foreach (RelayNodeDefinition nodeDefinition in clusterDefinition.RelayNodes)
			{
				if (nodeDefinition != meDefinition)
				{
					GetMessageQueuesFor(GetMessageQueueNameFor(nodeDefinition), clusterDefinition,
						NodeManager.Instance.InMessageDispatcher, NodeManager.Instance.OutMessageDispatcher,
						out nodeInQueue, out nodeOutQueue);
					Node node = new Node(nodeDefinition, nodeGroup, this, forwardingConfig,
						nodeInQueue, nodeOutQueue);

					Nodes.Add(node);
					
					if (node.Zone > maxDetectedZone)
					{
						maxDetectedZone = node.Zone;
					}
					
				}				
			}
			
			_nodesByZone = CalculateNodesByZone(Nodes, maxDetectedZone);
		}

		#region Node Mapping 
		private static List<Node>[] CalculateNodesByZone(IEnumerable<Node> nodes, ushort maxDetectedZone)
		{
			List<Node>[] nodesByDetectedZone = new List<Node>[maxDetectedZone + 1];
			for (int i = 0; i < nodesByDetectedZone.Length; i++)
			{
				nodesByDetectedZone[i] = new List<Node>(1);
			}
			try
			{
				foreach (Node node in nodes)
				{
					nodesByDetectedZone[node.Zone].Add(node);
				}

			}
			catch (Exception e)
			{
				if (log.IsErrorEnabled)
					log.ErrorFormat("Error calculating nodes by detected zone: {0}", e);
				nodesByDetectedZone = null;
			}
			return nodesByDetectedZone;
		}



		#endregion 

		internal void ReloadMapping(RelayNodeClusterDefinition relayNodeClusterDefinition, RelayNodeConfig newConfig, ForwardingConfig forwardingConfig)
		{
			_minimumId = relayNodeClusterDefinition.MinId;
			_maximumId = relayNodeClusterDefinition.MaxId;
			
			//figure out if anything changed. if it did, rebuild
			bool rebuild = false;

			ushort newLocalZone = newConfig.GetLocalZone();
			if (newLocalZone != _localZone)
			{
				rebuild = true;
				_localZone = newLocalZone;
			}
			else
			{
				if((_zoneDefinitions == null && newConfig.RelayNodeMapping.ZoneDefinitions != null) ||
				(_zoneDefinitions != null && newConfig.RelayNodeMapping.ZoneDefinitions == null) ||
				(_zoneDefinitions != null && !_zoneDefinitions.Equals(newConfig.RelayNodeMapping.ZoneDefinitions)))
				{
					rebuild = true;
					_zoneDefinitions = newConfig.RelayNodeMapping.ZoneDefinitions;
				}
				
			}
			

			int effectiveSize = (!_meInThisCluster ? Nodes.Count : Nodes.Count + 1);
			
			//if there's a different number of nodes, we definitely have to rebuild
			if (relayNodeClusterDefinition.RelayNodes.Length != effectiveSize)
			{
				if(log.IsInfoEnabled)
					log.InfoFormat("Number of nodes in a cluster in group {0} changed from {1} to {2}, rebuilding", _nodeGroup.GroupName, effectiveSize, relayNodeClusterDefinition.RelayNodes.Length);				
				rebuild = true;
			}
			else
			{
				//if any of the nodes we do have aren't in the config, rebuild				
				foreach (Node node in Nodes)
				{
					if (!relayNodeClusterDefinition.ContainsNode(node.EndPoint.Address, node.EndPoint.Port))
					{
						if (log.IsInfoEnabled)
							log.InfoFormat("Node {0} is no longer found in its cluster in group {1}, rebuilding.",
								node, _nodeGroup.GroupName);
						rebuild = true;						
						break;
					}
				}
				
				if (!rebuild && _meInThisCluster)
				{
					if (!relayNodeClusterDefinition.ContainsNode(Me.EndPoint.Address, Me.EndPoint.Port))
					{
						if (log.IsInfoEnabled)
							log.InfoFormat("Node {0} (this machine) is no longer found in its cluster in group {1}, rebuilding.",
								Me, _nodeGroup.GroupName);
						rebuild = true;						
					}
				}

				//or if there are any nodes in the config that aren't here, rebuild
				if (!rebuild)
				{
					foreach(RelayNodeDefinition nodeDefinition in relayNodeClusterDefinition.RelayNodes)
					{
						if (!ContainsNode(nodeDefinition))
						{
							if (log.IsInfoEnabled)
								log.InfoFormat("Node {0} is defined in the new config but does not exist in this cluster in group {1}, rebuilding.",
									nodeDefinition, _nodeGroup.GroupName);
							rebuild = true;
							break;
						}
					}
				}
			}

			if (rebuild)
			{			
				
				List<Node> newNodes = new List<Node>();

				RelayNodeDefinition meDefinition = newConfig.GetMyNode();
				DispatcherQueue nodeInQueue, nodeOutQueue;
				
				if (meDefinition != null)
				{                    
					GetMessageQueuesFor(GetMessageQueueNameFor(meDefinition), relayNodeClusterDefinition,
						NodeManager.Instance.InMessageDispatcher, NodeManager.Instance.OutMessageDispatcher,
						out nodeInQueue, out nodeOutQueue);
					//Me is in the new config
					//Either create it new or overwrite the old one					
					Me = new Node(meDefinition, _nodeGroup, this, forwardingConfig,
						nodeInQueue, nodeOutQueue);

				}
				else
				{
					//me is NOT in the new config.
					Me = null;
				}
				ushort maxDetectedZone = _localZone;
				foreach (RelayNodeDefinition nodeDefinition in relayNodeClusterDefinition.RelayNodes)
				{					
					if (nodeDefinition != meDefinition)
					{
						GetMessageQueuesFor(GetMessageQueueNameFor(nodeDefinition), relayNodeClusterDefinition,
						NodeManager.Instance.InMessageDispatcher, NodeManager.Instance.OutMessageDispatcher,
						out nodeInQueue, out nodeOutQueue);

						Node node = new Node(nodeDefinition, _nodeGroup, this, forwardingConfig,
							nodeInQueue, nodeOutQueue);

						newNodes.Add(node);
						
						if (node.Zone > maxDetectedZone)
						{
							maxDetectedZone = node.Zone;
						}
					}
				}
				Nodes = newNodes;
				_nodesByZone = CalculateNodesByZone(Nodes, maxDetectedZone);
				lock (_chooseLock)
				{
					ChosenNode = null;
				}
				ChosenZoneNodes = new Dictionary<ushort, Node>();
			}
			else
			{	
				//just reload the configs to get any new network or queue settings
				bool hitMe = false;
				string meString = String.Empty;
				if (Me != null)
				{
					meString = Me.ToString();
				}
				for (int i = 0; i < relayNodeClusterDefinition.RelayNodes.Length; i++)
				{
					string definitionString = relayNodeClusterDefinition.RelayNodes[i].Host + ":" + relayNodeClusterDefinition.RelayNodes[i].Port;
					if (definitionString == meString && Me != null)
					{
						hitMe = true;
						Me.ReloadMapping(relayNodeClusterDefinition.RelayNodes[i], forwardingConfig);
					}
					else
					{						
						Nodes[(hitMe ? i - 1 : i)].ReloadMapping(relayNodeClusterDefinition.RelayNodes[i],forwardingConfig);						
					}
				}
				lock (_chooseLock)
				{
					ChosenNode = null;
				}
			}
		}

		private bool ContainsNode(RelayNodeDefinition nodeDefinition)
		{
			if (Me != null && Me.ToString() == nodeDefinition.ToString())
			{
				return true;
			}
			
			foreach (Node node in Nodes)
			{
				if (node.ToString() == nodeDefinition.ToString())
				{
					return true;
				}
			}			

			return false;
		}

		#region Node Selection

		private Node GetChosenNode()
		{
			
			if (Nodes.Count == 0)
			{
				return null;
			}
			Node chosenNode = ChosenNode;
			//could potentially be nulled here, after selection made, 
			//in that case it wouldn't be re-chosen. that's ok, it will be next time
			if (chosenNode == null || !chosenNode.Activated || chosenNode.DangerZone)
			{
				lock (_chooseLock)
				{   
					if (ChosenNode == null || !ChosenNode.Activated || ChosenNode.DangerZone)
					{
						chosenNode = SelectANodeByZone(_nodesByZone, Randomizer, _localZone);
						
						ChosenNode = chosenNode; 
					}
				}
				//even if ChosenNode is nulled out before we can return,
				//the local variable will already have a node so we 
				//won't falsely return null
			}
			
			return chosenNode; 
		}

		private Node GetChosenZoneNode(ushort zone)
		{
			Node chosenNode = null;

			if (!ChosenZoneNodes.ContainsKey(zone) || ChosenZoneNodes[zone] == null || ChosenZoneNodes[zone].DangerZone)
			{
				lock (_chooseZoneNodeLock)
				{
					if (!ChosenZoneNodes.ContainsKey(zone) || ChosenZoneNodes[zone] == null || ChosenZoneNodes[zone].DangerZone)
					{
						if (zone < _nodesByZone.Length)
						{
							chosenNode = SelectSafeNode(new List<Node>(_nodesByZone[zone]), Randomizer);
							ChosenZoneNodes[zone] = chosenNode;
						}
						else
						{
							log.ErrorFormat("Got a request for nodes in Zone {0}, but the highest zone is {1}!", zone, _nodesByZone.Length - 1);
						}
					}
				}
			}
			else
			{
				chosenNode = ChosenZoneNodes[zone];
			}

			return chosenNode;
		}

		private static Node SelectSafeNode(IList<Node> candidates, Random randomizer)
		{
			Node candidate = null;
			if (candidates != null && candidates.Count > 0)
			{
				candidate = candidates[randomizer.Next(candidates.Count)];
				while (candidate != null && (candidate.DangerZone || !candidate.Activated))
				{
					candidates.Remove(candidate);
					candidate = null;
					if (candidates.Count > 0)
					{
						candidate = candidates[randomizer.Next(candidates.Count)];
					}
				}
			}
			return candidate;
		}
		
		/// <summary>
		/// Tries to find an activated non-Danger-zone node in nodeLayers, which is assumed to be indexed by zone id.
		/// It starts at startZone, and if it fails to find a node in that zone moves randomly through the remaining zones until one is found
		/// or all zones are exhausted.
		/// </summary>        
		private static Node SelectANodeByZone(List<Node>[] nodesByZone, Random randomizer, ushort startZone)
		{
			Node candidate = null;

			//first try to local zone; the vast majority of times we shouldn't need the 
			//overhead involved in the random zone selection process
			if (startZone < nodesByZone.Length)
			{
				candidate = SelectSafeNode(new List<Node>(nodesByZone[startZone]), randomizer);
			}
			
			if (candidate == null) //nothing found in local zone, randomly try others
			{
				//generate a list of potential layers
				List<int> zonesToTry = new List<int>(nodesByZone.Length);
				for (int i = 0; i < nodesByZone.Length; i++ )
				{
					if(i != startZone && nodesByZone[i] != null && nodesByZone[i].Count > 0)
					{
						zonesToTry.Add(i);
					}
				}
				while (zonesToTry.Count > 0 && candidate == null)
				{
					int layerToTryIndex = randomizer.Next(zonesToTry.Count);
					int layerToTry = zonesToTry[layerToTryIndex];
					candidate = SelectSafeNode(new List<Node>(nodesByZone[layerToTry]), randomizer);
					zonesToTry.RemoveAt(layerToTryIndex);
				}
				
			}
			
			return candidate;
		}


		private LinkedListStack<Node> SelectNodes(RelayMessage message)
		{
			LinkedListStack<Node> nodes;
			List<Node> zoneNodes = _nodesByZone[Me.Zone];
			if (zoneNodes != null)
			{
				// select all other nodes in the current zone if they exist
				nodes = new LinkedListStack<Node>(zoneNodes);
			}
			else
			{
				nodes = new LinkedListStack<Node>();
			}

			if (message.SourceZone == Me.Zone)
			{
				// Add 1 node from each foreign zone
				for (ushort zone = 0; zone < _nodesByZone.Length; zone++)
				{
					if (zone != Me.Zone)
					{
						Node ZoneNode = GetChosenZoneNode(zone);
						if (ZoneNode != null)
						{
							nodes.Push(ZoneNode);
						}
					}
				}
			}
	
			return nodes;
		}

		internal void ReselectNode()
		{
			lock (_chooseLock)
			{
				ChosenNode = null;
			}
		}

		private Random randomizer;
		private readonly object randomLock = new object();
		private Random Randomizer
		{
			get
			{
				if (randomizer == null)
				{
					lock (randomLock)
					{
						if (randomizer == null)
						{
							Randomizer = new Random((int)DateTime.Now.Ticks);
						}
					}
				}
				return randomizer;
			}
			set
			{
				randomizer = value;
			}
		}

        /// <summary>
        /// Gets a list of all nodes in the local zone.
        /// </summary>
        /// <returns>
        /// A List of nodes.  All of the nodes in the list are available to have requests made to them.  This list may be empty if there are no active nodes for the zone.
        /// </returns>
        internal IList<Node> SelectNodesInZoneForMessage()
        {
            var zoneNodes = _nodesByZone[_localZone] ?? new List<Node>(0);
            return zoneNodes;
        }

	    internal LinkedListStack<Node> GetNodesForMessage(RelayMessage message)
		{
			LinkedListStack<Node> nodes;
			Node node;
			
			if (message.IsTwoWayMessage)
			{
				//messages that always go to a selected node
				nodes = new LinkedListStack<Node>();
				node = GetChosenNode();
				if (node != null)
				{
					nodes.Push(node);  // this is where the single node is selected and returned for OUT messages
				}
			}
			else
			{
				//messages that are routed
				//  out of system: to a selected node
				//  in system: to every other node
				if (Me == null) //out of system
				{
					nodes = new LinkedListStack<Node>();
					node = GetChosenNode();
					if (node != null)
					{
						nodes.Push(node);
					}
				}
				else //in system
				{
					nodes = SelectNodes(message);
				}
			}
			return nodes;
		}

		internal bool ObjectInRange(int objectId)
		{
			return (objectId >= _minimumId && objectId <= _maximumId);
		}

		#endregion 

		internal void GetHtmlStatus(StringBuilder statusBuilder)
		{
			statusBuilder.Append("<table class=\"nodeClusterBox\">" + Environment.NewLine);
			const int maxColumns = 5;
			for (int i = 0; i < Nodes.Count; i++)
			{
				if ((i % maxColumns) == 0)
				{
					statusBuilder.Append("<tr>" + Environment.NewLine);
				}
				statusBuilder.Append("<td valign=top>" + Environment.NewLine);
				Nodes[i].GetHtmlStatus(statusBuilder);
				statusBuilder.Append("</td>" + Environment.NewLine);
				if ((((i + 1) % maxColumns) == 0) || (i == Nodes.Count - 1))
				{
					statusBuilder.Append("</tr>" + Environment.NewLine);
				}
			}
			statusBuilder.Append(@"</table>" + Environment.NewLine);
		}

		internal NodeClusterStatus GetNodeClusterStatus()
		{
			NodeClusterStatus nodeClusterStatus = new NodeClusterStatus();
			for (int i = 0; i < Nodes.Count; i++)
			{
				nodeClusterStatus.NodeStatuses.Add(Nodes[i].GetNodeStatus());
			}
			return nodeClusterStatus;
		}

		internal void ProcessQueues()
		{
			if (Me == null) //out of network, we just need queues to go SOMEWHERE
			{
				Node currentNode;
				for (int i = 0; i < Nodes.Count; i++)
				{
					currentNode = Nodes[i];
					if (currentNode.DangerZone)
					{
						Node node = GetChosenNode();
						if (node != null && !node.DangerZone)
						{
							SerializedMessageList errorsList = currentNode.DequeueErrors();
							if (errorsList != null)
							{
								if (log.IsInfoEnabled)
									log.InfoFormat("Using Node {0} to process {1} error queued messages from {2}",
										node, 
										errorsList.InMessageCount,
										currentNode.ToString());
								// the message being processed is coming from a queue.
								// In this case, we do not need to use setting to know
								// if the queue should be used.  Thus, no exception will be 
								// thrown, so we can send in "false" for "skipErrorQueueForSync
								// without having to check the settings.
								node.SendInMessages(errorsList.InMessages, false);								
							}
						}						
					}
					else
					{
						currentNode.ProcessQueue();
					}
				}
			}
			else //in network, queues need to stay with the nodes that generated them
			{
				for (int i = 0; i < Nodes.Count; i++)
				{
					Nodes[i].ProcessQueue();
				}
			}
		}

		internal void PopulateQueues(Dictionary<string, ErrorQueue> errorQueues, bool incrementCounter)
		{
			for (int i = 0; i < Nodes.Count; i++)
			{
				if (errorQueues.ContainsKey(Nodes[i].ToString()))
				{
					ErrorQueue errorQueue = errorQueues[Nodes[i].ToString()];
					if (errorQueue != null)
					{						
						if (errorQueue != Nodes[i].MessageErrorQueue) //when reloading configs, a repopulate will be called. Nodes might not be recreated, in which case the error queue hasn't changed.
						{
							if (log.IsInfoEnabled)
								log.InfoFormat("Repopulating {0} error queued messages for node {1}",
									errorQueue.InMessageQueueCount,
									Nodes[i]
									);
							if (Nodes[i].MessageErrorQueue == null)
							{
								Nodes[i].MessageErrorQueue = errorQueue;
								if (incrementCounter)
								{
									NodeManager.Instance.Counters.IncrementErrorQueueBy(Nodes[i].MessageErrorQueue.InMessageQueueCount);
								}
							}
							else
							{
								Nodes[i].MessageErrorQueue.Populate(errorQueue, incrementCounter);
							}
						}
					}
				}
			}
		}

		internal void AggregateCounterTicker()
		{
			//no need to calc hit ratio for "me"
			for (int i = 0; i < Nodes.Count; i++)
			{
				Nodes[i].AggregateCounterTicker();
			}
		}

		#region Dispatchers and Dispatcher Queues
		 
		internal void SetNewDispatchers(Dispatcher newInDispatcher, Dispatcher newOutDispatcher)
		{   
			DispatcherQueue inMessageQueue, outMessageQueue;
			if (Me != null)
			{
				GetMessageQueuesFor(Me.GetMessageQueueName(), _clusterDefinition, newInDispatcher, newOutDispatcher, out inMessageQueue, out outMessageQueue);   
				Me.SetNewDispatcherQueues(inMessageQueue, outMessageQueue);
			}
			for (int i = 0; i < Nodes.Count; i++)
			{
				GetMessageQueuesFor(Nodes[i].GetMessageQueueName(), _clusterDefinition, newInDispatcher, newOutDispatcher, out inMessageQueue, out outMessageQueue);
				Nodes[i].SetNewDispatcherQueues(inMessageQueue, outMessageQueue);
			}
		}

		internal void GetMessageQueuesFor(string nodeMessageQueueName, RelayNodeClusterDefinition cluster,
			Dispatcher inDispatcher, Dispatcher outDispatcher,
			out DispatcherQueue inMessageQueue, out DispatcherQueue outMessageQueue)
		{
			int maxQueueLength;
			string queueName;
			if (Me == null) //not in this cluster, going to use the cluster wide message queues
			{
				queueName = GetQueueNameFor(cluster);
				maxQueueLength = _nodeGroup.GetClusterQueueDepth();                                
			}
			else //going to use the message queues that are per-node
			{
				queueName = nodeMessageQueueName;
				maxQueueLength = _nodeGroup.GetClusterQueueDepth() / cluster.RelayNodes.Length;
			}
			NodeManager.GetMessageQueues(inDispatcher, outDispatcher, queueName, maxQueueLength,
					out inMessageQueue, out outMessageQueue);
		}

		#endregion 
		
	}
}
