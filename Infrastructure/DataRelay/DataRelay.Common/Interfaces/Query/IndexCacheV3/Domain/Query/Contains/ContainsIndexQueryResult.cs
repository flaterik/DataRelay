using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class ContainsIndexQueryResult : IVersionSerializable
	{
		#region Data Members

	    public MultiItemResult MultiItemResult { get; set; }

	    public byte[] Metadata { get; set; }
        
        public MetadataPropertyCollection MetadataPropertyCollection { get; set; }

	    public int IndexSize { get; set; }

	    public bool IndexExists { get; set; }

	    public int VirtualCount { get; set; }

        public int IndexCap { get; set; }

	    public string ExceptionInfo { get; set; }

	    #endregion

		#region Ctors

		public ContainsIndexQueryResult()
		{
			Init(null, null, null, -1, false, -1, 0, null);
		}

        public ContainsIndexQueryResult(MultiItemResult multiItemResult, 
            byte[] metadata,
            MetadataPropertyCollection metadataPropertyCollection,
            int indexSize, 
            bool indexExists, 
            int virtualCount, 
            int indexCap, 
            string exceptionInfo)
		{
            Init(multiItemResult, metadata, metadataPropertyCollection, indexSize, indexExists, virtualCount, indexCap, exceptionInfo);
		}

        private void Init(MultiItemResult multiItemResult, 
            byte[] metadata,
            MetadataPropertyCollection metadataPropertyCollection,
            int indexSize, 
            bool indexExists, 
            int virtualCount,
            int indexCap, 
            string exceptionInfo)
		{
            MultiItemResult = multiItemResult;
			Metadata = metadata;
            MetadataPropertyCollection = metadataPropertyCollection;
			IndexSize = indexSize;
			IndexExists = indexExists;
            VirtualCount = virtualCount;
			ExceptionInfo = exceptionInfo;
            IndexCap = indexCap;
		}

		#endregion

		#region Methods
		#endregion

		#region IVersionSerializable Members
		public void Serialize(IPrimitiveWriter writer)
		{
            //MultiItemResult
            if(MultiItemResult == null || MultiItemResult.Count == 0)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                MultiItemResult.Serialize(writer);
            }

			//Metadata
			if (Metadata == null || Metadata.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)Metadata.Length);
				writer.Write(Metadata);
			}

			//IndexSize
			writer.Write(IndexSize);

			//IndexExists
			writer.Write(IndexExists);

			//ExceptionInfo
			writer.Write(ExceptionInfo);

            //VirtualCount
            writer.Write(VirtualCount);

            //IndexCap
            writer.Write(IndexCap);

            //MetadataPropertyCollection
            if (MetadataPropertyCollection == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, MetadataPropertyCollection);
            }
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //MultiItemResult
            if (reader.ReadByte() != 0)
            {
                MultiItemResult = new MultiItemResult();
                MultiItemResult.Deserialize(reader);
            }

            //Metadata
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                Metadata = reader.ReadBytes(len);
            }

            //IndexSize
            IndexSize = reader.ReadInt32();

            //IndexExists
            IndexExists = reader.ReadBoolean();

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();

            //VirtualCount
            if(version >= 2)
            {
                VirtualCount = reader.ReadInt32();
            }

            //IndexCap
            if (version >= 3)
            {
                IndexCap = reader.ReadInt32();
            }

            if (version >= 4)
            {
                //MetadataPropertyCollection
                if (reader.ReadBoolean())
                {
                    MetadataPropertyCollection = new MetadataPropertyCollection();
                    Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollection);
                }
            }
        }

        private const int CURRENT_VERSION = 4;
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
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
            reader.Response = SerializationResponse.Unhandled;
		}

		#endregion
	}
}
