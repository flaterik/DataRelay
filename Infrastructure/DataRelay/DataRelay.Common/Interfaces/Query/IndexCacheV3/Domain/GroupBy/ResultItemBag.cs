using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using Wintellect.PowerCollections;
using System.Collections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class ResultItemBag : IVersionSerializable, IEnumerable
    {
        #region Data Members

        public byte[] CompositeKey { get; private set; }

        private OrderedBag<ResultItem> ItemBag { get; set; }

        private BaseComparer BaseComparer { get; set; }

        #endregion

        #region Ctor

        public ResultItemBag()
        {
        }

        public ResultItemBag(BaseComparer baseComparer, byte[] compositeKey)
        {
            BaseComparer = baseComparer;
            CompositeKey = compositeKey;
            ItemBag = new OrderedBag<ResultItem>(BaseComparer);
        }

        #endregion

        #region Methods

        public void Add(ResultItem resultItem)
        {
            ItemBag.Add(resultItem);
        }

        public ResultItem First
        {
            get
            {
                return ItemBag.GetFirst();
            }
        }

        public int Count
        {
            get
            {
                return ItemBag.Count;
            }
        }

        public ResultItem this[int index]
        {
            get
            {
                return ItemBag[index];
            }
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
                //OrderedBag
                if (ItemBag == null || ItemBag.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)ItemBag.Count);
                    foreach (var resultItem in ItemBag)
                    {
                        resultItem.Serialize(writer);
                    }
                }
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
                //OrderedBag
                ushort count = reader.ReadUInt16();
                if (count > 0)
                {
                    ItemBag = new OrderedBag<ResultItem>(BaseComparer);
                    ResultItem resultItem;
                    for (int i = 0; i < count; i++)
                    {
                        resultItem = new ResultItem();
                        resultItem.Deserialize(reader);
                        ItemBag.Add(resultItem);
                    }
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

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return ItemBag.GetEnumerator();
        }

        #endregion
    }
}