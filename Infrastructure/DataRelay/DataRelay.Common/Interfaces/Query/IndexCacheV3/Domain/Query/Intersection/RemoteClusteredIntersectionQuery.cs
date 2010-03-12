using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RemoteClusteredIntersectionQuery : IntersectionQuery
    {
        #region Ctors
        public RemoteClusteredIntersectionQuery()
        {
        }

        public RemoteClusteredIntersectionQuery(List<byte[]> indexIdList, string targetIndexName)
            : base(indexIdList, targetIndexName)
        {
        }
        #endregion

        #region IPrimaryQueryId Members
        public override int PrimaryId
        {
            get
            {
                if (PrimaryIdList != null && PrimaryIdList.Count > 0)
                {
                    return PrimaryIdList[0];
                }
                return IndexCacheUtils.GeneratePrimaryId(IndexIdList[0]);
            }
        }
        #endregion

        #region IRelayMessageQuery Members
        public override byte QueryId
        {
            get
            {
                return (byte)QueryTypes.RemoteClusteredIntersectionQuery;
            }
        }
        #endregion

    }
}
