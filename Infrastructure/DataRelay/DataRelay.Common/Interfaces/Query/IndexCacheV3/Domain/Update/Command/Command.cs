using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public abstract class Command : IVersionSerializable
    {
        #region Methods
 
        internal abstract CommandType CommandType { get; }

        public abstract int PrimaryId { get; set;}

        public abstract byte[] ExtendedId { get; set;}

        #endregion
    
        #region IVersionSerializable Members

        public abstract int  CurrentVersion { get; }

        public abstract void Deserialize(IPrimitiveReader reader, int version);

        public abstract void Serialize(IPrimitiveWriter writer);

        public bool  Volatile
        {
	        get
	        {
	            return false;
	        }
        }

        #endregion

        #region ICustomSerializable Members
        
        public void  Deserialize(IPrimitiveReader reader)
        {
 	        reader.Response = SerializationResponse.Unhandled;
        }
        
        #endregion
    }
}
