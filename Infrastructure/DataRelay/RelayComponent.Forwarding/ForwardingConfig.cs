using System.Xml.Serialization;
using MySpace.DataRelay.Configuration;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// XML Configuration for Forwarder component
	/// </summary>
	[XmlRoot("ForwardingConfig", Namespace = "http://myspace.com/ForwardingConfig.xsd")]
	public class ForwardingConfig
	{
		/// <summary>
		/// The configuration for forwarding error queues.
		/// </summary>
		[XmlElement("QueueConfig")]
		public Common.Schemas.QueueConfig QueueConfig = new Common.Schemas.QueueConfig();
		/// <summary>
		/// The maximum length of a message list that will be sent in a single network message.
		/// </summary>
		[XmlElement("MessageChunkLength")]
		public int MessageChunkLength;
		/// <summary>
		/// The bucket size for in messages that will attempt to be filled before forwarding the list.
		/// </summary>        
		[XmlElement("MessageBurstLength")]		
		public int MessageBurstLength = 10;
		/// <summary>
		/// The number of milliseconds that are waited to fill the MessageBurst bucket.
		/// </summary>
		[XmlElement("MessageBurstTimeout")]
		public int MessageBurstTimeout = 10;
		/// <summary>
		/// The number of threads used to process In Messages.
		/// </summary>
		[XmlElement("NumberOfThreads")]
		public int NumberOfThreads = 1;
		/// <summary>
		/// The number of threads used to process bulk Out Messages, if EnableAyncBulkGets is enabled. If it is not, calling threads are used.
		/// </summary>
		[XmlElement("NumberOfOutMessageThreads")]
		public int NumberOfOutMessageThreads = 1;
		/// <summary>
		/// The maximum number of messages that can be queued. In and Out messages both use this, but are queued seperately.
		/// </summary>
		[XmlElement("MaximumTaskQueueDepth")]
		public int MaximumTaskQueueDepth = 200000;
		/// <summary>
		/// Whether to use the OutMessageThreads to make bulk out message calls simultaneously against multiple client nodes.
		/// </summary>
		[XmlElement("EnableAsyncBulkGets")]
		public bool EnableAsyncBulkGets;
		/// <summary>
		/// Whether or not in message lists are reposted into the in message buckets. If false, lists are processed as passed and bypass the bucket/burst system.
		/// </summary>
		[XmlElement("RepostMessageLists")]
		public bool RepostMessageLists;
		/// <summary>
		/// If true, determine the number of hops away each node is.
		/// If false, use zone definitions.
		/// Only the value at startup is meaningful; changing it after initialization has no effect.
		/// </summary>
		[XmlElement("MapNetwork")]
		public bool MapNetwork; 
		/// <summary>
		/// If true, the forwarder will write the message.tostring and destination nodes of all handled RelayMessages to the default Trace.
		/// </summary>
		[XmlElement("WriteMessageTrace")]
		public bool WriteMessageTrace;
		/// <summary>
		/// If this and WriteMessageTrace are both true, then the external method that caused the message to be sent will be displayed as well.
		/// If WriteMessageTrace is false, this has no impact.
		/// </summary>
		[XmlElement("WriteCallingMethod")]        
		public bool WriteCallingMethod;
		/// <summary>
		/// This controls filtering and file writing options for the message tracing above.
		/// </summary>
		[XmlElement("TraceSettings")]
		public TraceSettings TraceSettings;
	}

	
}
