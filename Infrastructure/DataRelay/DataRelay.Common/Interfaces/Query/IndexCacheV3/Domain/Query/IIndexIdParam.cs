namespace MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3
{
    public interface IIndexIdParam
    {
        #region DataMembers
        
        int MaxItems
        {
            get; set;
        }

        Filter Filter
        {
            get; set;
        }
        
        #endregion
    }
}