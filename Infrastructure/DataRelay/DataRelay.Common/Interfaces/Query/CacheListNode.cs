using System;
using System.Collections.Generic;
using System.Text;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheListNode
	{
		// Data Members
		protected byte[] nodeId;
		protected DateTime timestamp;
		private byte[] data;

		// Constructors
		public CacheListNode()
		{
			Init(null, null, DateTime.MinValue);
		}
		public CacheListNode(byte[] nodeId, byte[] data, DateTime timestamp)
		{
			Init(nodeId, data, timestamp);
		}
		private void Init(byte[] nodeId, byte[] data, DateTime timestamp)
		{
			this.nodeId = nodeId;
			this.data = data;
			this.timestamp = timestamp;
		}

		// Properties
		public byte[] NodeId
		{
			get
			{
				return this.nodeId;
			}
			set
			{
				this.nodeId = value;
			}
		}		
		public DateTime Timestamp
		{
			get
			{
				return this.timestamp;
			}
			set
			{
				this.timestamp = value;
			}
		}
		public byte[] Data
		{
			get
			{
				return this.data;
			}
			set
			{
				this.data = value;
			}
		}
	}
}
