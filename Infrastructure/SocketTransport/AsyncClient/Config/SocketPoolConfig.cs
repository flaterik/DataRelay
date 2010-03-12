using System;
using System.Xml.Serialization;
using MySpace.Common;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// 	<para>Encapsulates configuration settings for
	/// 	socket pools used by <see cref="AsyncSocketClient"/>.</para>
	/// </summary>
	public class SocketPoolConfig : PoolConfig
	{
		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="SocketPoolConfig"/> class.</para>
		/// </summary>
		public SocketPoolConfig()
		{
			FetchOrder = PoolFetchOrder.Fifo;
			PoolCapacity = 10;
			MaxLifespan = 3600;
			MaxUses = -1;
			ConnectTimeout = 1000;
			ReceiveTimeout = 2000;
			NetworkOrdered = false;
		}

		/// <summary>
		/// How many milliseconds to wait for the remote host to accept a new connection.
		/// </summary>
		[XmlElement("ConnectTimeout")]
		public int ConnectTimeout { get; set; }

		/// <summary>
		/// How many milliseconds to wait for a response to a sync messages.
		/// </summary>
		[XmlElement("ReceiveTimeout")]
		public int ReceiveTimeout { get; set; }

		/// <summary>
		/// 	<para>Gets or sets a value indicating whether envelope data should be processed in network order.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if envelope data should be processed in network order; otherwise, <see langword="false"/>.</para>
		/// </value>
		[XmlElement("NetworkOrdered")]
		public bool NetworkOrdered { get; set; }
	}
}
