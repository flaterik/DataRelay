using System;
using System.Collections.Generic;
using MySpace.DataRelay.Common.Schemas;
using MySpace.DataRelay.Configuration;
using MySpace.DataRelay.RelayComponent.Forwarding;
using MySpace.Logging;

namespace MySpace.DataRelay.RelayComponent.Forwarding
{
	/// <summary>
	/// This class manages forwarder statistics on a per Type basis.
	/// </summary>
	internal class TypeSpecificStatisticsManager
	{       
		/// <summary>
		/// Gets an instance of the <see cref="TypeSpecificStatisticsManager"/>.
		/// </summary>
		internal static TypeSpecificStatisticsManager Instance
		{
			get
			{
				return GetInstance();
			}
		}

		private static TypeSpecificStatisticsManager _instance;
		private static readonly object _instanceLock = new object();
		private static readonly LogWrapper _log = new LogWrapper();
		private TypeSettingStatus[] _typeSettingStatusCollection;

		private static TypeSpecificStatisticsManager GetInstance()
		{
			//use local var because we set _instance to null on shutdown and could
			//cause concurrency problem
			TypeSpecificStatisticsManager instance = _instance;
			if (instance == null)
			{
				lock (_instanceLock)
				{
					if (_instance == null)
					{
						_instance = new TypeSpecificStatisticsManager();
					}
					instance = _instance;
				}
			}
			return instance;
		}

		private static bool _initialized;
		/// <summary>
		/// Calculates <see cref="TypeSpecificMessageCountInfo"/> statistics for the specified type. 
		/// </summary>
		/// <param name="typeID">The id of the type to collect statistics for.</param>
		/// <param name="milliseconds">The time it takes to send the message.</param>
		public void CalculateStatistics(short typeID, long milliseconds)
		{
			TypeSettingStatus status = RetrieveTypeSettingStatusForCalc(typeID);
			if (status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateStatistics " +
					"TypeSettingStatus is null for typeId:{0}",
					typeID);
				return;
			}
			if (!status.GatherStatistics) return;

			if (status.MessageInfo == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateStatistics " +
					"MessageInfo is null for typeId:{0}",
					typeID);
				return;
			}
			
			if (status.MessageInfo == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateStatistics " +
					"TypeSpecificMessageCountInfo is null for typeId:{0}",
					typeID);
				return;
			}

			status.MessageInfo.CaculateStatisics(milliseconds);
		}
		/// <summary>
		/// Calculates <see cref="BulkMessageInfo"/> statistics for IN messages for the
		/// specified type.
		/// </summary>
		/// <param name="typeID">The id of the type to collect statistics for.</param>
		/// <param name="messageLength">The number of messages sent in the bulk message.</param>
		/// <param name="milliseconds">The time it takes to send the message.</param>
		public void CalculateBulkInStatistics(short typeID, int messageLength, long milliseconds)
		{
			TypeSettingStatus status = RetrieveTypeSettingStatusForCalc(typeID);
			
			if (status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateBulkInStatistics " +
				                "TypeSettingStatus is null for typeId:{0}",
				                typeID);
				return;
			}
			if (!status.GatherStatistics) return;

			if (status.BulkInMessageInfo == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateBulkInStatistics " +
								"BulkInMessageInfo is null for typeId:{0}",
								typeID);
				return;
			}

			status.BulkInMessageInfo.CaculateStatisics(messageLength,milliseconds);
		}
		/// <summary>
		/// Calculates <see cref="BulkMessageInfo"/> statistics for OUT messages for the
		/// specified type.
		/// </summary>
		/// <param name="typeID">The id of the type to collect statistics for.</param>
		/// <param name="messageLength">The number of messages sent in the bulk message.</param>
		/// <param name="milliseconds">The time it takes to send the message.</param>
		public void CalculateBulkOutStatistics(short typeID, int messageLength, long milliseconds)
		{
			TypeSettingStatus status = RetrieveTypeSettingStatusForCalc(typeID);
			
			if (status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateBulkOutStatistics " +
								"TypeSettingStatus is null for typeId:{0}",
								typeID);
				return;
			}
			if (!status.GatherStatistics) return;
			if (status.BulkOutMessageInfo == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:CalculateBulkOutStatistics " +
								"BulkOutMessageInfo is null for typeId:{0}",
								typeID);
				return;
			}

			status.BulkOutMessageInfo.CaculateStatisics(messageLength, milliseconds);
		}

		private TypeSettingStatus RetrieveTypeSettingStatus(short typeID)
		{
			if (_typeSettingStatusCollection == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:RetrieveTypeSettingStatus " +
								"collection is null. Called for typeId:{0}.",
								typeID);
				return null;
			}
			
			if(typeID >= _typeSettingStatusCollection.Length)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:RetrieveTypeSettingStatus " +
								"typeId:{0} is outside of collection bounds:{1}.",
								typeID, _typeSettingStatusCollection.Length);
				return null;
			}
			if (typeID < 0)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:RetrieveTypeSettingStatus " +
								"typeId:{0} is less than zero.",
								typeID);
				return null;
			}

			return _typeSettingStatusCollection[(int) typeID];
		}

		private TypeSettingStatus RetrieveTypeSettingStatusForCalc(short typeID)
		{
			TypeSettingStatus status = RetrieveTypeSettingStatus(typeID);
			if (status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:RetrieveTypeSettingStatusForCalc " +
								"TypeSettingStatus is null for typeId:{0}",
								typeID);
				return null;
			}
			if (!status.GatherStatistics) return null;

			return status;
		}
		/// <summary>
		/// Determines whether statistics are supposed to be calculated for the specified type.
		/// </summary>
		/// <param name="typeID">The id of the type to collect statistics for.</param>
		/// <returns>True if statistics are to be gathered for the specified type; otherwise false.</returns>
		public bool GatherStats(short typeID)
		{
			if (_typeSettingStatusCollection == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:GatherStats " +
								"collection is null. Called for typeId:{0}.",
								typeID);
				return false;
			}

			if (typeID >= _typeSettingStatusCollection.Length)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:GatherStats " +
								"typeId:{0} is outside of collection bounds:{1}.",
								typeID, _typeSettingStatusCollection.Length);
				return false;
			}
			if (typeID < 0)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:GatherStats " +
								"typeId:{0} is less than zero.",
								typeID);
				return false;
			}

			TypeSettingStatus status = _typeSettingStatusCollection[typeID];
			if(status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:GatherStats " +
								"TypeSettingStatus is null for typeId:{0}",
								typeID);
				return false;
			}
			return _typeSettingStatusCollection[typeID].GatherStatistics;
		}
	
		/// <summary>
		/// Returns the <see cref="TypeSettingStatus"/> for the specified type.
		/// </summary>
		/// <param name="typeId">The <see cref="TypeSetting.TypeId"/> to return the Status for.</param>
		/// <returns>The statistics collected for the specified type.</returns>
		public TypeSettingStatus GetStatus(short typeId)
		{
			TypeSettingStatus status = RetrieveTypeSettingStatus(typeId);
			if (status == null)
			{
				LogTypeIdMessage("TypeSpecificStatisticsManager:GetStatus " +
								"TypeSettingStatus is null for typeId:{0}",
								typeId);
				return null;
			}
			return status.GetStatus();
		}
		/// <summary>
		/// Initializes the <see cref="TypeSpecificStatisticsManager"/>.
		/// </summary>
		/// <param name="typeSettingCollection">The <see cref="TypeSettingCollection"/> used to initialize.</param>
		internal static void Initialize(TypeSettingCollection typeSettingCollection)
		{
			lock (_instanceLock)
			{
				if (!_initialized)
				{
					Instance.InitializeInstance(typeSettingCollection);
					_initialized = true;
				}
			}
		}

		private void InitializeInstance(TypeSettingCollection typeSettingCollection)
		{
			if (typeSettingCollection == null)
			{
				_log.Warn("TypeSpecificStatisticsManager:InitializeInstance " +
						  "typeSettingCollection is null.");
				return;
			}
			
			//set up status collection with max size and whether each element should collect stats
			_typeSettingStatusCollection = new TypeSettingStatus[typeSettingCollection.MaxTypeId + 1];
			foreach (var setting in typeSettingCollection)
			{
				_typeSettingStatusCollection[setting.TypeId] = new TypeSettingStatus();
				_typeSettingStatusCollection[setting.TypeId].Update(setting);
			}

			//Add status for typeId Zero (DeleteAllTypes) which is not specific to a group
			//or specified in RelayTypeSettings configuration
			TypeSetting zeroTypeSetting = CreateZeroTypeSetting();
			_typeSettingStatusCollection[0] = new TypeSettingStatus();
			_typeSettingStatusCollection[0].Update(zeroTypeSetting);

		}

		private TypeSetting CreateZeroTypeSetting()
		{
			TypeSetting zeroTypeSetting = new TypeSetting();
			zeroTypeSetting.GroupName = "*";
			zeroTypeSetting.TypeName = "*";
			zeroTypeSetting.TypeId = 0;
			zeroTypeSetting.Description = "All Groups, All Types";
			return zeroTypeSetting;
		}
		/// <summary>
		/// Reloads the <see cref="TypeSpecificStatisticsManager"/> with new <see cref="TypeSetting"/> data.
		/// </summary>
		/// <param name="typeSettingCollection">The <see cref="TypeSettingCollection"/> used to initialize.</param>
		internal void ReloadMapping(TypeSettingCollection typeSettingCollection)
		{
			if (_initialized)
			{
				if (typeSettingCollection == null)
				{
					_log.Warn("TypeSpecificStatisticsManager:ReloadMapping " +
							  "typeSettingCollection is null.");
					return;
				}

				lock (_instanceLock)
				{
					_typeSettingStatusCollection = UpdateTypeSettingStatusCollection(typeSettingCollection);
				}
			}
			else
			{
				_log.Warn("TypeSpecificStatisticsManager:ReloadMapping " +
						  "ReloadMapping was called without initializing TypeSpecificStatisticsManager.");
			}
		}
		
		private TypeSettingStatus[] UpdateTypeSettingStatusCollection(TypeSettingCollection typeSettingCollection)
		{
			//create fresh list
			TypeSettingStatus[] newTypeSettingStatusCollection = null;

			newTypeSettingStatusCollection = new TypeSettingStatus[typeSettingCollection.MaxTypeId + 1];
			//Go through type settings
			foreach (var setting in typeSettingCollection)
			{
				//Add old TypeSettingStatus to the appropriate TypeId slot if key items are the same
				if (_typeSettingStatusCollection != null &&
					setting.TypeId < _typeSettingStatusCollection.Length &&
					_typeSettingStatusCollection[setting.TypeId] != null &&
					_typeSettingStatusCollection[setting.TypeId].TypeName == setting.TypeName &&
					setting.GatherStatistics == true)
				{
					newTypeSettingStatusCollection[setting.TypeId] = _typeSettingStatusCollection[setting.TypeId].Clone();
				}
				else
				//If there is a new item or one of the key items changed create a new empty TypeSettingStatus for the TypeId slot
				{
					newTypeSettingStatusCollection[setting.TypeId] = new TypeSettingStatus();
				}
				newTypeSettingStatusCollection[setting.TypeId].Update(setting);
			}
			//Add status for typeId Zero (DeleteAllTypes) which is not specific to a group
			//or specified in RelayTypeSettings configuration
			if (_typeSettingStatusCollection != null &&
				_typeSettingStatusCollection[0] != null)
			{
				newTypeSettingStatusCollection[0] = _typeSettingStatusCollection[0].Clone();
			}
			else
			//If there is a new item or one of the key items changed create a new empty TypeSettingStatus for the TypeId slot
			{
				newTypeSettingStatusCollection[0] = new TypeSettingStatus();
			}
			TypeSetting zeroTypeSetting = CreateZeroTypeSetting();
			newTypeSettingStatusCollection[0].Update(zeroTypeSetting);

			return newTypeSettingStatusCollection;
		}

		/// <summary>
		/// Cleans up the instance of the <see cref="TypeSpecificStatisticsManager"/> class.
		/// </summary>
		internal void Shutdown()
		{
			lock (_instanceLock)
			{
				_typeSettingStatusCollection = null;
				_initialized = false;
				//Release instance to free memory and to get initialized fresh.
				_instance = null;
			}
		}

		private static void LogTypeIdMessage(string message, short typeId)
		{
			_log.WarnFormat(message, typeId);
		}

		private static void LogTypeIdMessage(string message, short typeId, int length)
		{
			_log.WarnFormat(message, typeId, length);
		}
	}
}