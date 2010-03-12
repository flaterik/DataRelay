using System;
using System.Xml.Serialization;

namespace MySpace.SocketTransport
{
	/// <summary>
	/// Defines settings for socket client.
	/// </summary>
	[XmlRoot("SocketClient", Namespace = "http://myspace.com/SocketClientConfig.xsd")]
	public class SocketClientConfig
	{
		/// <summary>
		/// The default settings used for communication if none are supplied by the user.
		/// </summary>
		[XmlElement("DefaultSocketSettings")]
		public SocketSettings DefaultSocketSettings;

		/// <summary>
		/// By default, every socket pool uses its own pool of reuseable buffers. If this is set, they will all share one pool.
		/// </summary>
		[XmlElement("UseSharedBufferPool")]
		public bool UseSharedBufferPool;

		/// <summary>
		/// The minimum number of items for the shared buffer pool to contain at any time. 
		/// </summary>
		[XmlElement("SharedPoolMinimumItems")]
		public short SharedPoolMinimumItems;
	}

	/// <summary>
	/// Provides settings for transport sockets
	/// </summary>
	public class SocketSettings : IEquatable<SocketSettings>
	{
		/// <summary>
		/// The pooling mechanism used for open sockets. Linked is generally the best choice.
		/// </summary>
		[XmlElement("SocketPoolType")]
		public SocketPoolType PoolType = SocketPoolType.Linked;
		/// <summary>
		/// The maximum number of sockets to open for a given settings / host combination.
		/// </summary>
		[XmlElement("SocketPoolSize")]
		public int PoolSize = 10;
		/// <summary>
		/// How many milliseconds to wait for the remote host to accept a new connection.
		/// </summary>
		[XmlElement("ConnectTimeout")]
		public int ConnectTimeout = 1000;
		/// <summary>
		/// The initial size in bytes of the internal message buffers.
		/// </summary>
		[XmlElement("InitialMessageSize")]
		public int InitialMessageSize = 1024;
		/// <summary>
		/// The maximum expected size in bytes of sync message replies from a server.
		/// </summary>
		[XmlElement("MaximumMessageSize")]
		public int MaximumReplyMessageSize = 10240;
		/// <summary>
		/// The receive buffer size used by the raw socket.
		/// </summary>
		[XmlElement("ReceiveBufferSize")]
		public int ReceiveBufferSize = 8192;
		/// <summary>
		/// How many milliseconds to wait for a response to a sync messages.
		/// </summary>
		[XmlElement("ReceiveTimeout")]
		public int ReceiveTimeout = 1000;
		/// <summary>
		/// The send buffer size used by the raw socket.
		/// </summary>
		[XmlElement("SendBufferSize")]
		public int SendBufferSize = 8192;
		/// <summary>
		/// How many milliseconds to wait for a send to complete to a server.
		/// </summary>
		[XmlElement("SendTimeout")]
		public int SendTimeout = 1000;
		/// <summary>
		/// How long to keep open sockets alive.
		/// </summary>
		[XmlElement("SocketLifetimeMinutes")]
		public int SocketLifetimeMinutes = 60;
		/// <summary>
		/// Whether or not to reorder bytes to network order. Don't say yes unless you know why.
		/// </summary>
		[XmlElement("UseNetworkOrder")]
		public bool UseNetworkOrder;
		/// <summary>
		/// How many times to reuses messages rebuffering streams.
		/// </summary>
		[XmlElement("BufferReuses")]
		public int BufferReuses = 1000;

		/// <summary>
		/// Mersenne prime base hash algorithm,
		/// produces a perfect hash in the period of (0...2^31-1]
		/// should be all but impossible to produce collisions in the given space
		/// While the ability to collide exists, the USEFUL collection of settings
		/// should keep the collision level to 0.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			/*
					Algorithm by Craig Brown
					Based on Work by 
							A.P. Sanjay
							Donald Knuth
							C.R. Wentsley
							O. Patashnik
                      
							Produces a hash of n integer items of information in O(n) 
							wherein O() is equivilent to 3 clocks on an Intel single core 
							processor, and in 1.5 clocks on a dual core with enabled hyperthreading
                      
							This is a one-way (non-recoverable) hash equivilent to
                      
							SIGMA(0...n) f(x) = { x^31 + x^19 + x^17 + x^13 + x^7 + x^5 + x^3 + x^2 + 1 MOD 2^31-1 }
			*/

			int hash = 33550336;          // largest 32-bit Perfect #
			// a perfect number is:  2^(p-1)*(2^p-1) where p is prime and 2^p-1 is a Mersenne prime

			switch (PoolType)
			{
				case SocketPoolType.Array:
					hash = ((hash << 5) ^ (hash >> 27)) ^ 127;        // 7 bit Mersenne prime
					break;
				case SocketPoolType.Null:
					hash = ((hash << 5) ^ (hash >> 27)) ^ 8191;       // 13 bit Mersenne prime
					break;
				case SocketPoolType.Linked:
					hash = ((hash << 5) ^ (hash >> 27)) ^ 131071;     // 17-bit Mersenne prime
					break;
			}
			hash = ((hash << 5) ^ (hash >> 27)) ^ PoolSize;
			hash = ((hash << 5) ^ (hash >> 27)) ^ ConnectTimeout;
			hash = ((hash << 5) ^ (hash >> 27)) ^ InitialMessageSize;
			hash = ((hash << 5) ^ (hash >> 27)) ^ MaximumReplyMessageSize;
			hash = ((hash << 5) ^ (hash >> 27)) ^ ReceiveBufferSize;
			hash = ((hash << 5) ^ (hash >> 27)) ^ ReceiveTimeout;
			hash = ((hash << 5) ^ (hash >> 27)) ^ SendBufferSize;
			hash = ((hash << 5) ^ (hash >> 27)) ^ SendTimeout;
			hash = ((hash << 5) ^ (hash >> 27)) ^ SocketLifetimeMinutes;
			hash = ((hash << 5) ^ (hash >> 27)) ^ BufferReuses;
			if (UseNetworkOrder) { hash = ((hash << 5) ^ (hash >> 27)) ^ 524287; } // 19 bit Mersenne prime

			hash %= 2147483647; // 31 bit Mersenne prime

			return hash;
		}

		/// <summary>
		/// If obj is an instance of SocketSettings, compares each field and returns false if any are different.
		/// </summary>		
		public bool SameAs(SocketSettings settingsObj)
		{
			if (settingsObj != null)
			{
				if (
					PoolType != settingsObj.PoolType ||
					PoolSize != settingsObj.PoolSize ||
					ConnectTimeout != settingsObj.ConnectTimeout ||
					InitialMessageSize != settingsObj.InitialMessageSize ||
					MaximumReplyMessageSize != settingsObj.MaximumReplyMessageSize ||
					ReceiveBufferSize != settingsObj.ReceiveBufferSize ||
					ReceiveTimeout != settingsObj.ReceiveTimeout ||
					SendBufferSize != settingsObj.SendBufferSize ||
					SendTimeout != settingsObj.SendTimeout ||
					SocketLifetimeMinutes != settingsObj.SocketLifetimeMinutes ||
					UseNetworkOrder != settingsObj.UseNetworkOrder ||
					BufferReuses != settingsObj.BufferReuses
					)
					return false;
				else
					return true;
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// 	<para>Creates a copy of this instance.</para>
		/// </summary>
		/// <returns>
		/// 	<para>A new <see cref="SocketSettings"/> object with the
		///		same contents as this instance; never <see langword="null"/>.</para>
		/// </returns>
		public SocketSettings Copy()
		{
			SocketSettings copy = new SocketSettings();
			copy.BufferReuses = this.BufferReuses;
			copy.ConnectTimeout = this.ConnectTimeout;
			copy.InitialMessageSize = this.InitialMessageSize;
			copy.MaximumReplyMessageSize = this.MaximumReplyMessageSize;
			copy.PoolSize = this.PoolSize;
			copy.PoolType = this.PoolType;
			copy.ReceiveBufferSize = this.ReceiveBufferSize;
			copy.ReceiveTimeout = this.ReceiveTimeout;
			copy.SendBufferSize = this.SendBufferSize;
			copy.SendTimeout = this.SendTimeout;
			copy.SocketLifetimeMinutes = this.SocketLifetimeMinutes;
			copy.UseNetworkOrder = this.UseNetworkOrder;

			return copy;
		}

		/// <summary>
		/// Create a new SocketSettings object with hard-coded defaults.
		/// </summary>
		public SocketSettings()
		{
		}

		#region IEquatable<SocketSettings> Members

		bool IEquatable<SocketSettings>.Equals(SocketSettings other)
		{
			return SameAs(other);
		}

		#endregion
	}

	/// <summary>
	/// The pooling mechanism used for open sockets.
	/// </summary>
	public enum SocketPoolType
	{
		/// <summary>
		/// A fixed array of sockets. Simple and robust but inefficient. Deprecated by Linked.
		/// </summary>
		Array,
		/// <summary>
		/// Do not pool sockets, create a new one for each message.
		/// </summary>
		Null,
		/// <summary>
		/// A linked list of open sockets. The preferred pool type for most cases.
		/// </summary>
		Linked
	}
}
