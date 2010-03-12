using System;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.Logging;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FilterCap : IVersionSerializable
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the field value.
        /// </summary>
        /// <value>The field value.</value>
        public byte[] FieldValue
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use parent filter for the FilterCap.
        /// </summary>
        /// <value><c>true</c> if parent filter is to be used for the FilterCap; otherwise, <c>false</c>.</value>
        public bool UseParentFilter
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the capping filter.
        /// </summary>
        /// <value>The filter.</value>
        public Filter Filter
        { 
            get; set;
        }

        /// <summary>
        /// Gets or sets the cap.
        /// </summary>
        /// <value>The cap.</value>
        public int Cap
        { 
            get; set;
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
                //FieldValue
                if (FieldValue == null || FieldValue.Length == 0)
                {
                    new LogWrapper().Error("FieldValue in FilterCaps cannot be null or zero length byte array");
                    throw new Exception("FieldValue in FilterCaps cannot be null or zero length byte array");
                }
                writer.Write((ushort)FieldValue.Length);
                writer.Write(FieldValue);

                //UseParentFilter
                writer.Write(UseParentFilter);

                //Filter
                if (Filter == null)
                {
                    writer.Write((byte)0);
                }
                else
                {
                    writer.Write((byte)Filter.FilterType);
                    Serializer.Serialize(writer.BaseStream, Filter);
                }

                //Cap
                writer.Write(Cap);
            }
        }

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="reader"></param>
        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
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
                //FieldValue
                ushort len = reader.ReadUInt16();
                if (len > 0)
                {
                    FieldValue = reader.ReadBytes(len);
                }
                else
                {
                    new LogWrapper().Error("FieldValue in FilterCaps cannot be null or zero length byte array");
                    throw new Exception("FieldValue in FilterCaps cannot be null or zero length byte array");
                }

                //UseParentFilter
                UseParentFilter = reader.ReadBoolean();

                //Filter
                byte b = reader.ReadByte();
                if (b != 0)
                {
                    Filter = FilterFactory.CreateFilter(reader, (FilterType)b);
                }

                //Cap
                Cap = reader.ReadInt32();
            }
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
    }
}