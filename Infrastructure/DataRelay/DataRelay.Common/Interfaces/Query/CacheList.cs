using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.DataRelay.Common.Util;
using MySpace.Common.Framework;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
	public class CacheList : IExtendedCacheParameter, IVersionSerializable
	{
		private byte[] listId;
		private List<CacheListNode> addList;
		private List<CacheListNode> deleteList;

		// Constructors
		public CacheList()
		{
			// param less constructor required for RelayMessage.GetObject<T>()
			this.listId = null;
			this.addList = new List<CacheListNode>();
			this.deleteList = new List<CacheListNode>();
		}
		public CacheList(byte[] listId)
		{
			this.listId = listId;
			this.addList = new List<CacheListNode>();
			this.deleteList = new List<CacheListNode>();
		}

		// Properties
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


		#region CacheListNodeComparer/Comparison
		internal static int CacheListNodeComparer(CacheListNode o1, CacheListNode o2)
		{
			return o1.Timestamp.CompareTo(o2.Timestamp);
		}
		internal static Comparison<CacheListNode> CacheListNodeComparison = new Comparison<CacheListNode>(CacheListNodeComparer);
		#endregion

		// Methods
		public static int GeneratePrimaryId(byte[] bytes)
		{
			// TBD
			if (bytes == null || bytes.Length == 0)
			{
				return 1;
			}
			else
			{
				if (bytes.Length >= 4)
				{
					return Math.Abs(BitConverter.ToInt32(bytes, 0));
				}
				else
				{
					//return Math.Abs(Convert.ToBase64String(Id).GetHashCode());
					return Math.Abs((int)bytes[0]);
				}
			}
		}
		public void PruneAddList(DateTime MinValidDate)
		{
			int index;
			for (index = addList.Count - 1; index >= 0; index--)
			{
				if (addList[index].Timestamp <= MinValidDate)
				{
					// item at this index and all before are valid
					break;
				}
				else
				{
					// remove item at this index
					addList.RemoveAt(index);
				}
			}
		}
		public void Add(CacheListNode listNode)
		{
			this.addList.Add(listNode);
		}
		public void AddToDeleteList(CacheListNode listNode)
		{
			this.deleteList.Add(listNode);
		}
		public void SortAddList()
		{
			this.addList.Sort(CacheListNodeComparison);
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
				return GeneratePrimaryId(listId);
			}
			set
			{
				throw new Exception("Setter for 'CacheList.PrimaryId' is not implemented and should not be invoked!");
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

					writer.Write(listNode.NodeId.Length);
					writer.Write(listNode.NodeId);

					if (listNode.Data == null)
					{
						writer.Write((int)0);
					}
					else
					{
						writer.Write(listNode.Data.Length);
						writer.Write(listNode.Data);
					}
					writer.Write(new SmallDateTime(listNode.Timestamp).TicksInt32);					
				}
			}
			else
			{
				writer.Write(count);
			}
		}

		public void Serialize(MySpace.Common.IO.IPrimitiveWriter writer)
		{
			writer.Write(listId.Length);
			writer.Write(listId);

			SerializeList(writer, this.addList);
			SerializeList(writer, this.deleteList);
		}
		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}
		public int CurrentVersion
		{
			get { return 1; }
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
						reader.ReadBytes(reader.ReadInt32()),					// nodeId						
						reader.ReadBytes(reader.ReadInt32()),					// data
						new SmallDateTime(reader.ReadInt32()).FullDateTime));	// timestamp
				}
			}

			return list;
		}

		public void Deserialize(MySpace.Common.IO.IPrimitiveReader reader)
		{
			this.listId = reader.ReadBytes(reader.ReadInt32());
			this.addList = DeserializeList(reader);
			this.deleteList = DeserializeList(reader);
		}
		#endregion
	}
}
