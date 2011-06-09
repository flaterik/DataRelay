using System.Collections;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class GroupByResult : IVersionSerializable, IEnumerable
    {
        #region Data Members

        private Dictionary<byte[], ResultItemBag> KeyBagMapping { get; set; }

        private SortedResultItemBagList SortedResultItemBagList { get; set; }

        private BaseComparer BaseComparer { get; set; }
        #endregion

        #region Ctor

        public GroupByResult()
        {
        }

        public GroupByResult(BaseComparer baseComparer)
        {
            BaseComparer = baseComparer;
            KeyBagMapping = new Dictionary<byte[], ResultItemBag>(new ByteArrayEqualityComparer());
            SortedResultItemBagList = new SortedResultItemBagList(BaseComparer);
        }

        #endregion

        #region Methods

        public ResultItemBag this[int index]
        {
            get
            {
                return SortedResultItemBagList[index];
            }
        }

        public ResultItemBag this[byte[] compositeKey]
        {
            get
            {
                return KeyBagMapping[compositeKey];
            }
        }

        public bool Add(byte[] compositeKey, ResultItem resultItem)
        {
            bool retVal = false;
            ResultItemBag resultItemBag;
            if (KeyBagMapping.TryGetValue(compositeKey, out resultItemBag))
            {
                SortedResultItemBagList.Remove(resultItemBag);
                resultItemBag.Add(resultItem);
                retVal = true;
            }
            else
            {
                resultItemBag = new ResultItemBag(BaseComparer, compositeKey);
                resultItemBag.Add(resultItem);
                KeyBagMapping.Add(compositeKey, resultItemBag);
            }
            SortedResultItemBagList.Add(resultItemBag);
            return retVal;
        }

        public bool Add(byte[] compositeKey, ResultItemBag resultItemBag)
        {
            bool retVal = false;
            ResultItemBag storedResultItemBag;
            if (KeyBagMapping.TryGetValue(compositeKey, out storedResultItemBag))
            {
                SortedResultItemBagList.Remove(storedResultItemBag);
                foreach (ResultItem resultItem in resultItemBag)
                {
                    storedResultItemBag.Add(resultItem);
                }
                SortedResultItemBagList.Add(storedResultItemBag);
                retVal = true;
            }
            else
            {
                KeyBagMapping.Add(compositeKey, resultItemBag);
                SortedResultItemBagList.Add(resultItemBag);
            }
            return retVal;
        }

        public ResultItemBag First
        {
            get
            {
                return SortedResultItemBagList.First;
            }
        }

        public int Count
        {
            get
            {
                return KeyBagMapping.Count;
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
                //KeyBagMapping
                if (KeyBagMapping == null || KeyBagMapping.Count == 0)
                {
                    writer.Write((ushort)0);
                }
                else
                {
                    writer.Write((ushort)KeyBagMapping.Count);
                    foreach (var keyBag in KeyBagMapping)
                    {
                        //CompositeKey
                        if (keyBag.Key == null || keyBag.Key.Length == 0)
                        {
                            writer.Write((ushort)0);
                        }
                        else
                        {
                            writer.Write((ushort)keyBag.Key.Length);
                            writer.Write(keyBag.Key);

                            //ResultItemBag
                            if (keyBag.Value == null || keyBag.Value.Count == 0)
                            {
                                writer.Write(false);
                            }
                            else
                            {
                                writer.Write(true);
                                Serializer.Serialize(writer.BaseStream, keyBag.Value);
                            }
                        }
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
                //KeyBagMapping
                ushort count = reader.ReadUInt16();
                if (count > 0)
                {
                    ResultItemBag resultItemBag = null;
                    byte[] compositeKey;
                    ushort len;
                    KeyBagMapping = new Dictionary<byte[], ResultItemBag>(new ByteArrayEqualityComparer());
                    for (int i = 0; i < count; i++)
                    {
                        len = reader.ReadUInt16();
                        if (len > 0)
                        {
                            //CompositeKey
                            compositeKey = reader.ReadBytes(len);

                            //ResultItemBag
                            if (reader.ReadBoolean())
                            {
                                resultItemBag = new ResultItemBag(BaseComparer, compositeKey);
                                Serializer.Deserialize(reader.BaseStream, resultItemBag);
                            }
                            KeyBagMapping.Add(compositeKey, resultItemBag);
                            SortedResultItemBagList.Add(resultItemBag);
                        }
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
            return SortedResultItemBagList.GetEnumerator();
        }

        #endregion
    }
}