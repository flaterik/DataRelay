using System;
using System.Collections.Generic;
using MySpace.Common;
using MySpace.Common.IO;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MetadataPropertyCollectionUpdate : IVersionSerializable
    {
        #region Ctor

        public MetadataPropertyCollectionUpdate()
        {
            AddIndexPropertyCollection = new Dictionary<string, byte[]>();
            DeleteIndexPropertyCollection = new HashSet<string>();
        }

        public MetadataPropertyCollectionUpdate(Dictionary<string, byte[]> addIndexPropertyCollection)
        {
            // TBD: consider 
            // - making this constructor internal
            // - assigning 'addIndexPropertyCollection' DIRECTLY to 'AddIndexPropertyCollection' without allocating a dictionary
            AddIndexPropertyCollection = addIndexPropertyCollection == null
                                             ? new Dictionary<string, byte[]>()
                                             : AddIndexPropertyCollection =
                                               new Dictionary<string, byte[]>(addIndexPropertyCollection);

            DeleteIndexPropertyCollection = new HashSet<string>();
        }

        #endregion

        #region Data Members

        /// <summary>
        /// Gets or sets the add index property collection.
        /// </summary>
        /// <value>The add index property collection.</value>
        internal Dictionary<string, byte[]> AddIndexPropertyCollection
        {
            get; private set;
        }

        /// <summary>
        /// Gets or sets the delete index property collection.
        /// </summary>
        /// <value>The delete index property collection.</value>
        internal HashSet<string> DeleteIndexPropertyCollection
        {
            get; private set;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Adds a property to AddIndexPropertyCollection.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <param name="propertyValue">Value of the property.</param>
        /// <returns><c>true</c> if the addition was successful; otherwise, <c>false</c></returns>
        public bool AddToAddIndexPropertyCollection(string propertyName, byte[] propertyValue)
        {
            try
            {
                if (DeleteIndexPropertyCollection.Contains(propertyName))
                {
                    return false;
                }
                AddIndexPropertyCollection.Add(propertyName, propertyValue);
            }
            catch (ArgumentException)
            {
                //Silently fail if the key already exists
                return false;
            }
            return true;
        }

        /// <summary>
        /// Adds a property to DeleteIndexPropertyCollection.
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns><c>true</c> if the addition was successful; otherwise, <c>false</c></returns>
        public bool AddToDeleteIndexPropertyCollection(string propertyName)
        {
            try
            {
                if (AddIndexPropertyCollection.ContainsKey(propertyName))
                {
                    //take it out of add list and add it to the delete list
                    AddIndexPropertyCollection.Remove(propertyName);
                }
                DeleteIndexPropertyCollection.Add(propertyName);
            }
            catch (ArgumentException)
            {
                //Silently fail if the key already exists
                return false;
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
            //AddIndexPropertyCollection
            if (AddIndexPropertyCollection == null || AddIndexPropertyCollection.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)AddIndexPropertyCollection.Count);
                foreach (KeyValuePair<string /*PropertyName*/, byte[] /*PropertyValue*/> kvp in AddIndexPropertyCollection)
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

            //DeleteIndexPropertyCollection
            if (DeleteIndexPropertyCollection == null || DeleteIndexPropertyCollection.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)DeleteIndexPropertyCollection.Count);
                foreach (string propertyName in DeleteIndexPropertyCollection)
                {
                    writer.Write(propertyName);
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
            //AddIndexPropertyCollection
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                AddIndexPropertyCollection = new Dictionary<string, byte[]>(count);
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
                    AddIndexPropertyCollection.Add(propertyName, propertyValue);
                }
            }

            //DeleteIndexPropertyCollection
            count = reader.ReadUInt16();
            if (count > 0)
            {
                DeleteIndexPropertyCollection = new HashSet<string>();
                for (ushort i = 0; i < count; i++)
                {
                    DeleteIndexPropertyCollection.Add(reader.ReadString());
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
