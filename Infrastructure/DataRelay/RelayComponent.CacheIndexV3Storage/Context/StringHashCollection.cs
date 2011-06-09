using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using System.Text;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class StringHashCollection
    {
        #region Data Members
        
        private readonly XmlDocument xmlDoc;
        private readonly string stringHashFile;
        private Dictionary<short /*TypeId*/, Dictionary<int /*StringHashCode*/, string /*String*/>> typeStringHashCollection;
        private static readonly Encoding enc = new UTF8Encoding(false, true);
        
        #endregion

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="StringHashCollection"/> class.
        /// </summary>
        /// <param name="stringHashFile">The string hash file.</param>
        internal StringHashCollection(string stringHashFile)
        {
            this.stringHashFile = stringHashFile;
            typeStringHashCollection = new Dictionary<short, Dictionary<int, string>>();
            xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(stringHashFile);
                PopulateStringHashMapping();
            }
            catch (FileNotFoundException)
            {
                xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null));
                xmlDoc.AppendChild(xmlDoc.CreateElement("TypeStringHashCollection"));
                xmlDoc.Save(stringHashFile);
            }
        }
        
        #endregion

        #region Methods

        /// <summary>
        /// Populates the string hash mapping.
        /// </summary>
        private void PopulateStringHashMapping()
        {
            XmlNode typeStringHashCollectionNode = xmlDoc.GetElementsByTagName("TypeStringHashCollection")[0];
            if (typeStringHashCollectionNode != null && typeStringHashCollectionNode.HasChildNodes)
            {
                foreach (XmlNode typeStringHashNode in typeStringHashCollectionNode.ChildNodes)
                {
                    short typeId = Convert.ToInt16(typeStringHashNode.Attributes["TypeId"].Value);

                    XmlNode stringHashMappingCollectionNode = typeStringHashNode["StringHashMappingCollection"];
                    if (stringHashMappingCollectionNode != null && stringHashMappingCollectionNode.HasChildNodes)
                    {
                        foreach (XmlNode stringHashMappingNode in stringHashMappingCollectionNode.ChildNodes)
                        {
                            if (!typeStringHashCollection.ContainsKey(typeId))
                            {
                                typeStringHashCollection.Add(typeId, new Dictionary<int, string>());
                            }
                            typeStringHashCollection[typeId].Add(Convert.ToInt32(stringHashMappingNode.Attributes["StringHashCode"].Value),
                                stringHashMappingNode.Attributes["String"].Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes type from TypeStringHashMapping in a thread safe manner
        /// </summary>
        /// <param name="typeId">TypeId to remove from TypeStringHashMapping</param>
        internal void RemoveType(short typeId)
        {
            try
            {
                if (typeStringHashCollection.ContainsKey(typeId))
                {
                    lock (typeStringHashCollection)
                    {
                        if (typeStringHashCollection.ContainsKey(typeId))
                        {
                            //Remove from dictionary
                            Dictionary<short, Dictionary<int, string>> newTypeStringHashCollection = new Dictionary<short, Dictionary<int, string>>(typeStringHashCollection);
                            newTypeStringHashCollection.Remove(typeId);
                            typeStringHashCollection = newTypeStringHashCollection;

                            //Remove from file
                            XmlNodeList typeStringHashNodeList = xmlDoc.GetElementsByTagName("TypeStringHashCollection")[0].ChildNodes;
                            XmlNode typeStringHashNode;
                            for (int i = 0; i < typeStringHashNodeList.Count; i++)
                            {
                                typeStringHashNode = typeStringHashNodeList[i];
                                if (string.Equals(typeStringHashNode.Attributes["TypeId"].Value, typeId.ToString()))
                                {
                                    typeStringHashNode.ParentNode.RemoveChild(typeStringHashNode);
                                    break;
                                }
                            }
                            xmlDoc.Save(stringHashFile);
                        }
                    }
                }
            }
            catch
            {
                LoggingUtil.Log.InfoFormat("Error removing strings for typeId : {0} from TypeStringHashMapping", typeId);
            }
        }

        /// <summary>
        /// Adds new stringName to TypeStringHashMapping in a thread safe manner
        /// </summary>
        /// <param name="typeId">TypeId for which new StringHash is added</param>
        /// <param name="stringArray">String</param>
        internal void AddStringArray(short typeId, byte[] stringArray)
        {
            try
            {
                if (!typeStringHashCollection.ContainsKey(typeId))
                {
                    lock (typeStringHashCollection)
                    {
                        if (!typeStringHashCollection.ContainsKey(typeId))
                        {
                            //Add TypeId
                            AddTypeId(typeId);
                        }
                    }
                }

                //Add String
                string str = enc.GetString(stringArray);
                int stringHashCode = StringUtility.GetStringHash(str);
                if (!typeStringHashCollection[typeId].ContainsKey(stringHashCode))
                {
                    lock (typeStringHashCollection)
                    {
                        if (!typeStringHashCollection[typeId].ContainsKey(stringHashCode))
                        {
                            AddString(typeId, stringHashCode, str);
                        }
                    }
                }
            }
            catch
            {
                LoggingUtil.Log.InfoFormat("Error adding string name : {0} for typeId : {1} to TypeStringHashMapping", enc.GetString(stringArray), typeId);
            }
        }

        /// <summary>
        /// Adds the type id.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        private void AddTypeId(short typeId)
        {
            Dictionary<short, Dictionary<int, string>> newTypeStringHashCollection = new Dictionary<short, Dictionary<int, string>>(typeStringHashCollection);
            newTypeStringHashCollection.Add(typeId, new Dictionary<int, string>());
            typeStringHashCollection = newTypeStringHashCollection;

            //Add to file
            XmlElement typeStringHashElement = xmlDoc.CreateElement("TypeStringHash");
            typeStringHashElement.SetAttribute("TypeId", typeId.ToString());
            typeStringHashElement.AppendChild(xmlDoc.CreateElement("StringHashMappingCollection"));
            xmlDoc.GetElementsByTagName("TypeStringHashCollection")[0].AppendChild(typeStringHashElement);
            PersistState();
        }

        /// <summary>
        /// Adds the string.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="stringHashCode">The string hash code.</param>
        /// <param name="stringName">Name of the string.</param>
        private void AddString(short typeId, int stringHashCode, string stringName)
        {
            typeStringHashCollection[typeId].Add(stringHashCode, stringName);

            // Add to file
            XmlNodeList typeStringHashNodeList = xmlDoc.GetElementsByTagName("TypeStringHashCollection")[0].ChildNodes;
            foreach (XmlNode typeStringHashNode in typeStringHashNodeList)
            {
                if (string.Equals(typeStringHashNode.Attributes["TypeId"].Value, typeId.ToString()))
                {
                    XmlElement stringHashMappingElement = xmlDoc.CreateElement("StringHashMapping");
                    stringHashMappingElement.SetAttribute("String", stringName);
                    stringHashMappingElement.SetAttribute("StringHashCode", stringHashCode.ToString());

                    typeStringHashNode.ChildNodes[0].AppendChild(stringHashMappingElement);
                    PersistState();
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the string byte array.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="hashCodeByteArray">The hash code byte array.</param>
        /// <returns>Byte Array of the string</returns>
        internal byte[] GetStringByteArray(short typeId, byte[] hashCodeByteArray)
        {
            string stringName;
            int stringHashCode = BitConverter.ToInt32(hashCodeByteArray, 0);
            if (!typeStringHashCollection[typeId].TryGetValue(stringHashCode, out stringName))
            {
                throw new Exception("'StringHashCode - " + stringHashCode + " not found for TypeId - " + typeId);
            }
            return enc.GetBytes(stringName);
        }

        /// <summary>
        /// Gets the hash code byte array.
        /// </summary>
        /// <param name="stringArray">The string array.</param>
        /// <returns>Byte Array of the hash code</returns>
        internal static byte[] GetHashCodeByteArray(byte[] stringArray)
        {
            return BitConverter.GetBytes(StringUtility.GetStringHash(enc.GetString(stringArray)));
        }

        /// <summary>
        /// Tries the get string names for the specified typeId.
        /// </summary>
        /// <param name="typeId">The typeId.</param>
        /// <param name="stringNames">The string names.</param>
        /// <returns><c>true</c>if typeId exists in the StringHashCollection; otherwise, </returns>
        internal bool TryGetStringNames(short typeId, out string[] stringNames)
        {
            stringNames = null;
            bool typeExists = false;
            Dictionary<int /*StringHashCode*/, string /*String*/> stringCollection;

            if (typeStringHashCollection.TryGetValue(typeId, out stringCollection))
            {
                typeExists = true;
                stringNames = new string[stringCollection.Count];
                stringCollection.Values.CopyTo(stringNames, 0);
            }
            return typeExists;
        }

        /// <summary>
        /// Persists the state.
        /// </summary>
        internal void PersistState()
        {
            xmlDoc.Save(stringHashFile);
        }
        #endregion
    }
}