using System;
using System.Collections.Generic;
using MySpace.DataRelay.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Ccr.Core;
using MySpace.DataRelay.Common.Schemas;
using System.Diagnostics;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class NodeManager
	{	
		internal static NodeManager Instance
		{
			get
			{
				return GetInstance();
			}
		}

		private static NodeManager _instance;
		private static readonly object _instanceLock = new object();
		private static readonly LogWrapper _log = new LogWrapper();

		private static NodeManager GetInstance()
		{
			//use local var because we set _instance to null on shutdown and could
			//cause concurrency problem
			NodeManager instance = _instance;
			if (instance == null)
			{
				lock (_instanceLock)
				{
					if (_instance == null)
					{
						_instance = new NodeManager();
					}
					instance = _instance;
				}
			}
			return instance;
		}

		internal ForwardingConfig ForwardingConfig;
		
		internal ForwardingCounters Counters;
		internal Dispatcher InMessageDispatcher;
		internal Dispatcher OutMessageDispatcher;

		private ZoneDefinitionCollection _zoneDefinitions;
		private Timer _queuedMessageCounterTimer;
		private Timer _aggregateCounterTickTimer;
		private RelayNodeDefinition _myNodeDefinition;
		private static bool _initialized;
		
		internal static void Initialize(RelayNodeConfig config, ForwardingConfig forwardingConfig, Dictionary<string, Dictionary<string, MessageQueue>> errorQueues)
		{
			lock (_instanceLock)
			{
				if (!_initialized)
				{
					Instance.InitializeInstance(config, forwardingConfig, errorQueues);
					_initialized = true;
				}                
			}
		}

		internal ushort GetZoneForAddress(IPAddress address)
		{
			if (_zoneDefinitions == null)
			{
				return 0;
			}
			return _zoneDefinitions.GetZoneForAddress(address);
		}

		private void InitializeInstance(RelayNodeConfig config, ForwardingConfig forwardingConfig, Dictionary<string, Dictionary<string, MessageQueue>> errorQueues)
		{
			Counters = new ForwardingCounters();

			if (config != null)
			{
				//without this we'll get a null ref exception
				if (forwardingConfig == null) throw new ArgumentNullException("forwardingConfig", "Requires a forwardingConfig to initialize");
				  
				Config = config;
				ForwardingConfig = forwardingConfig;
				_zoneDefinitions = config.RelayNodeMapping.ZoneDefinitions;		
				Counters.Initialize(Config.InstanceName);

				if (InMessageDispatcher != null)
				{
					if (_log.IsInfoEnabled)
						_log.Info("Relay Forwarder Node Manager Initialized with non-null Dispatcher.");                    
				}
				else
				{
					InMessageDispatcher = new Dispatcher(forwardingConfig.NumberOfThreads, ThreadPriority.Normal, true, "Relay Forwarder");
				}

				if (OutMessageDispatcher == null)
				{
					OutMessageDispatcher = new Dispatcher(forwardingConfig.NumberOfOutMessageThreads, ThreadPriority.Normal, true, "Relay Forwader Out Messages");
				}
				
				NodeGroup.MaximumQueuedItems = forwardingConfig.MaximumTaskQueueDepth;

				BuildNodeGroups(config, errorQueues);

				if (config.MyAddresses != null && config.MyAddresses.Count > 0)
				{	
					for (int i = 0; i < config.MyAddresses.Count; i++)
					{
						AddressFamily family = config.MyAddresses[i].AddressFamily;
						if (family == AddressFamily.InterNetwork &&
							!IPAddress.Loopback.Equals(config.MyAddresses[i])
							)
						{
							MyIpAddress = config.MyAddresses[i];
							break;
						}
					}
				}

				_queuedMessageCounterTimer = new Timer(CountQueuedMessages, null, 5000, 5000);

				_aggregateCounterTickTimer = new Timer(AggregateCounterTicker, null, 500, 500);
				_myNodeDefinition = GetMyNodeDefinition();
			}
		}

		internal void ReloadConfig(RelayNodeConfig config, ForwardingConfig newForwardingConfig)
		{

			if (config.RelayNodeMapping == null)
			{
				if (_log.IsErrorEnabled)
					_log.Error("Got new config with no defined groups.");
			}
			else
			{
				if (config.RelayNodeMapping.Validate())
				{
					_zoneDefinitions = config.RelayNodeMapping.ZoneDefinitions;	//make sure this is set before reloading the mapping so any changes propogate	
					Dictionary<string, Dictionary<string, MessageQueue>> errorQueues = GetErrorQueues();
					NodeGroups.ReloadMapping(config, newForwardingConfig);
					//note that if a node changes groups, the error queue won't make it!
					NodeGroups.PopulateQueues(errorQueues, false);
				}
				else
				{
					if (_log.IsErrorEnabled)
						_log.Error("Forwarder not reloading invalid config.");
				}
			}

			Config = config;

			_myNodeDefinition = GetMyNodeDefinition();
			bool doNewInDispatcher, doNewOutDispatcher;
			if (newForwardingConfig.NumberOfThreads != ForwardingConfig.NumberOfThreads)
			{
				doNewInDispatcher = true;
			}
			else
			{
				doNewInDispatcher = false;
			}
			if (newForwardingConfig.NumberOfOutMessageThreads != ForwardingConfig.NumberOfOutMessageThreads)
			{
				doNewOutDispatcher = true;
			}
			else
			{
				doNewOutDispatcher = false;
			}


			if (doNewInDispatcher || doNewOutDispatcher)
			{
				Dispatcher oldInDispatcher = null, newInDispatcher, oldOutDispatcher = null, newOutDispatcher;

				if (doNewInDispatcher)
				{
					if (_log.IsInfoEnabled)
						_log.InfoFormat("Changing number of messaging threads from {0} to {1}", ForwardingConfig.NumberOfThreads, newForwardingConfig.NumberOfThreads);
					oldInDispatcher = InMessageDispatcher;
					newInDispatcher = new Dispatcher(newForwardingConfig.NumberOfThreads, ThreadPriority.Normal, true, "Relay Forwarder");

				}
				else
				{
					newInDispatcher = InMessageDispatcher;
				}
				if (doNewOutDispatcher)
				{
					if (_log.IsInfoEnabled)
						_log.InfoFormat("Changing number of out message threads from {0} to {1}", ForwardingConfig.NumberOfOutMessageThreads, newForwardingConfig.NumberOfOutMessageThreads);
					oldOutDispatcher = OutMessageDispatcher;
					newOutDispatcher = new Dispatcher(newForwardingConfig.NumberOfOutMessageThreads, ThreadPriority.Normal, true, "Relay Forwarder");
				}
				else
				{
					newOutDispatcher = OutMessageDispatcher;
				}

				InMessageDispatcher = newInDispatcher;
				OutMessageDispatcher = newOutDispatcher;

				NodeGroups.SetNewDispatchers(newInDispatcher, newOutDispatcher);

				ForwardingConfig = newForwardingConfig;

				if (doNewInDispatcher)
				{
					if (_log.IsInfoEnabled)
						_log.Info("Disposing old in message Dispatcher");
					oldInDispatcher.Dispose();
				}
				if (doNewOutDispatcher)
				{
					if (_log.IsInfoEnabled)
						_log.Info("Disposing old out message Dispatcher");
					oldOutDispatcher.Dispose();
				}
			}
			else
			{
				ForwardingConfig = newForwardingConfig;
			}
		}

		private void AggregateCounterTicker(object state)
		{
			for (int i = 0; i < NodeGroups.Count; i++)
			{
				NodeGroups[i].AggregateCounterTick();
			}
		}

		private void CountQueuedMessages(object state)
		{
			if (Counters != null)
			{
				Counters.SetNumberOfQueuedMessages(InMessageDispatcher.PendingTaskCount);
			}
		}

		private void BuildNodeGroups(RelayNodeConfig relayNodeConfig, Dictionary<string, Dictionary<string, MessageQueue>> errorQueues)
		{
			RelayNodeMapping relayNodeMapping = relayNodeConfig.RelayNodeMapping;
			if (relayNodeMapping != null)
			{
				if (relayNodeMapping.Validate())
				{
                    NodeGroupCollection nodeGroups = new NodeGroupCollection(relayNodeMapping.RelayNodeGroups, relayNodeConfig, ForwardingConfig);

					NodeGroups = nodeGroups;
					RelayNodeGroupDefinition myGroupDefinition = Config.GetMyGroup();
					if (myGroupDefinition != null && NodeGroups.Contains(myGroupDefinition.Name))
					{
						MyNodeGroup = NodeGroups[myGroupDefinition.Name];
					}
					else
					{
						MyNodeGroup = null;
					}

					if (errorQueues != null)
					{
						nodeGroups.PopulateQueues(errorQueues, true);
					}
				}
				else
				{
					if (_log.IsErrorEnabled)
						_log.Error("Forwarder not loading invalid config.");
					NodeGroups = new NodeGroupCollection();
				}
			}
			else
			{
				NodeGroups = new NodeGroupCollection();
			}
		}

		internal RelayNodeDefinition GetMyNodeDefinition()
		{	
			return Config == null ? null : Config.GetMyNode();
		}

		internal RelayNodeClusterDefinition GetMyNodeClusterDefinition()
		{
			return Config.GetMyCluster();
		}

		internal RelayNodeConfig Config;

		internal NodeGroupCollection NodeGroups;
		internal NodeGroup MyNodeGroup;
		internal IPAddress MyIpAddress;
		internal NodeGroup GetNodeGroup(short typeId)
		{
			string groupName = Config.TypeSettings.TypeSettingCollection.GetGroupNameForId(typeId);

			if (MyNodeGroup != null && string.Compare(groupName, MyNodeGroup.GroupName, true) == 0)
			{
				//If MyNodeGroup isn't null, then this node is "in system". In system requests
				//are assumed to be prerouted and already in the correct cluster, so they must be inside 
				//the group the cluster is in.
				return MyNodeGroup;
			}
			

			NodeGroup group = null;
			if (groupName != null)
			{
				if (NodeGroups.Contains(groupName))
				{
					group = NodeGroups[groupName];
				}
			}
			return group;
		}

		/// <summary>
		/// Gets the maximum number of times that the message may be retried.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <returns>The maximum number of times that the message may be retried.</returns>
		internal int GetRetryCountForMessage(RelayMessage message)
		{
			if (!message.IsTwoWayMessage) return 0;

			var group = GetNodeGroup(message.TypeId);
			if (group == null) return 0;
			if (group.GroupDefinition == null) return 1;
			if (group.GroupDefinition.RetryCount < 0) return 0;
			return group.GroupDefinition.RetryCount;
		}

		internal SimpleLinkedList<Node> GetNodesForMessage(RelayMessage message)
		{
			SimpleLinkedList<Node> nodes = null;
			
			if (message == null || message.RelayTTL < 1 || NodeGroups == null)
			{
				return new SimpleLinkedList<Node>();
			}

			const bool useLegacySerialization = true;

			//commands that, from out of system, route to all groups
			if (message.IsGroupBroadcastMessage)
			{
				message.PrepareMessageToBeSent(useLegacySerialization);
				if (MyNodeGroup == null)//out of system: all groups	
				{
					nodes = new SimpleLinkedList<Node>();
					for (int groupIndex = 0; groupIndex < this.NodeGroups.Count; groupIndex++)
					{
						nodes.Push(NodeGroups[groupIndex].GetNodesForMessage(message));							
					}
				}
				else//In system: my group
				{
					nodes = MyNodeGroup.MyCluster.GetNodesForMessage(message);						
				}
			}
			else
			{
				//Commands that always route to a single group
				NodeGroup group = GetNodeGroup(message.TypeId);
				if (group != null)
				{
					message.PrepareMessageToBeSent(group.GroupDefinition.LegacySerialization);
					nodes = group.GetNodesForMessage(message);
				}
				else
				{
					message.PrepareMessageToBeSent(useLegacySerialization);
					if (_log.IsErrorEnabled)
						_log.ErrorFormat("No group found for {0}", message);
					nodes = new SimpleLinkedList<Node>();
				}
			}
			
			if (nodes == null)
			{
				nodes = new SimpleLinkedList<Node>();
			}

			// If no nodes are returned, we predict that the caller
			// will drop the message.  Therefore we call the notification delegate.
			// This is admittedly a kludgy solution, but the only one 
			// available without changing this method's signature.
			// A better solution should be adopted. [tchow 01/29/2008]
			if (nodes.Count == 0)
			{
				Forwarder.RaiseMessageDropped(message);
			}

			return nodes;
		}

		/// <summary>
		/// Splits messages into various lists of in and out message destined for different nodes.
		/// </summary>
		/// <param name="messages"></param>
		/// <returns></returns>
		internal NodeWithMessagesCollection DistributeMessages(IList<RelayMessage> messages)
		{
			NodeWithMessagesCollection distribution = new NodeWithMessagesCollection();
			RelayMessage message;
			Node node;

			for(int i = 0 ; i < messages.Count ; i++)
			{
				if (messages[i] != null)
				{
					message = messages[i];
					
					RelayMessage interZoneMessage = null;

					SimpleLinkedList<Node> nodesForMessage = GetNodesForMessage(message);
					SimpleLinkedList<Node> nodesForInterZoneMessage = null;
					
					if (nodesForMessage.Count > 0)
					{
						message.AddressHistory.Add(MyIpAddress);
					}
					message.RelayTTL--;

					#region Identify InterZone Messages
					if(message.IsTwoWayMessage == false)
					{
						message.ResultOutcome = RelayOutcome.Queued; //will be queued, if sync will not get overwritten

						// Identify nodes in foreign zones
						int nodeCount = nodesForMessage.Count;
						for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
						{
							nodesForMessage.Pop(out node);
							if (_myNodeDefinition != null && _myNodeDefinition.Zone != node.NodeDefinition.Zone)
							{
								// Message needs to cross Zone bounderies
								if (interZoneMessage == null)
								{
									interZoneMessage = RelayMessage.CreateInterZoneMessageFrom(message);
									nodesForInterZoneMessage = new SimpleLinkedList<Node>();
								}
								nodesForInterZoneMessage.Push(node);
							}
							else
							{
								nodesForMessage.Push(node);
							}
						}
					}
					#endregion

					if (nodesForMessage.Count > 0)
					{
						DebugWriter.WriteDebugInfo(message, nodesForMessage);
						distribution.Add(message, nodesForMessage);
					}

					if (nodesForInterZoneMessage != null && nodesForInterZoneMessage.Count > 0)
					{
						DebugWriter.WriteDebugInfo(interZoneMessage, nodesForInterZoneMessage);
						distribution.Add(interZoneMessage, nodesForInterZoneMessage);
					}
				}				
			}

			return distribution;
		}

		internal Dictionary<string, Dictionary<string, MessageQueue>> GetErrorQueues()
		{

			Dictionary<string, Dictionary<string, MessageQueue>> queues = new Dictionary<string, Dictionary<string, MessageQueue>>(NodeGroups.Count);
			foreach (NodeGroup group in NodeGroups)
			{
				Dictionary<string, MessageQueue> groupQueues = new Dictionary<string, MessageQueue>();
				foreach (NodeCluster cluster in group.Clusters)
				{
					foreach (Node node in cluster.Nodes)
					{	
						if (node.MessageErrorQueue != null && node.MessageErrorQueue.InMessageQueueCount > 0)
						{
							groupQueues.Add(node.ToString(), node.MessageErrorQueue);
						}
					}
				}
				queues.Add(group.GroupName, groupQueues);
			}
			if (queues.Count > 0)
			{
				return queues;
			}
			
			return null;
			
		}

		internal void Shutdown()
		{
			if (Counters != null)
			{
				Counters.ResetCounters();
				Counters.Shutdown();
			}
			if (InMessageDispatcher != null)
			{
				InMessageDispatcher.Dispose();
			}
			if (_queuedMessageCounterTimer != null)
			{
				_queuedMessageCounterTimer.Change(Timeout.Infinite, Timeout.Infinite);
				_queuedMessageCounterTimer.Dispose();
			}
			if (_aggregateCounterTickTimer != null)
			{
				_aggregateCounterTickTimer.Change(Timeout.Infinite, Timeout.Infinite);
				_aggregateCounterTickTimer.Dispose();
			}

			if (NodeGroups != null)
			{
				for (int i = 0; i < NodeGroups.Count; i++)
				{
					NodeGroups[i].Shutdown();
				}
			}
				
			lock (_instanceLock)
			{
				_initialized = false;
				//Release instance to free memory and to get initialized fresh.
				_instance = null;
			}
		}

		private static bool TryGetExistingQueue(Dispatcher dispatcher, string queueName, out DispatcherQueue existingQueue)
		{
			List<DispatcherQueue> queues = dispatcher.DispatcherQueues;
			foreach (DispatcherQueue queue in queues)
			{
				if (queue.Name == queueName)
				{
					existingQueue = queue;
					return true;
				}
			}
			existingQueue = null;
			return false;
		}
		
		internal static void GetMessageQueues(
			Dispatcher inMessageDispatcher, Dispatcher outMessageDispatcher, string queueName, int queueDepth, 
			out DispatcherQueue inMessageQueue, out DispatcherQueue outMessageQueue)
		{
			//do the in message queue
			if (!TryGetExistingQueue(inMessageDispatcher, queueName, out inMessageQueue))
			{ //queues can't be removed from a dispatcher once attached. To handle dynamic remapping, always look for an existing one first.
				Debug.WriteLine(String.Format("Creating In Message DispatcherQueue {0} with maximum depth {1}.", queueName, queueDepth));
				try
				{
					inMessageQueue = new DispatcherQueue(queueName, inMessageDispatcher,
						TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, queueDepth);
				}
				catch (Exception ex)
				{
					if(_log.IsErrorEnabled)
					_log.ErrorFormat("Exception creating In Message DispatcherQueue {0} with maximum depth {1}: {2}", queueName, queueDepth, ex);
					throw;
				}
			}
			else
			{
				Debug.WriteLine(String.Format("Setting existing DispatcherQueue {0} to maximum depth {1}.", queueName, queueDepth));                
				inMessageQueue.MaximumQueueDepth = queueDepth;
			}
			//aaaaand the out message queue
			if (!TryGetExistingQueue(outMessageDispatcher, queueName, out outMessageQueue))
			{ //queues can't be removed from a dispatcher once attached. To handle dynamic remapping, always look for an existing one first.
				Debug.WriteLine(String.Format("Creating Out Message DispatcherQueue {0} with maximum depth {1}.", queueName, queueDepth));
				try
				{
					outMessageQueue = new DispatcherQueue(queueName, outMessageDispatcher,
						TaskExecutionPolicy.ConstrainQueueDepthThrottleExecution, queueDepth);
				}
				catch (Exception ex)
				{
					if(_log.IsErrorEnabled)
						_log.ErrorFormat("Exception creating Out Message DispatcherQueue {0} with maximum depth {1}: {2}", queueName, queueDepth, ex);
					throw;
				}
			}
			else
			{
				Debug.WriteLine(String.Format("Setting existing Out Message DispatcherQueue {0} to maximum depth {1}.", queueName, queueDepth));                
				outMessageQueue.MaximumQueueDepth = queueDepth;
			}
		}
	}
}
