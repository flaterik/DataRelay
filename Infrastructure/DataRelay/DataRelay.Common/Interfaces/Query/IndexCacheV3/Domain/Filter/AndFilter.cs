namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class AndFilter : AggregateFilter
    {
        #region Ctor
        public AndFilter(params Filter[] filters)
            : base(filters)
        {
        }
        #endregion

        #region Methods
        internal override FilterType FilterType
        {
            get
            {
                return FilterType.And;
            }
        }

        internal override bool ShortCircuitHint
        {
            get
            {
                return false;
            }
        }
        #endregion
    }
}
