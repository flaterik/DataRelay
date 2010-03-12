using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;


namespace MySpace.DataRelay.Common.Interfaces.Query.ListCache
{
    public class VirtualMultiContainsCacheListQuery : MultiContainsCacheListQuery, IVirtualCacheType
    {
        public VirtualMultiContainsCacheListQuery()
            : base()
        {
        }
        public VirtualMultiContainsCacheListQuery(byte[] cacheListId, byte[][] cacheListNodeIds)
            : base(cacheListId, cacheListNodeIds)
        {
        }
        public VirtualMultiContainsCacheListQuery(string virtualCacheTypeName, byte[] cacheListId, byte[][] cacheListNodeIds)
            : base(cacheListId, cacheListNodeIds)
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

    public class MultiContainsCacheListQuery : IRelayMessageQuery, IPrimaryQueryId
    {
        private byte[]   cacheListId;
		private int primaryId;
        private byte[][] cacheListNodeIds;
        private int virtualListCount;

        #region Consructors
        public MultiContainsCacheListQuery()
        {
            Init(null, null, 0);
        }
        public MultiContainsCacheListQuery(byte[] cacheListId, byte[][] cacheListNodeIds)
        {
            Init(cacheListId, cacheListNodeIds, 0);
        }

		public MultiContainsCacheListQuery(byte[] cacheListId, byte[][] cacheListNodeIds, int primaryId)
		{
            Init(cacheListId, cacheListNodeIds, primaryId);
		}
        private void Init(byte[] cacheListId, byte[][] cacheListNodeIds, int primaryId)
        {
            this.cacheListId = cacheListId;
            this.primaryId = primaryId;
            this.cacheListNodeIds = cacheListNodeIds;
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
                cacheListId = value;
            }
        }
        public byte[][] CacheListNodeIds
        {
            get
            {
                return this.cacheListNodeIds;
            }
            set
            {
                cacheListNodeIds = value;
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
                return (byte)QueryTypes.MultiContainsCacheListQuery;
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

			int count = reader.ReadInt32();

            if (version >= 2)
            {
                if (count > 0)
                {
                    this.cacheListNodeIds = new byte[count][];
                    DeserializeListV2(reader, count);
                }
                this.VirtualListCount = reader.ReadInt32();
                if (version >= 3)
                    this.PrimaryId = reader.ReadInt32();
            }
            else
            {
                if (count > 0)
                {
                    this.cacheListNodeIds = new byte[count][];
                    DeserializeListV1(reader, count);
                }
            }
        }

        private void DeserializeListV1(MySpace.Common.IO.IPrimitiveReader reader, int count)
        {

            int idLength = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                this.cacheListNodeIds[i] = reader.ReadBytes(idLength);
            }
        }

        private void DeserializeListV2(MySpace.Common.IO.IPrimitiveReader reader, int count)
        {
            for (int i = 0; i < count; i++)
            {
                this.cacheListNodeIds[i] = reader.ReadBytes(reader.ReadInt32());
            }
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
            if ((this.cacheListNodeIds != null) && (this.cacheListNodeIds.Length > 0))
            {
                writer.Write(this.cacheListNodeIds.Length);
                SerializeList(writer);
            }
            else
            {
                writer.Write((int)0);
            }
            writer.Write(this.VirtualListCount);
            writer.Write(this.PrimaryId);
        }

        private void SerializeList(MySpace.Common.IO.IPrimitiveWriter writer)
        {
            for (int i = 0; i < this.cacheListNodeIds.Length; i++)
            {
                writer.Write(this.cacheListNodeIds[i].Length);
                writer.Write(this.cacheListNodeIds[i]);
            }
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
