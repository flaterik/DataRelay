using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public abstract class Filter:IVersionSerializable
    {
        internal abstract FilterType FilterType
        {
            get;
        }

        internal abstract int FilterCount
        {
            get;
        }

        public abstract override string ToString();

        #region IVersionSerializable Members

        public abstract int CurrentVersion
        { 
            get;
        }

        //If this method is turned to virtual in future then all derived classes MUST call base.Deserialize(reader, version);
        public abstract void Deserialize(IPrimitiveReader reader, int version);

        //If this method is turned to virtual in future then all derived classes MUST call base.Deserialize(writer);
        public abstract void Serialize(IPrimitiveWriter writer);

        public abstract bool Volatile
        {
            get;
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
