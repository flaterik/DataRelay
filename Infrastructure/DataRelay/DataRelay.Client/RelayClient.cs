using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;
using MySpace.DataRelay.Configuration;
using MySpace.Common.Framework;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.RelayComponent.Forwarding;
using MySpace.DataRelay.RelayComponent.FlexForwarding;
using MySpace.DataRelay.Common.Schemas;
using System.IO;
using MySpace.Common.IO;
using MySpace.DataRelay.Common.Interfaces.Query;
using MySpace.DataRelay.SocketTransport;
using MySpace.ResourcePool;
using System.Linq;
using MySpace.SocketTransport;
using MySpace.Storage;
using MySpace.Storage.Cache;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// 	Provides a client interface into those components of the relay system
	///		that support <see cref="ICacheParameter"/> instances.
    /// </summary>
    /// <remarks>
    ///     <para>When a client wants to use the Relay transport, they use this class to 
    ///     to send messages into the transport.  Typical kinds of requests are <see cref="GetObject{T}"/>,
    ///     <see cref="SaveObject{T}"/>, <see cref="UpdateObject{T}"/>, <see cref="DeleteObject{T}"/>, Query
    ///     and <see cref="Invoke{T}"/>.
    ///     </para>
    ///     <para>When the <see cref="RelayClient"/> class creates a new <see cref="RelayMessage"/> and 
	 ///     sends it into the relay transport it's <see cref="RelayMessage.EnteredCurrentSystemAt"/> is timestamped.
    ///     </para>
    /// </remarks>
	public class RelayClient
    {
        #region Fields

		internal static readonly MySpace.Logging.LogWrapper log = new MySpace.Logging.LogWrapper();

		private FlexForwarder _flexForwarder;
		private Forwarder _relayForwarder;
        private RelayNodeConfig _configuration;
		private static RelayClient _instance = new RelayClient();
        
        #endregion

        #region Instance

		/// <summary>
		/// Gets the singleton instance.
		/// </summary>
        public static RelayClient Instance
		{
			get
			{
				return _instance;
			}
        }

        #endregion

        #region Ctor

		/// <summary>
		/// Prevents a default instance of the <see cref="RelayClient"/> class from being created.
		/// </summary>
        private RelayClient()
		{

			GetConfig();
			UpdateCacheLookup();
            if (_configuration != null && _configuration.TypeSettings != null)
            {
                RelayMessage.SetCompressionImplementation(_configuration.TypeSettings.Compressor);
            }

			_flexForwarder = new FlexForwarder();
			_flexForwarder.Initialize(_configuration, null);

			_relayForwarder = new Forwarder();
			_relayForwarder.Initialize(_configuration, null);
        }
        #endregion

        private void DrainRelay(RelayMessage message)
        {
            if (message.IsTwoWayMessage)
            {
                if (message.Payload == null)
                {
                    // a miss, try Data Relay and transfer the object to FlexCache on success
                    _relayForwarder.HandleMessage(message);

                    if (message.Payload != null)
                    {
                        var deleteMessage = new RelayMessage(message, MessageType.Delete);
                        var saveMessage = new RelayMessage(message, MessageType.Save);
                        _flexForwarder.HandleMessage(saveMessage);
                        _relayForwarder.HandleMessage(deleteMessage);
                    }
                }
            }
            else
            {
                // on all update messages, also send a delete to data relay
                var deleteMessage = new RelayMessage(message, MessageType.Delete);
                _relayForwarder.HandleMessage(deleteMessage);
            }
        }

        private void ForwardMessage(RelayMessage message)
		{
			var typeSetting = GetTypeSetting(message.TypeId);
            if(typeSetting.FlexCacheMode == FlexCacheMode.Enabled) _flexForwarder.HandleMessage(message);
            else if(typeSetting.FlexCacheMode == FlexCacheMode.Disabled) _relayForwarder.HandleMessage(message);
            else if(typeSetting.FlexCacheMode == FlexCacheMode.EnabledWithRelayDrain)
            {
                _flexForwarder.HandleMessage(message);
                DrainRelay(message);
            }
            else
            {
                log.ErrorFormat("The type setting for {0}, TypeId {1} specified a FlexCacheMode of {2}, which is not supported.  No action will be taken for this message in ForwardMessage().", typeSetting.TypeName, typeSetting.TypeId, typeSetting.FlexCacheMode);
            }
		}

    	private void ForwardMessages(IList<RelayMessage> messages)
		{
			var classicList = new List<RelayMessage>(messages.Count);
			var flexList = new List<RelayMessage>(messages.Count);

			foreach (var message in messages)
			{
                if (message != null)
                {
                    var typeSetting = GetTypeSetting(message.TypeId);
                    if (typeSetting.FlexCacheMode == FlexCacheMode.Enabled) flexList.Add(message);
                    else if (typeSetting.FlexCacheMode == FlexCacheMode.Disabled) classicList.Add(message);
                    else if (typeSetting.FlexCacheMode == FlexCacheMode.EnabledWithRelayDrain) flexList.Add(message);
                    else
                    {
                        log.ErrorFormat(
                            "The type setting for {0}, TypeId {1} specified a FlexCacheMode of {2}, which is not supported.  No action will be taken for this message in ForwardMessages().",
                            typeSetting.TypeName, typeSetting.TypeId, typeSetting.FlexCacheMode);
                    }
                }
			}

			if (classicList.Count > 0) _relayForwarder.HandleMessages(classicList);
			if (flexList.Count > 0)
			{
			    _flexForwarder.HandleMessages(flexList);
                
			    foreach (var relayMessage in flexList)
			    {
                    var typeSetting = GetTypeSetting(relayMessage.TypeId);
                    if (typeSetting.FlexCacheMode == FlexCacheMode.EnabledWithRelayDrain)
                    DrainRelay(relayMessage);
			    }
			}
		}

		private IAsyncDataHandler GetForwarder(RelayMessage message)
		{
			var typeSetting = GetTypeSetting(message.TypeId);
			var result = GetForwarder(typeSetting.FlexCacheMode);
			if (result == null)
			{
				log.ErrorFormat("The type setting for {0}, TypeId {1} specified a FlexCacheMode of {2}, which is not supported.  No action will be taken for this message in ForwardMessage().", typeSetting.TypeName, typeSetting.TypeId, typeSetting.FlexCacheMode);
			}
			return result;
		}

		private IAsyncDataHandler GetForwarder(FlexCacheMode mode)
		{
			switch (mode)
			{
				case FlexCacheMode.Disabled:
					return _relayForwarder;
				case FlexCacheMode.Enabled:
					return _flexForwarder;
				default:
					return null;
			}
		}

		private Future<RelayMessage> ForwardMessageAsync(RelayMessage message)
		{
			var forwarder = GetForwarder(message);
			if (forwarder == null)
			{
				message.SetError(RelayErrorType.Unknown);
				message.ResultOutcome = RelayOutcome.Error;
				message.ResultDetails = "Failed to obtain a forwarder. Check the FlexCacheMode setting.";
				return message;
			}

			return Future.FromAsyncPattern(
				ac => forwarder.BeginHandleMessage(message, null, ac),
				ar => { forwarder.EndHandleMessage(ar); return message; });
		}

		private Future ForwardMessagesAsync(IEnumerable<RelayMessage> messages)
		{
			var q = from message in messages
					let typeSetting = GetTypeSetting(message.TypeId)
					group message by typeSetting.FlexCacheMode into messageGroup
					let forwarder = GetForwarder(messageGroup.Key)
					where forwarder != null
					let messageList = messageGroup.ToList()
					select Future.FromAsyncPattern(
						ac => forwarder.BeginHandleMessages(messageList, null, ac),
						ar => forwarder.EndHandleMessages(ar));

			return q.Combine();
		}

    	#region Config

        private RelayNodeConfig GetConfig()
		{
			_configuration = RelayNodeConfig.GetRelayNodeConfig(new EventHandler(ReloadConfig));
			return _configuration;
		}

    	private Dictionary<short, bool> _useCacheLookup;

		private bool UseCacheLookup(short typeId)
		{
			return _useCacheLookup != null && _useCacheLookup[typeId];
		}

		private void ReloadConfig(object state, EventArgs args)
		{
			var config = state as RelayNodeConfig;
			if (config != null)
			{
				if (log.IsInfoEnabled)
					log.Info("Reloading config");
				_relayForwarder.ReloadConfig(config);
				if (config.TypeSettings != null)
				{
					RelayMessage.SetCompressionImplementation(config.TypeSettings.Compressor);
				}
				_configuration = config;
			}
			else
			{
				if (log.IsErrorEnabled)
					log.Error("Attempt to reload with null config");
			}
			UpdateCacheLookup();
		}

		private void UpdateCacheLookup()
		{
			if (_configuration != null)
			{
				var useCacheLookup = new Dictionary<short, bool>();
				foreach (var typeSetting in _configuration.TypeSettings.TypeSettingCollection)
				{
					useCacheLookup[typeSetting.TypeId] =
						(typeSetting.LocalCacheTTLSeconds ?? -1) >= 0;
				}
				_useCacheLookup = useCacheLookup;
			} else
			{
				_useCacheLookup = null;				
			}
		}

		internal int MaximumTypeId
		{
	    	get
	    	{
	    		if (_configuration != null && _configuration.TypeSettings != null)
	    		{
					return _configuration.TypeSettings.MaxTypeId;
	    		}
	    		return -1;
	    	}
		}

        #endregion

        #region Query

        internal delegate TQueryResult QueryProcessor<TQuery, TQueryResult, TCacheData>(TQuery query, string virtualCacheTypeName);

		public TQueryResult SubmitMultiGroupQuery<TQuery, TQueryResult, TCacheData>(TQuery query, bool inParallel, params string[] virtualCacheTypeNames)
			where TQuery : IRelayMessageQuery, IMergeableQueryResult<TQueryResult>
			where TQueryResult : class, new()
			where TCacheData : IVirtualTypeCacheParameter, new()
		{
			TQueryResult queryResult = default(TQueryResult);
			List<TQueryResult> partialResults = null;

			#region Submit Query
			if (inParallel)
			{
				partialResults = SubmitMultiGroupQueryInParallel<TQuery, TQueryResult, TCacheData>(query, virtualCacheTypeNames);
			}
			else
			{
				partialResults = SubmitMultiGroupQuerySequentially<TQuery, TQueryResult, TCacheData>(query, virtualCacheTypeNames);
			}
			#endregion

			#region Merge partial results into one final result
			if (partialResults.Count > 1)
			{
				queryResult = (query as IMergeableQueryResult<TQueryResult>).MergeResults(partialResults);
			}
			else if (partialResults.Count == 1)
			{
				queryResult = partialResults[0];
			}
			#endregion

			return queryResult;
		}

		internal List<TQueryResult> SubmitMultiGroupQuerySequentially<TQuery, TQueryResult, TCacheData>(TQuery query, params string[] virtualCacheTypeNames)
			where TQuery : IRelayMessageQuery, IMergeableQueryResult<TQueryResult>
			where TQueryResult : class, new()
			where TCacheData : IVirtualTypeCacheParameter, new()
		{			
			List<TQueryResult> partialResults = new List<TQueryResult>();
			TQueryResult tmpResult = default(TQueryResult);

			#region Invoke Queries
			for (int i = 0; i < virtualCacheTypeNames.Length; i++)
			{
				try
				{
					tmpResult = SubmitQuery<TQuery, TQueryResult, TCacheData>(query, virtualCacheTypeNames[i]);
					if (tmpResult != null)
					{
						partialResults.Add(tmpResult);
					}
				}
				catch (Exception ex)
				{
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("Exception in SubmitMultiGroupQuery() for {0}: {1}", virtualCacheTypeNames[i], ex);
				}
			}
			#endregion

			return partialResults;
		}

		internal List<TQueryResult> SubmitMultiGroupQueryInParallel<TQuery, TQueryResult, TCacheData>(TQuery query, params string[] virtualCacheTypeNames)
			where TQuery : IRelayMessageQuery, IMergeableQueryResult<TQueryResult>
			where TQueryResult : class, new()
			where TCacheData : IVirtualTypeCacheParameter, new()
		{	
			List<TQueryResult> partialResults = new List<TQueryResult>();
			TQueryResult tmpResult = default(TQueryResult);

			#region Invoke queries Asynchronously
			QueryProcessor<TQuery, TQueryResult, TCacheData>[] queryProcessorAry
				= new QueryProcessor<TQuery, TQueryResult, TCacheData>[virtualCacheTypeNames.Length - 1];
			IAsyncResult[] asynchResAry = new IAsyncResult[virtualCacheTypeNames.Length - 1];

			for (int i = 0; i < virtualCacheTypeNames.Length - 1; i++)
			{
				queryProcessorAry[i] = new QueryProcessor<TQuery, TQueryResult, TCacheData>
					(this.SubmitQuery<TQuery, TQueryResult, TCacheData>);
				asynchResAry[i] = queryProcessorAry[i].BeginInvoke(query, virtualCacheTypeNames[i], null, null);
			}
			#endregion

			#region Process Last Query Synchronously
			try
			{
				tmpResult = SubmitQuery<TQuery, TQueryResult, TCacheData>(query, virtualCacheTypeNames[virtualCacheTypeNames.Length - 1]);
				if (tmpResult != null)
				{
					partialResults.Add(tmpResult);
				}
			}
			catch (SyncRelayOperationException) 
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception in SubmitMultiGroupQuery() for {0}: {1}", 
                        (virtualCacheTypeNames.Length > 0) ? virtualCacheTypeNames[virtualCacheTypeNames.Length - 1] : "[no virtual cache types names defined]",
                        ex);
			}
			#endregion

			#region Get/Wait for all Asynch Query Results
			for (int i = 0; i < virtualCacheTypeNames.Length - 1; i++)
			{
				try
				{
					tmpResult = queryProcessorAry[i].EndInvoke(asynchResAry[i]);
					if (tmpResult != null)
					{
						partialResults.Add(tmpResult);
					}
				}
				catch (Exception ex)
				{
                    if (log.IsErrorEnabled)
                        log.ErrorFormat("Exception in SubmitMultiGroupQuery() for {0}: {1}", virtualCacheTypeNames[i], ex.ToString());					
				}
			}
			#endregion

			return partialResults;
		}

		#region Interfaces without TCacheData
		public TQueryResult SubmitQuery<TQuery, TQueryResult>(TQuery query)
			where TQuery : IRelayMessageQuery, IVirtualCacheType
			where TQueryResult : class, new()
		{
			TQueryResult queryResult;
			SubmitQuery<TQuery, TQueryResult>(query, out queryResult);
			return queryResult;
		}

		public bool SubmitQuery<TQuery, TQueryResult>(TQuery query, out TQueryResult queryResult)
			where TQuery : IRelayMessageQuery, IVirtualCacheType
			where TQueryResult : class, new()
		{
			//Passing in dummy object that implements IVirtualTypeCacheParameter. This type is never used.
			return SubmitQuery<TQuery, TQueryResult, MySpace.DataRelay.Common.Interfaces.Query.IndexCacheV3.CacheIndex>(query, null, out queryResult);
		}
    	#endregion

        #region Batched Query
        public List<TQueryResult> SubmitQueryList<TQuery, TQueryResult>(List<TQuery> queryList)
            where TQuery : IRelayMessageQuery
            where TQueryResult : class, new()
        {
            List<TypeSetting> typeSettingList;
            List<RelayMessage> messageList = GetQueryMessages<TQuery, TQueryResult>(queryList, out typeSettingList);
			ForwardMessages(messageList);
            return GetQueryResults<TQuery, TQueryResult>(queryList, typeSettingList, messageList);
        }

        private TypeSetting GetTypeSetting<TQuery>(TQuery query, out short typeId)
            where TQuery : IRelayMessageQuery
        {
            TypeSetting typeSetting = null;
            short tmpTypeId;
            typeId = -1;
            bool useCompression;

            if (query is IVirtualCacheType && TryGetTypeInfo(query, out tmpTypeId, out useCompression))
            {
                typeSetting = _configuration.TypeSettings.TypeSettingCollection[tmpTypeId];
            }

            if (typeSetting != null)
            {
                if (typeSetting.RelatedIndexTypeId > 0 && IsIndexCacheV1Query(query.QueryId))
                {
                    // RelatedTypeId is defined, use it since it points to Index Group
                    typeId = typeSetting.RelatedIndexTypeId;
                    typeSetting = _configuration.TypeSettings.TypeSettingCollection[typeId];
                }
                else
                {
                    // RelatedTypeId is undefined, there is no 'Index' group
                    typeId = typeSetting.TypeId;
                }
            }

            return typeSetting;
        }

        private List<RelayMessage> GetQueryMessages<TQuery, TQueryResult>(List<TQuery> queryList, out List<TypeSetting> typeSettingList)
            where TQuery : IRelayMessageQuery
            where TQueryResult : class, new()
        {
            short typeId;
            RelayMessage message;
            TypeSetting typeSetting;
            List<RelayMessage> messageList = new List<RelayMessage>();
            typeSettingList = new List<TypeSetting>(queryList.Count);

            foreach (TQuery query in queryList)
            {
                try
                {
                    typeSetting = GetTypeSetting<TQuery>(query, out typeId);
                    typeSettingList.Add(typeSetting);

                    if (typeSetting != null)
                    {
                        //MultiCluster Query
                        if (query is IMergeableQueryResult<TQueryResult>)
                        {
                            for (int i = 0; i < _configuration.RelayNodeMapping.RelayNodeGroups[typeSetting.GroupName].RelayNodeClusters.Length; i++)
                            {
                                message = RelayMessage.GetQueryMessageForQuery<TQuery>(typeSetting.TypeId, typeSetting.Compress, query);
                                message.Id = i;
                                message.ExtendedId = BitConverter.GetBytes(i);
                                messageList.Add(message);
                            }
                        }
                        //Single Cluster Query
                        else
                        {
                            message = RelayMessage.GetQueryMessageForQuery<TQuery>(typeId, typeSetting.Compress, query);
                            messageList.Add(message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if(log.IsErrorEnabled)                    
                        log.ErrorFormat("Unexpected Error in SubmitQuery() for query of type {0}: {1}", query.GetType().FullName, ex);
                }
            }
            return messageList;
        }

        private List<TQueryResult> GetQueryResults<TQuery, TQueryResult>(List<TQuery> queryList, List<TypeSetting> typeSettingList, List<RelayMessage> messageList)
            where TQuery : IRelayMessageQuery
            where TQueryResult : class, new()
        {
            List<TQueryResult> queryResultList = new List<TQueryResult>(queryList.Count);
            int queryCount = 0;
            int resultCount = 0;
            TQueryResult queryResult;            

            foreach (TQuery query in queryList)
            {
                queryResult = null;
                try
                {
                    if (typeSettingList[queryCount] != null)
                    {
                        int numClusters = _configuration.RelayNodeMapping.RelayNodeGroups[typeSettingList[queryCount].GroupName].RelayNodeClusters.Length;
                        if (numClusters > 1 && query is IMergeableQueryResult<TQueryResult>)
                        {
                            queryResult = MergeQueryResults<TQuery, TQueryResult>(messageList, query, numClusters, ref resultCount);
                        }
                        else
                        {
                            queryResult = new TQueryResult();
                            if (!messageList[resultCount].GetObject<TQueryResult>(queryResult))
                            {
                                queryResult = null;
                            }
                            resultCount++;
                        }
                    }
                    else
                    {
                        #region Handle TypeSetting Error
                        IVirtualCacheType virtualCacheType = query as IVirtualCacheType;
                        if (virtualCacheType != null && virtualCacheType.CacheTypeName != null)
                        {
                            throw (new Exception("TypeSetting not found for query of virtual type name " + virtualCacheType.CacheTypeName));
                        }
                        else
                        {
                            throw (new Exception("TypeSetting not found for query"));
                        }
                        #endregion
                    }
                }
                catch (Exception ex)
                {
                    #region Handle Exception
                    if(log.IsErrorEnabled)                    
                        log.ErrorFormat("Unexpected Error in GetQueryResults() for query of type {0}: {1}", query.GetType().FullName, ex);

                    //Note: Interface to express server side error and/or client side errors has not been implemented
                    //if (queryResult is IQueryError)
                    //{
                    //    //TBD
                    //    //queryResult = default(TQueryResult);
                    //    //queryResult.ExceptionInfo = ex.Message;
                    //}
                    //else
                    //{
                    queryResult = null;
                    //}
                    #endregion
                }
                queryResultList.Add(queryResult);
                queryCount++;
            }
            return queryResultList;
        }

        private TQueryResult MergeQueryResults<TQuery, TQueryResult>(List<RelayMessage> messageList, TQuery query, int numClusters, ref int resultCount)
            where TQuery : IRelayMessageQuery
            where TQueryResult : class, new()
        {
            //Collect partial results
            List<TQueryResult> partialResults = new List<TQueryResult>();
            for (int j = 0; j < numClusters; j++)
            {
                try
                {
                    if (messageList[resultCount].Payload != null)
                    {
                        partialResults.Add(messageList[resultCount].GetObject<TQueryResult>());
                    }
                }
                catch (Exception ex)
                {
                    resultCount += numClusters - j;
                    throw new Exception("Failed to merge partial query result for relay typeId : " + messageList[resultCount].TypeId + " for cluster number : " + j + " for query of type : " + query.GetType().FullName, ex);
                }
                resultCount++;
            }
            return (query as IMergeableQueryResult<TQueryResult>).MergeResults(partialResults);
        }
        #endregion

		public TQueryResult SubmitQuery<TQuery, TQueryResult, TCacheData>(TQuery query, string virtualCacheTypeName)
			where TQuery : IRelayMessageQuery
			where TQueryResult : class, new()
			where TCacheData : IVirtualTypeCacheParameter, new()
		{
			TQueryResult queryResult = default(TQueryResult);
			TCacheData data = new TCacheData();
			data.CacheTypeName = virtualCacheTypeName;
			SubmitQuery<TQuery, TQueryResult, TCacheData>(query, data, out queryResult);
			return queryResult;
		}		

		public TQueryResult SubmitQuery<TQuery, TQueryResult, TCacheData>(TQuery query)
			where TQuery : IRelayMessageQuery
			where TQueryResult : class, new()
			where TCacheData : ICacheParameter, new()
		{
			TQueryResult queryResult = default(TQueryResult);
			SubmitQuery<TQuery, TQueryResult, TCacheData>(query, out queryResult);
			return queryResult;
		}

		public bool SubmitQuery<TQuery, TQueryResult, TCacheData>(TQuery query, out TQueryResult queryResult)
			where TQuery : IRelayMessageQuery
			where TQueryResult : class, new()
			where TCacheData : ICacheParameter, new()
		{
			return SubmitQuery<TQuery, TQueryResult, TCacheData>(query, new TCacheData(), out queryResult);
		}

		internal bool SubmitQuery<TQuery, TQueryResult, TCacheData>(TQuery query, TCacheData data, out TQueryResult queryResult)
			where TQuery : IRelayMessageQuery
			where TQueryResult : class, new()
			where TCacheData : ICacheParameter, new()
		{
			queryResult = default(TQueryResult);
			bool retVal = false;
			try
			{
				TypeSetting typeSetting;

				// consider virtual types
				short tmpTypeId;
				bool useCompression;
                //Must check query type first to bypass the dummy CacheData object.
                if (query is IVirtualCacheType && TryGetTypeInfo(query, out tmpTypeId, out useCompression))
                {
                    //ME: Allow to pass the IVirtualTypeCacheParameter on the query
                    typeSetting = _configuration.TypeSettings.TypeSettingCollection[tmpTypeId];
                } 
				else if (data is IVirtualTypeCacheParameter && TryGetTypeInfo(data, out tmpTypeId, out useCompression))
				{
					typeSetting = _configuration.TypeSettings.TypeSettingCollection[tmpTypeId];
				}                
				else 
				{	
                    typeSetting = _configuration.TypeSettings.TypeSettingCollection.GetTypeMapping(typeof(TCacheData).FullName);
				}

				if (typeSetting != null)
				{
					short typeId;
                    if (typeSetting.RelatedIndexTypeId > 0 && IsIndexCacheV1Query(query.QueryId))
					{
						// RelatedTypeId is defined, use it since it points to Index Group
						typeId = typeSetting.RelatedIndexTypeId;
						typeSetting = _configuration.TypeSettings.TypeSettingCollection[typeId];
					}
					else
					{
						// RelatedTypeId is undefined, there is no 'Index' group
						typeId = typeSetting.TypeId;
					}

					if (!(query is IRemotable) && (query is IMergeableQueryResult<TQueryResult>))
					{
						// Submit a query to each cluster, collect all results, and produce a final result
						queryResult = ProcessMultiClusterQuery<TQuery, TQueryResult>(query, typeSetting);
						retVal = (queryResult != null);
					}
					else
					{
						// Submit a single query to a single cluster
						RelayMessage message = RelayMessage.GetQueryMessageForQuery<TQuery>(typeId, typeSetting.Compress, query);
						ForwardMessage(message);
						if (message.Payload != null)
						{
							queryResult = new TQueryResult();
							retVal = message.GetObject<TQueryResult>(queryResult);
						}
					}
				}
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Exception in SubmitQuery() for query of type {0}: {1}", query.GetType().FullName, ex);
			}

			return retVal;
		}

        private static bool IsIndexCacheV1Query(byte p)
        {
            return (p <= (byte)QueryTypes.CappedCompositeIndexQuery);
        }

        private List<RelayMessage> SplitQueryMessages<TQuery, TQueryResult>(TQuery query, TypeSetting typeSetting)
            where TQuery : IRelayMessageQuery
            where TQueryResult : class, new()
        {
            List<RelayMessage> messages;
            ISplitable<TQueryResult> splitableQuery = query as ISplitable<TQueryResult>;
            if (splitableQuery != null)
            {
                #region Create a relay message for each generated child query

                List<IPrimaryRelayMessageQuery> queryList = splitableQuery.SplitQuery(
                    _configuration.RelayNodeMapping.RelayNodeGroups[typeSetting.GroupName].RelayNodeClusters.Length);
                messages = new List<RelayMessage>(queryList.Count);
                RelayMessage message;
                foreach (IPrimaryRelayMessageQuery childQuery in queryList)
                {
                    message = RelayMessage.GetQueryMessageForQuery(typeSetting.TypeId, typeSetting.Compress, childQuery);
                    message.Id = childQuery.PrimaryId;
                    message.IsInterClusterMsg = true;
                    message.ExtendedId = BitConverter.GetBytes(childQuery.PrimaryId);
                    messages.Add(message);
                }
                #endregion
            }
            else
            {
                #region Create a relay message for each cluster in the group
                messages = new List<RelayMessage>();
                RelayMessage message;
                for (int i = 0;
                     i < _configuration.RelayNodeMapping.RelayNodeGroups[typeSetting.GroupName].RelayNodeClusters.Length;
                     i++)
                {
                    message = RelayMessage.GetQueryMessageForQuery(typeSetting.TypeId, typeSetting.Compress, query);
                    message.Id = i;
                    message.ExtendedId = BitConverter.GetBytes(i);
                    messages.Add(message);
                }
                #endregion
            }
            return messages;
        }

        private TQueryResult MergeQueryResults<TQueryResult>(IMergeableQueryResult<TQueryResult> query, List<RelayMessage> messages)
            where TQueryResult : class, new()
        {
            #region Collect partial results
            List<TQueryResult> partialResults = new List<TQueryResult>();
            foreach (RelayMessage msg in messages)
            {
                if (msg.Payload != null)
                {
                    partialResults.Add(msg.GetObject<TQueryResult>());
                }
            }
            #endregion

            // Merge partial results into one final result
            return query.MergeResults(partialResults);
            //return (query as IMergeableQueryResult<TQueryResult>).MergeResults(partialResults);
        }

		private TQueryResult ProcessMultiClusterQuery<TQuery, TQueryResult>(TQuery query, TypeSetting typeSetting)
			where TQuery : IRelayMessageQuery
			where TQueryResult : class, new()
		{
            List<RelayMessage> messages = SplitQueryMessages<TQuery, TQueryResult>(query, typeSetting);

		    // Issue the queries
			ForwardMessages(messages);

		    return MergeQueryResults<TQueryResult>(query as IMergeableQueryResult<TQueryResult>, messages);			
        }

        #endregion

        #region Get...

    	private readonly object _poolLock = new object();
    	private MemoryStreamPool _memoryPool;
    	private MemoryStreamPool MemoryPool
    	{
    		get
    		{
				var memoryPool = _memoryPool;
				if (memoryPool == null)
				{
					lock(_poolLock)
					{
						memoryPool = _memoryPool;
						if (memoryPool == null)
						{
							memoryPool = _memoryPool = new MemoryStreamPool();							
						}
					}
				}
				return memoryPool;
    		}
    	}

		private void Copy<T>(T source, T destination)
		{
			if (!ReferenceEquals(source, destination))
			{
				// returned entry instance not the same, so have to
				// copy
				using (var item = MemoryPool.GetItem())
				{
					var stm = item.Item;
					Serializer.Serialize(stm, source);
					stm.Position = 0;
					Serializer.Deserialize(stm, destination);
				}
			}			
		}

		/// <summary>
		/// Finds and loads the given <see cref="ICacheParameter"/> object from the transport.
		/// </summary>
		/// <typeparam name="T">A type that implements <see cref="ICacheParameter"/>.</typeparam>
		/// <param name="emptyObject">The object to be loaded and is found by its <see cref="ICacheParameter.PrimaryId"/>,
		/// or if <see cref="IExtendedCacheParameter"/> is implemented, the <see cref="IExtendedCacheParameter.ExtendedId"/>
		/// will be used.
		/// </param>
		/// <returns>True if successfully found and loaded.; otherwise false.</returns>
        public bool GetObject<T>(T emptyObject) where T : ICacheParameter
		{
			if (emptyObject == null)
			{
				return false;
			}
			try
			{
				emptyObject.DataSource = DataSource.Unknown;
				short typeId;
				bool useCompression;
				if (TryGetTypeInfo(emptyObject, out typeId, out useCompression))
				{
					var entry = new StorageEntry<T>();
					var useLocalCaching = UseCacheLookup(typeId);
					if (useLocalCaching)
					{
						// lookup first, providing emptyObject for deserialization if used
						entry = LocalCache.Get(emptyObject);
						if (entry.IsFound)
						{
							if (entry.Expires > DateTime.Now)
							{
								// if found in cache and not expired, then copy
								// cached state to user object and return
								Copy(entry.Instance, emptyObject);
								emptyObject.DataSource = DataSource.Cache;
								return true;
							}
						}
					}
					RelayMessage message;
					if (entry.IsFound)
					{
						// object found in cache but expired, so do get message
						// with cached object updated date as freshness
						message = RelayMessage.GetConditionalGetMessageForObject(typeId,
							emptyObject, entry.Updated);												
					} else
					{
						// object not in cache or caching not used
						message = RelayMessage.GetGetMessageForObject(typeId,
							emptyObject);						
					}
					ForwardMessage(message);
					if (useLocalCaching)
					{
						// checked after forwarder sends since that's when
						// legacy serialization can be set
						AssertNoLegacySerialization(message);
					}
                    bool result = false;
					try
					{
                        if (message.ErrorOccurred)
                        {
                            emptyObject.DataSource = DataSource.CacheError;
                        }
                        else if (!entry.IsFound)
						{
							// try to read from cache
							result = message.GetObject(emptyObject);
							if (result)
							{
								emptyObject.DataSource = DataSource.Cache;
								if (useLocalCaching)
								{
									SaveToLocalCache(emptyObject, message);
								}
							}
						}
						else if (!message.Freshness.HasValue)
						{
							// no freshness, try to get cache version
							result = message.GetObject(emptyObject);
							if (result)
							{
								emptyObject.DataSource = DataSource.Cache;
								// uses local caching must be true to get here
								SaveToLocalCache(emptyObject, message);
							} else
							{
								// was deleted on server
								LocalCache.Delete(emptyObject);								
							}
						}
						else
						{
							var cmp = entry.Updated.CompareTo(message.Freshness.Value);
							if (cmp < 0)
							{
								// cache version newer
								result = message.GetObject(emptyObject);
								if (result)
								{
									emptyObject.DataSource = DataSource.Cache;
									SaveToLocalCache(emptyObject, message);
								}
								else
								{
									log.WarnFormat("Got a response with newer freshness but with no payload for an object of type {0} and ID {1}.",
										typeof(T).FullName, LocalCache.GetKey(emptyObject));
								}
							}
							else
							{
								if (cmp > 0)
								{
									// server shouldn't have older version than cache
									log.WarnFormat("Got a response with older freshness for an object of type {0} and ID {1}.",
										typeof(T).FullName, LocalCache.GetKey(emptyObject));
								}
								result = true;
								Copy(entry.Instance, emptyObject);
								emptyObject.DataSource = DataSource.Cache;
								LocalCache.RefreshExpires(entry.Instance);
							}
						}									
					}
					catch (UnhandledVersionException uve)
					{
                        IVersionSerializable ivs = emptyObject as IVersionSerializable;
                        emptyObject.DataSource = DataSource.Unknown;
                        if (log.IsWarnEnabled)
                        {
                            if (ivs != null)
                            {
                                log.WarnFormat("Got UnhandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Received: {4}",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    ivs.CurrentVersion, uve.VersionExpected, uve.VersionRecieved);
                            }
                            else
                            {
                                log.WarnFormat("Got UnhandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Received: {3}",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    uve.VersionExpected,
                                    uve.VersionRecieved
                                    );
                            }
                        }
						result = false;
					}
					catch (HandledVersionException hve)
					{
                        IVersionSerializable ivs = emptyObject as IVersionSerializable;                        
						emptyObject.DataSource = DataSource.Cache;
                        if (log.IsErrorEnabled)
                        {
                            if (ivs != null)
                            {
                                log.WarnFormat("Got HandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Handled: {4}. Resaving object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    ivs.CurrentVersion,
                                    hve.VersionExpected,
                                    hve.VersionHandled);

                            }
                            else
                            {
                                log.WarnFormat("Got HandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Handled: {3}. Resaving object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId,
                                    hve.VersionExpected,
                                    hve.VersionHandled
                                    );
                            }
                        }
						this.SaveObject<T>(emptyObject);
						result = true;
					}
					return result;
				}
				
				emptyObject.DataSource = DataSource.Unknown;
				return false;
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if(log.IsErrorEnabled)                    
				    log.ErrorFormat("Error getting object {0} of type {1}: {2}", emptyObject.PrimaryId.ToString("N0"), emptyObject.GetType().FullName, ex);
				emptyObject.DataSource = DataSource.CacheError;
				return false;
			}			
		}

		private DateTime? RefreshLocalCacheExpires(short typeId, ICacheParameter instance)
		{
			return LocalCache.RefreshExpires(typeId, LocalCache.GetKey(instance));
		}

    	public void GetObjects(IList<ICacheParameter> emptyObjects)
		{
            try
            {
				var paramCount = emptyObjects.Count;
				var messages = new RelayMessage[paramCount];
            	var entries = new StorageEntry<ICacheParameter>[paramCount];
				var typeIds = new short[paramCount];
				short typeId;
				bool useCompression;
                bool gotOne = false;
                DateTime? lastUpdatedDate;
                byte[] extendedIdBytes;
				var now = DateTime.Now;
                for (int i = 0; i < paramCount; i++)
                {
                	var emptyObject = emptyObjects[i];
					RelayMessage message = null;
					var entry = StorageEntry<ICacheParameter>.NotFound;
					//check for null so exception won't be thrown and the rest of the objects can be retrieved
                    if ((emptyObject!=null) && TryGetTypeInfo(emptyObject, out typeId, out useCompression))
                    {
						emptyObject.DataSource = DataSource.Unknown;
						var useLocalCaching = UseCacheLookup(typeId);
						if (useLocalCaching)
						{
							entry = LocalCache.Get(typeId, LocalCache.GetKey(
								emptyObject), () => emptyObject);
							if (entry.IsFound)
							{
								if (entry.Expires > now)
								{
									Copy(entry.Instance, emptyObject);
									emptyObject.DataSource = DataSource.Cache;
								} else
								{
									RelayMessage.GetExtendedInfo(emptyObject, out extendedIdBytes, out lastUpdatedDate);
									message = new RelayMessage(typeId, emptyObject.PrimaryId, extendedIdBytes, MessageType.Get)
					              	{
					              		Freshness = entry.Updated
					              	};
								}
							}
						}
						if (!entry.IsFound)
						{
							RelayMessage.GetExtendedInfo(emptyObject, out extendedIdBytes, out lastUpdatedDate);
							message = new RelayMessage(typeId, emptyObject.PrimaryId, extendedIdBytes, MessageType.Get);
						}
                    } else
                    {
                    	typeId = -1;
                    }
					if (message != null)
					{
						gotOne = true;
					}
					entries[i] = entry;
					messages[i] = message;
					typeIds[i] = typeId;
                }
                if (gotOne)
                {
					ForwardMessages(messages);
                    IList<ICacheParameter> handledVersioned = null;
					now = DateTime.Now;						
					for (int i = 0; i < paramCount; i++)
                    {
						var emptyObject = emptyObjects[i];
						var message = messages[i];
						typeId = typeIds[i];
                    	var useLocalCaching = typeId >= 0 && UseCacheLookup(typeId);
						if (useLocalCaching)
						{
							// checked after forwarder sends since that's when
							// legacy serialization can be set
							AssertNoLegacySerialization(message);
						}
						try
                        {
							//check for null so exception won't be thrown and the rest of the objects can be retrieved
                            if (emptyObject != null)
                            {
								if (message != null)
                            	{
									var entry = useLocalCaching ? entries[i] :
										StorageEntry<ICacheParameter>.NotFound;
									if (message.ErrorOccurred)
									{
										emptyObject.DataSource = DataSource.CacheError;
									}
									else if (!entry.IsFound)
									{
										// try to read from cache
										if (message.GetObject(emptyObject))
										{
											emptyObject.DataSource = DataSource.Cache;
											if (useLocalCaching)
											{
												SaveToLocalCache(typeId, emptyObject, message);
											}
										}
									}
									else if (!message.Freshness.HasValue)
									{
										// no freshness, try to get cache version
										if (message.GetObject(emptyObject))
										{
											emptyObject.DataSource = DataSource.Cache;
											// uses local caching must be true to get here
											SaveToLocalCache(typeId, emptyObject, message);
										}
										else
										{
											// was deleted on server
											DeleteFromLocalCache(typeId, emptyObject);
										}
									}
									else
									{
										var cmp = entry.Updated.CompareTo(message.Freshness.Value);
										if (cmp < 0)
										{
											// cache version newer
											if (message.GetObject(emptyObject))
											{
												emptyObject.DataSource = DataSource.Cache;
												SaveToLocalCache(typeId, emptyObject, message);
											}
											else
											{
												log.WarnFormat("Got a response with newer freshness but with no payload for an object of type {0} and ID {1}.",
													emptyObject.GetType().FullName, LocalCache.GetKey(emptyObject));
											}
										}
										else
										{
											if (cmp > 0)
											{
												// server shouldn't have older version than cache
												log.WarnFormat("Got a response with older freshness for an object of type {0} and ID {1}.",
													emptyObject.GetType().FullName, LocalCache.GetKey(emptyObject));
											}
											Copy(entry.Instance, emptyObject);
											emptyObject.DataSource = DataSource.Cache;
											RefreshLocalCacheExpires(typeId, entry.Instance);
										}
									}									
                            	}
                            }
                        }
                        catch (UnhandledVersionException uve)
                        {
                            string typeName = emptyObject.GetType().FullName;

                            IVersionSerializable ivs = emptyObject as IVersionSerializable; //shouldn't throw this exception unless it was IVS, but still...
                            if (log.IsWarnEnabled)
                            {
                                if (ivs != null)
                                {
                                    log.WarnFormat("Got UnhandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Received: {4}",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        ivs.CurrentVersion,
                                        uve.VersionExpected,
                                        uve.VersionRecieved
                                        );
                                }
                                else
                                {
                                    log.WarnFormat("Got UnhandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Received: {3}",
                                         typeName,
                                         emptyObject.PrimaryId.ToString("N0"),
                                         uve.VersionExpected,
                                         uve.VersionRecieved                                        
                                        );
                                }
                            }
                            emptyObject.DataSource = DataSource.Unknown;
                        }
                        catch (HandledVersionException hve)
                        {
                            if (handledVersioned == null)
                            {
                                handledVersioned = new List<ICacheParameter>();
                            }
                            string typeName = emptyObject.GetType().FullName;

                            IVersionSerializable ivs = emptyObject as IVersionSerializable; //shouldn't throw this exception unless it was IVS, but still...
                            if (log.IsWarnEnabled)
                            {
                                if (ivs != null)
                                {
                                    log.WarnFormat("Got HandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Handled: {4}. Resaving object.",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        ivs.CurrentVersion,
                                        hve.VersionExpected,
                                        hve.VersionHandled
                                        );
                                }
                                else
                                {
                                    log.WarnFormat("Got HandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Handled: {3}. Resaving object.",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        hve.VersionExpected,
                                        hve.VersionHandled
                                        );
                                }
                            }
                            emptyObject.DataSource = DataSource.Cache;
                            handledVersioned.Add(emptyObject);
                        }
                    }
                    if (handledVersioned != null)
                    {
                        SaveObjects(handledVersioned);
                    }
                }
            }
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                {
                    log.ErrorFormat("Error getting object list: {0}", ex);
                }
            }
        }
      #endregion
		
        #region Get with result ...
		/// <summary>
		/// Finds and loads the given <see cref="ICacheParameter"/> object from the transport.
		/// </summary>
		/// <typeparam name="T">A type that implements <see cref="ICacheParameter"/>.</typeparam>
		/// <param name="emptyObject">The object to be loaded by its <see cref="ICacheParameter.PrimaryId"/>,
		/// or if <see cref="IExtendedCacheParameter"/> is implemented, the <see cref="IExtendedCacheParameter.ExtendedId"/>
		/// will be used. 
		/// </param>
		/// <param name="cacheGetArgs">Object of type <see cref="CacheGetArgs"/> that determines how objects will be 
		/// handled in cache.
		/// </param>
		/// <returns>A <see cref="RelayResult{T}"/> instance containing the result of this operation; never <see langword = "null"/>.</returns>
		public RelayResult<T> GetObjectWithResult<T>(T emptyObject, CacheGetArgs cacheGetArgs) where T : ICacheParameter
		{  	
			if (emptyObject == null)
			{
				throw new ArgumentNullException("emptyObject");
			}
			if (cacheGetArgs == null)
			{
				throw new ArgumentNullException("cacheGetArgs");
			}
			var relayResult = new RelayResult<T>(RelayResultType.NotFound, null,
				emptyObject);
			emptyObject.DataSource = DataSource.Unknown;
			DateTime now;
			try
			{
				short typeId;
				bool useCompression;

				if (TryGetTypeInfo(emptyObject, out typeId, out useCompression))
				{
					var useLocalCaching = UseCacheLookup(typeId);
					RelayMessage message;
					var entry = new StorageEntry<T>();
					if (useLocalCaching)
					{
						now = DateTime.Now;
						// lookup first, providing emptyObject for deserialization if used
						entry = LocalCache.Get(emptyObject);
						if (entry.IsFound)
						{
							if (entry.Expires > now)
							{
								Copy(entry.Instance, emptyObject);
								emptyObject.DataSource = DataSource.Cache;
								return new RelayResult<T>(RelayResultType.Success,
									null, emptyObject);
							}
						}
					}
					if (entry.IsFound)
					{
						message = RelayMessage.GetConditionalGetMessageForObject(typeId,
							emptyObject, entry.Updated);
					}
					else
					{
						message = RelayMessage.GetGetMessageForObject(typeId,
							emptyObject);
					}
					ForwardMessage(message);
					if (useLocalCaching)
					{
						// checked after forwarder sends since that's when
						// legacy serialization can be set
						AssertNoLegacySerialization(message);
					}
					try
					{
						if (message.ErrorOccurred)
						{
							emptyObject.DataSource = DataSource.CacheError;
							relayResult = new RelayResult<T>(RelayResultType.Error,
								new RelayException(message.ErrorType), emptyObject);

						}
						else if (!entry.IsFound)
						{
							// try to read from cache
							if (message.GetObject(emptyObject))
							{
								emptyObject.DataSource = DataSource.Cache;
								relayResult = new RelayResult<T>(RelayResultType.Success,
									null, emptyObject);
								if (useLocalCaching)
								{
									SaveToLocalCache(emptyObject, message);
								}
							}
						}
						else if (!message.Freshness.HasValue)
						{
							// no freshness, try to get cache version
							if (message.GetObject(emptyObject))
							{
								emptyObject.DataSource = DataSource.Cache;
								relayResult = new RelayResult<T>(RelayResultType.Success,
									null, emptyObject);
								// uses local caching must be true to get here
								SaveToLocalCache(emptyObject, message);
							}
							else
							{
								// was deleted on server
								LocalCache.Delete(emptyObject);
							}
						}
						else
						{
							var cmp = entry.Updated.CompareTo(message.Freshness.Value);
							if (cmp < 0)
							{
								// cache version newer
								if (message.GetObject(emptyObject))
								{
									relayResult = new RelayResult<T>(RelayResultType.Success,
										null, emptyObject);
									emptyObject.DataSource = DataSource.Cache;
									SaveToLocalCache(emptyObject, message);
								}
								else
								{
									log.WarnFormat("Got a response with newer freshness but with no payload for an object of type {0} and ID {1}.",
										typeof(T).FullName, LocalCache.GetKey(emptyObject));
								}
							}
							else
							{
								if (cmp > 0)
								{
									// server shouldn't have older version than cache
									log.WarnFormat("Got a response with older freshness for an object of type {0} and ID {1}.",
										typeof(T).FullName, LocalCache.GetKey(emptyObject));
								}
								relayResult = new RelayResult<T>(RelayResultType.Success,
									null, emptyObject);
								Copy(entry.Instance, emptyObject);
								emptyObject.DataSource = DataSource.Cache;
								LocalCache.RefreshExpires(entry.Instance);
							}
						}									
					}
					catch (UnhandledVersionException uve)
					{
						IVersionSerializable ivs = emptyObject as IVersionSerializable;
						emptyObject.DataSource = DataSource.Unknown;
                        if (log.IsWarnEnabled)
                        {
                            if (ivs != null)
                            {
                                log.WarnFormat("Got UnhandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Received: {4}. Sending delete for this object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    ivs.CurrentVersion,
                                    uve.VersionExpected,
                                    uve.VersionRecieved
                                    );
                            }
                            else
                            {
                                log.WarnFormat("Got UnhandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Received: {3}. Sending delete for this object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    uve.VersionExpected,
                                    uve.VersionRecieved
                                    );
                            }
                        }
						relayResult = new RelayResult<T>(RelayResultType.Error, uve, emptyObject);
						if(cacheGetArgs.RectifyCorruptObjects)
						{
							this.DeleteObject<T>(emptyObject);
						}
					}
					catch (HandledVersionException hve)
					{
						IVersionSerializable ivs = emptyObject as IVersionSerializable;
						emptyObject.DataSource = DataSource.Cache;
                        if (log.IsWarnEnabled)
                        {
                            if (ivs != null)
                            {
                                log.WarnFormat("Got HandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Handled: {4}. Resaving object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    ivs.CurrentVersion,
                                    hve.VersionExpected,
                                    hve.VersionHandled
                                    );
                            }
                            else
                            {
                                log.WarnFormat("Got HandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Handled: {3}. Resaving object.",
                                    typeof(T).FullName,
                                    emptyObject.PrimaryId.ToString("N0"),
                                    hve.VersionExpected,
                                    hve.VersionHandled
                                    );
                            }
                        }
						this.SaveObject<T>(emptyObject);
						relayResult = new RelayResult<T>(RelayResultType.Success, hve, emptyObject);
					}
					return relayResult;
				}
				else
				{
					return new RelayResult<T>(RelayResultType.Error, new ApplicationException("TypeInfo not found."), emptyObject);
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                {
                    log.ErrorFormat("Error getting object {0} of type {1}: {2}", 
                        emptyObject.PrimaryId.ToString("N0"),  
                        emptyObject.GetType().FullName, 
                        ex);
                }
				emptyObject.DataSource = DataSource.CacheError;
				relayResult = new RelayResult<T>(RelayResultType.Error, ex, emptyObject);
				return relayResult;
			}
		}

		/// <summary>
		/// Finds and loads the given list of <see cref="ICacheParameter"/> objects from the transport.
		/// </summary>
		/// <param name="emptyObjects">The objects to be loaded by the <see cref="ICacheParameter.PrimaryId"/>,
		/// or if <see cref="IExtendedCacheParameter"/> is implemented, the <see cref="IExtendedCacheParameter.ExtendedId"/>
		/// will be used. 
		/// </param>
		/// <param name="cacheGetArgs">Object of type <see cref="CacheGetArgs"/> that determines how objects will be 
		/// handled in cache.
		/// </param>
		/// <returns>A <see cref="RelayResults"/> instance containing the result of this operation; never <see langword = "null"/>.</returns>
		/// <exception cref="ArgumentNullException">If the specified <param name="emptyObjects"/> is <see langword="null"/>.</exception>
		public RelayResults GetObjectsWithResult(IList<ICacheParameter> emptyObjects, CacheGetArgs cacheGetArgs)
		{
			if (emptyObjects == null)
			{
				throw new ArgumentNullException("emptyObjects");
			}
			if (cacheGetArgs == null)
			{
				throw new ArgumentNullException("cacheGetArgs");
			}

			RelayResults relayResults = null;
			try
			{
				relayResults = new RelayResults();
				var paramCount = emptyObjects.Count;
				var messages = new RelayMessage[paramCount];
				var entries = new StorageEntry<ICacheParameter>[paramCount];
				var typeIds = new short[paramCount];

				short typeId;
				bool useCompression;
				bool gotOne = false;
				DateTime? lastUpdatedDate;
				byte[] extendedIdBytes;
				var now = DateTime.Now;
				for (int i = 0; i < emptyObjects.Count; i++)
				{
					ICacheParameter emptyObject = emptyObjects[i];
					RelayMessage message = null;
					var entry = StorageEntry<ICacheParameter>.NotFound;
					if ((emptyObject != null) && TryGetTypeInfo(emptyObject, out typeId, out useCompression))
					{
						emptyObject.DataSource = DataSource.Unknown;
						if (UseCacheLookup(typeId))
						{
							entry = LocalCache.Get(typeId, LocalCache.GetKey(
								emptyObject), () => emptyObject);
							if (entry.IsFound)
							{
								if (entry.Expires > now)
								{
									Copy(entry.Instance, emptyObject);
									emptyObject.DataSource = DataSource.Cache;
								}
								else
								{
									RelayMessage.GetExtendedInfo(emptyObject, out extendedIdBytes, out lastUpdatedDate);
									message = new RelayMessage(typeId, emptyObject.PrimaryId, extendedIdBytes, MessageType.Get);
									message.Freshness = entry.Updated;
								}
							}
						}
						if (!entry.IsFound)
						{
							RelayMessage.GetExtendedInfo(emptyObject, out extendedIdBytes, out lastUpdatedDate);
							message = new RelayMessage(typeId, emptyObject.PrimaryId, extendedIdBytes, MessageType.Get);
						}
					}
					else
					{
						typeId = -1;
					}
					if (message != null)
					{
						gotOne = true;
					}
					messages[i] = message;
					entries[i] = entry;
					typeIds[i] = typeId;
				}
				if (gotOne)
				{
					ForwardMessages(messages);
					IList<ICacheParameter> unhandledVersioned = null, handledVersioned = null;
					now = DateTime.Now;
					for (int i = 0; i < emptyObjects.Count; i++)
					{
						typeId = typeIds[i];
						var useLocalCaching = typeId >= 0 && UseCacheLookup(typeId);
						ICacheParameter emptyObject = emptyObjects[i];
						var message = messages[i];
						if (useLocalCaching)
						{
							// checked after forwarder sends since that's when
							// legacy serialization can be set
							AssertNoLegacySerialization(message);
						}
						try
						{
							if (message != null)
							{
								var entry = useLocalCaching ? entries[i] :
									StorageEntry<ICacheParameter>.NotFound;
								if (message.ErrorOccurred)
								{
									emptyObject.DataSource = DataSource.CacheError;
									relayResults.Add(new RelayResult<ICacheParameter>(
										RelayResultType.Error, new RelayException(
										message.ErrorType), emptyObject));

								}
								else if (!entry.IsFound)
								{
									// try to read from cache
									if (message.GetObject(emptyObject))
									{
										emptyObject.DataSource = DataSource.Cache;
										relayResults.Add(new RelayResult<ICacheParameter>(
											RelayResultType.Success, null, emptyObject));
										if (useLocalCaching)
										{
											SaveToLocalCache(typeId, emptyObject, message);
										}
									} else
									{
										relayResults.Add(new RelayResult<ICacheParameter>(
											RelayResultType.NotFound, null, emptyObject));
									}
								}
								else if (!message.Freshness.HasValue)
								{
									// no freshness, try to get cache version
									if (message.GetObject(emptyObject))
									{
										emptyObject.DataSource = DataSource.Cache;
										relayResults.Add(new RelayResult<ICacheParameter>(
											RelayResultType.Success, null, emptyObject));
										// uses local caching must be true to get here
										SaveToLocalCache(typeId, emptyObject, message);
									}
									else
									{
										// was deleted on server
										DeleteFromLocalCache(typeId, emptyObject);
										relayResults.Add(new RelayResult<ICacheParameter>(
											RelayResultType.NotFound, null, emptyObject));
									}
								}
								else
								{
									var cmp = entry.Updated.CompareTo(message.Freshness.Value);
									if (cmp < 0)
									{
										// cache version newer
										if (message.GetObject(emptyObject))
										{
											relayResults.Add(new RelayResult<ICacheParameter>(
												RelayResultType.Success, null, emptyObject));
											emptyObject.DataSource = DataSource.Cache;
											SaveToLocalCache(typeId, emptyObject, message);
										}
										else
										{
											log.WarnFormat("Got a response with newer freshness but with no payload for an object of type {0} and ID {1}.",
												emptyObject.GetType().FullName, LocalCache.GetKey(emptyObject));
											relayResults.Add(new RelayResult<ICacheParameter>(
												RelayResultType.NotFound, null, emptyObject));
										}
									}
									else
									{
										if (cmp > 0)
										{
											// server shouldn't have older version than cache
											log.WarnFormat("Got a response with older freshness for an object of type {0} and ID {1}.",
												emptyObject.GetType().FullName, LocalCache.GetKey(emptyObject));
										}
										relayResults.Add(new RelayResult<ICacheParameter>(
											RelayResultType.Success, null, emptyObject));
										Copy(entry.Instance, emptyObject);
										emptyObject.DataSource = DataSource.Cache;
										RefreshLocalCacheExpires(typeId, emptyObject);
									}
								}									
							}
							else
							{
								if (emptyObject == null)
								{
									relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Error, new System.ArgumentNullException("emptyObject"), emptyObject));
								}
								else if (!useLocalCaching)
								{
									emptyObject.DataSource = DataSource.Unknown;
									relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Error, new ApplicationException("TypeInfo not found."), emptyObject));
								}
							}
						}
						catch (UnhandledVersionException uve)
						{
							if (unhandledVersioned == null)
							{
								unhandledVersioned = new List<ICacheParameter>();
							}
							string typeName = emptyObject.GetType().FullName;

							IVersionSerializable ivs = emptyObject as IVersionSerializable; //shouldn't throw this exception unless it was IVS, but still...
                            if (log.IsWarnEnabled)
                            {
                                if (ivs != null)
                                {
                                    log.WarnFormat("Got UnhandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Received: {4}.{5}",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        ivs.CurrentVersion,
                                        uve.VersionExpected,
                                        uve.VersionRecieved,
                                        cacheGetArgs.RectifyCorruptObjects ? " Sending delete for this object." : String.Empty
                                        );
                                }
                                else
                                {
                                    log.WarnFormat("Got UnhandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Received: {3}.{4}",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        uve.VersionExpected,
                                        uve.VersionRecieved,
                                        cacheGetArgs.RectifyCorruptObjects ? " Sending delete for this object." : String.Empty
                                        );
                                }
                            }
							emptyObject.DataSource = DataSource.Unknown;
							unhandledVersioned.Add(emptyObject);
							relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Error, uve, emptyObject));
						}
						catch (HandledVersionException hve)
						{
							if (handledVersioned == null)
							{
								handledVersioned = new List<ICacheParameter>();
							}
							string typeName = emptyObject.GetType().FullName;

							IVersionSerializable ivs = emptyObject as IVersionSerializable; //shouldn't throw this exception unless it was IVS, but still...
                            if (log.IsWarnEnabled)
                            {
                                if (ivs != null)
                                {
                                    log.WarnFormat("Got HandledVersionException while getting an object of type {0} and ID {1}. Current version: {2}, Version Expected: {3}, Version Handled: {4}. Resaving object.",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        ivs.CurrentVersion,
                                        hve.VersionExpected,
                                        hve.VersionHandled
                                        );
                                }
                                else
                                {
                                    log.WarnFormat("Got HandledVersionException while getting a non-IVersionSerializable object of type {0} and ID {1}. Version Expected: {2}, Version Handled: {3}. Resaving object.",
                                        typeName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        hve.VersionExpected,
                                        hve.VersionHandled
                                        );
                                }
                            }
							emptyObject.DataSource = DataSource.Cache;
							handledVersioned.Add(emptyObject);
							relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Success, hve, emptyObject));
						}
						catch(Exception e)
						{   
							if (unhandledVersioned == null)
							{
								unhandledVersioned = new List<ICacheParameter>();
							}
							IVersionSerializable ivs = emptyObject as IVersionSerializable;
                            if (log.IsErrorEnabled)
                            {
                                if (ivs != null)
                                {
                                    log.ErrorFormat("Got Exception while getting an object of type {0} and ID {1}. Current version: {2}.{3}: {4}",
                                        emptyObject.GetType().FullName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        ivs.CurrentVersion,
                                        cacheGetArgs.RectifyCorruptObjects ?  " Sending delete for this object." : String.Empty,
                                        e
                                        );
                                }
                                else
                                {
                                    log.ErrorFormat("Got Exception while getting a non-IVersionSerializable object of type {0} and ID {1}.{2}: {3}",
                                        emptyObject.GetType().FullName,
                                        emptyObject.PrimaryId.ToString("N0"),
                                        cacheGetArgs.RectifyCorruptObjects ?  " Sending delete for this object." : String.Empty,
                                        e
                                        );
                                }
                            }
							emptyObject.DataSource = DataSource.Unknown;
							unhandledVersioned.Add(emptyObject);
							relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Error, e, emptyObject));
						}
					}
					if (unhandledVersioned != null && cacheGetArgs.RectifyCorruptObjects)
					{
						DeleteObjects(unhandledVersioned);
					}
					if (handledVersioned != null)
					{
						SaveObjects(handledVersioned);
					}
				}
				else
				{
					for (int i = 0; i < emptyObjects.Count; i++)
					{
						//Type not found
						relayResults.Add(new RelayResult<ICacheParameter>(RelayResultType.Error,
							new ApplicationException("TypeInfo not found."), emptyObjects[i]));
					}
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
				if(log.IsErrorEnabled)
                    log.ErrorFormat("Error getting object list: {0}", ex);
				return relayResults;
			}
			return relayResults;
		}

		#endregion

        #region Update...

		/// <summary>
		/// Updates a single object in the cache.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj"></param>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed executions will throw this exception
		/// </exception>
		/// <remarks>
		/// added: cbrown
		/// In the event that the type of object T is configured to do so,
		/// a synchronous "in" message may throw a SyncRelayOperationException
		/// This will be passed on to the calling process
		/// Since this is expected to happen frequently under normal operating
		/// load, logging is not included at this level.  The calling process
		/// is responsible for logging events as required.
		/// </remarks>
        public void UpdateObject<T>(T obj) where T : ICacheParameter
        {
            if (obj == null)
            {
                return;
            }

            try
			{
				var message = GetUpdateMessage(obj);
				if (message != null)
				{
					var useLocalCaching = UseCacheLookup(message.TypeId);
					if (useLocalCaching)
					{
						SaveToLocalCache(obj, message);
					}
					ForwardMessage(message);
					if (useLocalCaching)
					{
						// checked after forwarder sends since that's when
						// legacy serialization can be set
						AssertNoLegacySerialization(message);
					}					
				}
			}
			catch (SyncRelayOperationException) 
			{
				// pass this on to the caller
				throw;
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if(log.IsErrorEnabled)
                    log.ErrorFormat("Error updating object of type {0} and id {1}: {2}",
                        typeof(T).FullName,
                        obj.PrimaryId.ToString("N0"),
                        ex);
            }
        }

		/// <summary>
		/// Updates multiple objects in the cache.
		/// </summary>
		/// <param name="objects"></param>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed executions will throw this exception
		/// </exception>
		/// <remarks>
		/// added: cbrown
		/// In the event that the type of an object in the list is configured to do so,
		/// a synchronous "in" message may throw a SyncRelayOperationException
		/// This will be passed on to the calling process
		/// This will happen AFTER any possible successes have been processed.
		/// For example, if 10 objects are in the "objects" list, and 1 of them
		/// fails (and has the proper settings), the other 9 objects are properly
		/// updated, and the exception will be thrown.
		/// Since this is expected to happen frequently under normal operating
		/// load, logging is not included at this level.  The calling process
		/// is responsible for logging events as required.
		/// </remarks>
		public void UpdateObjects(IList<ICacheParameter> objects)
        {
            try
            {
                List<RelayMessage> messages = new List<RelayMessage>(objects.Count);
                short typeId;
                bool useCompression = false;                
                byte[] extendedIdBytes;
                DateTime? lastUpdatedDate;
				var now = DateTime.Now;
				RelayMessage message;
            	var count = objects.Count;
            	var useCacheLookups = new bool[count];
				var gotOne = false;
                for (int i = 0; i < count; i++)
                {
					var obj = objects[i];
					if (TryGetTypeInfo(obj, out typeId, out useCompression))
                    {
						RelayMessage.GetExtendedInfo(obj, out extendedIdBytes, out lastUpdatedDate);
						message = RelayMessage.GetUpdateMessageForObject(typeId, obj.PrimaryId, extendedIdBytes, lastUpdatedDate, obj, useCompression);
                    	var useCacheLookup = useCacheLookups[i] = UseCacheLookup(typeId);
						if (useCacheLookup)
						{
							gotOne = true;
							SaveToLocalCache(typeId, obj, message);
						}
						messages.Add(message);
                    }
                }
				ForwardMessages(messages);
				if (gotOne)
				{
					for (int i = 0; i < count; i++)
					{
						if (useCacheLookups[i])
						{
							AssertNoLegacySerialization(messages[i]);
						}
					}					
				}
            }
			catch (SyncRelayOperationException) 
			{
				// pass this on to the caller
				throw;
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating object list: {0}", ex);
            }
        }

        #endregion

        #region Save...

		private static StorageEntry<T> SaveToLocalCache<T>(T instance,
			RelayMessage message) where T : ICacheParameter
		{
			return LocalCache.Save(instance, message.Payload.LastUpdatedDate);
		}

		private static StorageEntry<ICacheParameter> SaveToLocalCache(short typeId,
			ICacheParameter instance, RelayMessage message)
		{
			return LocalCache.Save(typeId, instance, new LocalCacheOptions
				{Updated = message.Payload.LastUpdatedDate});
		}

		/// <summary>
		/// Adds an item to the cache.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj"></param>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed executions will throw this exception
		/// </exception>
		/// <remarks>
		/// added: cbrown
		/// In the event that the type of object T is configured to do so,
		/// a synchronous "in" message may throw a SyncRelayOperationException
		/// This will be passed on to the calling process
		/// Since this is expected to happen frequently under normal operating
		/// load, logging is not included at this level.  The calling process
		/// is responsible for logging events as required.
		/// </remarks>
        public void SaveObject<T>(T obj) where T : ICacheParameter
        {
            if (obj == null)
            {
                return;
            }
			try
            {
				var message = GetSaveMessage(obj);
				if (message != null)
				{
					var useCacheLookup = UseCacheLookup(message.TypeId);
					if (useCacheLookup)
					{
						SaveToLocalCache(obj, message);
					}
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}					
				}
			}
			catch (SyncRelayOperationException) 
			{
				// pass this on to the caller
				throw;
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error saving object of type {0} and id {1}: {2}",
                        typeof(T).FullName,
                        obj.PrimaryId.ToString("N0"),
                        ex);
            }
		}

		/// <summary>
		/// Adds multiple items to the cache.
		/// </summary>
		/// <param name="objects"></param>
		/// <exception cref="SyncRelayOperationException">
		/// When the type of an object is defined with settings
		///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
		///		SyncInMessages=true and 
		///		ThrowOnSyncFailure=true
		///	failed executions will throw this exception
		/// </exception>
		/// <remarks>
		/// added: cbrown
		/// In the event that the type of an object in the list is configured to do so,
		/// a synchronous "in" message may throw a SyncRelayOperationException
		/// This will be passed on to the calling process
		/// This will happen AFTER any possible successes have been processed.
		/// For example, if 10 objects are in the "objects" list, and 1 of them
		/// fails (and has the proper settings), the other 9 objects are properly
		/// saved, and the exception will be thrown.
		/// Since this is expected to happen frequently under normal operating
		/// load, logging is not included at this level.  The calling process
		/// is responsible for logging events as required.
		/// </remarks>
        public void SaveObjects(IList<ICacheParameter> objects)
		{
            try
            {
                List<RelayMessage> messages = new List<RelayMessage>(objects.Count);
                short typeId;
                bool useCompression = false;
                RelayMessage message;
                byte[] extendedIdBytes;
                DateTime? lastUpdatedDate;
				var now = DateTime.Now;
				var count = objects.Count;
				var useCacheLookups = new bool[count];
				var gotOne = false;
				for (int i = 0; i < count; i++)
                {
					var obj = objects[i];
                    //Check for null because if exception is thrown the rest of the list will not get saved.
                    if (obj == null)
                    {
                        if (log.IsWarnEnabled)
                        {
                            log.Warn("Null passed into SaveObjects list");
                        }
                    }
					else if (TryGetTypeInfo(obj, out typeId, out useCompression))
                    {
						RelayMessage.GetExtendedInfo(obj, out extendedIdBytes, out lastUpdatedDate);
						message = RelayMessage.GetSaveMessageForObject(typeId, obj.PrimaryId, extendedIdBytes, lastUpdatedDate, obj, useCompression);
						var useCacheLookup = useCacheLookups[i] = UseCacheLookup(typeId);
						if (useCacheLookup)
						{
							gotOne = true;
							SaveToLocalCache(typeId, obj, message);
						}
                        messages.Add(message);
                    }
                }
				ForwardMessages(messages);
				if (gotOne)
				{
					for (int i = 0; i < count; i++)
					{
						if (useCacheLookups[i])
						{
							AssertNoLegacySerialization(messages[i]);
						}
					}
				}
			}
			catch (SyncRelayOperationException) 
			{
				// pass this on to the caller
				throw;
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error saving object list: {0}", ex);
            }
        }

        #endregion

        #region Delete...

		private static bool DeleteFromLocalCache(short typeId, ICacheParameter instance)
		{
			return LocalCache.Delete(typeId, LocalCache.GetKey(instance));
		}

        public void DeleteObject<T>(T obj) where T : ICacheParameter
        {
            if (obj == null)
            {
                return;
            }
            int primaryId = obj.PrimaryId;
            byte[] extendedIdBytes = null;
            string typeName;
        	short typeId;
			typeName = GetTypeName(obj);
			DateTime? lastUpdatedDate;
            RelayMessage.GetExtendedInfo(obj, out extendedIdBytes, out lastUpdatedDate);
			var useLocalCaching = false;
			if (TryGetTypeId(typeName, out typeId))
			{
				useLocalCaching = UseCacheLookup(typeId);				
			}
			if (useLocalCaching)
			{
				var key = LocalCache.GetKey(obj);
				switch(key.Key.Type)
				{
					case DataBufferType.Int32:
						DeleteObject(key.PartitionId, typeName);
						break;
					case DataBufferType.String:
						DeleteObject(key.PartitionId, key.Key.ToString(), typeName);
						break;
					case DataBufferType.ByteArraySegment:
						DeleteObject(key.PartitionId, extendedIdBytes, typeName);
						break;
					default:
						throw new NotSupportedException(string.Format(
							"Key type {0} not supported", key.Key.Type));
				}
			} else
			{
				DeleteObject(primaryId, extendedIdBytes, typeName);		
			}
        }

        

		/// <summary>
		/// 	<para>Deletes from cache an object of the specified 
		///		type and ID.</para>
		/// </summary>
		/// <typeparam name="T">
		///		<para>The type of object to delete from cache.</para>
		/// </typeparam>
		/// <param name="primaryId">
		/// 	<para>The ID of object to delete from cache.</para>
		/// </param>
        [Obsolete("This function cannot support virtual type ids and has been deprecated")]
        public void DeleteObject<T>(int primaryId) where T : ICacheParameter
		{
            DeleteObject(primaryId, typeof(T).FullName);
		}

		/// <summary>
		/// 	<para>Deletes from cache an object of the specified 
		///		type and ID.</para>
		/// </summary>
		/// <typeparam name="T">
		///		<para>The type of object to delete from cache.</para>
		/// </typeparam>
		/// <param name="primaryId">
		/// 	<para>The ID of object to delete from cache.</para>
		/// </param>
        /// <param name="extendedId">
        ///     <para>The extended ID of the object to delete from cache.</para>
        /// </param>
        [Obsolete("This function cannot support virtual type ids and has been deprecated")]
        public void DeleteObject<T>(int primaryId, string extendedId) where T : ICacheParameter
		{
            DeleteObject(primaryId, extendedId, typeof(T).FullName);
		}

        /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and extended ID. MySpace.Common.HelperObjects.StringUtility.GetStringHash
        ///		will be used to generate the primaryId.
        ///		</para>
        /// </summary>
        /// <typeparam name="T">
        ///		<para>The type of object to delete from cache.</para>
        /// </typeparam>
        /// <param name="extendedId">
        /// 	<para>The extended ID of object to delete from cache as a string.</para>
        /// </param>
        [Obsolete("This function cannot support virtual type ids and has been deprecated")]
        public void DeleteObject<T>(string extendedId) where T : ICacheParameter
        {
            int primaryId = 0;
            try            
            {
                primaryId = MySpace.Common.HelperObjects.StringUtility.GetStringHash(extendedId);
                DeleteObject(primaryId, extendedId, typeof(T).FullName);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object of type {0} and id {1}-{2}: {3}", 
                        typeof(T).FullName,
                        primaryId.ToString("N0"),
                        extendedId,
                        ex);
            }
        }

        /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and ID.</para>
        /// </summary>
        /// <typeparam name="T">
        ///		<para>The type of object to delete from cache.</para>
        /// </typeparam>
        /// <param name="primaryId">
        /// 	<para>The ID of object to delete from cache.</para>
        /// </param>
        [Obsolete("This function cannot support virtual type ids and has been deprecated")]
        public void DeleteObject<T>(int primaryId, byte[] extendedId) where T : ICacheParameter
        {
            DeleteObject(primaryId, extendedId, typeof(T).FullName);
        }

        /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and ID.</para>
        /// </summary>
        /// <param name="typeName">
        ///		<para>The name of the type of object to delete from cache.</para>
        /// </param>
        /// <param name="primaryId">
        /// 	<para>The ID of object to delete from cache.</para>
        /// </param>
		public void DeleteObject(int primaryId, string typeName)
		{
			try
			{
				short typeId;

				if (TryGetTypeId(typeName, out typeId))
				{
					var useCacheLookup = UseCacheLookup(typeId);
					if (useCacheLookup)
					{
						LocalCache.Delete(typeId, primaryId);
					}
					RelayMessage message = new RelayMessage(typeId, primaryId, MessageType.Delete);
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object of type {0} and id {1}: {2}", 
                        typeName,
                        primaryId.ToString("N0"),
                        ex);
			}
		}

        /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and ID.</para>
        /// </summary>
        /// <param name="typeName">
        ///		<para>The name of the type of object to delete from cache.</para>
        /// </typeparam>
        /// <param name="primaryId">
        /// 	<para>The ID of object to delete from cache.</para>
        /// </param>
        /// <param name="extendedId">
        /// 	<para>The extended ID of object to delete from cache as a string.</para>
        /// </param>
		public void DeleteObject(int primaryId, string extendedId, string typeName)
		{
			try
			{
				short typeId;

				if (TryGetTypeId(typeName, out typeId))
				{
					var useCacheLookup = UseCacheLookup(typeId);
					if (useCacheLookup)
					{
						LocalCache.Delete(typeId, new StorageKey(extendedId, primaryId));
					}
                    byte[] extendedIdBytes = RelayMessage.GetStringBytes(extendedId);
					RelayMessage message = new RelayMessage(typeId, primaryId, extendedIdBytes, MessageType.Delete);
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object of type {0} and id {1} and extended Id {2}: {3}",
                        typeName,
                        primaryId.ToString("N0"),
                        extendedId,
                        ex);
			}
		}

        /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and ID.</para>
        /// </summary>
        /// <param name="typeName">
        ///		<para>The name of the type of object to delete from cache.</para>
        /// </typeparam>
        /// <param name="primaryId">
        /// 	<para>The ID of object to delete from cache.</para>
        /// </param>
        /// <param name="extendedId">
        /// 	<para>The extended ID of object to delete from cache as a string.</para>
        /// </param>
        public void DeleteObject(int primaryId, byte[] extendedIdBytes, string typeName)
        {
            try
            {
                short typeId;

                if (TryGetTypeId(typeName, out typeId))
                {
					var useCacheLookup = UseCacheLookup(typeId);
					if (useCacheLookup)
					{
						LocalCache.Delete(typeId, new StorageKey(extendedIdBytes, primaryId));
					}
					RelayMessage message = new RelayMessage(typeId, primaryId, extendedIdBytes, MessageType.Delete);
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}
				}
            }
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object of type {0} and id {1} and raw extended Id {2}: {3}",
                        typeName,
                        primaryId.ToString("N0"),                        
                        GetByteString(extendedIdBytes),
                        ex);
            }
        }

         /// <summary>
        /// 	<para>Deletes from cache an object of the specified 
        ///		type and ID. MySpace.Common.HelperObjects.StringUtility.GetStringHash
        ///		will be used to generate the primaryId.</para>
        /// </summary>
        /// <param name="typeName">
        ///		<para>The name of the type of object to delete from cache.</para>
        /// </typeparam>        
        /// <param name="extendedId">
        /// 	<para>The extended ID of object to delete from cache as a string.</para>
        /// </param>
		public void DeleteObject(string extendedId, string typeName)
		{
            int primaryId = 0;
            try
			{
				short typeId;
                primaryId = MySpace.Common.HelperObjects.StringUtility.GetStringHash(extendedId);
				if (TryGetTypeId(typeName, out typeId))
				{
					var useCacheLookup = UseCacheLookup(typeId);
					if (useCacheLookup)
					{
						LocalCache.Delete(typeId, new StorageKey(extendedId, primaryId));
					}
                    byte[] extendedIdBytes = RelayMessage.GetStringBytes(extendedId);
					RelayMessage message = new RelayMessage(typeId, primaryId, extendedIdBytes, MessageType.Delete);
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object of type {0} and id {1} and extended Id {2}: {3}", 
                        typeName, primaryId.ToString("N0"), extendedId, ex);
			}
		}
		
		public void DeleteObjects(IList<ICacheParameter> objects)
		{
            try
            {
                List<RelayMessage> messages = new List<RelayMessage>(objects.Count);
                short typeId;
				bool useCompression;
                RelayMessage message;
                
                byte[] extendedIdBytes;
                DateTime? lastupdatedDate;
            	var count = objects.Count;
            	var useCacheLookups = new bool[count];
				var gotOne = false;

                for (int i = 0; i < count; i++)
                {
                	var obj = objects[i];
					//check for null in objects list so other items in the list can still be deleted
                    if (obj!=null && TryGetTypeInfo(obj, out typeId, out useCompression))
                    {
                        RelayMessage.GetExtendedInfo(obj, out extendedIdBytes, out lastupdatedDate);
                    	var useCacheLookup = useCacheLookups[i] = UseCacheLookup(typeId);
						if (useCacheLookup)
						{
							gotOne = true;
							DeleteFromLocalCache(typeId, obj);
						}                       
                        message = new RelayMessage(typeId, obj.PrimaryId, extendedIdBytes, MessageType.Delete);
                        messages.Add(message);
                    }
                }
				ForwardMessages(messages);
				if (gotOne)
				{
					for (int i = 0; i < count; i++)
					{
						if (useCacheLookups[i])
						{
							AssertNoLegacySerialization(messages[i]);
						}
					}
				}
            }
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object list: {0}", ex);
            }
		}

		private void DeleteObjectInAllTypes(StorageKey key)
		{
			var typeSettings = _configuration.TypeSettings.TypeSettingCollection;
			var maxId = typeSettings.MaxTypeId;
			for(short id = 0; id <= maxId; ++id)
			{
				var typeSetting = typeSettings[id];
				if (typeSetting == null) continue;
				if (UseCacheLookup(typeSetting.TypeId))
				{
					LocalCache.Delete(typeSetting.TypeId, key);
				}
			}
		}

		public void DeleteObjectInAllTypes(int objectId)
		{
			try
			{
				DeleteObjectInAllTypes(new StorageKey(objectId, objectId));
				RelayMessage message = new RelayMessage();
				message.MessageType = MessageType.DeleteInAllTypes;
				message.Id = objectId;
				ForwardMessage(message);
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object {0} in all types - {1}", objectId.ToString("N0"), ex);
			}
		}

		public void DeleteObjectInAllTypes(int objectId, string extendedId)
		{
			try
			{
				DeleteObjectInAllTypes(new StorageKey(extendedId, objectId));
				RelayMessage message = new RelayMessage();
				message.MessageType = MessageType.DeleteInAllTypes;
				message.Id = objectId;
                message.ExtendedId = RelayMessage.GetStringBytes(extendedId);
				ForwardMessage(message);
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object {0}:{1} in all types - {2}", objectId.ToString("N0"), extendedId, ex);
			}
		}

        public void DeleteObjectInAllTypes(int objectId, byte[] extendedId)
        {
            try
            {
				DeleteObjectInAllTypes(new StorageKey(extendedId, objectId));
				RelayMessage message = new RelayMessage();
                message.MessageType = MessageType.DeleteInAllTypes;
                message.Id = objectId;
                message.ExtendedId = extendedId;
				ForwardMessage(message);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting object {0}:{1} in all types - {2}", objectId.ToString("N0"), extendedId, ex);
            }
        }

        [Obsolete("This function cannot support virtual type ids and has been deprecated", true)]
		public void DeleteAllObjectsInType<T>() where T : ICacheParameter
		{
            return;
		}

		public void DeleteAllObjectsInType(string typeName)
		{
			short typeId = -1;
			try
			{
				if (TryGetTypeId(typeName, out typeId))
				{
					var useCacheLookup = UseCacheLookup(typeId);
					if (useCacheLookup)
					{
						LocalCache.Clear(typeId);
					}
					var message = new RelayMessage();
					message.TypeId = typeId;
					message.MessageType = MessageType.DeleteAllInType;
					ForwardMessage(message);
					if (useCacheLookup)
					{
						AssertNoLegacySerialization(message);
					}
				}
			}
			catch (CacheConfigurationException)
			{
				// pass this on to the caller
				throw;
			}
			catch (Exception ex)
			{
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error deleting all in type {0}: {1}", typeId.ToString("N0"), ex);
			}
		}
        #endregion

        #region Send
        /// <summary>
        /// Sends a message constructed manually by the caller. This method is intended
		/// for advanced usage only. Behavior will be synchronous or asynchronous depending
        /// on the message type.
        /// </summary>
        /// <param name="msg">The message to send</param>
        public void SendMessage(RelayMessage msg)
        {
			ForwardMessage(msg);
        }
        
        /// <summary>
        /// Sends a list of messages constructed manually by the caller. This method is intended
		/// for advanced usage only. Behavior will be synchronous or asynchronous depending
        /// on the message type.
        /// </summary>
        /// <param name="msgs">The list of messages to send</param>
        public void SendMessages(IList<RelayMessage> msgs)
        {
			ForwardMessages(msgs);
        }

		/// <summary>
		/// Sends a message constructed manually by the caller. This method is intended
		/// for advanced usage only. Behavior will be synchronous or asynchronous depending
		/// on the message type.
		/// </summary>
		/// <param name="msg">The message to send</param>
		public Future<RelayMessage> SendMessageAsync(RelayMessage msg)
		{
			return ForwardMessageAsync(msg);
		}

		/// <summary>
		/// Sends a list of messages constructed manually by the caller. This method is intended
		/// for advanced usage only. Behavior will be synchronous or asynchronous depending
		/// on the message type.
		/// </summary>
		/// <param name="msgs">The list of messages to send</param>
		public Future SendMessagesAsync(IEnumerable<RelayMessage> msgs)
		{
			return ForwardMessagesAsync(msgs);
		}

    	private static SocketClient _socketClient = null;
		private readonly Object _lock = new Object();
		/// <summary>
		/// Sends a message constructed manually by the caller using an <see cref="IPEndPoint"/>. 
		/// This method is intended for advanced usage only. Behavior will be synchronous.
		/// </summary>
		/// <param name="ipEndPoint"><see cref="IPEndPoint"/> to send message to.</param>
		/// <param name="msg">The message to send.</param>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="ipEndPoint"/> is <see langword="null"/> or empty.</para>
		/// 	<para>-or-</para>
		/// 	<para>The argument <paramref name="msg"/> is <see langword="null"/>.</para>
		/// </exception>
		public void SendMessage(IPEndPoint ipEndPoint, RelayMessage msg)
		{
			if (ipEndPoint == null) throw new ArgumentNullException("ipEndPoint");
			if (msg == null) throw new ArgumentNullException("msg");
			if(_socketClient == null)
			{
				lock(_lock)
				{
					if (_socketClient == null)
					{
						_socketClient = new SocketClient();
					}
				}				
			}

			MemoryStream stream = new MemoryStream();
			RelayMessageFormatter.WriteRelayMessage(msg, stream);
			stream.Seek(0, SeekOrigin.Begin);
			MemoryStream memoryStream = _socketClient.SendSync(ipEndPoint,msg.IsTwoWayMessage ? (int)SocketCommand.HandleSyncMessage : (int)SocketCommand.HandleOneWayMessage, stream);
			RelayMessage replyMessage = RelayMessageFormatter.ReadRelayMessage(memoryStream);
			msg.ExtractResponse(replyMessage);
		}
        #endregion

        #region Invoke

        /// <summary>
        /// Invokes a method on a relay component
        /// </summary>
        /// <typeparam name="TInParam">The class type that contains the input parameters</typeparam>
        /// <typeparam name="TOutParam">The class type that contains the output parameters</typeparam>
        /// <param name="inParam">Instance containing the input parameters for this method call</param>
		/// <param name="messageId">The ID of the <see cref="RelayMessage"/> that will be sent to the server.
		///		Varying this ID can help determine the relay cluster to which the message is sent.</param>
        /// <returns>An instance of TOutParam containing the output for this method call. Returns NULL if the method failed.</returns>
        public TOutParam Invoke<TInParam, TOutParam>(TInParam inParam, int messageId) where TInParam : class where TOutParam : class, new()
        {
            return Invoke<TInParam, TOutParam>(inParam, messageId, null);
        }

        /// <summary>
        /// Invokes a method on a relay component
        /// </summary>
        /// <typeparam name="TInParam">The class type that contains the input parameters</typeparam>
        /// <typeparam name="TOutParam">The class type that contains the output parameters</typeparam>
        /// <param name="inParam">Instance containing the input parameters for this method call</param>
		/// <param name="messageId">The ID of the <see cref="RelayMessage"/> that will be sent to the server.
		///		Varying this ID can help determine the relay cluster to which the message is sent.</param>
        /// <param name="virtualCacheTypeName">Tells the relay system to route the message using an alternate type name 
        /// instead of the class name</param>
        /// <returns>An instance of TOutParam containing the output for this method call. Returns NULL if the method failed.</returns>
        public TOutParam Invoke<TInParam, TOutParam>(TInParam inParam, int messageId, string virtualCacheTypeName) where TInParam : class where TOutParam : class, new()
        {
            RelayMessage    msg = null;
            short           typeID = 0;
            byte[]          payload = null;
            
            if (inParam == null) throw new ArgumentNullException("inParam");
            
            if ((virtualCacheTypeName == null) || (virtualCacheTypeName.Length == 0))
            {
                virtualCacheTypeName = typeof(TOutParam).FullName;
            }
            
            if (TryGetTypeId(virtualCacheTypeName, out typeID) == false)
            {
                throw new ArgumentException(string.Format("The type {0} is not registered with the relay system", virtualCacheTypeName), "inParam");
            }
            
            payload = Serializer.Serialize<TInParam>(inParam, SerializerFlags.Default);
            msg = new RelayMessage(typeID, messageId, MessageType.Invoke);
            msg.QueryData = payload;
            
			ForwardMessage(msg);
            
            if ((msg.ErrorOccurred == false) && (msg.Payload != null))
            {
                return msg.Payload.GetObject<TOutParam>();
            }
            else
            {
                return null;
            }
        }

        #endregion 

        #region Helpers

		private static void AssertNoLegacySerialization(RelayMessage message)
		{
			if (message != null && message.UsingLegacySerialization)
			{
				throw new CacheConfigurationException(message.TypeId);
			}
		}

        public string GetHtmlStatus()
        {
			return _relayForwarder.GetHtmlStatus();
        }

		/// <summary>
		/// The returned <see cref="RelayInfo"/> object can be serialized 
		/// into XML using the <see cref="XmlSerializer"/>.
		/// </summary>
		/// <returns><see cref="RelayInfo"/> object that contains data
		/// relay statistical information.</returns>
		public RelayInfo GetRelayInformation()
		{
			RelayInfo relayInfo = new RelayInfo();
			relayInfo.ForwarderStatus = _relayForwarder.GetForwarderStatus();
			return relayInfo;
		}

		private bool TryGetTypeId(string typeName, out short typeId)
		{
			TypeSetting typeSetting = GetTypeSetting(typeName);
			if (typeSetting != null && !typeSetting.Disabled)
			{
				typeId = typeSetting.TypeId;
				return true;
			}
			
			typeId = 0;
			return false;
		}

		/// <summary>
		/// Gets the type setting for a given type name.
		/// </summary>
		/// <param name="typeName">The <see cref="String"/> type name.</param>
		/// <returns>The <see cref="TypeSetting"/> for the <paramref name="typeName"/>; null if
		/// not found.</returns>
		public TypeSetting GetTypeSetting(string typeName)
		{
			if (_configuration == null || _configuration.TypeSettings == null || !_configuration.TypeSettings.TypeSettingCollection.Contains(typeName))
			{
				return null;
			}

			return _configuration.TypeSettings.TypeSettingCollection[typeName];
		}

		/// <summary>
		/// Gets the type setting for a given type id.
		/// </summary>
		/// <param name="typeId">The <see cref="Int32"/> type id.</param>
		/// <returns>The <see cref="TypeSetting"/> for the <paramref name="typeId"/>; null if
		/// not found.</returns>
		public TypeSetting GetTypeSetting(int typeId)
		{
			if (_configuration == null || _configuration.TypeSettings == null)
			{
				return null;
			}

			return _configuration.TypeSettings.TypeSettingCollection[(short)typeId];
		}

        [Obsolete("This method will not function properly with IVirtualCacheType objects and has been deprecated", false)]
        public TypeSetting GetTypeSetting<T>() where T : ICacheParameter
        {
            if (_configuration == null || _configuration.TypeSettings == null || _configuration.TypeSettings.TypeSettingCollection == null)
            {
                return null;
            }

            return _configuration.TypeSettings.TypeSettingCollection.GetTypeMapping(typeof(T).FullName);
        }

        internal bool TryGetTypeInfo(object parameter, out short typeId, out bool useCompression)
		{
            string typeName = GetTypeName(parameter);

			TypeSetting typeSetting = GetTypeSetting(typeName);

			if (typeSetting != null && !typeSetting.Disabled)
			{
				typeId = typeSetting.TypeId;
				useCompression = typeSetting.Compress;
				return true;
			}
			if(typeSetting == null)
			{
				log.ErrorFormat("Type {0} is not defined.", typeName);
			}
			else if(typeSetting.Disabled)
			{
				log.DebugFormat("Type {0} is disabled.", typeName);
			}
			typeId = 0;
			useCompression = false;
			return false;
		}

        /// <summary>
        /// Gets the type name to use for the object. If the object defines IVirtualCacheType, then its CacheTypeName will be returned.
        /// </summary>        
        internal string GetTypeName(object parameter)
        {
            string typeName;            
            IVirtualCacheType virtualCacheType = parameter as IVirtualCacheType;
            
            if (virtualCacheType != null && virtualCacheType.CacheTypeName != null)
                typeName = virtualCacheType.CacheTypeName;
            else
                typeName = parameter.GetType().FullName;
            
            return typeName;
        }
		
        /// <summary>
		/// 	<para>Gets the relay message necessary to save an object to cache. If the object has an extended ID, it will be used.</para>
		/// </summary>
		/// <param name="obj">
		/// 	<para>The object to save to cache.</para>
		/// </param>
		/// <returns>
		/// 	<para>A <see cref="RelayMessage"/> that, when passed to a <see cref="Forwarder"/>,
		///		will save the specifed object to cache; <see langword="null"/> if the specified
		///		object type is not mapped to a type ID in the configuration file.</para>
		/// </returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para>The argument <paramref name="obj"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <typeparam name="T">
		///		<para>The type of object to save.</para>
		/// </typeparam>
		public RelayMessage GetSaveMessage<T>(T obj) where T : ICacheParameter
		{
			if (obj == null) throw new ArgumentNullException("obj");

			short typeId;
			bool useCompression = false;

			if (TryGetTypeInfo(obj, out typeId, out useCompression))
			{
				RelayMessage message = RelayMessage.GetSaveMessageForObject<T>(typeId, useCompression, obj);
				return message;
			}
			else
			{
				return null;
			}
		}

		public RelayMessage GetUpdateMessage<T>(T obj) where T : ICacheParameter
		{
			if (obj == null) throw new ArgumentNullException("obj");

			short typeId;
			bool useCompression = false;

			if (TryGetTypeInfo(obj, out typeId, out useCompression))
			{
				RelayMessage message = RelayMessage.GetUpdateMessageForObject<T>(typeId, useCompression, obj);
				return message;
			}
			else
			{
				return null;
			}
		}

        /// <summary>
        /// Returns a string representation of the byte array, using D3 for each byte.
        /// </summary>        
        private string GetByteString(byte[] bytes)
        {
            if (bytes == null)
            {
                return "[ null ]";
            }
            else if (bytes.Length == 0)
            {
                return "[ empty ]";
            }
            else
            {
                StringBuilder sb = new StringBuilder(bytes.Length * 4 + 2);
                sb.Append("[ ");
                for (int i = 0; i < bytes.Length; )
                {
                    sb.Append(bytes[i++].ToString("D3"));
                    sb.Append(" ");
                }
                sb.Append("]");
                return sb.ToString();
            }
        }
		
        #endregion

        #region Increment
        /// <summary>
        /// Updates a single object in the cache.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <exception cref="SyncRelayOperationException">
        /// When the type of an object is defined with settings
        ///		<see cref="MySpace.DataRelay.Common.Schemas.TypeSettings"></see> with 
        ///		SyncInMessages=true and 
        ///		ThrowOnSyncFailure=true
        ///	failed executions will throw this exception
        /// </exception>
        /// <remarks>
        /// added: cbrown
        /// In the event that the type of object T is configured to do so,
        /// a synchronous "in" message may throw a SyncRelayOperationException
        /// This will be passed on to the calling process
        /// Since this is expected to happen frequently under normal operating
        /// load, logging is not included at this level.  The calling process
        /// is responsible for logging events as required.
        /// </remarks>
        public void IncrementObject<T>(T obj) where T : ICacheParameter
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                RelayMessage message = GetUpdateMessage<T>(obj);
                message.MessageType = MessageType.Increment;
                if (message != null)
                {
                    ForwardMessage(message);
                }
            }
            catch (SyncRelayOperationException)
            {
                // pass this on to the caller
                throw;
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled)
                    log.ErrorFormat("Error updating object of type {0} and id {1}: {2}",
                        typeof(T).FullName,
                        obj.PrimaryId.ToString("N0"), 
                        ex);
            }
        }
        #endregion
    }
}
