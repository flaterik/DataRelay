using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IntersectionQuery : IPrimaryRelayMessageQuery
    {
        #region Data Members

        public string TargetIndexName { get; set; }

        public List<byte[]> IndexIdList { get; set; }

        public bool ExcludeData { get; set; }

        public bool GetIndexHeader { get; set; }

        public List<int> PrimaryIdList { get; set; }

        public Filter Filter { get; set; }

        internal Dictionary<byte[] /*IndexId*/, IntersectionQueryParams /*IntersectionQueryParams*/> intersectionQueryParamsMapping;
        /// <summary>
        /// If IntersectionQueryParams is specified then it will override Filter from the query
        /// </summary>
        public Dictionary<byte[] /*IndexId*/, IntersectionQueryParams /*IntersectionQueryParams*/> IntersectionQueryParamsMapping
        {
            get
            {
                return intersectionQueryParamsMapping;
            }
        }

        public FullDataIdInfo FullDataIdInfo { get; set; }

        public int Count { get; set; }

        public int MaxResultItems { get; set; }

        internal bool IsSingleClusterQuery { get; set; }

        public IndexCondition IndexCondition { get; set; }

        public DomainSpecificProcessingType DomainSpecificProcessingType { get; set; }

        #endregion

        #region Methods

        internal IntersectionQueryParams GetIntersectionQueryParamForIndexId(byte[] indexId)
        {
            IntersectionQueryParams retVal;

            if ((intersectionQueryParamsMapping == null) || !intersectionQueryParamsMapping.TryGetValue(indexId, out retVal))
            {
                retVal = new IntersectionQueryParams(this);
            }
            return retVal;
        }

        public void AddIntersectionQueryParam(byte[] indexId, IntersectionQueryParams intersectionQueryParam)
        {
            if (intersectionQueryParamsMapping == null)
            {
                intersectionQueryParamsMapping = new Dictionary<byte[], IntersectionQueryParams>(new ByteArrayEqualityComparer());
            }
            intersectionQueryParam.BaseQuery = this;
            intersectionQueryParamsMapping.Add(indexId, intersectionQueryParam);
        }

        public void DeleteIntersectionQueryParam(byte[] indexId)
        {
            if (intersectionQueryParamsMapping != null)
            {
                intersectionQueryParamsMapping.Remove(indexId);
            }
        }

        #endregion

        #region Ctors

        public IntersectionQuery()
        {
            Init(null, null, null, false, false, null, null, -1, null, -1, false, DomainSpecificProcessingType.None);
        }

        public IntersectionQuery(List<byte[]> indexIdList, string targetIndexName)
        {
            Init(indexIdList, null, targetIndexName, false, false, null, null, -1, null, -1, false, DomainSpecificProcessingType.None);
        }

        public IntersectionQuery(List<byte[]> indexIdList, List<int> primaryIdList, string targetIndexName, bool excludeData, bool getIndexHeader)
        {
            Init(indexIdList, primaryIdList, targetIndexName, excludeData, getIndexHeader, null, null, -1, null, -1, false, DomainSpecificProcessingType.None);
        }

        public IntersectionQuery(IntersectionQuery query)
        {
            Init(query.IndexIdList,
                query.PrimaryIdList,
                query.TargetIndexName,
                query.ExcludeData,
                query.GetIndexHeader,
                query.Filter,
                query.FullDataIdInfo,
                query.Count,
                query.IndexCondition,
                query.MaxResultItems,
                query.IsSingleClusterQuery,
                query.DomainSpecificProcessingType);
        }

        private void Init(List<byte[]> indexIdList,
            List<int> primaryIdList,
            string targetIndexName,
            bool excludeData,
            bool getIndexHeader,
            Filter filter,
            FullDataIdInfo fullDataIdInfo,
            int count,
            IndexCondition indexCondition,
            int maxResultItems,
            bool canApplyMaxResultItemsOnServer,
            DomainSpecificProcessingType domainSpecificProcessingType)
        {
            IndexIdList = indexIdList;
            PrimaryIdList = primaryIdList;
            TargetIndexName = targetIndexName;
            ExcludeData = excludeData;
            GetIndexHeader = getIndexHeader;
            Filter = filter;
            FullDataIdInfo = fullDataIdInfo;
            Count = count;
            IndexCondition = indexCondition;
            MaxResultItems = maxResultItems;
            IsSingleClusterQuery = canApplyMaxResultItemsOnServer;
            DomainSpecificProcessingType = domainSpecificProcessingType;
        }

        #endregion

        #region IRelayMessageQuery Members

        public virtual byte QueryId
        {
            get
            {
                return (byte)QueryTypes.IntersectionQuery;
            }
        }

        #endregion

        #region IPrimaryQueryId Members

        internal int primaryId;
        public virtual int PrimaryId
        {
            get
            {
                return primaryId;
            }
            set
            {
                primaryId = value;
            }
        }

        #endregion

        #region IVersionSerializable Members

        public virtual void Serialize(IPrimitiveWriter writer)
        {
            //TargetIndexName
            writer.Write(TargetIndexName);

            //IndexIdList
            if (IndexIdList == null || IndexIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexIdList.Count);
                foreach (byte[] indexId in IndexIdList)
                {
                    if (indexId == null || indexId.Length == 0)
                    {
                        writer.Write((ushort)0);
                    }
                    else
                    {
                        writer.Write((ushort)indexId.Length);
                        writer.Write(indexId);
                    }
                }
            }

            //ExcludeData
            writer.Write(ExcludeData);

            //GetIndexHeader
            writer.Write(GetIndexHeader);

            //PrimaryIdList
            if (PrimaryIdList == null || PrimaryIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)PrimaryIdList.Count);
                foreach (int primaryId in PrimaryIdList)
                {
                    writer.Write(primaryId);
                }
            }

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

            //IndexIdParamsMapping
            if (intersectionQueryParamsMapping == null || intersectionQueryParamsMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)intersectionQueryParamsMapping.Count);
                foreach (KeyValuePair<byte[] /*IndexId*/, IntersectionQueryParams /*IntersectionQueryParams*/> kvp in intersectionQueryParamsMapping)
                {
                    //IndexId
                    if (kvp.Key == null || kvp.Key.Length == 0)
                    {
                        writer.Write((ushort)0);

                        //No need to serialize IntersectionQueryParams
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Key.Length);
                        writer.Write(kvp.Key);

                        //IntersectionQueryParams
                        Serializer.Serialize(writer, kvp.Value);
                    }
                }
            }

            //FullDataIdInfo
            if (FullDataIdInfo == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, FullDataIdInfo);
            }

            //Count
            writer.Write(Count);
            
            //MaxResultItems
            writer.Write(MaxResultItems);

            //CanApplyMaxResultItemsOnServer
            writer.Write(IsSingleClusterQuery);

            //IndexCondition
            if (IndexCondition == null)
            {
                writer.Write(false);
            }
            else
            {
                writer.Write(true);
                Serializer.Serialize(writer.BaseStream, IndexCondition);
            }

            //DomainSpecificProcessingType
            writer.Write((byte)DomainSpecificProcessingType);
        }

        public virtual void Deserialize(IPrimitiveReader reader, int version)
        {
            //TargetIndexName
            TargetIndexName = reader.ReadString();

            //IndexIdList
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexIdList = new List<byte[]>(count);
                ushort len;
                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        IndexIdList.Add(reader.ReadBytes(len));
                    }
                }
            }

            //ExcludeData
            ExcludeData = reader.ReadBoolean();

            //GetIndexHeader
            GetIndexHeader = reader.ReadBoolean();

            //PrimaryIdList
            count = reader.ReadUInt16();
            if (count > 0)
            {
                PrimaryIdList = new List<int>(count);
                for (ushort i = 0; i < count; i++)
                {
                    PrimaryIdList.Add(reader.ReadInt32());
                }
            }

            //Filter
            byte b = reader.ReadByte();
            if (b != 0)
            {
                FilterType filterType = (FilterType)b;
                Filter = FilterFactory.CreateFilter(reader, filterType);
            }

            //IndexIdParamsMapping
            count = reader.ReadUInt16();
            if (count > 0)
            {
                intersectionQueryParamsMapping = new Dictionary<byte[], IntersectionQueryParams>(count, new ByteArrayEqualityComparer());
                byte[] indexId;
                IntersectionQueryParams intersectionQueryParam;
                ushort len;

                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    indexId = null;
                    if (len > 0)
                    {
                        indexId = reader.ReadBytes(len);

                        intersectionQueryParam = new IntersectionQueryParams();
                        Serializer.Deserialize(reader.BaseStream, intersectionQueryParam);

                        intersectionQueryParamsMapping.Add(indexId, intersectionQueryParam);
                    }
                }
            }

            if (version == 2)
            {
                //FullDataIdInfo
                FullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
            }

            if(version >= 3)
            {
                //FullDataIdInfo
                if (reader.ReadBoolean())
                {
                    FullDataIdInfo = new FullDataIdInfo();
                    Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
                }

                //Count
                Count = reader.ReadInt32();

                //MaxResultItems
                MaxResultItems = reader.ReadInt32();


                //CanApplyMaxResultItemsOnServer
                IsSingleClusterQuery = reader.ReadBoolean();

                //IndexCondition
                if (reader.ReadBoolean())
                {
                    IndexCondition = new IndexCondition();
                    Serializer.Deserialize(reader.BaseStream, IndexCondition);
                }
            }

            if (version >= 4)
            {
                //DomainSpecificProcessingType
                DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
            }
        }

        private const int CURRENT_VERSION = 4;
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        public bool Volatile
        {
            get
            {
                return false;
            }
        }

        #endregion

        #region ICustomSerializable Members

        public void Deserialize(IPrimitiveReader reader)
        {
            reader.Response = SerializationResponse.Unhandled;
        }

        #endregion
    }
}