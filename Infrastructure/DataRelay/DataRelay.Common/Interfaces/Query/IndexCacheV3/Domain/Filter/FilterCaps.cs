using System.Collections.ObjectModel;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using System;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class FilterCaps : KeyedCollection<byte[], FilterCap>, IVersionSerializable
    {
        #region Ctor
        
        public FilterCaps() : base(new ByteArrayEqualityComparer())
        {
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
                if (Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)Count);
                    foreach (FilterCap filterCap in this)
                    {
                        //FilterCap
                        Serializer.Serialize(writer.BaseStream, filterCap);
                    }
                }
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
                FilterCap filterCap;
                ushort count = reader.ReadUInt16();

                for (ushort i = 0; i < count; i++)
                {
                    //FilterCap
                    filterCap = new FilterCap();
                    Serializer.Deserialize(reader.BaseStream, filterCap);
                    Add(filterCap);
                }
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

        #region Methods

        /// <summary>
        /// When implemented in a derived class, extracts the key from the specified element.
        /// </summary>
        /// <param name="item">The element from which to extract the key.</param>
        /// <returns>The key for the specified element.</returns>
        protected override byte[] GetKeyForItem(FilterCap item)
        {
            return item.FieldValue;
        }


        /// <summary>
        /// Gets the FilterCap associated with the specified fieldValue.
        /// </summary>
        /// <param name="fieldValue">The field value.</param>
        /// <param name="filterCap">The filter cap.</param>
        /// <returns><c>true</c> if FilterCap associated with the specified fieldValue is present in FilterCaps; otherwise, <c>false</c></returns>
        public bool TryGetValue(byte[] fieldValue, out FilterCap filterCap)
        {
            if(Contains(fieldValue))
            {
                filterCap = this[fieldValue];
                return true;
            }
            filterCap = null;
            return false;
        }

        public override string ToString()
        {
            var stb = new StringBuilder();
            stb.Append("(").Append("FilterCaps Count: ").Append(Count);
            if (Count > 0)
            {
                foreach (var filterCap in this)
                {
                    stb.Append("(").Append("FilterCap: ").Append(filterCap.ToString()).Append("),");
                }
            }
            stb.Append("),");
            return stb.ToString();
        }

        #endregion
    }
}