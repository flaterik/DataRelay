using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheListNode
	{
		// Data Members
		protected byte[] nodeId;
		protected int timestampTicks;
		private byte[] data;

		// Constructors
		public CacheListNode()
		{
            Init(null, null, int.MinValue);
		}
		public CacheListNode(byte[] nodeId, byte[] data, int timestampTicks)
		{
			Init(nodeId, data, timestampTicks);
		}
		private void Init(byte[] nodeId, byte[] data, int timestampTicks)
		{
			this.nodeId = nodeId;
			this.data = data;
			this.timestampTicks = timestampTicks;
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
		public int TimestampTicks
		{
			get
			{
				return this.timestampTicks;
			}
			set
			{
				this.timestampTicks = value;
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
