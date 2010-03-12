using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.DataRelay.Common.Util;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class CacheListQueryResult : IVersionSerializable
    {
        private List<CacheListNode> cacheList;
        private int cacheListTotalCount;
        private bool containsCacheListNode;
        private bool listExists;
        private byte[] listMetadata;
        private int virtualListCount;

        #region Constructor
        public CacheListQueryResult()
        {
            Init(0, false, false, null, -1);
        }
        private void Init(int cacheListTotalCount, bool containsCacheListNode, bool listExists, byte[] listMetadata, int virtualListCount)
        {
            this.CacheList = new List<CacheListNode>();
            this.CacheListTotalCount = cacheListTotalCount;
            this.ContainsCacheListNode = containsCacheListNode;
            this.ListExists = listExists;
            this.ListMetadata = listMetadata;
            this.virtualListCount = virtualListCount;
        }
        #endregion

        #region Properties
        public List<CacheListNode> CacheList
        {
            get
            {
                return this.cacheList;
            }
            set
            {
                this.cacheList = value;
            }
        }
        public int CacheListTotalCount
        {
            get
            {
                return this.cacheListTotalCount;
            }
            set
            {
                this.cacheListTotalCount = value;
            }
        }
        public bool ContainsCacheListNode
        {
            get
            {
                return this.containsCacheListNode;
            }
            set
            {
                this.containsCacheListNode = value;
            }
        }
        public bool ListExists
        {
            get
            {
                return this.listExists;
            }
            set
            {
                this.listExists = value;
            }
        }
        public byte[] ListMetadata
        {
            get
            {
                return this.listMetadata;
            }
            set
            {
                this.listMetadata = value;
            }
        }
		
        #endregion

        public void AddCacheListNode(CacheListNode node)
        {
            this.CacheList.Add(node);
        }

        public void InsertCacheListNodeInFront(CacheListNode node)
        {
            this.CacheList.Insert(0, node);
        }

        #region IVersionSerializable Members

        public int CurrentVersion
        {
            get { return 2; }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
			this.ContainsCacheListNode = reader.ReadBoolean();
			this.ListExists = reader.ReadBoolean();
			this.ListMetadata = reader.ReadBytes(reader.ReadInt32());
			this.CacheListTotalCount = reader.ReadInt32();
			int count = reader.ReadInt32();

            if (version >= 2)
            {
                this.CacheList = DeserializeListV2(reader, count);
                this.VirtualListCount = reader.ReadInt32();
            }
            else
            {
                this.CacheList = DeserializeListV1(reader, count);
            }
        }

        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            writer.Write(this.ContainsCacheListNode);
            writer.Write(this.ListExists);

            if (this.ListMetadata == null || this.ListMetadata.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.ListMetadata.Length);
                writer.Write(this.ListMetadata);
            }

            writer.Write(this.CacheListTotalCount);
            writer.Write(this.CacheList.Count);

            if (this.CacheList.Count > 0)
            {
                SerializeList(writer);
            }
            writer.Write(this.VirtualListCount);
        }

        private void SerializeList(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            foreach (CacheListNode node in this.CacheList)
            {
                if (node.NodeId == null || node.NodeId.Length <= 0)
                {
                    writer.Write((int)0);
                }
                else
                {
                    writer.Write(node.NodeId.Length);
                    writer.Write(node.NodeId);
                }
                if (node.Data == null || node.Data.Length <= 0)
                {
                    writer.Write((int)0);
                }
                else
                {
                    writer.Write(node.Data.Length);
                    writer.Write(node.Data);
                }
                writer.Write(node.TimestampTicks);
            }
        }

        private static List<CacheListNode> DeserializeListV1(MySpace.Common.IO.IPrimitiveReader reader, int count)
        {
            List<CacheListNode> list = new List<CacheListNode>();
            if (count > 0)
            {
                int nodeIdLen = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    list.Add(new CacheListNode(
                        reader.ReadBytes(nodeIdLen),					// nodeId						
                        reader.ReadBytes(reader.ReadInt32()),					// data
                        reader.ReadInt32()));	// timestamp
                }
            }

            return list;
        }

        private static List<CacheListNode> DeserializeListV2(MySpace.Common.IO.IPrimitiveReader reader, int count)
        {
            List<CacheListNode> list = new List<CacheListNode>();
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    list.Add(new CacheListNode(
                        reader.ReadBytes(reader.ReadInt32()),					// nodeId						
                        reader.ReadBytes(reader.ReadInt32()),					// data
                        reader.ReadInt32()));	// timestamp
                }
            }

            return list;
        }

        public bool Volatile
        {
            get { return false; }
        }

    	public int VirtualListCount
    	{
    		get
    		{
    			return this.virtualListCount;
    		}
    		set
    		{
    			this.virtualListCount = value;
    		}
    	}

    	#endregion

        #region ICustomSerializable Members
        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

      #endregion
    }
}
