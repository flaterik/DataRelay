using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Statistical information collected by the <see cref="Forwarder"/> that is specific 
	/// to a <see cref="NodeCluster"/>.
	/// </summary>
	[XmlRoot("NodeClusterStatus")]
    public class NodeClusterStatus
	{
		/// <summary>
		/// The <see cref="NodeStatus"/> items in for a cluster; Never <see langword="null"/>
		/// </summary>
		public List<NodeStatus> NodeStatuses
		{
			get
			{
				return _nodeStatuses;
			}
		}
		[XmlElement("NodeStatuses")]
		readonly private List<NodeStatus> _nodeStatuses = new List<NodeStatus>(); //All nodes in the cluster EXCEPT "Me"		
	}
}
