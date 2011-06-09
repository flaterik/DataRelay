using System;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class StringHashQueryProcessor
    {
        /// <summary>
        /// Processes the specified StringHash query.
        /// </summary>
        /// <param name="stringHashQuery">The string hash query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>StringHashQueryResult</returns>
        internal static StringHashQueryResult Process(StringHashQuery stringHashQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            StringHashQueryResult stringHashQueryResult;
            string[] stringNames = null;
            bool typeExists = false;

            try
            {
                typeExists = storeContext.StringHashCollection.TryGetStringNames(messageContext.TypeId, out stringNames);
                stringHashQueryResult = new StringHashQueryResult
                                     {
                                         TypeExists = typeExists,
                                         StringNames = stringNames
                                     };
            }
            catch (Exception ex)
            {
                stringHashQueryResult = new StringHashQueryResult 
                { 
                    TypeExists = typeExists,
                    StringNames = stringNames, 
                    ExceptionInfo = ex.Message
                };
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing StringHashQuery : {1}", messageContext.TypeId, ex);
            }
            return stringHashQueryResult;
        }

    }
}
