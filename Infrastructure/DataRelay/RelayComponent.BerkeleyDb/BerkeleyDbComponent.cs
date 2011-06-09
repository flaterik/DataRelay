using System;
using System.Collections.Generic;
using System.Threading;
using BerkeleyDbWrapper;
using MySpace.BerkeleyDb.Configuration;
using MySpace.BerkeleyDb.Facade;
using MySpace.Logging;
using MySpace.DataRelay.Configuration;
using System.Runtime.InteropServices;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Common.Interfaces.Notifications;


namespace MySpace.DataRelay.RelayComponent.BerkeleyDb
{
	internal struct UpdateMsg
	{
		public byte Counter;
		public int IncrementBy;
		public int StrategyLimit;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	struct PayloadStorage
	{
		public bool Compressed;           //1
		public int TTL;                   //5
		public long LastUpdatedTicks;     //13
		public long ExpirationTicks;      //21
		public bool Deactivated;          //22    
	};

	public class BerkeleyDbComponent : IRelayComponent
	{
		private static readonly LogWrapper Log = new LogWrapper();

		private RelayNodeConfig relayNodeConfig;
		private BerkeleyDbConfig bdbConfig;
		private BerkeleyDbStorage storage;
		public BerkeleyDbStorage Storage
		{
			get { return storage; }
			set { storage = value; }
		}
		private int threadCount;
		private string instanceName;
		//private Dispatcher[] dispatchers;
		//private Port<RelayMessage>[] ports;
		private ThrottledQueue[] queues;
		Timer queueCounterTimer;
		private readonly Dictionary<short, bool> RaceConditionLookup = new Dictionary<short, bool>();

		#region dummy constructor
		public BerkeleyDbComponent()
		{
			instanceName = GetComponentName();
		}
		#endregion

		#region Private Methods

		private void CountThrottledQueues(object state)
		{
			SetAvgThrottledQueueCount();
			SetMaxThrottledQueueCount();
		}

		/// <summary>
		/// Seems to be the best peformance (~0.06%)
		/// </summary>
		/// <param name="bytes"></param>
		/// <param name="startIndex"></param>
		/// <returns></returns>
		unsafe public long DeserializeLong(byte[] bytes, int startIndex)
		{
			ulong result;
			fixed (byte* pBytes = &bytes[startIndex])
			{
				result = *((ulong*)pBytes);
			}
			return (long)result;
		}

		unsafe private static RelayPayload DeserializePayload(short typeId, int objectId, byte[] bytes, int offset, int length)
		{
			if (bytes == null || length == 0)
			{
				return null;
			}

			int startIdx = (sizeof(PayloadStorage) + offset);
			int size = length - startIdx;
			byte[] byteArray = new byte[size];
			Array.Copy(bytes, startIdx, byteArray, 0, size);
			RelayPayload payload = null;
			fixed (byte* pBytes = &bytes[0])
			{
				var pPayload = (PayloadStorage*) pBytes;
				if (pPayload->Deactivated == false)
				{
					payload = new RelayPayload(typeId,
												objectId,
												byteArray,
												pPayload->Compressed,
												pPayload->TTL,
												pPayload->LastUpdatedTicks,
												pPayload->ExpirationTicks);
				}
			}

			return payload;
		}


		private static byte[] SerializePayload(RelayPayload payload)
		{
			return SerializePayload(payload, false);
		}

		static unsafe private byte[] SerializePayload(RelayPayload payload, bool deactivate)
		{
			if (payload == null || payload.ByteArray == null)
			{
				return null;
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SerializePayload() TypeId={0}, ObjectId={1}, TTL={2}, ExpirationTicks={3}, LastUpadatedTicks={4}, Compressed={5}, ByteArrayLength={6}"
					, payload.TypeId, payload.Id, payload.TTL, payload.ExpirationTicks, payload.LastUpdatedTicks, payload.Compressed, payload.ByteArray.Length);
			}

			int size = sizeof(PayloadStorage) + payload.ByteArray.Length;
			byte[] bytes = new byte[size];

			PayloadStorage payloadStorage = new PayloadStorage
												{
													Compressed = payload.Compressed,
													TTL = payload.TTL,
													LastUpdatedTicks = payload.LastUpdatedTicks,
													ExpirationTicks = payload.ExpirationTicks,
													Deactivated = deactivate
												};

			fixed (byte* pBytes = &bytes[0])
			{
				*((PayloadStorage*)pBytes) = payloadStorage;
			}

			if (payload.ByteArray != null)
			{
				Array.Copy(payload.ByteArray, 0, bytes, sizeof(PayloadStorage), payload.ByteArray.Length);

			}
			return bytes;
		}

		/// <summary>
		/// Serialize Payload Header
		/// </summary>
		/// <remarks>Turns out this stuff is actually used for deserialization. Who knew?</remarks>
		/// <param name="payload"></param>
		/// <returns></returns>
		static unsafe private byte[] SerializePayloadHeader(RelayPayload payload)
		{
			if (payload == null || payload.ByteArray == null)
			{
				return null;
			}

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("SerializePayloadHeader() TypeId={0}, ObjectId={1}, TTL={2}, ExpirationTicks={3}, LastUpadatedTicks={4}, Compressed={5}, ByteArrayLength={6}"
					, payload.TypeId, payload.Id, payload.TTL, payload.ExpirationTicks, payload.LastUpdatedTicks, payload.Compressed, payload.ByteArray.Length);
			}

			int size = sizeof(PayloadStorage);
			byte[] bytes = new byte[size];

			PayloadStorage payloadStorage = new PayloadStorage
												{
													Compressed = payload.Compressed,
													TTL = payload.TTL,
													LastUpdatedTicks = payload.LastUpdatedTicks,
													ExpirationTicks = payload.ExpirationTicks,
													Deactivated = false
												};

			fixed (byte* pBytes = &bytes[0])
			{
				*((PayloadStorage*)pBytes) = payloadStorage;
			}

			return bytes;
		}

		// Record for counter doens't exist in BDB. Creates new record, size is length of iPayloadStorage + 4 bytes for each counter 
		static unsafe private byte[] CreateCounterBytes(byte[] payloadHeader, int iVersion, byte byteCounterOffset,
			int iPayloadStorageLength, int iIncrementBy, int iStrategyLimit)
		{
			if (byteCounterOffset < 0 || iVersion == 0)
				return null;

			int iSize = iPayloadStorageLength + 1 + (byteCounterOffset + 1) * sizeof(int); // + 1 byte for version
			byte[] bytes = new byte[iSize];

			// Set the payload info
			if (payloadHeader != null)
			{
				fixed (byte* pBytes = &bytes[0], pPh = &payloadHeader[0])
				{
					*((PayloadStorage*) pBytes) = *(PayloadStorage*) pPh;
				}
			}
			// Set the Version
			bytes[iPayloadStorageLength] = (byte)iVersion;

			fixed (byte* pBytes = &bytes[iSize - 4])
			{
				int* pBytesInt = (int*)pBytes;
				*pBytesInt += iIncrementBy;

				if (iStrategyLimit > 0 && *pBytesInt > iStrategyLimit)
					*pBytesInt = iStrategyLimit;
			}

			return bytes;
		}

		// Record exists in BDB. Resize updated record.
		// Update value of counter defined by byteCounterOffset 
		static unsafe byte[] ModifyByteArray(byte[] bufferByteArr, int length, int iPayloadStorageLength, int iIncrementBy,
			byte byteCounterOffset, int iStrategyLimit)
		{
			// Length is DBEntry Length, what is 4 bytes for each counter + 1 byte for version
			//Length of record = Length - headerSize - versionbyte (in bytes)
			int iCounterOffset = byteCounterOffset;
			int iNumOfCountersInBDB = (length - iPayloadStorageLength - 1) / 4; // 4 is sizeof(int)
			int iBytesToAllocate;

			if (iCounterOffset >= iNumOfCountersInBDB)
				iBytesToAllocate = length + (iCounterOffset - iNumOfCountersInBDB + 1) * 4; // resize
			else
				iBytesToAllocate = length;

			Array.Resize(ref bufferByteArr, iBytesToAllocate);
		
			fixed (byte* pCounter = &bufferByteArr[iPayloadStorageLength + 1 + iCounterOffset * 4]) // 1
			{
				int* pCounterInt = (int*)pCounter;
				*pCounterInt += iIncrementBy;

				if (*pCounterInt < 0)
					*pCounterInt = 0;

				if (iStrategyLimit > 0 && *pCounterInt > iStrategyLimit)
					*pCounterInt = iStrategyLimit;
			}

			return bufferByteArr;
		}

		private static BerkeleyDbConfig GetConfig(RelayNodeConfig config)
		{
			object configObject = config.RelayComponents.GetConfigFor(componentName);
			BerkeleyDbConfig getConfig = configObject as BerkeleyDbConfig;
			if (getConfig == null)
			{
				if (Log.IsInfoEnabled)
				{
					Log.Info("Initialize() No configuration found, using defaults.");
				}
				getConfig = new BerkeleyDbConfig();
			}

			return getConfig;
		}

		private int GetQueueIndex(int typeId, int messageId)
		{
			DatabaseConfigs dbConfigs = bdbConfig.EnvironmentConfig.DatabaseConfigs;
			int federationSize = dbConfigs.GetFederationSize(typeId);
			int minTypeId = bdbConfig.MinTypeId;
			int federationIndex = DatabaseConfig.CalculateFederationIndex(messageId, federationSize);
			int queueIndex = (federationSize * (typeId - minTypeId) + federationIndex) % threadCount;
			return queueIndex;
		}

		private void SetAvgThrottledQueueCount()
		{
			int totalQueueCount = 0;
			for (int i = 0; i < queues.Length; i++)
			{
				totalQueueCount += queues[i].Count;
			}
			BerkeleyDbCounters.Instance.SetCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.AvgThrottledQueueCount, totalQueueCount / queues.Length);
		}

		private void SetMaxThrottledQueueCount()
		{
			int maxQueueCount = 0;
			if (queues != null)
			{
				for (int i = 0; i < queues.Length; i++)
				{
					int count = queues[i].Count;
					if (count > maxQueueCount)
					{
						maxQueueCount = count;
					}
				}
			}
			BerkeleyDbCounters.Instance.SetCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.MaxThrottledQueueCount, maxQueueCount);
		}


		#endregion

		#region IRelayComponent Members
		public void Initialize(RelayNodeConfig config, ComponentRunState runState)
		{
			try
			{
				if (config == null)
				{
					if (Log.IsErrorEnabled)
					{
						Log.ErrorFormat("RelayNodeConfig is NULL");
					}
					throw new ApplicationException("RelayNodeConfig is NULL");
				}

				

				relayNodeConfig = config;

				Initialize(GetConfig(config), GetInstanceName(), runState);
			}
			catch (Exception exc)
			{
				if (Log.IsErrorEnabled)
				{
					Log.ErrorFormat("Error initializing: {0}", exc);
				}
				throw;
			}
		}
		public void Initialize(BerkeleyDbConfig config, string InstanceName, ComponentRunState runState)
		{
			try
			{
			   
				instanceName = InstanceName;
				#region Perf Counter init
				BerkeleyDbCounters.Instance.Initialize(InstanceName);
				storage = new BerkeleyDbStorage
						  {
							  TrickledPagesCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.PagesTrickled),
							  DeletedObjectsCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.DeletedObjects),
							  StoredObjectsCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.ObjectsStored),
							  PooledBufferSizeCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 PooledBufferSize),
							  AllocatedBuffersCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 AllocatedBuffers),
							  BuffersInUseCounter =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.BuffersInUse),
							  LockStatCurrentMaxLockerId =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatCurrentMaxLockerId),
							  LockStatLastLockerId =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLastLockerId),
							  LockStatLockersNoWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockersNoWait),
							  LockStatLockersWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockersWait),
							  LockStatLockNoWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockNoWait),
							  LockStatLocksWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLocksWait),
							  LockStatLockTimeout =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockTimeout),
							  LockStatLockWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockWait),
							  LockStatMaxLockersPossible =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatMaxLockersPossible),
							  LockStatMaxLockObjectsPossible =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatMaxLockObjectsPossible),
							  LockStatMaxLocksPossible =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatMaxLocksPossible),
							  LockStatMaxNumberLockersAtOneTime =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatMaxNumberLockersAtOneTime),
							  LockStatMaxNumberLocksAtOneTime =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatMaxNumberLocksAtOneTime),
							  LockStatNumberCurrentLockers =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberCurrentLockers),
							  LockStatNumberCurrentLockObjects =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberCurrentLockObjects),
							  LockStatNumberCurrentLockObjectsAtOneTime =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberCurrentLockObjectsAtOneTime),
							  LockStatNumberCurrentLocks =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberCurrentLocks),
							  LockStatNumberDeadLocks =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberDeadLocks),
							  LockStatNumberLockModes =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLockModes),
							  LockStatNumberLocksDownGraded =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLocksDownGraded),
							  LockStatNumberLocksReleased =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLocksReleased),
							  LockStatNumberLocksRequested =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLocksRequested),
							  LockStatNumberLocksUpgraded =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLocksUpgraded),
							  LockStatNumberLockTimeouts =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberLockTimeouts),
							  LockStatNumberTxnTimeouts =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatNumberTxnTimeouts),
							  LockStatObjectsNoWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatObjectsNoWait),
							  LockStatObjectsWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatObjectsWait),
							  LockStatTxnTimeout =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatTxnTimeout),
							  LockStatLockHashLen =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockHashLen),
							  LockStatLockRegionSize =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLockRegionSize),
							  LockStatLocksNoWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatLocksNoWait),
							  LockStatRegionNoWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatRegionNoWait),
							  LockStatRegionWait =
								  BerkeleyDbCounters.Instance.GetCounter(GetInstanceName(),
																		 BerkeleyDbCounters.PerformanceCounterIndexes.
																			 LockStatRegionWait)
						  };


				
					if (storage.LockStatRegionWait == null)
					{
						Log.WarnFormat("Lock Statistics not initialized properly from BerkeleyDbComponent.");
					}
					else
					{
						Log.DebugFormat("Lock Statistics initialized properly from BerkeleyDbComponent.");
					}
				
				#endregion
				bdbConfig = config;
				storage.Initialize(InstanceName, bdbConfig);

				if (relayNodeConfig != null)
				{
					// tell database objects if they are to check for race conditions - Dale Earnhardt Jr. # 8 yes!
					DatabaseConfig dbConfig;
					foreach (TypeSetting typeSetting in relayNodeConfig.TypeSettings.TypeSettingCollection)
					{
						try
						{
							dbConfig = bdbConfig.EnvironmentConfig.DatabaseConfigs.GetConfigFor(typeSetting.TypeId);
						}
						catch (Exception e)
						{
							Log.ErrorFormat("Exception getting database config for type {0}: {1}", typeSetting.TypeId, e);
							throw;
						}
						if (dbConfig.Id == 0)
						{
							DatabaseConfig newDbConfig = dbConfig.Clone(typeSetting.TypeId);
							bdbConfig.EnvironmentConfig.DatabaseConfigs.Add(newDbConfig);
						}						
						RaceConditionLookup[typeSetting.TypeId] = typeSetting.CheckRaceCondition;
					}
				}

				ThrottleThreads throttleThreads = bdbConfig.ThrottleThreads;
				bool throttleThreadsEnabled = throttleThreads != null && throttleThreads.Enabled;
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("Initialize() BerkeleyDbConfig: ThrottleThreads = {0}", throttleThreadsEnabled);
				}
				if (throttleThreadsEnabled)
				{
					threadCount = bdbConfig.ThrottleThreads.ThreadCount;
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("Initialize() BerkeleyDbConfig: ThreadCount = {0}", threadCount);
					}
					//int federationSize = 10;
					int dbCount = bdbConfig.GetDatabaseCount();
					if (Log.IsInfoEnabled)
					{
						Log.InfoFormat("Initialize() Calculated number of databases = {0}", dbCount);
					}
					if (dbCount < threadCount)
					{
						threadCount = dbCount;
						if (Log.IsInfoEnabled)
						{
							Log.InfoFormat("Initialize() Thread count reduced to {0}", threadCount);
						}
					}
					const string threadPoolName = "BerkeleyDb Thread Pool";
					const string dispatcherQueueName = "Single Thread Queue";
					queues = new ThrottledQueue[threadCount];
					PostMessageDelegate postMessageDelegate = PostMessage;
					for (int i = 0; i < threadCount; i++)
					{
						queues[i] = new ThrottledQueue(threadPoolName, dispatcherQueueName + i,
							postMessageDelegate, bdbConfig.MaxPoolItemReuse);
					}
					queueCounterTimer = new Timer(CountThrottledQueues, null, 5000, 5000);
				}
			}
			catch (Exception exc)
			{
				if (Log.IsErrorEnabled)
				{
					Log.Error("Error initializing.", exc);
				}
				throw;
			}
		}

		private const string componentName = "BerkeleyDb";
		public string GetComponentName()
		{
			return componentName;
		}

		public string GetInstanceName()
		{
			return instanceName;
		}

		public ComponentRunState GetRunState()
		{
			return null;
		}

		public ComponentRuntimeInfo GetRuntimeInfo()
		{
			return null;
		}

		public void ReloadConfig(BerkeleyDbConfig newConfig)
		{
			if (newConfig != null && storage != null)
			{
				storage.ReloadConfig(newConfig);
			}
			else
			{
				if (Log.IsErrorEnabled)
				{
					Log.ErrorFormat("Either BerkeleyDbConfig or BerkeleyDbStorage is null");
				}
			}
		}

		public void ReloadConfig(RelayNodeConfig config)
		{
			bdbConfig = GetConfig(config);
			ReloadConfig(bdbConfig);
		}

		public void Shutdown()
		{
			if (queueCounterTimer != null)
			{
				queueCounterTimer.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
				queueCounterTimer.Dispose();
			}
			if (storage != null)
			{
				storage.Shutdown();
			}
			BerkeleyDbCounters.Instance.ResetAllCounters();
			BerkeleyDbCounters.Instance.Shutdown();
			if (queues != null)
			{
				for (int i = 0; i < queues.Length; i++)
				{
					queues[i].Dispose();
					queues[i] = null;
				}
			}
		}
		#endregion

		#region IDataHandler Members
		public void HandleMessage(RelayMessage message)
		{
			try
			{
				ThrottleThreads throttleThreads = bdbConfig.ThrottleThreads;
				if (throttleThreads != null && throttleThreads.Enabled)
				{
					if (Log.IsDebugEnabled)
					{
						Log.DebugFormat("HandleMessage() Posts message to ThrottledQueue (TypeId={0}, MessageId={1}, MessageType={2})"
							, message.TypeId, message.Id, message.MessageType);
					}
					int i = GetQueueIndex(message.TypeId, message.Id);
					switch (message.MessageType)
					{
						case MessageType.Get:
						case MessageType.SaveWithConfirm:
						case MessageType.DeleteWithConfirm:
						case MessageType.DeleteAllInTypeWithConfirm:
						case MessageType.DeleteAllWithConfirm:
						case MessageType.DeleteInAllTypesWithConfirm:
						case MessageType.NotificationWithConfirm:
						case MessageType.IncrementWithConfirm:
							short waitId = queues[i].SetWaitHandle(throttleThreads.WaitTimeout);
							queues[i].Post(message, waitId);
							queues[i].WaitForReply(waitId);
							break;
						case MessageType.Save:
						case MessageType.Delete:
						case MessageType.DeleteAll:
						case MessageType.DeleteAllInType:
						case MessageType.DeleteInAllTypes:
						case MessageType.Notification:
						case MessageType.Increment:
							queues[i].Post(message);
							break;
						default:
							throw new ApplicationException("Unknown Message Type" + message.MessageType);
					}
				}
				else
				{
					if (Log.IsDebugEnabled)
					{
						Log.DebugFormat("HandleMessage() Redirects message to PostMessage (TypeId={0}, MessageId={1}, MessageType={2})"
							, message.TypeId, message.Id, message.MessageType);
					}
					PostMessage(message);
				}
			}
			catch (Exception exc)
			{
				MarkOutcome(message, false);
				message.ResultDetails = exc.ToString();
				throw;
			}
		}

		/// <summary>
		/// Process SNM messages
		/// </summary>
		/// <remarks>Assumed there will only be deletes</remarks>
		/// <param name="message"></param>
		private void ProcessNotificationMessage(RelayMessage message)
		{
			switch ((NotificationType)message.NotificationId)
			{
				case NotificationType.CacheExpiration:
					ProcessCacheExpirationNotification(message);
					break;
				default:
					MarkOutcome(message, false);
					const string warnMessage = "Invalid scheduled notification message type.";
					message.ResultDetails = warnMessage;
					if (Log.IsWarnEnabled)
					{
						Log.Warn(warnMessage);
					}
					break;
			}
		}

		/// <summary>
		/// Process a Scheduled Notification Message
		/// </summary>
		/// <remarks>It's assumed that the correct key information, whether
		/// PrimaryID, Message.ID, or ExtendedID will be present in notification
		/// message </remarks>
		/// <param name="message"></param>
		private void ProcessCacheExpirationNotification(RelayMessage message)
		{
			try
			{
				MessageType originalMessageType = message.MessageType;

				// Change message to "Get" type
				message.MessageType = MessageType.Get;

				// Request the message
				int len = GetPayloadForMessage(message, message.TypeId, message.Id, message.ExtendedId);

				// Did we get something back?
				if (len > 0 && message.Payload != null)
				{
					// Has the object actually expired?
					if (message.Payload.ExpirationTicks <= DateTime.Now.Ticks)
					{
						if (originalMessageType == MessageType.NotificationWithConfirm)
						{
							message.MessageType = MessageType.DeleteWithConfirm;
							//synchronous because of confirm
							//requires cleanup in finally block
							HandleMessage(message);
						}
						else
						{
							// Change message to "Delete" type
							message.MessageType = MessageType.Delete;

							// Process message (should be posted to queue and be asynchronous
							//if changed the finally block will not be good for this.
							HandleMessage(message);
						}
					}
				}
			}
			catch (Exception ex)
			{
				MarkOutcome(message, false);
				string errorMessage = string.Format("Error processing scheduled notification message: {0} ", ex);
				message.ResultDetails = errorMessage;
				if (Log.IsErrorEnabled)
				{
					Log.Error(errorMessage, ex);
				}
			}
			finally
			{
				// Change back to original message type
				message.MessageType = MessageType.Notification;

				// Change back to original payload
				message.Payload = null;
			}
		}

		void PostMessage(QueueItem queueItem)
		{
			short waitId = queueItem.WaitId;
			RelayMessage message = queueItem.Message;
			PostMessage(message);
			switch (message.MessageType)
			{
				case MessageType.Get:
				case MessageType.SaveWithConfirm:
				case MessageType.UpdateWithConfirm:
				case MessageType.DeleteWithConfirm:
				case MessageType.DeleteAllInTypeWithConfirm:
				case MessageType.DeleteAllWithConfirm:
				case MessageType.DeleteInAllTypesWithConfirm:
				case MessageType.NotificationWithConfirm:
				case MessageType.IncrementWithConfirm:
					int i = GetQueueIndex(message.TypeId, message.Id);
					queues[i].ReleaseWait(waitId);
					break;
			}
		}

		/// <summary>
		/// PostMessage() - called from Handlemessage and is a PostMessageDelegate type
		/// </summary>
		/// <param name="message">RelayMessage type</param>
		public unsafe void PostMessage(RelayMessage message)
		{
			byte[] byteArray = null;
			short typeId = message.TypeId;
			int objectId = message.Id;
			byte[] key = message.ExtendedId;

			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("PostMessage() Posts message to BerkeleyDb (TypeId={0}, ObjectId={1}, MessageType={2})"
					, typeId, objectId, message.MessageType);
			}
			try
			{
				if (storage != null)
				{
					bool success;
					switch (message.MessageType)
					{
						case MessageType.Get:
							int len = GetPayloadForMessage(message, typeId, objectId, key);
							BerkeleyDbCounters.Instance.CountGet(GetInstanceName(), (message.Payload != null), len);
							success = true;
							MarkOutcome(message, true);
							break;
						case MessageType.Save:
						case MessageType.SaveWithConfirm:
							if (message.Payload != null)
							{
								// we want to pull the PayloadStorage out of the stored record for comparison.
								const long startPosition = 0x00;
								int length = sizeof(PayloadStorage);

								bool bHasKey;
								if (RaceConditionLookup.TryGetValue(typeId, out bHasKey) && RaceConditionLookup[typeId])
								{

									bool bRaceCondition = false;
									success = storage.SaveObject(typeId, objectId, key,
										(int)startPosition, length, delegate(DatabaseEntry dbEntry)
										{
											if (dbEntry.Length > 0)
											{
												// found a record. check the lastupdateticks 
												long clientValue = message.Payload.LastUpdatedTicks;
												PayloadStorage storedValue;
												fixed (byte* pBytes = &dbEntry.Buffer[0])
												{
													storedValue = *(PayloadStorage*)pBytes;
												}

												if (clientValue > storedValue.LastUpdatedTicks)
												{
													// this is a good set.  
													// does not matter if record was deactivated...
													byteArray = SerializePayload(message.Payload);
													dbEntry.Buffer = byteArray;
													dbEntry.StartPosition = 0;
													dbEntry.Length = byteArray.Length;
												}
												else if (clientValue < storedValue.LastUpdatedTicks)
												{
													// not a good thing.  this update is older than whats stored
													// deactivate this record!
													message.Payload.LastUpdatedTicks = storedValue.LastUpdatedTicks;
													byteArray = SerializePayload(message.Payload, true); // true for deactivation!
													dbEntry.Buffer = byteArray;
													dbEntry.StartPosition = 0;
													dbEntry.Length = byteArray.Length;
													BerkeleyDbCounters.Instance.IncrementCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.RaceDeletes);
													bRaceCondition = true;
												}
												else if (storedValue.Deactivated == false && clientValue == storedValue.LastUpdatedTicks)
												{
													// client and stored lastUpdateTime are equal.  Store existing record
													// with dateTime.Now.Ticks
													message.Payload.LastUpdatedTicks = DateTime.Now.Ticks;
													byteArray = SerializePayload(message.Payload); // keep it that way it was with 
													dbEntry.Buffer = byteArray;
													dbEntry.StartPosition = 0;
													dbEntry.Length = byteArray.Length;
												}
												else if (storedValue.Deactivated && clientValue == storedValue.LastUpdatedTicks)
												{
													// client and stored lastUpdateTime are equal and
													// the record has already been deactivated. 'do-nothing'!
													// this will keep the existing stored timestamp.
													message.Payload.LastUpdatedTicks = storedValue.LastUpdatedTicks;
													byteArray = SerializePayload(message.Payload, true); // keep it that way it was with 
													dbEntry.Buffer = byteArray;
													dbEntry.StartPosition = 0;
													dbEntry.Length = byteArray.Length;
													BerkeleyDbCounters.Instance.IncrementCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.RaceDeletes);
													bRaceCondition = true;
												}
											}
											else
											{
												// did not find an existing record
												byteArray = SerializePayload(message.Payload);
												dbEntry.Buffer = byteArray;
												dbEntry.StartPosition = 0;
												dbEntry.Length = byteArray.Length;
											}
										});
									MarkOutcome(message, success);
									BerkeleyDbCounters.Instance.CountSave(GetInstanceName(), (success && !bRaceCondition), byteArray.Length);
								}
								else
								{
									byteArray = SerializePayload(message.Payload);
									success = storage.SaveObject(typeId, objectId, key, byteArray);
									MarkOutcome(message, success);
									BerkeleyDbCounters.Instance.CountSave(GetInstanceName(), success, byteArray.Length);
								}
							}
							break;
						case MessageType.Increment:
						case MessageType.IncrementWithConfirm:
							// Client sends positive value of updateMessage.IncrementBy for Increment and negative for Decrement
							if (message.Payload != null && message.Payload.ByteArray.Length >= 10) // valid Payload
							{
								UpdateMsg updateMessage = GetUpdateMsg(message.Payload.ByteArray);

								byte byteCounterOffset = updateMessage.Counter;
								int iVersion = message.Payload.ByteArray[0]; // Assumes standard serialization
								int iIncrementBy = updateMessage.IncrementBy;
								int iStrategyLimit = updateMessage.StrategyLimit;
								// we want to pull the PayloadStorage out of the stored record for comparison.
								const long startPosition = 0;
								int iPayloadStorageLength = sizeof(PayloadStorage);
								int length = 0;
								// length calculated for existing record in GetLengthForDBRecord
								success = storage.SaveObject(typeId, objectId, key, (int)startPosition, length,
										delegate(DatabaseEntry dbEntry)
										{
											byte[] btBuffer = GetDBRecord(typeId, objectId, key);

											if (btBuffer != null && btBuffer.Length > sizeof(PayloadStorage))
												byteArray = ModifyByteArray(btBuffer, btBuffer.Length, iPayloadStorageLength,
													iIncrementBy, byteCounterOffset, iStrategyLimit);
											else // no record in BDB
												byteArray = CreateCounterBytes(SerializePayloadHeader(message.Payload), iVersion, byteCounterOffset,
													iPayloadStorageLength, iIncrementBy, iStrategyLimit);

											dbEntry.Buffer = byteArray;
											dbEntry.StartPosition = 0;
											dbEntry.Length = byteArray.Length;
										});

								MarkOutcome(message, success);
								BerkeleyDbCounters.Instance.CountSave(GetInstanceName(), success, byteArray.Length);
							}
							break;
						case MessageType.Delete:
						case MessageType.DeleteWithConfirm:
							success = storage.DeleteObject(typeId, objectId, key);
							MarkOutcome(message, success);
							BerkeleyDbCounters.Instance.IncrementCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.Delete);
							break;
						case MessageType.DeleteAll:
						case MessageType.DeleteAllWithConfirm:
							int deleteCount = storage.DeleteAll();
							MarkOutcome(message, true);
							BerkeleyDbCounters.Instance.SetCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.DeleteAll, deleteCount);
							if (Log.IsDebugEnabled)
							{
								storage.GetStats();
							}
							break;
						case MessageType.DeleteAllInType:
						case MessageType.DeleteAllInTypeWithConfirm:
							deleteCount = storage.DeleteAllInType(typeId);
							MarkOutcome(message, true);
							BerkeleyDbCounters.Instance.SetCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.DeleteInAllTypes, deleteCount);
							if (Log.IsDebugEnabled)
							{
								storage.GetStats();
							}
							break;
						case MessageType.DeleteInAllTypes:
						case MessageType.DeleteInAllTypesWithConfirm:
							deleteCount = storage.DeleteObjectInAllTypes(objectId, key);
							MarkOutcome(message, true);
							BerkeleyDbCounters.Instance.SetCounter(GetInstanceName(), BerkeleyDbCounters.PerformanceCounterIndexes.DeleteInAllTypes, deleteCount);
							if (Log.IsDebugEnabled)
							{
								storage.GetStats();
							}
							break;
						case MessageType.Notification:
						case MessageType.NotificationWithConfirm:
							ProcessNotificationMessage(message);
							break;
					}
				}
			}
			catch (Exception exc)
			{
				MarkOutcome(message, false);
				message.ResultDetails = exc.ToString();
				throw;
			}
		}

		private static void MarkOutcome(RelayMessage message, bool success)
		{
			if (success) 
				message.ResultOutcome = RelayOutcome.Success;
			else 
				message.ResultOutcome = RelayOutcome.Error; //should this be fail?
		}

		private byte[] GetDBRecord(short typeId, int objectId, byte[] key)
		{
			byte[] byteDbEntry = null;

			storage.GetDbObject(typeId, objectId, key,
								dbEntry =>
									{
										if (dbEntry == null) throw new ArgumentNullException("dbEntry");
										byteDbEntry = dbEntry.Buffer;
										Array.Resize(ref byteDbEntry, dbEntry.Length);
									});

			return byteDbEntry;
		}

		private int GetPayloadForMessage(RelayMessage message, short typeId, int objectId, byte[] key)
		{
			int len = 0;
			RelayPayload payload = null;
			storage.GetDbObject(typeId, objectId, key,
				delegate(DatabaseEntry dbEntry)
				{
					len = dbEntry.Length;
					payload = DeserializePayload(typeId, objectId, dbEntry.Buffer, dbEntry.StartPosition, len);
				});

			if (payload != null)
			{
				if (Log.IsDebugEnabled)
				{
					Log.DebugFormat("PostMessage() Deserialized Payload TypeId={0}, ObjectId={1}, TTL={2}, ExpirationTicks={3}, LastUpadatedTicks={4}, Compressed={5}, ByteArrayLength={6}"
						, payload.TypeId, payload.Id, payload.TTL, payload.ExpirationTicks, payload.LastUpdatedTicks, payload.Compressed, payload.ByteArray.Length);
				}
				if (message.Freshness.HasValue)
				{
					var lastUpdatedDate = payload.LastUpdatedDate;
					if (message.Freshness.Value >= lastUpdatedDate)
					{
						payload = null;
					}
					message.Freshness = lastUpdatedDate;
				}
				if (payload != null)
				{
					payload.ExtendedId = message.ExtendedId;
					message.Payload = payload;
				}
			} else
			{
				message.Freshness = null;
			}
			return len;
		}

		private static UpdateMsg GetUpdateMsg(byte[] byteUpdate)
		{
			UpdateMsg updMsg = new UpdateMsg
								   {
									   Counter = byteUpdate[1],
									   IncrementBy = BitConverter.ToInt32(byteUpdate, 2),
									   StrategyLimit = BitConverter.ToInt32(byteUpdate, 6)
								   };

			return updMsg;
		}

		public void HandleMessages(IList<RelayMessage> messages)
		{
			for (int i = 0; i < messages.Count; i++)
			{
				HandleMessage(messages[i]);
			}
		}
		#endregion
	}
}
