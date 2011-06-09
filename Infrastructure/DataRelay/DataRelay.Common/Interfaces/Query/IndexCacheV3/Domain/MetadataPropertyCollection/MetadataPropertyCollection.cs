using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MetadataPropertyCollection : Dictionary<string, byte[]>, IVersionSerializable
    {
        #region Methods

        /// <summary>
        /// Processes the specified MetadataPropertyCollectionUpdate.
        /// </summary>
        /// <param name="metadataPropertyCollectionUpdate">The MetadataPropertyCollectionUpdate.</param>
        /// <returns><c>true</c> if MetadataPropertyCollectionUpdate is processed successfully; otherwise, <c>false</c></returns>
        internal bool Process(MetadataPropertyCollectionUpdate metadataPropertyCollectionUpdate)
        {
            try
            {
                //Process Deletes
                if (metadataPropertyCollectionUpdate.DeleteIndexPropertyCollection != null &&
                    metadataPropertyCollectionUpdate.DeleteIndexPropertyCollection.Count > 0 &&
                    Count > 0)
                {
                    foreach (string propertyName in metadataPropertyCollectionUpdate.DeleteIndexPropertyCollection)
                    {
                        Remove(propertyName);
                    }
                }

                //Process Adds
                if (metadataPropertyCollectionUpdate.AddIndexPropertyCollection != null &&
                    metadataPropertyCollectionUpdate.AddIndexPropertyCollection.Count > 0)
                {
                    foreach (var kvp in metadataPropertyCollectionUpdate.AddIndexPropertyCollection)
                    {
                        this[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error processing MetadataPropertyCollectionUpdate.", ex);
            }
            return true;
        }

        #endregion

        #region IVersionSerializable Members

        /// <summary>
        /// Serialize the class data to a stream.
        /// </summary>
        /// <param name="writer">The <see cref="T:MySpace.Common.IO.IPrimitiveWriter"/> that writes to the stream.</param>
        public void Serialize(IPrimitiveWriter writer)
        {
            if (Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)Count);
                foreach (KeyValuePair<string /*PropertyName*/, byte[] /*PropertyValue*/> kvp in this)
                {
                    writer.Write(kvp.Key);
                    if (kvp.Value == null || kvp.Value.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Value.Length);
                        writer.Write(kvp.Value);
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
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                string propertyName;
                byte[] propertyValue;
                ushort propertyValueLen;

                for (ushort i = 0; i < count; i++)
                {
                    propertyName = reader.ReadString();
                    propertyValueLen = reader.ReadUInt16();
                    propertyValue = null;
                    if (propertyValueLen > 0)
                    {
                        propertyValue = reader.ReadBytes(propertyValueLen);
                    }
                    Add(propertyName, propertyValue);
                }
            }
        }

        /// <summary>
        /// Gets the current serialization data version of your object.  The <see cref="M:MySpace.Common.IVersionSerializable.Serialize(MySpace.Common.IO.IPrimitiveWriter)"/> method
        /// will write to the stream the correct format for this version.
        /// </summary>
        /// <value></value>
        public int CurrentVersion
        {
            get
            {
                return 1;
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