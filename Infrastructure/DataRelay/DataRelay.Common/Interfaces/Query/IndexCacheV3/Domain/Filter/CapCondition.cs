using System.Text;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class CapCondition : IVersionSerializable
    {
        #region Data Members

        /// <summary>
        /// Gets or sets the name of the field.
        /// </summary>
        /// <value>The name of the field.</value>
        public string FieldName
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the filter caps.
        /// </summary>
        /// <value>The filter caps.</value>
        public FilterCaps FilterCaps
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore non capped items or not.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if non capped items are to be ignored; otherwise, <c>false</c>.
        /// </value>
        public bool IgnoreNonCappedItems
        {
            get; set;
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("FieldName: ").Append(FieldName).Append("),");
            stb.Append("(").Append("FilterCaps: ").Append(FilterCaps == null ? "Null" : FilterCaps.ToString()).Append("),");
            stb.Append("(").Append("IgnoreNonCappedItems: ").Append(IgnoreNonCappedItems).Append("),");
            return stb.ToString();
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
                //FieldName
                writer.Write(FieldName);

                //FilterCaps
                Serializer.Serialize(writer.BaseStream, FilterCaps);

                //IgnoreNonCappedItems
                writer.Write(IgnoreNonCappedItems);
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
                //FieldName
                FieldName = reader.ReadString();

                //FilterCaps
                FilterCaps = new FilterCaps();
                Serializer.Deserialize(reader.BaseStream, FilterCaps);

                if (version >= 2)
                {
                    //IgnoreNonCappedItems
                    IgnoreNonCappedItems = reader.ReadBoolean();
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
    }
}