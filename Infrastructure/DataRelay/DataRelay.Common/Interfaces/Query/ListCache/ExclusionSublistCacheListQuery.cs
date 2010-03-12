using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{

	public class VirtualExclusionSublistCacheListQuery : ExclusionSublistCacheListQuery, IVirtualCacheType
	{
		public VirtualExclusionSublistCacheListQuery()
			: base()
		{
		}
		public VirtualExclusionSublistCacheListQuery(byte[] cacheListId, int startIndex, int count, byte[][] excludedIds)
			: base(cacheListId, startIndex, count, excludedIds)
		{
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
	
	public class ExclusionSublistCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[] cacheListId;
		private int primaryId;
        private byte[][] excludedIds;
        private int startIndex;
        private int count;
        private int virtualListCount;

        #region Constructors
        public ExclusionSublistCacheListQuery()
        {
            Init(null, 0, 0, 0, new byte[0][]);
        }

        public ExclusionSublistCacheListQuery(byte[] cacheListId, int startIndex, int count, byte[][] excludedIds)
        {
            Init(cacheListId, 0, startIndex, count, excludedIds);
        }

		public ExclusionSublistCacheListQuery(byte[] cacheListId, int primaryId, int startIndex, int count, byte[][] excludedIds)
		{
            Init(cacheListId, primaryId, startIndex, count, excludedIds);
		}
        private void Init(byte[] cacheListId, int primaryId, int startIndex, int count, byte[][] excludedIds)
        {
            this.CacheListId = cacheListId;
            this.primaryId = primaryId;
            this.startIndex = startIndex;
            this.count = count;
            this.excludedIds = excludedIds;
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
                if (value >= 0)
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
                if (value >= 0)
                    this.count = value;
            }
        }
        public byte[][] ExcludedIds
        {
            get
            {
                return excludedIds;
            }
            set
            {
                this.excludedIds = value;
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
                return (byte)QueryTypes.ExclusionSublistCacheListQuery;
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
			this.cacheListId = reader.ReadBytes(reader.ReadInt32());
			this.startIndex = reader.ReadInt32();
			this.count = reader.ReadInt32();
			int numExcludedIds = reader.ReadInt32();

            if (version >= 2)
            {
                if (numExcludedIds > 0)
                {
                    this.excludedIds = new byte[numExcludedIds][];
                    DeserializeListV2(reader, numExcludedIds);
                }
                this.VirtualListCount = reader.ReadInt32();
                if (version >= 3)
                    this.PrimaryId = reader.ReadInt32();
            }
            else
            {
                if (numExcludedIds > 0)
                {
                    this.excludedIds = new byte[numExcludedIds][];
                    DeserializeListV1(reader, numExcludedIds);
                }
            }
        }

        private void DeserializeListV1(MySpace.Common.IO.IPrimitiveReader reader, int numExcludedIds)
        {
            //support fixed size node id length
            int idLength = reader.ReadInt32();
            for (int i = 0; i < numExcludedIds; i++)
            {
                this.excludedIds[i] = reader.ReadBytes(idLength);
            }
        }

        private void DeserializeListV2(MySpace.Common.IO.IPrimitiveReader reader, int numExcludedIds)
        {
            //support variable size node id length
            for (int i = 0; i < numExcludedIds; i++)
            {
                this.excludedIds[i] = reader.ReadBytes(reader.ReadInt32());
            }
        }


        public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            if (this.cacheListId == null || this.cacheListId.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.cacheListId.Length);
                writer.Write(this.cacheListId);
            }
            writer.Write(this.startIndex);
            writer.Write(this.count);
            if ((this.excludedIds != null) && (this.excludedIds.Length > 0))
            {
                writer.Write(this.excludedIds.Length);
                for (int i = 0; i < this.excludedIds.Length; i++)
                {
                    writer.Write(this.excludedIds[i].Length);
                    writer.Write(this.excludedIds[i]);
                }
            }
            else
            {
                writer.Write((int)0);
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
