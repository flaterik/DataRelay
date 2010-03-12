namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class RemoteClusteredSpanQuery : SpanQuery, IRemotable
    {
        #region Ctors
        public RemoteClusteredSpanQuery()
        {
        }

        public RemoteClusteredSpanQuery(SpanQuery query)
            : base(query)
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
                return (byte)QueryTypes.RemoteClusteredSpanQuery;
            }
        }
        #endregion
    }
}
