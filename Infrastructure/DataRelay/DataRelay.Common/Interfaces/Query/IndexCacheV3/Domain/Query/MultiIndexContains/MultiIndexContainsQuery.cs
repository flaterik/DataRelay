using System.Collections.Generic;

namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class MultiIndexContainsQuery : BaseMultiIndexContainsQuery<MultiIndexContainsQueryResult>
    {

        #region Ctors

        public MultiIndexContainsQuery()
        {
        }

        public MultiIndexContainsQuery(MultiIndexContainsQuery query) : base (query)
        {
        }

        #endregion

        #region Methods

        internal MultiIndexContainsQueryParams GetMultiIndexContainsQueryParamForIndexId(byte[] indexId)
        {
            MultiIndexContainsQueryParams retVal;

            if ((MultiIndexContainsQueryParamsMapping == null) || !MultiIndexContainsQueryParamsMapping.TryGetValue(indexId, out retVal))
            {
                retVal = new MultiIndexContainsQueryParams(this);
            }
            return retVal;
        }

        public void AddMultiIndexContainsQueryParam(byte[] indexId, MultiIndexContainsQueryParams multiIndexContainsQueryParam)
        {
            if (MultiIndexContainsQueryParamsMapping == null)
            {
                MultiIndexContainsQueryParamsMapping = new Dictionary<byte[], MultiIndexContainsQueryParams>(new ByteArrayEqualityComparer());
            }
            multiIndexContainsQueryParam.BaseQuery = this;
            MultiIndexContainsQueryParamsMapping.Add(indexId, multiIndexContainsQueryParam);
        }

        public void DeleteMultiIndexContainsQueryParam(byte[] indexId)
        {
            if (MultiIndexContainsQueryParamsMapping != null)
            {
                MultiIndexContainsQueryParamsMapping.Remove(indexId);
            }
        }

        #endregion
    }
}