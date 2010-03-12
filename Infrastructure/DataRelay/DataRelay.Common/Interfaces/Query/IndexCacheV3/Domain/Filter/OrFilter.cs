namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public class OrFilter : AggregateFilter
    {
        #region Ctor
        public OrFilter(params Filter[] filters)
            : base(filters)
        {
        }
        #endregion

        #region Methods
        internal override FilterType FilterType
        {
            get
            {
                return FilterType.Or;
            }
        }

        internal override bool ShortCircuitHint
        {
            get
            {
                return true;
            }
        }
        #endregion
    }
}
