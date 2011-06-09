using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class TagQueryResult : IVersionSerializable
	{
		#region Data Members

	    public bool TypeExists
	    { 
            get; set;
	    }

		public string[] TagNames
		{
			get; set;
		}

		public string ExceptionInfo
		{
			get; set;
		}

		#endregion

		#region IVersionSerializable Members

        public void Serialize(IPrimitiveWriter writer)
        {
            //TypeExists
            writer.Write(TypeExists);

            //TagNameList
            if(TagNames == null || TagNames.Length == 0)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)TagNames.Length);
                for(int i = 0; i < TagNames.Length; i++)
                {
                    writer.Write(TagNames[i]);
                }
            }

            //ExceptionInfo
            writer.Write(ExceptionInfo);
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //TypeExists
            TypeExists = reader.ReadBoolean();
            
            //TagNameList
            byte tagNamesLength = reader.ReadByte();
            if (tagNamesLength > 0)
            {
                TagNames = new string[tagNamesLength];
                for (int i = 0; i < tagNamesLength; i++)
                {
                    TagNames[i] = reader.ReadString();
                }
            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();
        }

	    private const int CURRENT_VERSION = 1;
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
