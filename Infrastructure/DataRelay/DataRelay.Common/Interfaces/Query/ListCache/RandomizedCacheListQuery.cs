using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualRandomizedCacheListQuery : RandomizedCacheListQuery, IVirtualCacheType
    {
        public VirtualRandomizedCacheListQuery()
            : base()
        {
        }
        public VirtualRandomizedCacheListQuery(byte[] cacheListId, int count)
            : base(cacheListId, count)
        {
        }
        public VirtualRandomizedCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, int count)
            : base(cacheListId, count)
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

    public class RandomizedCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[] cacheListId;
		private int primaryId;
        private int count;
        private int virtualListCount;

        #region Constructors
        public RandomizedCacheListQuery()
        {
            Init(null, -1, 0);
        }

        public RandomizedCacheListQuery(byte[] cacheListId, int count)
        {
            Init(cacheListId, count, 0);
        }

		public RandomizedCacheListQuery(byte[] cacheListId, int count, int primaryId)
		{
            Init(cacheListId, count, primaryId);
		}
        private void Init(byte[] cacheListId, int count, int primaryId)
        {
            this.CacheListId = cacheListId;
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
                return (byte)QueryTypes.RandomizedCacheListQuery;
            }
        }

        #endregion

        #region IVersionSerializable Members

        const int CURRENTVERSION = 3;

        public int CurrentVersion
        {
            get { return CURRENTVERSION; }
        }

        public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
        {
			this.CacheListId = reader.ReadBytes(reader.ReadInt32());
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
            writer.Write(this.Count);
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
