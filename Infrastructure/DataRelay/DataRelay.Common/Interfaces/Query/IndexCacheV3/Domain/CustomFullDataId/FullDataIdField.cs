using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FullDataIdField : IVersionSerializable
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the type of the FullDataIdField.
        /// </summary>
        /// <value>The type of the FullDataIdField.</value>
        public FullDataIdType FullDataIdType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the name of the tag.
        /// </summary>
        /// <value>The name of the tag.</value>
        public string TagName
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the offset.
        /// </summary>
        /// <value>The offset.</value>
        public int Offset
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the count.
        /// </summary>
        /// <value>The count.</value>
        public int Count
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the DataType of the FullDataIdField.
        /// </summary>
        /// <value>The DataType of the FullDataIdField.</value>
        public DataType DataType
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the FullDataIdFieldList of the FullDataIdField.
        /// </summary>
        /// <value>The FullDataIdFieldList.</value>
        public FullDataIdFieldList FullDataIdFieldList
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the FullDataIdPartFormat of the FullDataIdField.
        /// </summary>
        /// <value>The FullDataIdPartFormat.</value>
        internal FullDataIdPartFormat FullDataIdPartFormat
        {
            get;
            set;
        }

        #endregion

        #region IVersionSerializable Members

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
        {
            using (writer.CreateRegion())
            {
                //FullDataIdType
                writer.Write((byte)FullDataIdType);

                //TagName
                writer.Write(TagName);

                //Offset
                writer.Write(Offset);

                //Count
                writer.Write(Count);

                //DataType
                writer.Write((byte)DataType);

                //FullDataIdFieldList
                if (FullDataIdFieldList == null)
                {
                    writer.Write(false);
                }
                else
                {
                    writer.Write(true);
                    Serializer.Serialize(writer.BaseStream, FullDataIdFieldList);
                }

                //FullDataIdCollectionType
                writer.Write((byte)FullDataIdPartFormat);
            }
        }

        /// <summary>
        /// Deserialize the class data from a stream.
        /// </summary>
        /// <param name="reader">The <see cref="T:MySpace.Common.IO.IPrimitiveReader"/> that extracts used to extra data from a stream.</param>
        /// <param name="version">The value of <see cref="P:MySpace.Common.IVersionSerializable.CurrentVersion"/> that was written to the stream when it was originally serialized to a stream;
        /// the version of the <paramref name="reader"/> data.</param>
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            using (reader.CreateRegion())
            {
                //FullDataIdType
                FullDataIdType = (FullDataIdType)reader.ReadByte();

                //TagName
                TagName = reader.ReadString();

                if (version >= 2)
                {
                    //Offset
                    reader.ReadInt32();

                    //Count
                    reader.ReadInt32();

                    //DataType
                    DataType = (DataType)reader.ReadByte();

                    //FullDataIdFieldList
                    if (reader.ReadBoolean())
                    {
                        FullDataIdFieldList = new FullDataIdFieldList();
                        Serializer.Deserialize(reader.BaseStream, FullDataIdFieldList);
                    }

                    //FullDataIdPartFormat
                    FullDataIdPartFormat = (FullDataIdPartFormat)reader.ReadByte();
                }
            }
        }

        private const int CURRENT_VERSION = 2;
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