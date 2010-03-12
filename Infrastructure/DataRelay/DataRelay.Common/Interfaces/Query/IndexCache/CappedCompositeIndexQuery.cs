using System;
using System.Collections.Generic;
using System.Text;
using MySpace.DataRelay;
using System.IO;
using MySpace.Common.IO;
using MySpace.Common;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
    public class CappedCompositeIndexQuery : BaseCappedCompositeIndexQuery<PagedIndexQueryResult>
    {
    }

    public class BaseCappedCompositeIndexQuery<TQueryResult> : IRelayMessageQuery, IMergeableQueryResult<TQueryResult> where TQueryResult : PagedIndexQueryResult, new()
    {
        protected bool useCompression;
        protected int pageSize;
        protected DateTime minValidDateTime;

        protected Dictionary<int /*CacheType*/, int /*CapValue*/ > cacheTypeCaps;
        protected List<Pair<List<byte[]> /*IndexIdList*/ , List<int> /*CacheTypeList*/ > /*Query*/ > queryList;

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
        public BaseCappedCompositeIndexQuery()
        {
            //a parameterless constructor is required in order to use it as parameter 'T' in method 'MySpace.Common.IO.Serializer.Deserialize<T>(System.IO.Stream, bool)			
            Init(false, -1, DateTime.MinValue);
        }
        protected void Init(bool useCompression, int pageSize, DateTime minValidDateTime)
        {
            this.useCompression = useCompression;
            this.pageSize = pageSize;
            this.minValidDateTime = minValidDateTime;
            this.queryList = new List<Pair<List<byte[]>, List<int>>>();
            this.cacheTypeCaps = new Dictionary<int, int>();
        }


        // public API for Caps
        public void AddCap(int CacheType, int CapValue)
        {
            this.cacheTypeCaps[CacheType] = CapValue;
        }
        public int GetCap(int CacheType)
        {
            int retVal;
            if (!this.cacheTypeCaps.TryGetValue(CacheType, out retVal))
            {
                retVal = -1;
            }
            return retVal;
        }
        public Dictionary<int /*CacheType*/, int /*CapValue*/ > Caps
        {
            get
            {
                return this.cacheTypeCaps;
            }
            set
            {
                if (this.cacheTypeCaps == null)
                {
                    this.cacheTypeCaps = new Dictionary<int, int>();
                }
                else
                {
                    this.cacheTypeCaps = value;
                }
            }
        }

        // public API for Queries
        public void AddQuery(List<byte[]> IndexIdList, List<int> CacheTypeList)
        {
            this.queryList.Add(new Pair<List<byte[]>, List<int>>(IndexIdList, CacheTypeList));
        }
        public Pair<List<byte[]> /*IndexIdList*/ , List<int> /*CacheTypeList*/ > this[int index]
        {
            get
            {
                return this.queryList[index];
            }
        }
        public void ClearQueries()
        {
            this.queryList.Clear();
        }
        public int QueryCount
        {
            get
            {
                return this.queryList.Count;
            }

        }

        // Properties		
        public int PageSize
        {
            get { return this.pageSize; }
            set { this.pageSize = value; }
        }
        public DateTime MinValidDate
        {
            get { return this.minValidDateTime; }
            set { this.minValidDateTime = value; }
        }

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.CappedCompositeIndexQuery;
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

                    #region apply cap restriction and get FilteredResults
                    List<CacheData> FilteredResults = new List<CacheData>();
                    if (Caps.Count > 0)
                    {
                        if (CompleteResults.Count > PageSize)
                        {
                            #region apply cap restrictions

                            Dictionary<int /*activity type*/ , int /*count*/> Occurance = new Dictionary<int, int>();
                            List<Pair<int /*insert position*/, CacheData /*item*/>> SkippedItems = new List<Pair<int, CacheData>>();
                            int OccuranceCount;
                            int Cap;
                            for (int i = 0; i < CompleteResults.Count && FilteredResults.Count < PageSize; i++)
                            {
                                if (Caps.TryGetValue(CompleteResults[i].CacheTypeId, out Cap))
                                {
                                    #region Cap exists
                                    if (!Occurance.TryGetValue(CompleteResults[i].CacheTypeId, out OccuranceCount))
                                    {
                                        // first occurance, add it
                                        Occurance.Add(CompleteResults[i].CacheTypeId, 1);
                                        FilteredResults.Add(CompleteResults[i]);
                                    }
                                    else if (OccuranceCount < Cap)
                                    {
                                        // max not reached, add it
                                        Occurance[CompleteResults[i].CacheTypeId]++;
                                        FilteredResults.Add(CompleteResults[i]);
                                    }
                                    else
                                    {
                                        // skip it
                                        SkippedItems.Add(new Pair<int, CacheData>(FilteredResults.Count, CompleteResults[i]));
                                    }


                                    #endregion
                                }
                                else
                                {
                                    // No Cap - Just Add it
                                    FilteredResults.Add(CompleteResults[i]);
                                }
                            }

                            #region Expand FilteredCacheDataRefList to include SkippedItems if necessary
                            int pos = 0;
                            int skippedItemIndex = 0;
                            while (FilteredResults.Count < PageSize && skippedItemIndex < SkippedItems.Count)
                            {
                                FilteredResults.Insert(SkippedItems[skippedItemIndex].First + (pos++), SkippedItems[skippedItemIndex++].Second);
                            }
                            #endregion

                            #endregion


                        }
                        else
                        {
                            //else no need to cap						
                            FilteredResults = CompleteResults;
                        }
                    }
                    else
                    {
                        #region use page logic
                        int pageSize = (CompleteResults.Count < PageSize ? CompleteResults.Count : PageSize);
                        FilteredResults = CompleteResults.GetRange(0, pageSize);
                        #endregion
                    }
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

        #region IVersionSerializable Members

        public int CurrentVersion
        {
            get { return 1; }
        }
        public void Deserialize(IPrimitiveReader reader, int version)
        {
            Deserialize(reader);
        }
        public void Serialize(IPrimitiveWriter writer)
        {
            writer.Write(this.pageSize);
            writer.Write(this.minValidDateTime.Ticks);

            #region cacheTypeCaps
            writer.Write(cacheTypeCaps.Count);
            if (cacheTypeCaps.Count > 0)
            {
                foreach (KeyValuePair<int, int> keyValue in cacheTypeCaps)
                {
                    writer.Write(keyValue.Key);
                    writer.Write(keyValue.Value);
                }
            }
            #endregion

            #region queryList
            writer.Write(queryList.Count);
            if (queryList.Count > 0)
            {
                // IndexId Length in bytes
                writer.Write(queryList[0].First[0].Length);

                foreach (Pair<List<byte[]> /*IndexIdList*/ , List<int> /*CacheTypeList*/ > pair in queryList)
                {
                    #region IndexIdList
                    writer.Write(pair.First.Count);
                    if (pair.First.Count > 0)
                    {
                        foreach (byte[] IndexId in pair.First)
                        {
                            writer.Write(IndexId);
                        }
                    }
                    #endregion

                    #region CacheTypeList
                    writer.Write(pair.Second.Count);
                    if (pair.Second.Count > 0)
                    {
                        foreach (int CacheType in pair.Second)
                        {
                            writer.Write(CacheType);
                        }
                    }
                    #endregion
                }
            }
            #endregion
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
            this.minValidDateTime = new DateTime(reader.ReadInt64());

            #region cacheTypeCaps
            int count = reader.ReadInt32();
            if (count > 0)
            {
                this.cacheTypeCaps = new Dictionary<int, int>(count);
                for (int i = 0; i < count; i++)
                {
                    this.cacheTypeCaps[reader.ReadInt32()] = reader.ReadInt32();
                }
            }
            #endregion

            #region queryList
            count = reader.ReadInt32();
            if (count > 0)
            {
                this.queryList = new List<Pair<List<byte[]>, List<int>>>(count);
                List<byte[]> IndexIdList = null;
                List<int> CacheTypeList = null;

                // IndexId Length in bytes
                int IndexIdLen = reader.ReadInt32();
                int IndexIdListLen;
                int CacheTypeListLen;
                for (int j = 0; j < count; j++)
                {
                    #region  IndexIdList
                    IndexIdListLen = reader.ReadInt32();
                    if (IndexIdListLen > 0)
                    {
                        IndexIdList = new List<byte[]>(IndexIdListLen);
                        for (int k = 0; k < IndexIdListLen; k++)
                        {
                            IndexIdList.Add(reader.ReadBytes(IndexIdLen));
                        }
                    }
                    #endregion

                    #region CacheTypeList
                    CacheTypeListLen = reader.ReadInt32();
                    if (CacheTypeListLen > 0)
                    {
                        CacheTypeList = new List<int>(CacheTypeListLen);
                        for (int l = 0; l < CacheTypeListLen; l++)
                        {
                            CacheTypeList.Add(reader.ReadInt32());
                        }
                    }
                    #endregion

                    this.queryList.Add(new Pair<List<byte[]>, List<int>>(IndexIdList, CacheTypeList));
                }
            }
            #endregion
        }

        #endregion
    }
}
