using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Xml.Serialization;

namespace MySpace.SocketTransport
{
	[XmlRoot("SocketServerConfig", Namespace = "http://myspace.com/SocketServerConfig.xsd")]
	public class SocketServerConfig
	{
		[XmlElement("ConnectionCheckIntervalSeconds")]
		public int ConnectionCheckIntervalSeconds = 120;
		[XmlElement("ReceiveTimeout")]
		public int ReceiveTimeout = 1000;
		[XmlElement("ReceiveBufferSize")]
		public int ReceiveBufferSize = 8192;
		[XmlElement("SendTimeout")]
		public int SendTimeout = 1000;
		[XmlElement("SendBufferSize")]
		public int SendBufferSize = 8192;
		[XmlElement("InitialMessageSize")]
		public int InitialMessageSize = 8192;
		[XmlElement("MaximumMessageSize")]
		public int MaximumMessageSize = 20480;
		[XmlElement("DiscardTooBigMessages")]
		public bool DiscardTooBigMessages = false;
		[XmlElement("OnewayQueueDepth")]
		public int OnewayQueueDepth = 1000;
		[XmlElement("SyncQueueDepth")]
		public int SyncQueueDepth = 1000;
		[XmlElement("OnewayThreads")]
		public int OnewayThreads = 0;
		[XmlElement("SyncThreads")]
		public int SyncThreads = 0;
		[XmlElement("BufferPoolReuses")]
		public int BufferPoolReuses = 0;
		[XmlElement("ConnectionStateReuses")]
		public int ConnectionStateReuses = 0;
		[XmlElement("UseNetworkOrder")]
		public bool UseNetworkOrder = false;
		/// <summary>
		/// A value when set to a number greater than zero will override the default
		/// maximum number of <see cref="ThreadPool"/> threads used for task queuing.
		/// </summary>
		[XmlElement("MaximumWorkerThreads")]
		public int MaximumWorkerThreads;
		/// <summary>
		/// A value when set to a number greater than zero will override the default
		/// maximum number of <see cref="ThreadPool"/> threads used for IO completion.
		/// </summary>
		[XmlElement("MaximumCompletionPortThreads")]
		public int MaximumCompletionPortThreads = 100;
		[XmlElement("MaximumOpenSockets")]
		public int MaximumOpenSockets = 0;
		[XmlElement("SendServerCapabilities")] //whether or not to treat Int16.MinValue as a special command to request sever capabilities
		public bool SendServerCapabilities = true;
	}
}
