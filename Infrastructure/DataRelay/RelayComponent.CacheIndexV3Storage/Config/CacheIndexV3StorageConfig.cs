using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using MySpace.BerkeleyDb.Configuration;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Enums;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using System.Text;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Config
{
    [XmlRoot("CacheIndexV3StorageConfiguration", Namespace = "http://myspace.com/CacheIndexV3StorageConfig.xsd")]
    public class CacheIndexV3StorageConfiguration
    {
        [XmlElement("CacheIndexV3StorageConfig")]
        public CacheIndexV3StorageConfig CacheIndexV3StorageConfig;

        [XmlElement("BerkeleyDbConfig", Namespace = "http://myspace.com/BerkeleyDbConfig.xsd")]
        public BerkeleyDbConfig BerkeleyDbConfig;

        public void InitializeCustomFields()
        {
            foreach (IndexTypeMapping indexTypeMapping in CacheIndexV3StorageConfig.IndexTypeMappingCollection)
            {
                indexTypeMapping.Initialize();
                foreach (Index index in indexTypeMapping.IndexCollection)
                {
                    index.InitializeLocalIdentityTagList();
                    index.InitializeStringHashCodeDictionary();
                    index.PrimarySortInfo.InitializeSortOrderList();
                }
            }
        }
    }

    public class CacheIndexV3StorageConfig
    {
        [XmlElement("StorageStateFile")]
        public string StorageStateFile;

        [XmlElement("TagHashFile")]
        public string TagHashFile;

        [XmlElement("StringHashFile")]
        public string StringHashFile;

        [XmlElement("LockMultiplier")]
        public int LockMultiplier;

        [XmlElement("SupportLegacySerialization")]
        public bool SupportLegacySerialization = true;

        [XmlArray("IndexTypeMappingCollection")]
        [XmlArrayItem(typeof(IndexTypeMapping))]
        public IndexTypeMappingCollection IndexTypeMappingCollection;
    }

    public class IndexTypeMappingCollection : KeyedCollection<short, IndexTypeMapping>
    {
        protected override short GetKeyForItem(IndexTypeMapping item)
        {
            return item.TypeId;
        }
    }

    public class IndexTypeMapping
    {
        [XmlElement("TypeId")]
        public short TypeId;

        [XmlElement("Mode")]
        public string ModeString;

        [XmlElement("MetadataStoredSeperately")]
        public bool MetadataStoredSeperately;

        [XmlElement("QueryOverrideSettings")]
        public QueryOverrideSettings QueryOverrideSettings;

        [XmlArray("FullDataIdPartCollection")]
        [XmlArrayItem("FullDataIdPart")]
        public Collection<FullDataIdPart> FullDataIdPartCollection;

        [XmlArray("IndexCollection")]
        [XmlArrayItem(typeof(Index))]
        public IndexCollection IndexCollection;

        internal void Initialize()
        {
            InitializeServerMode();
            InitializeFullDataIdFieldList();
        }

        public IndexServerMode IndexServerMode;
        internal void InitializeServerMode()
        {
            IndexServerMode = (IndexServerMode)Enum.Parse(typeof(IndexServerMode), ModeString, true);
        }

        public FullDataIdFieldList FullDataIdFieldList;
        internal void InitializeFullDataIdFieldList()
        {
            FullDataIdFieldList = ProcessFullDataIdPartCollection(FullDataIdPartCollection);
        }

        private FullDataIdFieldList ProcessFullDataIdPartCollection(Collection<FullDataIdPart> fullDataIdPartCollection)
        {
            FullDataIdFieldList fullDataIdFieldList = null;
            if (fullDataIdPartCollection != null && fullDataIdPartCollection.Count > 0)
            {
                fullDataIdFieldList = new FullDataIdFieldList();
                foreach (FullDataIdPart fullDataIdPart in fullDataIdPartCollection)
                {
                    fullDataIdFieldList.Add(ProcessFullDataIdPart(fullDataIdPart));
                }

                // Verify MinMax FullDataIdPart
                if (fullDataIdFieldList[0].FullDataIdPartFormat == FullDataIdPartFormat.MinMax)
                {
                    StringBuilder sb = new StringBuilder();
                    if (fullDataIdFieldList[0].DataType != fullDataIdFieldList[1].DataType)
                    {
                        sb.Append("TypeId - ").Append(TypeId).Append(", MinMax FullDataIdPart 1 DataType : ").Append(fullDataIdFieldList[0].DataType).
                            Append(" does not match with MinMax FullDataIdPart 2 DataType : ").Append(fullDataIdFieldList[1].DataType).Append(Environment.NewLine);
                    }
                    if (fullDataIdFieldList[0].Count != fullDataIdFieldList[1].Count)
                    {
                        sb.Append("TypeId - ").Append(TypeId).Append(", MinMax FullDataIdPart 1 Count : ").Append(fullDataIdFieldList[0].Count).
                            Append(" does not match with MinMax FullDataIdPart 2 Count : ").Append(fullDataIdFieldList[1].Count);
                    }
                    if (sb.Length > 0)
                    {
                        string str = sb.ToString();
                        LoggingUtil.Log.Error(str);
                        throw new Exception(str);
                    }
                }
            }
            return fullDataIdFieldList;
        }

        private FullDataIdField ProcessFullDataIdPart(FullDataIdPart fullDataIdPart)
        {
            FullDataIdField fullDataIdField = new FullDataIdField
            {
                FullDataIdPartFormat = String.IsNullOrEmpty(fullDataIdPart.Format)
                ? FullDataIdPartFormat.Sequential
                : (FullDataIdPartFormat)Enum.Parse(typeof(FullDataIdPartFormat), fullDataIdPart.Format, true)
            };

            if (fullDataIdPart.FullDataIdPartCollection != null && fullDataIdPart.FullDataIdPartCollection.Count > 0)
            {
                fullDataIdField.FullDataIdFieldList = ProcessFullDataIdPartCollection(fullDataIdPart.FullDataIdPartCollection);
            }
            else
            {
                fullDataIdField.Count = fullDataIdPart.Count;
                fullDataIdField.Offset = fullDataIdPart.Offset;
                fullDataIdField.DataType = string.IsNullOrEmpty(fullDataIdPart.DataType)
                                               ? DataType.ByteArray
                                               : (DataType)Enum.Parse(typeof(DataType), fullDataIdPart.DataType, true);


                int dataTypeSize = DataTypeSize.Size[fullDataIdField.DataType];
                if (dataTypeSize > -1 && dataTypeSize != fullDataIdField.Count)
                {
                    string expStr = string.Format("TypeId - {0}, FullDataIdPart.PartName : {1}, FullDataIdPart.DataType :  {2}, DataTypeSize = {3} does not match (FullDataIdPart.Count ({4}) - FullDataIdPart.Offset ({5}))",
                            TypeId, fullDataIdPart.PartName, fullDataIdField.DataType, dataTypeSize, fullDataIdField.Count, fullDataIdField.Offset);
                    LoggingUtil.Log.Error(expStr);
                    throw new Exception(expStr);
                }

                if (fullDataIdPart.IsTag)
                {
                    fullDataIdField.FullDataIdType = FullDataIdType.Tag;
                    fullDataIdField.TagName = fullDataIdPart.PartName;
                }
                else if (String.Compare(fullDataIdPart.PartName, "IndexId", true) == 0)
                {
                    fullDataIdField.FullDataIdType = FullDataIdType.IndexId;
                }
                else if (String.Compare(fullDataIdPart.PartName, "ItemId", true) == 0)
                {
                    fullDataIdField.FullDataIdType = FullDataIdType.ItemId;
                }
                else
                {
                    string expStr = string.Format("TypeId - {0}, Invalid FullDataIdPart.PartName : {1} in the configuration", TypeId, fullDataIdPart.PartName);
                    LoggingUtil.Log.Error(expStr);
                    throw new Exception(expStr);
                }

            }
            return fullDataIdField;
        }
    }

    public class QueryOverrideSettings
    {
        [XmlElement("MaxItemsPerIndexThreshold")]
        public int MaxItemsPerIndexThreshold;

        [XmlElement("MaxResultItemsThresholdLog")]
        public int MaxResultItemsThresholdLog;

        [XmlElement("DisableFullPageQuery")]
        public bool DisableFullPageQuery;
    }

    public class FullDataIdPart
    {
        [XmlAttribute("Format")]
        public string Format;

        [XmlElement("IsTag")]
        public bool IsTag;

        [XmlElement("PartName")]
        public string PartName;

        [XmlElement("Offset")]
        public int Offset;

        [XmlElement("Count")]
        public int Count;

        [XmlElement("DataType")]
        public string DataType;

        [XmlArray("FullDataIdPartCollection")]
        [XmlArrayItem("FullDataIdPart")]
        public Collection<FullDataIdPart> FullDataIdPartCollection;
    }

    public class IndexCollection : KeyedCollection<string, Index>
    {
        protected override string GetKeyForItem(Index item)
        {
            return item.IndexName;
        }
    }

    public class Index
    {
        [XmlElement("IndexName")]
        public string IndexName;

        [XmlElement("ExtendedIdSuffix")]
        public short ExtendedIdSuffix;

        [XmlElement("MaxIndexSize")]
        public int MaxIndexSize;

        [XmlElement("TrimFromTail")]
        public bool TrimFromTail;

        [XmlElement("PrimarySortInfo")]
        public PrimarySortInfo PrimarySortInfo;

        [XmlElement("MetadataPresent")]
        public bool MetadataPresent;

        [XmlArray("TagCollection")]
        [XmlArrayItem(typeof(Tag))]
        public TagCollection TagCollection;

        private List<string> localIdentityTagList;
        internal List<string> LocalIdentityTagList
        {
            get
            {
                return localIdentityTagList;
            }
        }

        internal void InitializeLocalIdentityTagList()
        {
            localIdentityTagList = new List<string>();
            foreach (Tag tag in TagCollection)
            {
                if (tag.LocalIdentity)
                {
                    localIdentityTagList.Add(tag.Name);
                }
            }
        }

        private Dictionary<int /*TagHashCode*/, bool> stringHashCodeDictionary;
        internal Dictionary<int /*TagHashCode*/, bool> StringHashCodeDictionary
        {
            get
            {
                return stringHashCodeDictionary;
            }
        }

        internal void InitializeStringHashCodeDictionary()
        {
            stringHashCodeDictionary = new Dictionary<int, bool>();
            foreach (Tag tag in TagCollection)
            {
                if (tag.StringHash && tag.DataType == DataType.String)
                {
                    stringHashCodeDictionary.Add(StringUtility.GetStringHash(tag.Name), true);
                }
            }
        }
    }

    public class PrimarySortInfo
    {
        [XmlElement("IsTag")]
        public bool IsTag;

        [XmlElement("FieldName")]
        public string FieldName;

        [XmlArray("SortOrderStructureCollection")]
        [XmlArrayItem(typeof(SortOrderStructure))]
        public Collection<SortOrderStructure> SortOrderStructureCollection;

        private List<SortOrder> sortOrderList;
        internal List<SortOrder> SortOrderList
        {
            get
            {
                return sortOrderList;
            }
        }

        public void InitializeSortOrderList()
        {
            sortOrderList = new List<SortOrder>(SortOrderStructureCollection.Count);
            SortOrder sortOrder;
            foreach (SortOrderStructure sortOrderStructure in SortOrderStructureCollection)
            {
                sortOrder = new SortOrder();
                //Set DataType
                try
                {
                    sortOrder.DataType = (DataType)Enum.Parse(typeof(DataType), sortOrderStructure.DataType);
                }
                catch
                {
                    throw new Exception("DataType " + sortOrderStructure.DataType + "NOT supported");
                }

                //Set SortBy
                try
                {
                    sortOrder.SortBy = (SortBy)Enum.Parse(typeof(SortBy), sortOrderStructure.SortBy);
                }
                catch
                {
                    throw new Exception("SortBy " + sortOrderStructure.SortBy + "NOT supported");
                }
                sortOrderList.Add(sortOrder);
            }
        }
    }

    public class SortOrderStructure
    {
        [XmlElement("DataType")]
        public string DataType;

        [XmlElement("SortBy")]
        public string SortBy;
    }

    public class TagCollection : KeyedCollection<string, Tag>
    {
        protected override string GetKeyForItem(Tag item)
        {
            return item.Name;
        }
    }

    public class Tag
    {
        [XmlElement("Name")]
        public string Name;

        [XmlElement("DataType")]
        public DataType DataType;

        [XmlElement("LocalIdentity")]
        public bool LocalIdentity;

        [XmlElement("StringHash")]
        public bool StringHash;
    }
}