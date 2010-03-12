using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class ContainsIndexQueryResult : IVersionSerializable
	{
		#region Data Members
        private MultiItemResult multiItemResult;
        public MultiItemResult MultiItemResult
		{
			get
			{
				return multiItemResult;
			}
			set
			{
				multiItemResult = value;
			}
		}

		private byte[] metadata;
		public byte[] Metadata
		{
			get
			{
				return metadata;
			}
			set
			{
				metadata = value;
			}
		}

		private int indexSize;
		public int IndexSize
		{
			get
			{
				return indexSize;
			}
			set
			{
				indexSize = value;
			}
		}

		private bool indexExists;
		public bool IndexExists
		{
			get
			{
				return indexExists;
			}
			set
			{
				indexExists = value;
			}
		}

        private int virtualCount;
        public int VirtualCount
        {
            get
            {
                return virtualCount;
            }
            set
            {
                virtualCount = value;
            }
        }

		private string exceptionInfo;
		public string ExceptionInfo
		{
			get
			{
				return exceptionInfo;
			}
			set
			{
				exceptionInfo = value;
			}
		}
		#endregion

		#region Ctors
		public ContainsIndexQueryResult()
		{
			Init(null, null, -1, false, -1, null);
		}

        public ContainsIndexQueryResult(MultiItemResult multiItemResult, byte[] metadata, int indexSize, bool indexExists, int virtualCount, string exceptionInfo)
		{
            Init(multiItemResult, metadata, indexSize, indexExists, virtualCount, exceptionInfo);
		}

        private void Init(MultiItemResult multiItemResult, byte[] metadata, int indexSize, bool indexExists, int virtualCount, string exceptionInfo)
		{
            this.multiItemResult = multiItemResult;
			this.metadata = metadata;
			this.indexSize = indexSize;
			this.indexExists = indexExists;
            this.virtualCount = virtualCount;
			this.exceptionInfo = exceptionInfo;
		}
		#endregion

		#region Methods
		#endregion

		#region IVersionSerializable Members
		public void Serialize(IPrimitiveWriter writer)
		{
            //MultiItemResult
            if(multiItemResult == null || multiItemResult.Count == 0)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                multiItemResult.Serialize(writer);
            }

			//Metadata
			if (metadata == null || metadata.Length == 0)
			{
				writer.Write((ushort)0);
			}
			else
			{
				writer.Write((ushort)metadata.Length);
				writer.Write(metadata);
			}

			//IndexSize
			writer.Write(indexSize);

			//IndexExists
			writer.Write(indexExists);

			//ExceptionInfo
			writer.Write(exceptionInfo);

            //VirtualCount
            writer.Write(virtualCount);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //MultiItemResult
            if (reader.ReadByte() != 0)
            {
                multiItemResult = new MultiItemResult();
                multiItemResult.Deserialize(reader);
            }

            //Metadata
            ushort len = reader.ReadUInt16();
            if (len > 0)
            {
                metadata = reader.ReadBytes(len);
            }

            //IndexSize
            indexSize = reader.ReadInt32();

            //IndexExists
            indexExists = reader.ReadBoolean();

            //ExceptionInfo
            exceptionInfo = reader.ReadString();

            //VirtualCount
            if(version >= 2)
            {
                virtualCount = reader.ReadInt32();
            }
        }

        private const int CURRENT_VERSION = 2;
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
