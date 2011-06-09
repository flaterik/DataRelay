using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FullDataIdFieldList : List<FullDataIdField>, IVersionSerializable
    {
        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //Count
                writer.Write((byte)Count);

                foreach (FullDataIdField fullDataIdField in this)
                {
                    //FullDataIdField
                    Serializer.Serialize(writer.BaseStream, fullDataIdField);
                }
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //Count
                byte count = reader.ReadByte();

                FullDataIdField fullDataIdField;
                for(int i = 0; i < count; i++)
                {
                    //FullDataIdField
                    fullDataIdField = new FullDataIdField();
                    Serializer.Deserialize(reader.BaseStream, fullDataIdField);
                    Add(fullDataIdField);
                }
            }
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
