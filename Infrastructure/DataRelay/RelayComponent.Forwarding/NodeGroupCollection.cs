using System.Collections.Generic;
using System.Collections.ObjectModel;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.Common.Schemas;
using Microsoft.Ccr.Core;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	internal class NodeGroupCollection : KeyedCollection<string, NodeGroup>
	{


		private static readonly LogWrapper _log = new LogWrapper();

		internal NodeGroupCollection()
		{ }

		internal NodeGroupCollection(RelayNodeGroupDefinitionCollection groupDefinitions, RelayNodeConfig nodeConfig, ForwardingConfig forwardingConfig) : base()
		{
			foreach (RelayNodeGroupDefinition groupDefinition in groupDefinitions)
			{
				Add(new NodeGroup(groupDefinition, nodeConfig, forwardingConfig));
			}
		}

		internal void ReloadMapping(RelayNodeConfig newConfig, ForwardingConfig forwardingConfig)
		{
			RelayNodeMapping relayNodeMapping = newConfig.RelayNodeMapping;
			RelayNodeGroupDefinitionCollection groupDefinitions = relayNodeMapping.RelayNodeGroups;			
			foreach (RelayNodeGroupDefinition groupDefinition in groupDefinitions)
			{
				if (Contains(groupDefinition.Name))
				{
					this[groupDefinition.Name].ReloadMapping(groupDefinition, newConfig, forwardingConfig);
				}
				else
				{
					if (_log.IsInfoEnabled)
						_log.InfoFormat("Adding new node group {0}", groupDefinition.Name);
					Add(new NodeGroup(groupDefinition,newConfig, forwardingConfig));
				}
			}

			bool removedOne;
			//make sure if any groups have been removed they are removed!
			do
			{
				removedOne = false;
				foreach (NodeGroup group in this)
				{
					if (!groupDefinitions.Contains(group.GroupName))
					{
						if (_log.IsInfoEnabled)
							_log.InfoFormat("Removing node group {0}", group.GroupName);
						Remove(group.GroupName);
						removedOne = true;
						break; //collection modified can't continue with foreach!
					}
				}
			} while (removedOne);


		}

		protected override string GetKeyForItem(NodeGroup item)
		{
			return item.GroupName;
		}

		internal void PopulateQueues(Dictionary<string, Dictionary<string, MessageQueue>> errorQueues, bool incrementCounter)
		{
			if (errorQueues != null)
			{
				foreach (string groupName in errorQueues.Keys)
				{	
					if (Contains(groupName) && this[groupName] != null)
					{
						this[groupName].PopulateQueues(errorQueues[groupName], incrementCounter);
					}
				}
			}
		}

		internal void SetNewDispatchers(Dispatcher newInDispatcher, Dispatcher newOutDispatcher)
		{
			foreach (NodeGroup group in this)
			{
				group.SetNewDispatchers(newInDispatcher, newOutDispatcher);
			}

		}

	}
}
