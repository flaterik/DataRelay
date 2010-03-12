using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using MySpace.DataRelay.Common.Schemas;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// A class that stores the statistical information collected by the <see cref="Forwarder"/>
	/// specific to a <see cref="NodeGroup"/>.
	/// </summary>
	[XmlRoot("NodeGroupStatus")]
	public class NodeGroupStatus
	{
		/// <summary>
		/// Name of the <see cref="NodeGroup"/> that statistical information is 
		/// collected for.
		/// </summary>
		[XmlElement("GroupName")]
		public String GroupName { set; get; }

		/// <summary>
		/// <see cref="TypeSetting"/> information specific to the <see cref="NodeGroup"/>; 
		/// Never <see langword="null"/>.
		/// </summary>
		public List<TypeSettingStatus> TypeSettingStatuses
		{
			get
			{
				return _typeSettingStatuses;
			}
		}

		[XmlElement("TypeSettingStatus")]
		readonly private List<TypeSettingStatus> _typeSettingStatuses = new List<TypeSettingStatus>();
		
		/// <summary>
		/// Statistical information collected by the <see cref="Forwarder"/> that is specific 
		/// to each <see cref="NodeCluster"/> in the <see cref="NodeGroup"/>; 
		/// Never <see langword="null"/>.
		/// </summary>
		public List<NodeClusterStatus> NodeClusterStatuses
		{
			get
			{
				return _nodeClusterStatuses;
			}
		}
		[XmlElement("NodeClusterStatuses")]
		readonly private List<NodeClusterStatus> _nodeClusterStatuses = new List<NodeClusterStatus>(); 
	}
}
