using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.DataRelay.Common.Util;
using MySpace.Common.Framework;
using System.Timers;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheList : IExtendedCacheParameter, IVersionSerializable
	{
        private byte[] listId;
        private double ttlSeconds;
        private bool updateListMetadata;
        private byte[] listMetadata;
		private List<CacheListNode> addList;
		private List<CacheListNode> deleteList;
        private bool clearList;
        private bool replaceList;
        private int primaryId;
        private int updateVirtualListCount;

        #region Constructors
        public CacheList()
		{
            Init(null, 0, false, new byte[0], false, 0, false, 0);
		}
		public CacheList(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata)
		{
            Init(listId, listTTLSeconds, updateListMetadata, listMetadata, false, 0, false, 0);
		}
        public CacheList(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata, bool clearList) 
        {
            Init(listId, listTTLSeconds, updateListMetadata, listMetadata, clearList, 0, false, 0);
        }
        public CacheList(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata, bool clearList, int primaryId)
        {
            Init(listId, listTTLSeconds, updateListMetadata, listMetadata, clearList, primaryId, false, 0);
        }
        public CacheList(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata, bool clearList, int primaryId, bool replaceList)
        {
            Init(listId, listTTLSeconds, updateListMetadata, listMetadata, clearList, primaryId, replaceList, 0);
        }
        public CacheList(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata, bool clearList, int primaryId, bool replaceList, int virtualListCount)
        {
            Init(listId, listTTLSeconds, updateListMetadata, listMetadata, clearList, primaryId, replaceList, virtualListCount);
        }
        private void Init(byte[] listId, double listTTLSeconds, bool updateListMetadata, byte[] listMetadata, bool clearList, int primaryId, bool replaceList, int virtualListCount)
        {
            this.ListId = listId;
            this.TTLSeconds = listTTLSeconds;
            this.UpdateListMetadata = updateListMetadata;
            this.ListMetadata = ListMetadata;
            this.addList = new List<CacheListNode>();
            this.deleteList = new List<CacheListNode>();
            this.ClearList = clearList;
            this.PrimaryId = primaryId;
            this.ReplaceList = replaceList;
            this.VirtualListCount = virtualListCount;
        }
        #endregion

        #region Properties
		public byte[] ListId
		{
			get
			{
				return this.listId;
			}
			set
			{
                this.listId = value;
			}
        }
        public double TTLSeconds
        {
            get
            {
                return this.ttlSeconds;
            }
            set
            {
                this.ttlSeconds = value;
            }
        }
        public bool UpdateListMetadata
        {
            get
            {
                return this.updateListMetadata;
            }
            set
            {
                this.updateListMetadata = value;
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
                if (value != null)
                {
                    this.listMetadata = value;
                }
                else
                {
                    this.listMetadata = new byte[0];

                }
            }
        }
		public IList<CacheListNode> AddList
		{
			get
			{
				return this.addList;
			}
		}
		public IList<CacheListNode> DeleteList
		{
			get
			{
				return this.deleteList;
			}
        }
        public bool ClearList
        {
            get
            {
                return this.clearList;
            }
            set
            {
                this.clearList = value;
            }
        }
        public bool ReplaceList
        {
            get
            {
                return this.replaceList;
            }
            set
            {
                this.replaceList = value;
            }
        }
        public int VirtualListCount
        {
            get
            {
                return this.updateVirtualListCount;
            }
            set
            {
                this.updateVirtualListCount = value;
            }
        }
        #endregion

		// Methods
		public static int GeneratePrimaryId(byte[] bytes)
		{
            return MySpace.Common.HelperObjects.CacheHelper.GeneratePrimaryId(bytes);
		}
        public void AddToAddList(CacheListNode listNode)
        {
            this.addList.Add(listNode);
        }
        public void AddToDeleteList(CacheListNode listNode)
        {
            this.deleteList.Add(listNode);
        }


		#region IExtendedCacheParameter Members

		public string ExtendedId
		{
			get
			{
				return Convert.ToBase64String(listId);
			}
			set
			{
				throw new Exception("Setter for 'CacheList.ExtendedId' is not implemented and should not be invoked!");
			}
		}

		private DateTime? lastUpdatedDate;
		public DateTime? LastUpdatedDate
		{
			get { return lastUpdatedDate; }
			set { lastUpdatedDate = value; }
		}

		#endregion

		#region ICacheParameter Members
		public int PrimaryId
		{
			get
			{
                if (this.primaryId > 0)
                    return this.primaryId;
                else
				    return GeneratePrimaryId(listId);
			}
			set
			{
                this.primaryId = value;
			}
		}
		private DataSource dataSource = DataSource.Unknown;
		public MySpace.Common.Framework.DataSource DataSource
		{
			get
			{
				return dataSource;
			}
			set
			{
				dataSource = value;
			}
		}
		public bool IsEmpty
		{
			get { return false; }
			set { return; }
		}
		#endregion

		#region IVersionSerializable Members

		private static void SerializeList(MySpace.Common.IO.IPrimitiveWriter writer, IList<CacheListNode> list)
		{
			int count = 0;
			if (list != null && list.Count > 0)
			{
				count = list.Count;
				writer.Write(count);

				CacheListNode listNode;
				for (int i = 0; i < count; i++)
				{
					listNode = list[i];

                    if (listNode.NodeId == null || listNode.NodeId.Length <= 0)
                        writer.Write((int)0);
                    else
                    {
                        writer.Write(listNode.NodeId.Length);
                        writer.Write(listNode.NodeId);
                    }

					if (listNode.Data == null || listNode.Data.Length <= 0)
					{
						writer.Write((int)0);
					}
					else
					{
						writer.Write(listNode.Data.Length);
						writer.Write(listNode.Data);
					}
					writer.Write(listNode.TimestampTicks);					
				}
			}
			else
			{
				writer.Write(count);
			}
		}

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
            if (this.ListId == null || this.ListId.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.ListId.Length);
                writer.Write(this.ListId);
            }

            writer.Write(this.TTLSeconds);
            writer.Write(this.UpdateListMetadata);

            if (this.ListMetadata == null || this.ListMetadata.Length <= 0)
            {
                writer.Write((int)0);
            }
            else
            {
                writer.Write(this.ListMetadata.Length);
                writer.Write(this.ListMetadata);
            }

			SerializeList(writer, this.addList);
			SerializeList(writer, this.deleteList);

            writer.Write(this.ClearList);
            writer.Write(this.ReplaceList);
            writer.Write(this.VirtualListCount);
            writer.Write(this.PrimaryId);
		}
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
            this.ListId = reader.ReadBytes(reader.ReadInt32());
            this.TTLSeconds = reader.ReadDouble();
            this.UpdateListMetadata = reader.ReadBoolean();
            this.ListMetadata = reader.ReadBytes(reader.ReadInt32());
			this.addList = DeserializeList(reader);
			this.deleteList = DeserializeList(reader);

            if (version >= 2)
                this.ClearList = reader.ReadBoolean();

            if (version >= 3)
                this.ReplaceList = reader.ReadBoolean();

            if (version >= 4)
                this.VirtualListCount = reader.ReadInt32();

            if (version >= 5)
                this.PrimaryId = reader.ReadInt32();
		}
        const int CURRENTVERSION = 5;

		public int CurrentVersion
		{
			get { return CURRENTVERSION; }
		}
		public bool Volatile
		{
			get { return false; }
		}
		#endregion

		#region ICustomSerializable Members
		private static List<CacheListNode> DeserializeList(MySpace.Common.IO.IPrimitiveReader reader)
		{
			int count = reader.ReadInt32();
			List<CacheListNode> list = new List<CacheListNode>();
			if (count > 0)
			{
				for (int i = 0; i < count; i++)
				{
					list.Add(new CacheListNode(
                        reader.ReadBytes(reader.ReadInt32()),			        // nodeId						
						reader.ReadBytes(reader.ReadInt32()),					// data
						reader.ReadInt32()));	// timestamp
				}
			}

			return list;
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
            reader.Response = SerializationResponse.Unhandled;
        }
        #endregion
	}
}
