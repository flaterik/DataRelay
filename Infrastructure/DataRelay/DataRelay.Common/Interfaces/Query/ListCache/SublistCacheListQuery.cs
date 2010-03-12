using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualSublistCacheListQuery : SublistCacheListQuery, IVirtualCacheType
    {
        public VirtualSublistCacheListQuery()
            : base()
        {
        }
        public VirtualSublistCacheListQuery(byte[] cacheListId, int startIndex, int count)
            : base(cacheListId, startIndex, count)
        {
        }
        public VirtualSublistCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, int startIndex, int count)
            : base(cacheListId, startIndex, count)
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

    public class SublistCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[] cacheListId;
        private int startIndex;
    	private int primaryId;
        private int count;
        private int virtualListCount;

        #region Constructors
        public SublistCacheListQuery()
        {
            Init(null, -1, -1, 0);
        }

        public SublistCacheListQuery(byte[] cacheListId, int startIndex, int count)
        {
            Init(cacheListId, startIndex, count, 0);
        }

		public SublistCacheListQuery(byte[] cacheListId, int startIndex, int count, int primaryId)
		{
            Init(cacheListId, startIndex, count, primaryId);
		}

        private void Init(byte[] cacheListId, int startIndex, int count, int primaryId)
        {
            this.CacheListId = cacheListId;
            this.startIndex = startIndex;
            this.primaryId = primaryId;
            this.count = count;
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
        public int StartIndex
        {
            get
            {
                return this.startIndex;
            }
            set
            {
                this.startIndex = value;
            }
        }
        public int Count
        {
            get
            {
                return this.count;
            }
            set
            {
                this.count = value;
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
                return (byte)QueryTypes.SublistCacheListQuery;
            }
        }

        #endregion

		const int CURRENTVERSION = 3;
        public int CurrentVersion
        {
			get { return CURRENTVERSION; }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {

            this.CacheListId = reader.ReadBytes(reader.ReadInt32());
            this.StartIndex = reader.ReadInt32();
            this.Count = reader.ReadInt32();
			if(version>=2)
			   this.VirtualListCount = reader.ReadInt32();
            if (version >= 3)
                this.PrimaryId = reader.ReadInt32();
		}

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
            if (this.CacheListId == null || this.CacheListId.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.CacheListId.Length);
                writer.Write(this.CacheListId);
            }
            writer.Write(this.StartIndex);
			writer.Write(this.Count);
			writer.Write(this.virtualListCount);
            writer.Write(this.PrimaryId);
		}

        public bool Volatile
        {
            get { return false; }
        }

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			reader.Response = SerializationResponse.Unhandled;
		}

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
	}
}
