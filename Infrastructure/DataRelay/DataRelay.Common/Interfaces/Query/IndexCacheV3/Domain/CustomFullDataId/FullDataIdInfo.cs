using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FullDataIdInfo : IVersionSerializable
    {
        #region Data Members
        private string relatedTypeName;
        public string RelatedTypeName
        {
            get
            {
                return relatedTypeName;
            }
            set
            {
                relatedTypeName = value;
            }
        }

        private FullDataIdFieldList fullDataIdFieldList;
        public FullDataIdFieldList FullDataIdFieldList
        {
            get
            {
                return fullDataIdFieldList;
            }
            set
            {
                fullDataIdFieldList = value;
            }
        }
        #endregion

        #region Ctors
        public FullDataIdInfo()
        {
            Init(null, null);
        }

        public FullDataIdInfo(string relatedTypeName, FullDataIdFieldList fullDataIdFieldList)
        {
            Init(relatedTypeName, fullDataIdFieldList);
        }

        private void Init(string relatedTypeName, FullDataIdFieldList fullDataIdFieldList)
        {
            this.relatedTypeName = relatedTypeName;
            this.fullDataIdFieldList = fullDataIdFieldList;
        }
        #endregion

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //RelatedTypeName
                writer.Write(relatedTypeName);

                //FullDataIdFieldList
                Serializer.Serialize(writer.BaseStream, fullDataIdFieldList);
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //RelatedTypeName
                relatedTypeName = reader.ReadString();

                //FullDataIdFieldList
                fullDataIdFieldList = new FullDataIdFieldList();
                Serializer.Deserialize(reader.BaseStream, fullDataIdFieldList);
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
