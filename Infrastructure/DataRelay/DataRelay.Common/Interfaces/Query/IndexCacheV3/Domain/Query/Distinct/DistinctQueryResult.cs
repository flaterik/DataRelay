using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
	public class DistinctQueryResult : IVersionSerializable
	{
		#region Data Members

	    public bool IndexExists { get; set; }

	    public Dictionary<byte[], int> DistinctValueCountMapping { get; set; }

	    public string ExceptionInfo { get; set; }

	    #endregion

		#region IVersionSerializable Members

		public void Serialize(IPrimitiveWriter writer)
		{
			//IndexExists
			writer.Write(IndexExists);

            //DistinctValueCountPairList
            if (DistinctValueCountMapping == null || DistinctValueCountMapping.Count == 0)
			{
				writer.Write(0);
			}
			else
			{
                writer.Write(DistinctValueCountMapping.Count);
                foreach (KeyValuePair<byte[], int> kvp in DistinctValueCountMapping)
				{
                    //Value
                    if (kvp.Key == null || kvp.Key.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Key.Length);
                        writer.Write(kvp.Key);

                        //Count
                        writer.Write(kvp.Value);
                    }
				}
			}

			//ExceptionInfo
			writer.Write(ExceptionInfo);
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
            //IndexExists
            IndexExists = reader.ReadBoolean();

            //DistinctValueCountPairList
            int listCount = reader.ReadInt32();
            DistinctValueCountMapping =  new Dictionary<byte[], int>(listCount, new ByteArrayEqualityComparer());
            if (listCount > 0)
            {
                for (int i = 0; i < listCount; i++)
                {
                    //Value
                    ushort len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        DistinctValueCountMapping.Add(reader.ReadBytes(len), reader.ReadInt32());
                    }
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