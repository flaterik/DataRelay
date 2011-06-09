using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class StringHashQueryResult : IVersionSerializable
	{
		#region Data Members

	    public bool TypeExists
	    { 
            get; set;
	    }

		public string[] StringNames
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

            //StringNames
            if (StringNames == null || StringNames.Length == 0)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)StringNames.Length);
                for(int i = 0; i < StringNames.Length; i++)
                {
                    writer.Write(StringNames[i]);
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
                StringNames = new string[tagNamesLength];
                for (int i = 0; i < tagNamesLength; i++)
                {
                    StringNames[i] = reader.ReadString();
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
