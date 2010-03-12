using System;
using System.Collections.Generic;
using MySpace.Common.HelperObjects;
using MySpace.DataRelay.Interfaces.Query.IndexCacheV3;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    /// <summary>
    /// Provides set of utility methods for Index Cache
    /// </summary>
    internal static class IndexCacheUtils
    {
        /// <summary>
        /// Default primary id value for the multi index id query
        /// </summary>
        internal const int MUTILEINDEXQUERYDEFAULTPRIMARYID = -1;

        /// <summary>
        /// static random generator for generate the primary id 
        /// </summary>
        static Random randomGenerator = new Random();

        internal static bool EqualsLocalId(IItem item1, IItem item2, List<string> localIdentityTagNames)
        {
            try
            {
                byte[] tag1, tag2;
                foreach (string tagName in localIdentityTagNames)
                {
                    item1.TryGetTagValue(tagName, out tag1);
                    item2.TryGetTagValue(tagName, out tag2);
                    if (tag1 == null || tag2 == null)
                    {
                        throw new Exception("Tag required for local identity not found on the IndexItem");
                    }
                    return ByteArrayComparerUtil.CompareByteArrays(tag1, tag2);
                }
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Unexptected error while comparing local id.", ex);
            }
        }
   
        internal static int GeneratePrimaryId(byte[] bytes)
        {
            return CacheHelper.GeneratePrimaryId(bytes);
        }

        /// <summary>
        /// It will use a random index in the primary id list or if the primary id list is 
        /// null, will come from index id list
        /// </summary>
        /// <param name="primaryIdList">primary id list</param>
        /// <param name="indexIdList">index id list</param>
        /// <returns>the primary id</returns>
        internal static int GetRandomPrimaryId(List<int> primaryIdList, List<byte[]> indexIdList)
        {
            if (primaryIdList != null && primaryIdList.Count > 0)
            {
                return primaryIdList[GetRandom(0, primaryIdList.Count)];
            }

            return GeneratePrimaryId(indexIdList[GetRandom(0, indexIdList.Count)]);
        }

        // return a random number between the range
        internal static int GetRandom(int min, int max)
        {
            return randomGenerator.Next(min, max);
        }


        internal static void SplitIndexIdsByCluster(
            List<byte[]> indexIdList,
            List<int> primaryIdList,
            Dictionary<byte[], IndexIdParams> indexIdParamsMapping,
            int numClustersInGroup,
            out Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IndexIdParams>>> clusterParamsMapping)
        {
            // NOTE - This method RELIES on the fact that DataRelay partitions clusters using "mod" logic.  If this ever changes,
            // this method will need to be updated!!!

            int id, clusterId;
            clusterParamsMapping = new Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IndexIdParams>>>(numClustersInGroup);
            bool generatePrimaryId = !(primaryIdList != null && indexIdList.Count == primaryIdList.Count);
            IndexIdParams tempIndexIdParams;

            for (int i = 0; i < indexIdList.Count; i++)
            {
                id = generatePrimaryId ? GeneratePrimaryId(indexIdList[i]) : primaryIdList[i];
                clusterId = id % numClustersInGroup;

                if (!clusterParamsMapping.ContainsKey(clusterId))
                {
                    clusterParamsMapping.Add(
                        clusterId,
                        new Triple<List<byte[]>, List<int>, Dictionary<byte[], IndexIdParams>>(
                            new List<byte[]>(),
                            new List<int>(),
                            new Dictionary<byte[], IndexIdParams>()));
                }

                clusterParamsMapping[clusterId].First.Add(indexIdList[i]);
                if (!generatePrimaryId)
                {
                    clusterParamsMapping[clusterId].Second.Add(primaryIdList[i]);
                }
                if (indexIdParamsMapping != null && indexIdParamsMapping.TryGetValue(indexIdList[i], out tempIndexIdParams))
                {
                    clusterParamsMapping[clusterId].Third.Add(indexIdList[i], tempIndexIdParams);
                }
            }
        }

        internal static void SplitIndexIdsByCluster(
            List<byte[]> indexIdList,
            List<int> primaryIdList,
            Dictionary<byte[], IntersectionQueryParams> intersectionQueryParamsMapping,
            int numClustersInGroup,
            out Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>> clusterParamsMapping)
        {
            // NOTE - This method RELIES on the fact that DataRelay partitions clusters using "mod" logic.  If this ever changes,
            // this method will need to be updated!!!

            int id, clusterId;
            clusterParamsMapping = new Dictionary<int, Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>>(numClustersInGroup);
            bool generatePrimaryId = !(primaryIdList != null && indexIdList.Count == primaryIdList.Count);
            IntersectionQueryParams tempIntersectionQueryParams;

            for (int i = 0; i < indexIdList.Count; i++)
            {
                id = generatePrimaryId ? GeneratePrimaryId(indexIdList[i]) : primaryIdList[i];
                clusterId = id % numClustersInGroup;

                if (!clusterParamsMapping.ContainsKey(clusterId))
                {
                    clusterParamsMapping.Add(
                        clusterId,
                        new Triple<List<byte[]>, List<int>, Dictionary<byte[], IntersectionQueryParams>>(
                            new List<byte[]>(),
                            new List<int>(),
                            new Dictionary<byte[], IntersectionQueryParams>()));
                }

                clusterParamsMapping[clusterId].First.Add(indexIdList[i]);
                if (!generatePrimaryId)
                {
                    clusterParamsMapping[clusterId].Second.Add(primaryIdList[i]);
                }
                if (intersectionQueryParamsMapping != null && intersectionQueryParamsMapping.TryGetValue(indexIdList[i], out tempIntersectionQueryParams))
                {
                    clusterParamsMapping[clusterId].Third.Add(indexIdList[i], tempIntersectionQueryParams);
                }
            }
        }

        /// <summary>
        /// Gets the readable byte array.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        internal static string GetReadableByteArray(byte[] buffer)
        {
            string retVal = null;
            if (buffer == null || buffer.Length == 0)
            {
                retVal = "Null Buffer";
            }
            else
            {
                if (buffer.Length == 4)
                {
                    retVal = BitConverter.ToInt32(buffer, 0).ToString();
                }
                else
                {
                    foreach (byte b in buffer)
                    {
                        retVal += (int)b + " ";
                    }
                    retVal += "(Bytes)";
                }
            }
            return retVal;
        }
    }
}