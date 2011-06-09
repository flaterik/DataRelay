using System.Collections.Generic;
using MySpace.Common;

namespace MySpace.DataRelay.Common.Interfaces.Query
{
    public enum QueryTypes : byte
    {
        PagedIndexQuery = 1,
        CappedCompositeIndexQuery,
        ContainsCacheListQuery,
        PagedCacheListQuery,
        ReversePagedCacheListQuery,
        ContainsIndexQuery,
        SublistCacheListQuery,
        RandomizedCacheListQuery,
        MultiContainsCacheListQuery,
        ExclusionSublistCacheListQuery,
        PagedTaggedIndexQuery,
        FirstLastQuery,
        GetRangeQuery,
        RandomQuery,
        IntersectionQuery,
        RemoteClusteredIntersectionQuery, 
        SpanQuery,
        RemoteClusteredPagedIndexQuery,
        RemoteClusteredSpanQuery,
        TagQuery,
        StringHashQuery,
        MultiIndexContainsQuery,
        MetadataPropertyQuery,
        DistinctQuery

        // ALWAYS add new values to the END of the enumeration
    }

	public interface IRelayMessageQuery : IVersionSerializable
	{
		byte QueryId
		{
			get;
		}
	}

	public interface IMergeableQueryResult<TQueryResult>
	{
		TQueryResult MergeResults(IList<TQueryResult> QueryResults);
	}

    public interface ISplitable<TQueryResult> : IMergeableQueryResult<TQueryResult>
    {
        List<IPrimaryRelayMessageQuery> SplitQuery(int numClustersInGroup);

        List<IPrimaryRelayMessageQuery> SplitQuery(
            int numClustersInGroup,
            int localClusterPosition,
            out IPrimaryRelayMessageQuery localQuery);
    }

    /// <summary>
    /// Implement this interface if MultiIndexId Queries need to perform spilt & merge functions remotely on the server-side
    /// </summary>
    public interface IRemotable
    {
    }

    public interface IPrimaryRelayMessageQuery : IRelayMessageQuery, IPrimaryQueryId
    {
    }

    public interface IPrimaryQueryId
    {
        int PrimaryId
        {
            get;
        }
    }

}
