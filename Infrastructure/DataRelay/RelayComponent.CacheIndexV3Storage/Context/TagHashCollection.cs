using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context
{
    internal class TagHashCollection
    {
        #region Data Members

        private readonly XmlDocument xmlDoc;
        private readonly string tagHashFile;
        private Dictionary<short /*TypeId*/, Dictionary<int /*TagHashCode*/, string /*TagName*/>> typeTagHashCollection;
        
        #endregion

        #region Ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="TagHashCollection"/> class.
        /// </summary>
        /// <param name="tagHashFile">The tag hash file.</param>
        internal TagHashCollection(string tagHashFile)
        {
            this.tagHashFile = tagHashFile;
            typeTagHashCollection = new Dictionary<short, Dictionary<int, string>>();
            xmlDoc = new XmlDocument();
            try
            {
                xmlDoc.Load(tagHashFile);
                PopulateTypeTagHashMapping();
            }
            catch (FileNotFoundException)
            {
                xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null));
                xmlDoc.AppendChild(xmlDoc.CreateElement("TypeTagHashCollection"));
                xmlDoc.Save(tagHashFile);
            }
        }

        #endregion

        #region Methods   

        /// <summary>
        /// Populates the type tag hash mapping.
        /// </summary>
        private void PopulateTypeTagHashMapping()
        {
            XmlNode typeTagHashCollectionNode = xmlDoc.GetElementsByTagName("TypeTagHashCollection")[0];
            if (typeTagHashCollectionNode != null && typeTagHashCollectionNode.HasChildNodes)
            {
                foreach (XmlNode typeTagHashNode in typeTagHashCollectionNode.ChildNodes)
                {
                    short typeId = Convert.ToInt16(typeTagHashNode.Attributes["TypeId"].Value);

                    XmlNode tagHashMappingCollectionNode = typeTagHashNode["TagHashMappingCollection"];
                    if (tagHashMappingCollectionNode != null && tagHashMappingCollectionNode.HasChildNodes)
                    {
                        foreach (XmlNode tagHashMappingNode in tagHashMappingCollectionNode.ChildNodes)
                        {
                            if (!typeTagHashCollection.ContainsKey(typeId))
                            {
                                typeTagHashCollection.Add(typeId, new Dictionary<int, string>());
                            }
                            typeTagHashCollection[typeId].Add(
                                Convert.ToInt32(tagHashMappingNode.Attributes["TagHashCode"].Value),
                                tagHashMappingNode.Attributes["TagName"].Value);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes type from TypeTagHashMapping in a thread safe manner
        /// </summary>
        /// <param name="typeId">TypeId to remove from TypeTagHashMapping</param>
        internal void RemoveType(short typeId)
        {
            try
            {
                if (typeTagHashCollection.ContainsKey(typeId))
                {
                    lock (typeTagHashCollection)
                    {
                        if (typeTagHashCollection.ContainsKey(typeId))
                        {
                            //Remove from dictionary
                            Dictionary<short, Dictionary<int, string>> newTypeTagHashCollection = new Dictionary<short, Dictionary<int, string>>(typeTagHashCollection);
                            newTypeTagHashCollection.Remove(typeId);
                            typeTagHashCollection = newTypeTagHashCollection;

                            //Remove from file
                            XmlNodeList typeTagHashNodeList = xmlDoc.GetElementsByTagName("TypeTagHashCollection")[0].ChildNodes;
                            XmlNode typeTagHashNode;
                            for (int i = 0; i < typeTagHashNodeList.Count; i++)
                            {
                                typeTagHashNode = typeTagHashNodeList[i];
                                if (string.Equals(typeTagHashNode.Attributes["TypeId"].Value, typeId.ToString()))
                                {
                                    typeTagHashNode.ParentNode.RemoveChild(typeTagHashNode);
                                    break;
                                }
                            }
                            xmlDoc.Save(tagHashFile);
                        }
                    }
                }
            }
            catch
            {
                LoggingUtil.Log.InfoFormat("Error removing tags for typeId : {0} from TypeTagHashMapping", typeId);
            }
        }

        /// <summary>
        /// Adds new tagName to TypeTagHashMapping in a thread safe manner
        /// </summary>
        /// <param name="typeId">TypeId for which new TagHash is added</param>
        /// <param name="tagName">TagName</param>
        internal void AddTag(short typeId, string tagName)
        {
            try
            {
                if (!typeTagHashCollection.ContainsKey(typeId))
                {
                    lock (typeTagHashCollection)
                    {
                        if (!typeTagHashCollection.ContainsKey(typeId))
                        {
                            //Add TypeId
                            AddTypeId(typeId);
                        }
                    }
                }

                //Add TagName
                int tagHashCode = StringUtility.GetStringHash(tagName);
                if (!typeTagHashCollection[typeId].ContainsKey(tagHashCode))
                {
                    lock (typeTagHashCollection)
                    {
                        if (!typeTagHashCollection[typeId].ContainsKey(tagHashCode))
                        {
                            AddTagName(typeId, tagHashCode, tagName);
                        }
                    }
                }
            }
            catch
            {
                LoggingUtil.Log.InfoFormat("Error adding tag name : {0} for typeId : {1} to TypeTagHashMapping", tagName, typeId);
            }
        }

        /// <summary>
        /// Adds the type id.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        private void AddTypeId(short typeId)
        {
            Dictionary<short, Dictionary<int, string>> newTypeTagHashCollection = new Dictionary<short, Dictionary<int, string>>(typeTagHashCollection);
            newTypeTagHashCollection.Add(typeId, new Dictionary<int, string>());
            typeTagHashCollection = newTypeTagHashCollection;

            //Add to file
            XmlElement typeTagHashElement = xmlDoc.CreateElement("TypeTagHash");
            typeTagHashElement.SetAttribute("TypeId", typeId.ToString());
            typeTagHashElement.AppendChild(xmlDoc.CreateElement("TagHashMappingCollection"));
            xmlDoc.GetElementsByTagName("TypeTagHashCollection")[0].AppendChild(typeTagHashElement);
            PersistState();
        }

        /// <summary>
        /// Adds the name of the tag.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="tagHashCode">The tag hash code.</param>
        /// <param name="tagName">Name of the tag.</param>
        private void AddTagName(short typeId, int tagHashCode, string tagName)
        {
            typeTagHashCollection[typeId].Add(tagHashCode, tagName);

            // Add to file
            XmlNodeList typeTagHashNodeList = xmlDoc.GetElementsByTagName("TypeTagHashCollection")[0].ChildNodes;
            foreach (XmlNode typeTagHashNode in typeTagHashNodeList)
            {
                if (string.Equals(typeTagHashNode.Attributes["TypeId"].Value, typeId.ToString()))
                {
                    XmlElement tagHashMappingElement = xmlDoc.CreateElement("TagHashMapping");
                    tagHashMappingElement.SetAttribute("TagName", tagName);
                    tagHashMappingElement.SetAttribute("TagHashCode", tagHashCode.ToString());

                    typeTagHashNode.ChildNodes[0].AppendChild(tagHashMappingElement);
                    PersistState();
                    break;
                }
            }
        }

        /// <summary>
        /// Gets the name of the tag.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="tagHashCode">The tag hash code.</param>
        /// <returns>Tag</returns>
        internal string GetTagName(short typeId, int tagHashCode)
        {
            string tagName;
            if (!typeTagHashCollection[typeId].TryGetValue(tagHashCode, out tagName))
            {
                throw new Exception("'TagHashCode - " + tagHashCode + " not found for TypeId - " + typeId);
            }
            return tagName;
        }

        /// <summary>
        /// Gets the tag hash code.
        /// </summary>
        /// <param name="tagName">Name of the tag.</param>
        /// <returns>HashCode</returns>
        internal static int GetTagHashCode(string tagName)
        {
            return StringUtility.GetStringHash(tagName);
        }

        /// <summary>
        /// Tries the get tag names for the specified typeId.
        /// </summary>
        /// <param name="typeId">The typeId.</param>
        /// <param name="tagNames">The tag names.</param>
        /// <returns><c>true</c>if typeId exists in the TagHashCollection; otherwise, </returns>
        internal bool TryGetTagNames(short typeId, out string[] tagNames)
        {
            tagNames = null;
            bool typeExists = false;
            Dictionary<int /*TagHashCode*/, string /*TagName*/> tagCollection;

            if(typeTagHashCollection.TryGetValue(typeId, out tagCollection))
            {
                typeExists = true;
                tagNames = new string[tagCollection.Count];
                tagCollection.Values.CopyTo(tagNames, 0);
            }
            return typeExists;
        }

        /// <summary>
        /// Persists the state.
        /// </summary>
        internal void PersistState()
        {
            xmlDoc.Save(tagHashFile);
        }
        #endregion
    }
}