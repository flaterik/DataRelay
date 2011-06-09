using System;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors
{
    internal static class TagQueryProcessor
    {
        /// <summary>
        /// Processes the specified tag query.
        /// </summary>
        /// <param name="tagQuery">The tag query.</param>
        /// <param name="messageContext">The message context.</param>
        /// <param name="storeContext">The store context.</param>
        /// <returns>TagQueryResult</returns>
        internal static TagQueryResult Process(TagQuery tagQuery, MessageContext messageContext, IndexStoreContext storeContext)
        {
            TagQueryResult tagQueryResult;
            string[] tagNames = null;
            bool typeExists = false;

            try
            {
                typeExists = storeContext.TagHashCollection.TryGetTagNames(messageContext.TypeId, out tagNames);
                tagQueryResult = new TagQueryResult
                                     {
                                         TypeExists = typeExists,
                                         TagNames = tagNames
                                     };
            }
            catch (Exception ex)
            {
                tagQueryResult = new TagQueryResult 
                { 
                    TypeExists = typeExists,
                    TagNames = tagNames, 
                    ExceptionInfo = ex.Message
                };
                LoggingUtil.Log.ErrorFormat("TypeId {0} -- Error processing TagQuery : {1}", messageContext.TypeId, ex);
            }
            return tagQueryResult;
        }

    }
}
