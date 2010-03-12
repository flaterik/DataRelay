using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class TagSort : IVersionSerializable
	{
		#region Data Members
		private string tagName;
		public string TagName
		{
			get
			{
				return tagName;
			}
			set
			{
				tagName = value;
			}
		}

		private bool isTag;
		public bool IsTag
		{
			get 
			{ 
				return isTag; 
			}
			set 
			{ 
				isTag = value; 
			}
		}

		private SortOrder sortOrder;
		public SortOrder SortOrder
		{
			get 
			{ 
				return sortOrder; 
			}
			set 
			{ 
				sortOrder = value; 
			}
		}

		#endregion

		#region Ctors
		public TagSort()
		{
			Init(null, true, null);
		}

		public TagSort(string tagName, SortOrder sortOrder)
		{
			Init(tagName, true, sortOrder);
		}

		public TagSort(string tagName, bool isTag, SortOrder sortOrder)
		{
			Init(tagName, isTag, sortOrder);
		}

		private void Init(string tagName, bool isTag, SortOrder sortOrder)
		{
			this.tagName = tagName;
			this.isTag = isTag;
			this.sortOrder = sortOrder;
		}
		#endregion

		#region IVersionSerializable Members
		public void Serialize(IPrimitiveWriter writer)
		{
			//TagName
			writer.Write(tagName);

			//IsTag
			writer.Write(isTag);

			//SortOrder
			sortOrder.Serialize(writer);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
			Deserialize(reader);
		}

		public int CurrentVersion
		{
			get
			{
				return 1;
			}
		}

		public bool Volatile
		{
			get
			{
				return false;
			}
		}
		#endregion

		#region ICustomSerializable Members

		public void Deserialize(IPrimitiveReader reader)
		{
			//TagName
			tagName = reader.ReadString();

			//IsTag
			isTag = reader.ReadBoolean();

			//SortOrder
			sortOrder = new SortOrder();
			sortOrder.Deserialize(reader);
		}

		#endregion
	}
}
