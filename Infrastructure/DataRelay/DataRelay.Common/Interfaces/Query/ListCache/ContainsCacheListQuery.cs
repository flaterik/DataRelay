using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualContainsCacheListQuery : ContainsCacheListQuery, IVirtualCacheType
    {
        public VirtualContainsCacheListQuery()
            : base()
        {
        }
        public VirtualContainsCacheListQuery(byte[] cacheListId, byte[] cacheListNodeId)
            : base(cacheListId, cacheListNodeId)
        {
        }
        public VirtualContainsCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, byte[] cacheListNodeId)
            : base(cacheListId, cacheListNodeId)
        {
            this.cacheTypeName = virtualCacheTypeName;
        }
        public VirtualContainsCacheListQuery(byte[] cacheListId, int primaryId, byte[] cacheListNodeId)
            : base(cacheListId, primaryId, cacheListNodeId)
        {
        }
        public VirtualContainsCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, int primaryId, byte[] cacheListNodeId)
            : base(cacheListId, primaryId, cacheListNodeId)
        {
            this.cacheTypeName = virtualCacheTypeName;
        }

        #region IVirtualCacheType Members
        private string cacheTypeName;
        public string CacheTypeName
        {
            get
            {
                return this.cacheTypeName;
            }
            set
            {
                this.cacheTypeName = value;
            }
        }
        #endregion
    }

    public class ContainsCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[] cacheListId;
        private int primaryId;
        private byte[] cacheListNodeId;
        private int virtualListCount;

        #region Consructors
        public ContainsCacheListQuery()
        {
            Init(null, 0, null);
        }
        public ContainsCacheListQuery(byte[] cacheListId, byte[] cacheListNodeId)
        {
            Init(cacheListId, 0, cacheListNodeId);
        }
        public ContainsCacheListQuery(byte[] cacheListId, int primaryId, byte[] cacheListNodeId)
        {
            Init(cacheListId, primaryId, cacheListNodeId);
        }
        private void Init(byte[] cacheListId, int primaryId, byte[] cacheListNodeId)
        {
            this.CacheListId = cacheListId;
            this.PrimaryId = primaryId;
            this.CacheListNodeId = cacheListNodeId;
            this.virtualListCount = -1;
        }
        #endregion

        #region Properties
        public byte[] CacheListId
        {
            get
            {
                return this.cacheListId;
            }
            set
            {
                this.cacheListId = value;
            }
        }
        public byte[] CacheListNodeId
        {
            get
            {
                return this.cacheListNodeId;
            }
            set
            {
                this.cacheListNodeId = value;
            }
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

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get 
            {
                return (byte)QueryTypes.ContainsCacheListQuery;
            }
        }

        #endregion

        #region IVersionSerializable Members
                    
        public int CurrentVersion
        {
            get { return 3; }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
			this.CacheListId = reader.ReadBytes(reader.ReadInt32());
			this.CacheListNodeId = reader.ReadBytes(reader.ReadInt32());
			if(version>=2)
				this.virtualListCount = reader.ReadInt32();
            if (version >= 3)
                this.PrimaryId = reader.ReadInt32();
        }

        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            if (this.CacheListId == null && this.CacheListId.Length <= 0 )
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.CacheListId.Length);
                writer.Write(this.CacheListId);
            }
			if (this.CacheListNodeId == null && this.CacheListNodeId.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
			{
				writer.Write(this.CacheListNodeId.Length);
				writer.Write(this.CacheListNodeId);
			}
        	writer.Write(this.VirtualListCount);
            writer.Write(this.PrimaryId);
        }

        public bool Volatile
        {
            get { return false; }
        }

        #endregion

        #region ICustomSerializable Members
        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion

        #region IPrimaryQueryId Members

        public int PrimaryId
        {
            get
            {
                if (this.primaryId > 0)
                    return this.primaryId;
                else
                    return ListCache.VirtualCacheList.GeneratePrimaryId(cacheListId);
            }
            set
            {
                this.primaryId = value;
            }
        }

        #endregion
    }
}
