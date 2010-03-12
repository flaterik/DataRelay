using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RemoteClusteredPagedIndexQuery : PagedIndexQuery, IRemotable
    {
        #region Ctors
        public RemoteClusteredPagedIndexQuery()
        {
        }

        public RemoteClusteredPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName)
            : base(indexIdList, pageSize, pageNum, targetIndexName)
        {
        }

        public RemoteClusteredPagedIndexQuery(List<byte[]> indexIdList, int pageSize, int pageNum, string targetIndexName, int maxItemsPerIndex)
            : base(indexIdList, pageSize, pageNum, targetIndexName, maxItemsPerIndex)
        {
        }

        #endregion

        #region IPrimaryQueryId Members

        public override int PrimaryId
        {
            get
            {
                if (this.primaryId == IndexCacheUtils.MUTILEINDEXQUERYDEFAULTPRIMARYID)
                {
                    return IndexCacheUtils.GetRandomPrimaryId(PrimaryIdList, IndexIdList);
                }

                return this.primaryId;
            }
        }
        #endregion

        #region IRelayMessageQuery Members
        public override byte QueryId
        {
            get
            {
                return (byte)QueryTypes.RemoteClusteredPagedIndexQuery;
            }
        }
        #endregion
    }
}
