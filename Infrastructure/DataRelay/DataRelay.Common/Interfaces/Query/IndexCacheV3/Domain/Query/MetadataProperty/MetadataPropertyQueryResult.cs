using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MetadataPropertyQueryResult : IVersionSerializable
	{
		#region Data Members

        /// <summary>
        /// Gets or sets the metadata property collection.
        /// </summary>
        /// <value>The metadata property collection.</value>
        public MetadataPropertyCollection MetadataPropertyCollection
        {
            get; internal set;
        }

        /// <summary>
        /// Gets or sets the exception info.
        /// </summary>
        /// <value>The exception info.</value>
		public string ExceptionInfo
		{
			get; internal set;
		}

		#endregion

		#region IVersionSerializable Members

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
        {
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

            //ExceptionInfo
            writer.Write(ExceptionInfo);
        }

        /// <summary>
        /// Deserialize the class data from a stream.
        /// </summary>
        /// <param name="reader">The <see cref="T:MySpace.Common.IO.IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
        /// <param name="version">The value of <see cref="P:MySpace.Common.IVersionSerializable.CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
        /// the version of the <paramref name="reader"/> data.</param>
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            //MetadataPropertyCollection
            if (reader.ReadBoolean())
            {
                MetadataPropertyCollection = new MetadataPropertyCollection();
                Serializer.Deserialize(reader.BaseStream, MetadataPropertyCollection);
            }

            //ExceptionInfo
            ExceptionInfo = reader.ReadString();
        }

	    private const int CURRENT_VERSION = 1;
        /// <summary>
        /// Gets the current serialization data version of your object.  The <see cref="M:MySpace.Common.IVersionSerializable.Serialize(MySpace.Common.IO.IPrimitiveWriter)"/> method
        /// will write to the stream the correct format for this version.
        /// </summary>
        /// <value></value>
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        /// <summary>
        /// Deprecated. Has no effect.
        /// </summary>
        /// <value></value>
		public bool Volatile
		{
			get
			{
				return false;
			}
		}

		#endregion

		#region ICustomSerializable Members

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="reader"></param>
		public void Deserialize(IPrimitiveReader reader)
		{
            reader.Response = SerializationResponse.Unhandled;
		}

		#endregion
	}
}
