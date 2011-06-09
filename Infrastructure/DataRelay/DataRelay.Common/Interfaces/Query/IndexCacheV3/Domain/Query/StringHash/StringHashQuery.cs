using System;
using System.Collections.Generic;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using Wintellect.PowerCollections;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class StringHashQuery : IPrimaryRelayMessageQuery,
        ISplitable<StringHashQueryResult>
    {
        #region Data Members

        public bool IsSingleClusterQuery
        {
            get; set;
        }

        #endregion

        #region IRelayMessageQuery Members

        public byte QueryId
        {
            get
            {
                return (byte)QueryTypes.StringHashQuery;
            }
        }

        #endregion

        #region IPrimaryQueryId Members

        private int primaryId = IndexCacheUtils.MULTIINDEXQUERY_DEFAULT_PRIMARYID;
        public int PrimaryId
        {
            get
            {
                if (primaryId == IndexCacheUtils.MULTIINDEXQUERY_DEFAULT_PRIMARYID)
                {
                    return IndexCacheUtils.GetRandom(1, Int32.MaxValue);
                }
                return primaryId;
            }
            set
            {
                primaryId = value;
            }
        }

        #endregion

        #region IVersionSerializable Members

        private const int CURRENT_VERSION = 1;
        public int CurrentVersion
        {
            get
            {
                return CURRENT_VERSION;
            }
        }

        public void Deserialize(IPrimitiveReader reader, int version)
        {
        }

        public void Serialize(IPrimitiveWriter writer)
        {
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

        #region ISplitable<TagQueryResult> Members

        public List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup)
        {
            if (!IsSingleClusterQuery)
            {
                StringHashQuery stringHashQuery;
                List<IPrimaryRelayMessageQuery> stringHashQueryList = new List<IPrimaryRelayMessageQuery>(numClustersInGroup);

                for (int i = 0; i < numClustersInGroup; i++)
                {
                    stringHashQuery = new StringHashQuery
                                   {
                                       PrimaryId = i
                                   };
                    stringHashQueryList.Add(stringHashQuery);
                }
                return stringHashQueryList;
            }
            return new List<IPrimaryRelayMessageQuery>(1) {this};
        }

        public List<IPrimaryRelayMessageQuery> SplitQuery(
            int numClustersInGroup,
            int localClusterPosition,
            out IPrimaryRelayMessageQuery localQuery)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region IMergeableQueryResult<TagQueryResult> Members

        public StringHashQueryResult MergeResults(IList<StringHashQueryResult> partialResults)
        {
            StringHashQueryResult finalResult = default(StringHashQueryResult);

            if (partialResults == null || partialResults.Count == 0)
            {
                return finalResult;
            }

            if (partialResults.Count == 1)
            {
                finalResult = partialResults[0];
            }
            else
            {
                //Merge partial results
                bool finalTypeExists = false;
                string[] finalStringHashNames = null;
                Set<string> stringHashNames = new Set<string>();
                StringBuilder finalExceptionInfo = new StringBuilder();

                for (int i = 0; i < partialResults.Count; i++)
                {
                    if (!String.IsNullOrEmpty(partialResults[i].ExceptionInfo))
                    {
                        finalExceptionInfo.Append("Cluster ").Append(i).Append(" Info ").Append(partialResults[i].ExceptionInfo).Append(",");
                    }
                    else if (partialResults[i].TypeExists)
                    {
                        finalTypeExists = true;

                        for (int j = 0; j < partialResults[i].StringNames.Length; j++)
                        {
                            if (!stringHashNames.Contains(partialResults[i].StringNames[j]))
                            {
                                stringHashNames.Add(partialResults[i].StringNames[j]);
                            }
                        }
                    }
                }

                if (stringHashNames.Count > 0)
                {
                    finalStringHashNames = new string[stringHashNames.Count];
                    stringHashNames.CopyTo(finalStringHashNames, 0);
                }

                finalResult = new StringHashQueryResult
                                  {
                                      TypeExists = finalTypeExists,
                                      StringNames = finalStringHashNames,
                                      ExceptionInfo = finalExceptionInfo.ToString()
                                  };
            }
            return finalResult;
        }

        #endregion
    }
}