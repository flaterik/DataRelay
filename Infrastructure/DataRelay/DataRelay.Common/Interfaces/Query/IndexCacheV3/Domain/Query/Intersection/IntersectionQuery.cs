using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.Common;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class IntersectionQuery : IPrimaryRelayMessageQuery
    {
        #region Data Members
        internal string targetIndexName;
        public string TargetIndexName
        {
            get
            {
                return targetIndexName;
            }
            set
            {
                targetIndexName = value;
            }
        }

        internal List<byte[]> indexIdList;
        public List<byte[]> IndexIdList
        {
            get
            {
                return indexIdList;
            }
            set
            {
                indexIdList = value;
            }
        }

        internal bool excludeData;
        public bool ExcludeData
        {
            get
            {
                return excludeData;
            }
            set
            {
                excludeData = value;
            }
        }

        internal bool getIndexHeader;
        public bool GetIndexHeader
        {
            get
            {
                return getIndexHeader;
            }
            set
            {
                getIndexHeader = value;
            }
        }

        internal List<int> primaryIdList;
        public List<int> PrimaryIdList
        {
            get
            {
                return primaryIdList;
            }
            set
            {
                primaryIdList = value;
            }
        }

        internal Filter filter;
        public Filter Filter
        {
            get
            {
                return filter;
            }
            set
            {
                filter = value;
            }
        }

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

        private FullDataIdInfo fullDataIdInfo;
        public FullDataIdInfo FullDataIdInfo
        {
            get
            {
                return fullDataIdInfo;
            }
            set
            {
                fullDataIdInfo = value;
            }
        }
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
            Init(null, null, null, false, false, null, null, null);
        }

        public IntersectionQuery(List<byte[]> indexIdList, string targetIndexName)
        {
            Init(indexIdList, null, targetIndexName, false, false, null, null, null);
        }

        public IntersectionQuery(List<byte[]> indexIdList, List<int> primaryIdList, string targetIndexName, bool excludeData, bool getIndexHeader)
        {
            Init(indexIdList, primaryIdList, targetIndexName, excludeData, getIndexHeader, null, null, null);
        }

        public IntersectionQuery(IntersectionQuery query)
        {
            Init(query.indexIdList,
                query.primaryIdList,
                query.targetIndexName,
                query.excludeData,
                query.getIndexHeader,
                query.intersectionQueryParamsMapping,
                query.filter,
                null);
        }

        private void Init(List<byte[]> indexIdList,
            List<int> primaryIdList,
            string targetIndexName,
            bool excludeData,
            bool getIndexHeader,
            Dictionary<byte[], IntersectionQueryParams> intersectionQueryParamsMapping,
            Filter filter,
            FullDataIdInfo fullDataIdInfo)
        {
            this.indexIdList = indexIdList;
            this.primaryIdList = primaryIdList;
            this.targetIndexName = targetIndexName;
            this.excludeData = excludeData;
            this.getIndexHeader = getIndexHeader;
            this.intersectionQueryParamsMapping = intersectionQueryParamsMapping;
            this.filter = filter;
            this.fullDataIdInfo = fullDataIdInfo;
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
            writer.Write(targetIndexName);

            //IndexIdList
            if (indexIdList == null || indexIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)indexIdList.Count);
                foreach (byte[] indexId in indexIdList)
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
            writer.Write(excludeData);

            //GetIndexHeader
            writer.Write(getIndexHeader);

            //PrimaryIdList
            if (primaryIdList == null || primaryIdList.Count == 0)
            {
                writer.Write((ushort)0);
            }
            else
            {
                writer.Write((ushort)primaryIdList.Count);
                foreach (int primaryId in primaryIdList)
                {
                    writer.Write(primaryId);
                }
            }

            //Filter
            if (filter == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)filter.FilterType);
                Serializer.Serialize(writer.BaseStream, filter);
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
            Serializer.Serialize(writer.BaseStream, fullDataIdInfo);
        }

        public virtual void Deserialize(IPrimitiveReader reader, int version)
        {
            //TargetIndexName
            targetIndexName = reader.ReadString();

            //IndexIdList
            ushort count = reader.ReadUInt16();
            if (count > 0)
            {
                indexIdList = new List<byte[]>(count);
                ushort len;
                for (ushort i = 0; i < count; i++)
                {
                    len = reader.ReadUInt16();
                    if (len > 0)
                    {
                        indexIdList.Add(reader.ReadBytes(len));
                    }
                }
            }

            //ExcludeData
            excludeData = reader.ReadBoolean();

            //GetIndexHeader
            getIndexHeader = reader.ReadBoolean();

            //PrimaryIdList
            count = reader.ReadUInt16();
            if (count > 0)
            {
                primaryIdList = new List<int>(count);
                for (ushort i = 0; i < count; i++)
                {
                    primaryIdList.Add(reader.ReadInt32());
                }
            }

            //Filter
            byte b = reader.ReadByte();
            if (b != 0)
            {
                FilterType filterType = (FilterType)b;
                filter = FilterFactory.CreateFilter(reader, filterType);
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

            if (version >= 2)
            {
                //FullDataIdInfo
                fullDataIdInfo = new FullDataIdInfo();
                Serializer.Deserialize(reader.BaseStream, fullDataIdInfo);
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
    }
}