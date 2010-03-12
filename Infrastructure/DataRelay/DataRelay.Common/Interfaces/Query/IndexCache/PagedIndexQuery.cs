using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;
using System.IO;
using MySpace.Common.IO;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
    public class PagedIndexQuery : BasePagedIndexQuery<PagedIndexQueryResult>
    {
    }

    public class BasePagedIndexQuery<TQueryResult> : IRelayMessageQuery, IMergeableQueryResult<TQueryResult> where TQueryResult : PagedIndexQueryResult, new()
    {
        protected int pageSize;
        protected int pageNum;
        protected DateTime minValidDateTime;
        protected List<byte[]> indexIdList;
        protected List<int> cacheTypeList;
        protected bool useCompression;

        #region CacheDataComparer
        internal static int CacheDataComparer(CacheData o1, CacheData o2)
        {
            // sort in descending order
            int retVal = o2.CreateTimestamp.CompareTo(o1.CreateTimestamp);
            if (retVal == 0)
            {
                // since timestamps are equal, use id to sort
                switch (o1.Id.Length)
                {
                    case 4:
                        retVal = BitConverter.ToInt32(o2.Id, 0).CompareTo(BitConverter.ToInt32(o1.Id, 0));
                        break;
                    case 8:
                        retVal = BitConverter.ToInt64(o2.Id, 0).CompareTo(BitConverter.ToInt64(o1.Id, 0));
                        break;
                    case 12:
                        retVal = BitConverter.ToInt64(o2.Id, 4).CompareTo(BitConverter.ToInt64(o1.Id, 4));
                        break;
                }
            }

            return retVal;
        }
        internal static Comparison<CacheData> CacheDataComparison = new Comparison<CacheData>(CacheDataComparer);
        #endregion

        // Constructor
        public BasePagedIndexQuery()
        {
            //a parameterless constructor is required in order to use it as parameter 'T' in method 'MySpace.Common.IO.Serializer.Deserialize<T>(System.IO.Stream, bool)			
            Init(false, -1, -1, DateTime.MinValue, null, null);
        }
        public BasePagedIndexQuery(bool useCompression, int pageSize, int pageNum, DateTime minValidDateTime, List<byte[]> indexIdList, List<int> cacheTypeList)
        {
            Init(useCompression, pageSize, pageNum, minValidDateTime, indexIdList, cacheTypeList);
        }
        private void Init(bool useCompression, int pageSize, int pageNum, DateTime minValidDateTime, List<byte[]> indexIdList, List<int> cacheTypeList)
        {
            this.useCompression = useCompression;
            this.pageSize = pageSize;
            this.pageNum = pageNum;
            this.minValidDateTime = minValidDateTime;

            if (indexIdList == null)
            {
                this.indexIdList = new List<byte[]>();
            }
            else
            {
                this.indexIdList = indexIdList;
            }

            if (cacheTypeList == null)
            {
                this.cacheTypeList = new List<int>();
            }
            else
            {
                this.cacheTypeList = cacheTypeList;
            }
        }

        // Properties		
        public int PageSize
        {
            get { return this.pageSize; }
        }
        public int PageNum
        {
            get { return this.pageNum; }
        }
        public DateTime MinValidDate
        {
            get { return this.minValidDateTime; }
        }
        public List<byte[]> IndexIdList
        {
            get { return this.indexIdList; }
        }
        public List<int> CacheTypeList
        {
            get { return this.cacheTypeList; }
        }

        #region IVersionSerializable Members
        public void Serialize(IPrimitiveWriter writer)
        {
            writer.Write(this.pageSize);
            writer.Write(this.pageNum);
            writer.Write(this.minValidDateTime.Ticks);
            writer.Write(IndexIdList.Count);

            if (IndexIdList.Count > 0)
            {
                writer.Write(IndexIdList[0].Length);
                foreach (byte[] indexId in IndexIdList)
                {
                    writer.Write(indexId);
                }
            }

            writer.Write(CacheTypeList.Count);
            if (CacheTypeList.Count > 0)
            {
                foreach (int cacheType in CacheTypeList)
                {
                    writer.Write(cacheType);
                }
            }

        }
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }
        public int CurrentVersion
        {
            get { return 1; }
        }
        public bool Volatile
        {
            get { return false; }
        }
        #endregion

        #region ICustomSerializable Members
        public void Deserialize(IPrimitiveReader reader)
        {
            this.pageSize = reader.ReadInt32();
            this.pageNum = reader.ReadInt32();
            this.minValidDateTime = new DateTime(reader.ReadInt64());
            int count = reader.ReadInt32();
            indexIdList = new List<byte[]>(count);
            if (count > 0)
            {
                int keyLen = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    indexIdList.Add(reader.ReadBytes(keyLen));
                }
            }
            count = reader.ReadInt32();
            cacheTypeList = new List<int>(count);
            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    cacheTypeList.Add(reader.ReadInt32());
                }
            }
        }
        #endregion

        #region IRelayMessageQuery Methods
        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.PagedIndexQuery;
            }
        }
        #endregion

        #region IMergeableQueryResult Methods
        public TQueryResult MergeResults(IList<TQueryResult> PartialResults)
        {
            TQueryResult finalResult = default(TQueryResult);

            if (PartialResults != null && PartialResults.Count > 0)
            {
                if (PartialResults.Count == 1)
                {
                    // no need to merge anything
                    finalResult = PartialResults[0];
                }
                else
                {
                    #region Need to merge

                    #region Iterate over PartialResults and create CompleteResults
                    List<CacheData> CompleteResults = new List<CacheData>();
                    int totalCount = 0;
                    foreach (TQueryResult partialResult in PartialResults)
                    {
                        if (partialResult != null)
                        {
                            totalCount += partialResult.TotalCount;
                            CompleteResults.AddRange(partialResult.CacheDataList);
                        }
                    }
                    #endregion

                    #region sort CompleteResults
                    CompleteResults.Sort(CacheDataComparison);
                    #endregion

                    #region Use page logic
                    List<CacheData> FilteredResults = new List<CacheData>();
                    int pageSize = (CompleteResults.Count < PageSize ? CompleteResults.Count : PageSize);
                    FilteredResults = CompleteResults.GetRange(0, pageSize);
                    #endregion

                    #region create finalResult
                    finalResult = new TQueryResult();
                    finalResult.CacheDataList = FilteredResults;
                    finalResult.TotalCount = totalCount;
                    #endregion


                    #endregion
                }
            }

            return finalResult;
        }
        #endregion
    }



}
