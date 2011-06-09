using System;
using System.Collections.Generic;
using System.Net;
using System.Xml.Serialization;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// Class containing information for status of a data relay server.
	/// </summary>
	[XmlRoot("NodeStatus")]
    public class NodeStatus
	{
		/// <summary>
		/// Status of the data relay server as defined by <see cref="ServerStatus"/>.
		/// </summary>
		[XmlElement("Status")]
		public string Status { set; get; }

		/// <summary>
		/// Host for the node.
		/// </summary>
		[XmlElement("Host")]
		public string Host{ set; get;}

		/// <summary>
		/// Port used by the node. 
		/// </summary>
		[XmlElement("Port")]
		public int Port { set; get; }

		/// <summary>
		/// Indicates data center location.
		/// </summary>
		[XmlElement("Zone")]
		public ushort Zone { set; get; }

		/// <summary>
		/// Number of network hops the relay server is from the web server.
		/// </summary>
		[XmlElement("Hops")]
		public int Hops { set; get; }
		
		/// <summary>
		/// The number of open socket connections to that server. 
		/// </summary>
		[XmlElement("OpenConnections")]
		public int OpenConnections { set; get; }

		/// <summary>
		/// The number of active socket connections to that server. 
		/// It will almost always show zero because the connections 
		/// are in use for such short periods of time. 
		/// </summary>
		[XmlElement("ActiveConnections")]
		public int ActiveConnections { set; get; }

		/// <summary>
		/// <see langword="true"/> if data for <see cref="MessageCounts"/> will be collected; 
		/// otherwise <see langword="false"/>.
		/// </summary>
		[XmlElement("GatheringStats")]
		public bool GatheringStats { set; get; }

		/// <summary>
		/// Information for servers that were down.
		/// </summary>
		[XmlElement("ServerDownErrorInfo")]
		public ServerDownErrorInfo ServerDownErrorInfo{ set; get; }
		
		/// <summary>
		/// Information for servers that were unreachable.
		/// </summary>
		[XmlElement("ServerUnreachableErrorInfo")]
		public ServerUnreachableErrorInfo ServerUnreachableErrorInfo { set; get; }

		/// <summary>
		/// Number of queued messages.
		/// </summary>
        [XmlElement("InMessageQueueCount")]
		public int InMessageQueueCount { set; get; }

		/// <summary>
		/// The <see cref="MessageCountInfo"/> items for the node; Never <see langword="null"/>
		/// </summary>
		public List<MessageCountInfo> MessageCounts
		{
			get
			{
				return _messageCounts;
			}
		}
		[XmlElement("MessageCounts")]
		readonly private List<MessageCountInfo> _messageCounts = new List<MessageCountInfo>();

		/// <summary>
		/// Bulk In refers to saves, deletes, and other updates messages sent as lists 
		/// against the relay server (the message is putting data “in” the server. 
		/// </summary>
		[XmlElement("BulkInMessageInfo")]
		public BulkMessageInfo BulkInMessageInfo { set; get; }

		/// <summary>
		/// Bulk Out refers to gets and queries sent as lists against the 
		/// relay server (the message is getting data “out” of the server. 
		/// </summary>
		[XmlElement("BulkOutMessageInfo")]
		public BulkMessageInfo BulkOutMessageInfo { set; get; }
	}
}
