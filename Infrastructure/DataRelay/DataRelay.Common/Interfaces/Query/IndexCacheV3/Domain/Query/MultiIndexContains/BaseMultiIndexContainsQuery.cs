using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common.IO;
using MySpace.Common;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class BaseMultiIndexContainsQuery<TQueryResult> : IPrimaryRelayMessageQuery,
        ISplitable<TQueryResult> where TQueryResult : MultiIndexContainsQueryResult, new()
    {

        #region Data Members

        public List<byte[]> IndexIdList { get; set; }

        public List<IndexItem> IndexItemList { get; set; }

        public string TargetIndexName { get; set; }

        public bool ExcludeData { get; set; }

        public bool GetIndexHeader { get; set; }

        public List<int> PrimaryIdList { get; set; }

        public Filter Filter { get; set; }

        public Dictionary<byte[], MultiIndexContainsQueryParams> MultiIndexContainsQueryParamsMapping { get; internal set; }

        public FullDataIdInfo FullDataIdInfo { get; set; }

        private int count = Int32.MaxValue;
        public int Count
        {
            get
            {
                return count;
            }
            set
            {
                count = value;
            }
        }

        public IndexCondition IndexCondition { get; set; }

        public DomainSpecificProcessingType DomainSpecificProcessingType { get; set; }

        #endregion

        #region Ctors

        public BaseMultiIndexContainsQuery()
        {
            
        }

        public BaseMultiIndexContainsQuery(BaseMultiIndexContainsQuery<TQueryResult> query)
        {
            Init(query.IndexIdList,
                query.IndexItemList,
                query.TargetIndexName,
                query.ExcludeData,
                query.GetIndexHeader,
                query.PrimaryIdList,
                query.Filter,
                query.FullDataIdInfo,
                query.Count,
                query.IndexCondition,
                query.DomainSpecificProcessingType);
        }

        private void Init(List<byte[]> indexIdList,
            List<IndexItem> indexItemList,
            string targetIndexName,
            bool excludeData,
            bool getIndexHeader,
            List<int> primaryIdList,
            Filter filter,
            FullDataIdInfo fullDataIdInfo,
            int count,
            IndexCondition indexCondition,
            DomainSpecificProcessingType domainSpecificProcessingType)
        {
            IndexIdList = indexIdList;
            IndexItemList = indexItemList;
            TargetIndexName = targetIndexName;
            ExcludeData = excludeData;
            GetIndexHeader = getIndexHeader;
            PrimaryIdList = primaryIdList;
            Filter = filter;
            FullDataIdInfo = fullDataIdInfo;
            Count = count;
            IndexCondition = indexCondition;
            DomainSpecificProcessingType = domainSpecificProcessingType;
        }

        #endregion

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.MultiIndexContainsQuery;
            }
        }

        #endregion

        #region IPrimaryQueryId Members

        public int PrimaryId { get; set; }

        #endregion

        #region IVersionSerializable Members

        public void Serialize(IPrimitiveWriter writer)
        {
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

            //IndexItemList
            if (IndexItemList == null || IndexItemList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)IndexItemList.Count);
                foreach (IndexItem indexItem in IndexItemList)
                {
                    indexItem.Serialize(writer);
                }
            }

            //TargetIndexName
            writer.Write(TargetIndexName);

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

            //IndexIdParamsMapping
            if (MultiIndexContainsQueryParamsMapping == null || MultiIndexContainsQueryParamsMapping.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)MultiIndexContainsQueryParamsMapping.Count);
                foreach (KeyValuePair<byte[] /*IndexId*/, MultiIndexContainsQueryParams> kvp in MultiIndexContainsQueryParamsMapping)
                {
                    //IndexId
                    if (kvp.Key == null || kvp.Key.Length == 0)
                    {
                        writer.Write((ushort)0);

                        //No need to serialize MultiIndexContainsQueryParams
                    }
                    else
                    {
                        writer.Write((ushort)kvp.Key.Length);
                        writer.Write(kvp.Key);

                        //MultiIndexContainsQueryParams
                        if (kvp.Value == null)
                        {
                            writer.Write(false);
                        }
                        else
                        {
                            writer.Write(true);
                            Serializer.Serialize(writer.BaseStream, kvp.Value);
                        }
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

        public void Deserialize(IPrimitiveReader reader, int version)
        {
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

            //IndexItemList
            count = reader.ReadUInt16();
            if (count > 0)
            {
                IndexItem indexItem;
                IndexItemList = new List<IndexItem>(count);
                for (ushort i = 0; i < count; i++)
                {
                    indexItem = new IndexItem();
                    indexItem.Deserialize(reader);
                    IndexItemList.Add(indexItem);
                }
            }

            //TargetIndexName
            TargetIndexName = reader.ReadString();

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

            //MultiIndexContainsQueryParamsMapping
            count = reader.ReadUInt16();
            if (count > 0)
            {
                MultiIndexContainsQueryParamsMapping = new Dictionary<byte[], MultiIndexContainsQueryParams>(count, new ByteArrayEqualityComparer());
                byte[] indexId;
                MultiIndexContainsQueryParams multiIndexContainsQueryParams;
                ushort len;

                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    indexId = null;
                    if (len > 0)
                    {
                        indexId = reader.ReadBytes(len);

                        multiIndexContainsQueryParams = null;
                        if (reader.ReadBoolean())
                        {
                            multiIndexContainsQueryParams = new MultiIndexContainsQueryParams();
                            Serializer.Deserialize(reader.BaseStream, multiIndexContainsQueryParams);
                        }

                        MultiIndexContainsQueryParamsMapping.Add(indexId, multiIndexContainsQueryParams);
                    }
                }
            }

            //FullDataIdInfo
            if (reader.ReadBoolean())
            {
                FullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, FullDataIdInfo);
            }

            //Count
            Count = reader.ReadInt32();

            //IndexCondition
            if (reader.ReadBoolean())
            {
                IndexCondition = new IndexCondition();
                Serializer.Deserialize(reader.BaseStream, IndexCondition);
            }

            if (version >= 2)
            {
                //DomainSpecificProcessingType
                DomainSpecificProcessingType = (DomainSpecificProcessingType)reader.ReadByte();
            }
        }

        private const int CURRENT_VERSION = 2;
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

        #region ISplitable<TQueryResult> Members

        public List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            BaseMultiIndexContainsQuery<TQueryResult> multiIndexContainsQuery;
            List<IPrimaryRelayMessageQuery> queryList = new List<IPrimaryRelayMessageQuery>();
            Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], MultiIndexContainsQueryParams>>> clusterParamsMapping;

            IndexCacheUtils.SplitIndexIdsByCluster(IndexIdList, PrimaryIdList, MultiIndexContainsQueryParamsMapping, numClustersInGroup, out clusterParamsMapping);

            foreach (KeyValuePair<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], MultiIndexContainsQueryParams>>> clusterParam in clusterParamsMapping)
            {
                multiIndexContainsQuery = new BaseMultiIndexContainsQuery<TQueryResult>(this)
                {
                    PrimaryId = clusterParam.Key,
                    IndexIdList = clusterParam.Value.First,
                    PrimaryIdList = clusterParam.Value.Second,
                    MultiIndexContainsQueryParamsMapping = clusterParam.Value.Third
                };
                queryList.Add(multiIndexContainsQuery);
            }
            return queryList;
        }

        public List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup, int localClusterPosition, out IPrimaryRelayMessageQuery localQuery)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IMergeableQueryResult<TQueryResult> Members

        public TQueryResult MergeResults(IList<TQueryResult> partialResults)
        {
            TQueryResult completeResult = null;

            if (partialResults != null && partialResults.Count > 0)
            {
                if (partialResults.Count == 1)
                {
                    // No need to merge anything
                    completeResult = partialResults[0];
                }
                else
                {
                    StringBuilder exceptionStringBuilder = new StringBuilder();

                    #region Merge partialResults into completeResultList

                    for (int i = 0; i < partialResults.Count; i++)
                    {
                        TQueryResult partialResult = partialResults[i];
                        if (partialResult != null)
                        {
                            if (partialResult.MultiIndexContainsQueryResultItemIndexHeaderList != null &&
                                partialResult.MultiIndexContainsQueryResultItemIndexHeaderList.Count > 0)
                            {
                                if (completeResult == null)
                                {
                                    completeResult = partialResult;
                                }
                                else
                                {
                                    completeResult.MultiIndexContainsQueryResultItemIndexHeaderList.AddRange(partialResult.MultiIndexContainsQueryResultItemIndexHeaderList);
                                }
                            }
                            if (!String.IsNullOrEmpty(partialResult.ExceptionInfo))
                            {
                                exceptionStringBuilder.Append(partialResult.ExceptionInfo);
                            }
                        }
                    }

                    if (completeResult != null && exceptionStringBuilder.Length > 0)
                    {
                        completeResult.ExceptionInfo = exceptionStringBuilder.ToString();
                    }

                    #endregion
                }
            }
            return completeResult;
        }
        #endregion

    }
}
