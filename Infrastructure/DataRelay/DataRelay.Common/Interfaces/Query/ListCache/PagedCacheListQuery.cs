using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualPagedCacheListQuery : PagedCacheListQuery, IVirtualCacheType
    {
        public VirtualPagedCacheListQuery()
            : base()
        {
        }
        public VirtualPagedCacheListQuery(byte[] cacheListId, int pageSize, int pageNum, bool reversePagedQuery)
            : base(cacheListId, pageSize, pageNum, reversePagedQuery)
        {
        }
        public VirtualPagedCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, int pageSize, int pageNum, bool reversePagedQuery)
            : base(cacheListId, pageSize, pageNum, reversePagedQuery)
        {
            this.cacheTypeName = virtualCacheTypeName;
        }
        public VirtualPagedCacheListQuery(byte[] cacheListId, int primaryId, int pageSize, int pageNum, bool reversePagedQuery)
            : base(cacheListId, primaryId, pageSize, pageNum, reversePagedQuery)
        {
        }
        public VirtualPagedCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, int primaryId, int pageSize, int pageNum, bool reversePagedQuery)
            : base(cacheListId, primaryId, pageSize, pageNum, reversePagedQuery)
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

    public class PagedCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[] cacheListId;
        private int primaryId;
        private int pageSize;
        private int pageNum;
        private bool reversePagedQuery;
        private int virtualListCount;

        #region Constructors
        public PagedCacheListQuery()
        {
            Init(null, 0, 0, 1, false);
        }
        public PagedCacheListQuery(byte[] cacheListId, int pageSize, int pageNum, bool reversePagedQuery)
        {
            Init(cacheListId, 0, pageSize, pageNum, reversePagedQuery);
        }
        public PagedCacheListQuery(byte[] cacheListId, int primaryId, int pageSize, int pageNum, bool reversePagedQuery)
        {
            Init(cacheListId, primaryId, pageSize, pageNum, reversePagedQuery);
        }
        private void Init(byte[] cacheListId, int primaryId, int pageSize, int pageNum, bool reversePagedQuery)
        {
            this.CacheListId = cacheListId;
            this.PrimaryId = primaryId;
            this.PageSize = pageSize;
            this.PageNum = pageNum;
            this.ReversePagedQuery = reversePagedQuery;
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
        public int PageSize
        {
            get
            {
                return this.pageSize;
            }
            set
            {
                this.pageSize = value;
            }
        }
        public int PageNum
        {
            get
            {
                return this.pageNum;
            }
            set
            {
                this.pageNum = value;
            }
        }
        public bool ReversePagedQuery
        {
            get
            {
                return this.reversePagedQuery;
            }
            set
            {
                this.reversePagedQuery = value;
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
                if (this.ReversePagedQuery)
                    return (byte)QueryTypes.ReversePagedCacheListQuery;
                else
                    return (byte)QueryTypes.PagedCacheListQuery;
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
			this.PageSize = reader.ReadInt32();
			this.PageNum = reader.ReadInt32();
			this.ReversePagedQuery = reader.ReadBoolean();
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
            writer.Write(this.PageSize);
            writer.Write(this.PageNum);
            writer.Write(this.ReversePagedQuery);
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
