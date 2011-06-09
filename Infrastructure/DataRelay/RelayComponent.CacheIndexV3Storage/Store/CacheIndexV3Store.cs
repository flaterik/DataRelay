using System;
using System.Collections.Generic;
using MySpace.Common.IO;
using MySpace.DataRelay.Common.Interfaces.Query;
using MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Context;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.PerfCounters;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Processors;
using MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Utils;
using CacheIndex = MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3.CacheIndex;
using PagedIndexQuery = MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3.PagedIndexQuery;
using PagedIndexQueryResult = MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3.PagedIndexQueryResult;

namespace MySpace.DataRelay.RelayComponent.CacheIndexV3Storage.Store
{
    public class CacheIndexV3Store : IRelayComponent
    {
        #region Data Members

        internal IndexStoreContext storeContext = IndexStoreContext.Instance;

        #endregion

        #region IRelayComponent Members

        /// <summary>
        /// Returns a unique human readable component name.  This name MUST match the name used
        /// in the component config file.
        /// </summary>
        /// <returns>The name of the component.</returns>
        public string GetComponentName()
        {
            return IndexStoreContext.COMPONENT_NAME;
        }

        /// <summary>
        /// Initializes and starts the component.
        /// </summary>
        /// <param name="config">The configuration to use.</param>
        /// <param name="runState">Component Run State</param>
        public void Initialize(RelayNodeConfig config, ComponentRunState runState)
        {
            LoggingUtil.Log.Info("Initializing CacheIndexV3Storage");
            storeContext.InitializeReloadConfig(config, true);
        }

        /// <summary>
        /// Reloads the configuration from the given <see cref="RelayNodeCofig"/> and applies the new settings.
        /// </summary>
        /// <param name="config">The given <see cref="RelayNodeConfig"/>.</param>
        public void ReloadConfig(RelayNodeConfig config)
        {
            LoggingUtil.Log.Info("Reloading CacheIndexV3Storage");
            storeContext.InitializeReloadConfig(config, false);
        }

        /// <summary>
        /// Gets the state of the run.
        /// </summary>
        /// <returns></returns>
        public ComponentRunState GetRunState()
        {
            return null;
        }

        /// <summary>
        /// Gets the runtime info.
        /// </summary>
        /// <returns></returns>
        public ComponentRuntimeInfo GetRuntimeInfo()
        {
            return null;
        }

        /// <summary>
        /// Stops the component and releases it's resources.
        /// </summary>
        public void Shutdown()
        {
            LoggingUtil.Log.Info("Shutting down CacheIndexV3Storage");
            storeContext.ShutDown();
        }

        #endregion

        #region IDatahandler members

        /// <summary>
        /// Handles the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void HandleMessage(RelayMessage message)
        {
            MessageContext messageContext = new MessageContext
                                                {
                                                    PrimaryId = message.Id,
                                                    TypeId = message.TypeId,
                                                    ExtendedId = message.ExtendedId,
                                                    RelayTTL = message.RelayTTL,
                                                    SourceZone = message.SourceZone,
                                                    AddressHistory = message.AddressHistory
                                                };

            switch (message.MessageType)
            {
                case MessageType.Query:
                    ProcessQueryMessage(message, messageContext); 
                    break;
                case MessageType.Save:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.Save,
                        messageContext.TypeId,
                        1);

                    ProcessSaveMessage(message, messageContext);
                    break;
                case MessageType.Update:
                    ProcessUpdateMessage(message, messageContext);
                    break;
                case MessageType.Delete:
                    ProcessDeleteMessage(messageContext);
                    break;
                case MessageType.DeleteAll:
                    LoggingUtil.Log.InfoFormat("CacheIndexV3Store does NOT support Message Type {0}", message.MessageType);
                    break;
                case MessageType.DeleteAllInType:
                    ProcessDeleteAllInType(messageContext);
                    break;
                case MessageType.DeleteInAllTypes:
                    LoggingUtil.Log.InfoFormat("CacheIndexV3Store does NOT support Message Type {0}", message.MessageType);
                    // do not throw exception - we can safely ignore this message type
                    break;
                case MessageType.Get:
                    ProcessGetMessage(message, messageContext);
                    break;
                default:
                    LoggingUtil.Log.ErrorFormat("Unknown Message Type {0}", message.MessageType);
                    break;
            }
        }

        /// <summary>
        /// Handles the messages.
        /// </summary>
        /// <param name="messages">The messages.</param>
        public void HandleMessages(IList<RelayMessage> messages)
        {
            foreach (RelayMessage message in messages)
            {
                HandleMessage(message);
            }
        }

        #endregion

        #region HandleMessage Methods

        /// <summary>
        /// Processes the get message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessGetMessage(RelayMessage message, MessageContext messageContext)
        {
            byte[] payloadByteArray = GetProcessor.Process(messageContext, storeContext);
            if (payloadByteArray != null)
            {
                bool compress = storeContext.GetCompressOption(message.TypeId);
                message.Payload = new RelayPayload(message.TypeId, message.Id, payloadByteArray, compress);
            }
            else
            {
                LoggingUtil.Log.InfoFormat("Miss in CacheIndexStorage for Id :  {0}, ExtendedId : {1}", message.Id, IndexCacheUtils.GetReadableByteArray(message.ExtendedId));
            }
        }

        /// <summary>
        /// Processes the type of the delete all in.
        /// </summary>
        /// <param name="messageContext">The message context.</param>
        private void ProcessDeleteAllInType(MessageContext messageContext)
        {
            DeleteAllInTypeProcessor.Process(messageContext, storeContext);
        }

        /// <summary>
        /// Processes the delete message.
        /// </summary>
        /// <param name="messageContext">The message context.</param>
        private void ProcessDeleteMessage(MessageContext messageContext)
        {
            DeleteProcessor.Process(messageContext, storeContext);
        }

        /// <summary>
        /// Processes the update message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessUpdateMessage(RelayMessage message, MessageContext messageContext)
        {
            lock (LockingUtil.Instance.GetLock(message.Id))
            {
                try
                {
                    CacheIndexUpdate cacheIndexUpdate = message.GetObject<CacheIndexUpdate>();
                    UpdateProcessor.Process(cacheIndexUpdate, messageContext, storeContext);
                }
                catch (Exception ex)
                {
                    LoggingUtil.Log.ErrorFormat("TypeID {0} -- Error processing update message : {1}", message.TypeId, ex);
                    throw new Exception("Error processing update message");
                }
            }
        }

        /// <summary>
        /// Processes the save message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessSaveMessage(RelayMessage message, MessageContext messageContext)
        {
            SaveProcessor.Process(message.GetObject<CacheIndex>(), messageContext, storeContext);
        }

        /// <summary>
        /// Processes the query message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessQueryMessage(RelayMessage message, MessageContext messageContext)
        {
            switch (message.QueryId)
            {
                case (byte)QueryTypes.PagedTaggedIndexQuery:
                    // increment performance counter 
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.PagedIndexQuery,
                        messageContext.TypeId,
                        1);
                    ProcessPagedIndexQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.RemoteClusteredPagedIndexQuery:
                    // increment performance counter 
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.RemoteClusteredPagedIndexQuery,
                        messageContext.TypeId,
                        1);
                    ProcessRemoteClusteredPagedIndexQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.ContainsIndexQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.ContainsIndexQuery,
                        messageContext.TypeId,
                        1);
                    ProcessContainsIndexQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.FirstLastQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.FirstLastQuery,
                        messageContext.TypeId,
                        1);
                    ProcessFirstLastQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.GetRangeQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.GetRangeQuery,
                        messageContext.TypeId,
                        1);
                    ProcessGetRangeQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.RandomQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.RandomQuery,
                        messageContext.TypeId,
                        1);
                    ProcessRandomQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.IntersectionQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.IntersectionQuery,
                        messageContext.TypeId,
                        1);
                    ProcessIntersectionQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.RemoteClusteredIntersectionQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.RemoteClusteredIntersectionQuery,
                        messageContext.TypeId,
                        1);
                    ProcessRemoteClusteredIntersectionQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.SpanQuery:
                    // increment performance counter 
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.SpanQuery,
                        messageContext.TypeId,
                        1);
                    ProcessSpanQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.RemoteClusteredSpanQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.RemoteClusteredSpanQuery,
                        messageContext.TypeId,
                        1);
                    ProcessRemoteClusteredSpanQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.TagQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.TagQuery,
                        messageContext.TypeId,
                        1);
                    ProcessTagQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.StringHashQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.StringHashQuery,
                        messageContext.TypeId,
                        1);
                    ProcessStringHashQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.MultiIndexContainsQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.MultiIndexContainsQuery,
                        messageContext.TypeId,
                        1);
                    ProcessMultiIndexContainsQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.MetadataPropertyQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.MetadataPropertyQuery,
                        messageContext.TypeId,
                        1);
                    ProcessMetadataPropertyQuery(message, messageContext);
                    break;

                case (byte)QueryTypes.DistinctQuery:
                    // increment performance counter
                    PerformanceCounters.Instance.IncrementCounter(
                        PerformanceCounterEnum.DistinctQuery,
                        messageContext.TypeId,
                        1);
                    ProcessDistinctQuery(message, messageContext);
                    break;

                default:
                    throw new Exception("QueryId +" + (int)message.QueryId + "NOT supported");
            }
        }

        /// <summary>
        /// Processes the Distinct Query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessDistinctQuery(RelayMessage message, MessageContext messageContext)
        {
            DistinctQueryResult distinctQueryResult = DistinctQueryProcessor.Process(message.GetQueryObject<DistinctQuery>(),
                messageContext,
                storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(distinctQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the get range query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessGetRangeQuery(RelayMessage message, MessageContext messageContext)
        {
            GetRangeQueryResult getRangeQueryResult = GetRangeQueryProcessor.Process(message.GetQueryObject<GetRangeQuery>(),
                messageContext,
                storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(getRangeQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the random query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessRandomQuery(RelayMessage message, MessageContext messageContext)
        {
            RandomQueryResult randomQueryResult = RandomQueryProcessor.Process(message.GetQueryObject<RandomQuery>(),
                messageContext,
                storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(randomQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the intersection query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessIntersectionQuery(RelayMessage message, MessageContext messageContext)
        {
            IntersectionQueryResult intersectionQueryResult = IntersectionQueryProcessor.Process(message.GetQueryObject<IntersectionQuery>(),
                messageContext,
                storeContext);
            AttachIntersectionQueryPayload(message, intersectionQueryResult);
        }

        /// <summary>
        /// Processes the remote clustered intersection query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessRemoteClusteredIntersectionQuery(RelayMessage message, MessageContext messageContext)
        {
            IntersectionQueryResult intersectionQueryResult = RemoteClusteredIntersectionQueryProcessor.Process(message.GetQueryObject<RemoteClusteredIntersectionQuery>(),
                messageContext,
                storeContext);
            AttachIntersectionQueryPayload(message, intersectionQueryResult);
        }

        /// <summary>
        /// Attaches the intersection query payload.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="intersectionQueryResult">The intersection query result.</param>
        private void AttachIntersectionQueryPayload(RelayMessage message, IntersectionQueryResult intersectionQueryResult)
        {
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(intersectionQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the first last query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessFirstLastQuery(RelayMessage message, MessageContext messageContext)
        {
            FirstLastQueryResult firstLastQueryResult = FirstLastQueryProcessor.Process(message.GetQueryObject<FirstLastQuery>(), 
                messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(firstLastQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the contains index query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessContainsIndexQuery(RelayMessage message, MessageContext messageContext)
        {
            ContainsIndexQueryResult containsIndexQueryResult = ContainsQueryProcessor.Process(message.GetQueryObject<ContainsIndexQuery>(), 
                messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId, 
                message.Id, 
                Serializer.Serialize(containsIndexQueryResult, compressOption), 
                compressOption);
        }

        /// <summary>
        /// Processes the paged index query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessPagedIndexQuery(RelayMessage message, MessageContext messageContext)
        {          
            PagedIndexQueryResult pagedIndexQueryResult = PagedQueryProcessor.Instance.Process(message.GetQueryObject<PagedIndexQuery>(), 
                messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId, 
                message.Id, 
                Serializer.Serialize(pagedIndexQueryResult, compressOption), 
                compressOption);
        }

        /// <summary>
        /// Process a RemoteClusteredPagedIndexQuery
        /// </summary>
        /// <param name="message">relay message</param>
        /// <param name="messageContext">message context</param>
        private void ProcessRemoteClusteredPagedIndexQuery(RelayMessage message, MessageContext messageContext)
        {
            PagedIndexQueryResult result = RemoteClusteredQueryProcessor.Instance.Process<PagedIndexQueryResult, PagedIndexQuery>(
                PerformanceCounterEnum.IndexLookupAvgPerRemoteClusteredPagedIndexQuery,
                message.GetQueryObject<RemoteClusteredPagedIndexQuery>(),
                messageContext,
                storeContext,
                PagedQueryProcessor.Instance);

            bool compressOption = storeContext.GetCompressOption(message.TypeId);

            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(result, compressOption), 
                compressOption);
        }

        /// <summary>
        /// Processes the span query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessSpanQuery(RelayMessage message, MessageContext messageContext)
        {
            SpanQueryResult spanQueryResult = SpanQueryProcessor.Instance.Process(message.GetQueryObject<SpanQuery>(),
                messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(spanQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Process a RemoteClusteredSpanQuery
        /// </summary>
        /// <param name="message"></param>
        /// <param name="messageContext"></param>
        private void ProcessRemoteClusteredSpanQuery(RelayMessage message, MessageContext messageContext)
        {
            SpanQueryResult result = RemoteClusteredQueryProcessor.Instance.Process<SpanQueryResult, SpanQuery>(
                PerformanceCounterEnum.IndexLookupAvgPerRemoteClusteredSpanQuery,
                message.GetQueryObject<RemoteClusteredSpanQuery>(),
                messageContext,
                storeContext,
                SpanQueryProcessor.Instance);

            bool compressOption = storeContext.GetCompressOption(message.TypeId);

            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(result, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the TagQuery.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessTagQuery(RelayMessage message, MessageContext messageContext)
        {
            TagQueryResult tagQueryResult = TagQueryProcessor.Process(message.GetQueryObject<TagQuery>(), messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(tagQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the StringHashQuery.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessStringHashQuery(RelayMessage message, MessageContext messageContext)
        {
            StringHashQueryResult stringHashQueryResult = StringHashQueryProcessor.Process(message.GetQueryObject<StringHashQuery>(), messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(stringHashQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the MultiIndexContainsQuery.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessMultiIndexContainsQuery(RelayMessage message, MessageContext messageContext)
        {
            MultiIndexContainsQueryResult multiIndexContainsQueryResult = MultiIndexContainsQueryProcessor.Process(message.GetQueryObject<MultiIndexContainsQuery>(), 
                messageContext, storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(multiIndexContainsQueryResult, compressOption),
                compressOption);
        }

        /// <summary>
        /// Processes the metadata property query.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageContext">The message context.</param>
        private void ProcessMetadataPropertyQuery(RelayMessage message, MessageContext messageContext)
        {
            MetadataPropertyQueryResult metadataPropertyQueryResult = MetadataPropertyQueryProcessor.Process(message.GetQueryObject<MetadataPropertyQuery>(),
                messageContext,
                storeContext);
            bool compressOption = storeContext.GetCompressOption(message.TypeId);
            message.Payload = new RelayPayload(message.TypeId,
                message.Id,
                Serializer.Serialize(metadataPropertyQueryResult, compressOption),
                compressOption);
        }
        #endregion
    }
}
