using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Text;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Common.Interfaces.Query;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Responsible for encapsulating all communication for the relay transport.
	/// </summary>
	[Serializable]
	public class RelayMessage : IVersionSerializable
	{
		#region Fields
		private static readonly Encoding stringEncoder = new UTF8Encoding(false, true); //same as the default for a BinaryWriter
		/// <summary>
		/// The number of possible message types
		/// </summary>
		public const int NumberOfTypes = (int)MessageType.NumTypes;
		private RelayPayload payload;
		public int Id;
		public byte[] ExtendedId;
		public short TypeId;
		public MessageType MessageType = MessageType.Undefined;
		public byte NotificationId;
		public byte QueryId;
		public byte[] QueryData;
		protected SerializerFlags SerializerFlags = SerializerFlags.Default;
		private List<IPAddress> addressHistory;
		private bool usingLegacySerialization;
		public bool IsInterClusterMsg = false;

		/// <summary>
		/// 	<para>Gets a value indicating whether an error occurred.</para>
		/// </summary>
		/// <value>
		/// 	<para><see langword="true"/> if and error occurred; otherwise, <see langword="false"/>.</para>
		/// </value>
		public bool ErrorOccurred
		{
			get { return ErrorType != RelayErrorType.None; }
		}

		/// <summary>
		/// Should be called before this message is sent to the server.
		/// </summary>
		internal void PrepareMessageToBeSent(bool useLegacySerialization)
		{
			SetError(RelayErrorType.None); // clear any previous errors
			ResultOutcome = null;
			ResultDetails = null;
			usingLegacySerialization = useLegacySerialization;
		}

		/// <summary>
		/// 	<para>Gets or sets the hydration policy for this message.</para>
		/// </summary>
		/// <value>
		/// 	<para>The hydration policy for this message.</para>
		/// </value>
		public IRelayHydrationPolicy HydrationPolicy { get; set; }

		/// <summary>
		/// Gets or sets a value that indicates how to extract the key from this instance.
		/// </summary>
		/// <value>A value that indicates how to extract the key from this instance.</value>
		public RelayKeyType KeyType { get; set; }

		/// <summary>
		/// Gets the type of the last error encountered by this message.
		/// </summary>
		/// <value>The type of the last error encountered by this message.</value>
		public RelayErrorType ErrorType { get; private set; }

		/// <summary>
		/// Sets <see cref="ErrorType"/> to the specified value.
		/// </summary>
		/// <param name="error">The type of error that occurred.</param>
		public void SetError(RelayErrorType error)
		{
			ErrorType = error;

			switch (error)
			{
				case RelayErrorType.None:
					//ResultOutcome must be reset manually
					break; // do nothing
				default:
					ResultOutcome = RelayOutcome.Error;
					break;
			};
		}

		/// <summary>
		/// Sets <see cref="ErrorType"/> to the appropriate value for the specified exception.
		/// </summary>
		/// <param name="exception">The exception that occurred.</param>
		internal void SetError(Exception exception)
		{
			var socketException = exception as SocketException;

			if (socketException == null)
			{
				SetError(RelayErrorType.Unknown);
				return;
			}

			switch (socketException.SocketErrorCode)
			{
				case SocketError.ConnectionReset:
				case SocketError.ConnectionRefused:
				case SocketError.HostDown:
				case SocketError.HostNotFound:
				case SocketError.HostUnreachable:
					SetError(RelayErrorType.NodeUnreachable);
					this.ResultDetails = "client: " + socketException.ToString();
					break;
				case SocketError.TimedOut:
					SetError(RelayErrorType.TimedOut);
					this.ResultOutcome = RelayOutcome.Timeout;
					this.ResultDetails = "client: " + socketException.ToString();
					break;
				default:
					SetError(RelayErrorType.Unknown);
					break;
			}
		}

		/// <summary>
		/// Gets a value indicating if this <see cref="RelayMessage"/> allows a <see cref="Payload"/>  
		/// response from the server for those messages where <see cref="IsTwoWayMessage"/> is true.
		/// </summary>
		/// <value>Returns true if a <see cref="Payload"/> is allowed to be returned.</value>
		public bool AllowsReturnPayload
		{
			get
			{
				return GetAllowsReturnPayload(MessageType);
			}
		}

		/// <summary>
		/// Gets a value indicating if this <see cref="RelayMessage"/> requires a response from
		/// the server.  If <see langword="true"/>, indicates this is an "OUT" message; otherwise,
		/// this is an "IN" message.
		/// </summary>
		public bool IsTwoWayMessage
		{
			get
			{
				return GetIsTwoWayMessage(MessageType);
			}
		}

		/// <summary>
		/// Gets a value indicating if the <paramref name="MessageType"/> 
		/// will be broadcast to all clusters.
		/// </summary>
		public bool IsClusterBroadcastMessage
		{
			get
			{
				return GetIsClusterBroadcastMessage(MessageType);
			}
		}

		/// <summary>
		/// Gets a value indicating if the <paramref name="type"/>
		/// will be broadcast to all clusters.
		/// </summary>
		/// <param name="type">The type to evaluate.</param>
		/// <returns>True if this is a cluster broadcast message; otherwise, false.</returns>
		internal static bool GetIsClusterBroadcastMessage(MessageType type)
		{
			switch (type)
			{
				case MessageType.DeleteAllInType:
				case MessageType.DeleteAll:
				case MessageType.DeleteAllWithConfirm:
				case MessageType.DeleteAllInTypeWithConfirm:
					return true;
			}

			return false;
		}

		/// <summary>
		/// Gets a value indicating if the <paramref name="MessageType"/> will be 
		/// broadcast to all groups.
		/// </summary>
		public bool IsGroupBroadcastMessage
		{
			get
			{
				return GetIsGroupBroadcastMessage(MessageType);
			}
		}

		/// <summary>
		/// Gets a value indicating if the <paramref name="type"/>
		/// will be broadcast to all groups.
		/// </summary>
		/// <param name="type">The type to evaluate.</param>
		/// <returns>True if this is a group broadcast message; otherwise, false.</returns>
		internal static bool GetIsGroupBroadcastMessage(MessageType type)
		{
			switch (type)
			{
				case MessageType.DeleteInAllTypes:
				case MessageType.DeleteAll:
				case MessageType.DeleteAllWithConfirm:
				case MessageType.DeleteInAllTypesWithConfirm:
					return true;
			}

			return false;
		}

		/// <summary>
		/// Gets a value indicating if the "OUT" <paramref name="type"/> allows a <see cref="Payload"/> from
		/// the server.  If <see langword="true"/>, indicates the framework returns a <see cref="Payload"/> if one
		/// is present and gives it back to the caller; otherwise, no <see cref="Payload"/> is returned.
		/// </summary>
		/// <param name="type">The type to evaluate.</param>
		/// <returns>True if a <see cref="Payload"/> may be returned; otherwise, false.</returns>
		internal static bool GetAllowsReturnPayload(MessageType type)
		{
			switch (type)
			{
				case MessageType.Get:
				case MessageType.Query:
				case MessageType.Invoke:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Gets a value indicating if the <paramref name="type"/> requires a response from
		/// the server.  If <see langword="true"/>, indicates this is an "OUT" message; otherwise,
		/// this is an "IN" message.
		/// </summary>
		/// <param name="type">The type to evaluate.</param>
		/// <returns>True if this is an "OUT" message; otherwise, false.</returns>
		internal static bool GetIsTwoWayMessage(MessageType type)
		{
			//if you change this also check GetAllowsReturnPayload
			switch (type)
			{
				case MessageType.Get:
				case MessageType.Query:
				case MessageType.Invoke:
				case MessageType.SaveWithConfirm:
				case MessageType.UpdateWithConfirm:
				case MessageType.DeleteWithConfirm:
				case MessageType.DeleteAllInTypeWithConfirm:
				case MessageType.DeleteAllWithConfirm:
				case MessageType.DeleteInAllTypesWithConfirm:
				case MessageType.NotificationWithConfirm:
				case MessageType.IncrementWithConfirm:
					return true;
				default:
					return false;
			}
		}

		/// <summary>
		/// Used to capture a performance metric of when the message arrived on the 
		/// current machine.  Do not serialize.
		/// </summary>
		[NonSerialized]
		public long EnteredCurrentSystemAt = 0;

		public int Priority;

		public short RelayTTL = 2;
		public ushort SourceZone = 0;

		/// <summary>
		/// Gets or sets the result details. This should never
		/// be set by a client. If it does and the messages is sent to
		/// an older server, the server will be unable to deserialize the
		/// message. So this field should be set with caution.
		/// </summary>
		/// <value>The result details.</value>
		public string ResultDetails { get; set; }

		/// <summary>
		/// Gets or sets the result outcome. This should never
		/// be set by a client. If it does and the messages is sent to
		/// an older server, the server will be unable to deserialize the
		/// message. So this field should be set with caution.
		/// </summary>
		/// <value>The result outcome.</value>
		public RelayOutcome? ResultOutcome { get; set; }

		public DateTime? Freshness { get; set; }
		#endregion

		#region Ctor

		public RelayMessage()
		{
			EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
		}

		public RelayMessage(RelayMessage oldMessage, MessageType newType)
		{
			this.EnteredCurrentSystemAt = oldMessage.EnteredCurrentSystemAt;
			this.TypeId = oldMessage.TypeId;
			this.Id = oldMessage.Id;
			this.ExtendedId = oldMessage.ExtendedId;
			this.MessageType = newType;
			this.ResultDetails = oldMessage.ResultDetails;
			this.ResultOutcome = oldMessage.ResultOutcome;
			this.Freshness = oldMessage.Freshness;
			this.usingLegacySerialization = oldMessage.usingLegacySerialization;
		}

		[Obsolete("don't use this, you risk losing ResultDetails, ResultOutcome, Freshness, UsingLegacySerialization")]
		public RelayMessage(RelayPayload payload, MessageType type)
		{
			this.MessageType = type;
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			if (payload != null)
			{
				this.TypeId = payload.TypeId;
				this.Id = payload.Id;
				this.ExtendedId = payload.ExtendedId;
				this.payload = payload;
			}
		}

		public RelayMessage(RelayPayload payload, MessageType type, string resultDetails, RelayOutcome? resultOutcome, DateTime? freshness, bool usingLegacySerialization)
		{
			this.MessageType = type;
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			if (payload != null)
			{
				this.TypeId = payload.TypeId;
				this.Id = payload.Id;
				this.ExtendedId = payload.ExtendedId;
				this.payload = payload;
			}
			this.ResultDetails = resultDetails;
			this.ResultOutcome = resultOutcome;
			this.Freshness = freshness;
			this.usingLegacySerialization = usingLegacySerialization;
		}

		public RelayMessage(short typeId, int id, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.TypeId = typeId;
			this.Id = id;
			this.MessageType = type;
		}

		public RelayMessage(short typeId, int id, byte[] extendedId, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.TypeId = typeId;
			this.Id = id;
			this.ExtendedId = extendedId;
			this.MessageType = type;
		}

		/// <summary>
		/// Creates a cache message with an extended Id but no defined TTL.
		/// </summary>
		/// <param name="typeID">The type of the object to be cached.</param>
		/// <param name="id">The id of the object to be cached.</param>
		/// <param name="extendedId">The extended id of the object to be cached.</param>
		/// <param name="byteArray">The bytes that make up the object to be cached.</param>
		/// <param name="isCompressed">Whether byteArray is compressed.</param>
		public RelayMessage(short typeId, int id, byte[] extendedId, DateTime? lastUpdatedDate, byte[] byteArray, bool isCompressed, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.Id = id;
			this.TypeId = typeId;
			this.MessageType = type;
			this.ExtendedId = extendedId;
			payload = new RelayPayload(typeId, id, extendedId, lastUpdatedDate, byteArray, isCompressed);
		}

		/// <summary>
		/// Creates a cache message with an extended Id.
		/// </summary>
		/// <param name="typeID">The type of the object to be cached.</param>
		/// <param name="id">The id of the object to be cached.</param>
		/// <param name="extendedId">The extended id of the object to be cached.</param>
		/// <param name="byteArray">The bytes that make up the object to be cached.</param>
		/// <param name="isCompressed">Whether byteArray is compressed.</param>
		public RelayMessage(short typeId, int id, byte[] extendedId, DateTime? lastUpdatedDate, byte[] byteArray, bool isCompressed, int ttlSeconds, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.Id = id;
			this.TypeId = typeId;
			this.MessageType = type;
			this.ExtendedId = extendedId;
			payload = new RelayPayload(typeId, id, extendedId, lastUpdatedDate, byteArray, isCompressed, ttlSeconds);
		}


		/// <summary>
		/// Creates a cache message with no defined TTL.
		/// </summary>
		/// <param name="typeID">The type of the object to be cached.</param>
		/// <param name="id">The id of the object to be cached.</param>
		/// <param name="byteArray">The bytes that make up the object to be cached.</param>
		/// <param name="isCompressed">Whether byteArray is compressed.</param>
		public RelayMessage(short typeId, int id, byte[] byteArray, bool isCompressed, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.Id = id;
			this.TypeId = typeId;
			this.MessageType = type;
			payload = new RelayPayload(typeId, id, byteArray, isCompressed);
		}

		/// <summary>
		/// Creates a cache message.
		/// </summary>
		/// <param name="typeID">The type of the object to be cached.</param>
		/// <param name="id">The id of the object to be cached.</param>
		/// <param name="byteArray">The bytes that make up the object to be cached.</param>
		/// <param name="isCompressed">Whether byteArray is compressed.</param>
		/// <param name="ttlSeconds">The number of seconds from now the object should remain in cache. -1 indicates no expiration.</param>
		public RelayMessage(short typeId, int id, byte[] byteArray, bool isCompressed, int ttlSeconds, MessageType type)
		{
			this.EnteredCurrentSystemAt = Stopwatch.GetTimestamp();
			this.Id = id;
			this.TypeId = typeId;
			this.MessageType = type;
			payload = new RelayPayload(typeId, id, byteArray, isCompressed, ttlSeconds);
		}

		#endregion

		#region Properties

		public List<IPAddress> AddressHistory
		{
			get
			{
				if (addressHistory == null)
				{
					addressHistory = new List<IPAddress>(3);
				}
				return addressHistory;
			}
		}

		public bool QueryDataCompressed
		{
			get
			{
				return (this.SerializerFlags & SerializerFlags.Compress) != 0;
			}
			set
			{
				this.SerializerFlags &= ~SerializerFlags.Compress;
				if (value) this.SerializerFlags |= SerializerFlags.Compress;
			}
		}

		public RelayPayload Payload
		{
			get
			{
				return payload;
			}
			set
			{
				payload = value;
			}
		}

		#endregion

		#region Get...MessageFor

		public static RelayMessage GetQueryMessageForQuery<TQuery>(short typeId, bool useCompression, TQuery objQuery)
			where TQuery : IRelayMessageQuery
		{
			RelayMessage message;
			byte[] extendedKeyBytes = null;

			IExtendedRawCacheParameter iercp = objQuery as IExtendedRawCacheParameter;
			if (iercp != null)
			{
				extendedKeyBytes = iercp.ExtendedId;
				message = new RelayMessage(typeId, iercp.PrimaryId, extendedKeyBytes, MessageType.Query);
			}
			else
			{
				IExtendedCacheParameter iecp = objQuery as IExtendedCacheParameter;

				if (iecp != null)
				{
					extendedKeyBytes = GetStringBytes(iecp.ExtendedId);
					message = new RelayMessage(typeId, iecp.PrimaryId, extendedKeyBytes, MessageType.Query);
				}
				else
				{
					// ExtendedId is REQUIRED!
					if (objQuery is IPrimaryQueryId)
					{
						// Set msg.PrimaryId to Query.PrimaryId
						int Id = (objQuery as IPrimaryQueryId).PrimaryId;
						message = new RelayMessage(typeId, Id, BitConverter.GetBytes(Id), MessageType.Query);
					}
					else
					{
						// TBD - Consider generating an ID for a Query.  For now, use const id
						message = new RelayMessage(typeId, 1, BitConverter.GetBytes(1), MessageType.Query);
					}
				}
			}
			// set Query data memebers explicitly
			message.QueryDataCompressed = useCompression;
			message.QueryId = objQuery.QueryId;
			message.QueryData = Serializer.Serialize<TQuery>(objQuery, useCompression, RelayCompressionImplementation);

			return message;
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, bool useCompression, T obj) where T : ICacheParameter
		{
			byte[] extendedKeyBytes = null;
			DateTime? lastUpdatedDate = null;

			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, obj.PrimaryId, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Save);
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, bool useCompression, int ttlSeconds, T obj) where T : ICacheParameter
		{
			byte[] extendedKeyBytes = null;
			DateTime? lastUpdatedDate = null;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);
			return new RelayMessage(typeId, obj.PrimaryId, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Save);

		}

		// use this to dictate RelayMessage properties like lastUpdateTicks
		public static RelayMessage GetSaveMessageForObject<T>(short typeId, bool useCompression, int ttlSeconds, long lastUpdateTicks, T obj) where T : ICacheParameter
		{

			byte[] extendedKeyBytes = null;
			DateTime? lastUpdatedDate = null;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, obj.PrimaryId, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Save);
		}


		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, bool useCompression, T obj) where T : ICacheParameter
		{

			DateTime? lastUpdatedDate = null;
			byte[] extendedKeyBytes = null;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, obj.PrimaryId, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Update);
		}

		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, bool useCompression, int ttlSeconds, T obj) where T : ICacheParameter
		{

			DateTime? lastUpdatedDate = null;
			byte[] extendedKeyBytes = null;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, obj.PrimaryId, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Update);
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, int id, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Save);
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, int id, byte[] extendedKeyBytes, DateTime? lastUpdatedDate, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Save);
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, int id, int ttlSeconds, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Save);
		}

		public static RelayMessage GetSaveMessageForObject<T>(short typeId, int id, byte[] extendedKeyBytes, DateTime? lastUpdatedDate, int ttlSeconds, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Save);
		}

		public static RelayMessage GetGetMessageForObject<T>(short typeId, T obj) where T : ICacheParameter
		{
			int primaryId = obj.PrimaryId;
			byte[] extendedKeyBytes;
			DateTime? lastUpdatedDate;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, primaryId, extendedKeyBytes, MessageType.Get);

		}

		public static RelayMessage GetDeleteMessageForObject<T>(short typeId, int id, T obj, bool useCompression) where T : ICacheParameter
		{
			byte[] extendedKeyBytes;
			DateTime? lastUpdatedDate;
			GetExtendedInfo(obj, out extendedKeyBytes, out lastUpdatedDate);

			return new RelayMessage(typeId, id, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Delete);
		}

		public static RelayMessage GetDeleteMessageForObject<T>(short typeId, int id, int ttlSeconds, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Delete);
		}

		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, int id, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Update);
		}

		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, int id, int ttlSeconds, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Update);
		}

		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, int id, byte[] extendedKeyBytes, DateTime? lastUpdatedDate, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, MessageType.Update);
		}

		public static RelayMessage GetUpdateMessageForObject<T>(short typeId, int id, byte[] extendedKeyBytes, DateTime? lastUpdatedDate, int ttlSeconds, T obj, bool useCompression)
		{
			return new RelayMessage(typeId, id, extendedKeyBytes, lastUpdatedDate, Serializer.Serialize<T>(obj, useCompression, RelayCompressionImplementation), useCompression, ttlSeconds, MessageType.Update);
		}

		#endregion

		#region Get Object Methods
		public bool GetQueryObject<TQuery>(TQuery instance) where TQuery : IRelayMessageQuery
		{
			return GetInputObject<TQuery>(instance);
		}

		public TQuery GetQueryObject<TQuery>() where TQuery : IRelayMessageQuery, new()
		{
			return GetInputObject<TQuery>();
		}

		public bool GetInputObject<TInput>(TInput instance)
		{
			if (QueryData == null)
			{
				return false;
			}
			using (MemoryStream stream = new MemoryStream(QueryData))
			{
				return Serializer.Deserialize<TInput>(stream, instance, this.SerializerFlags, RelayCompressionImplementation);
			}
		}

		public TInput GetInputObject<TInput>() where TInput : new()
		{
			if (QueryData == null)
			{
				return default(TInput);
			}
			using (MemoryStream stream = new MemoryStream(QueryData))
			{
				return Serializer.Deserialize<TInput>(stream, this.SerializerFlags, RelayCompressionImplementation);
			}
		}

		public bool GetObject<T>(T instance)
		{
			if (payload == null)
			{
				return false;
			}

			return payload.GetObject<T>(instance);

		}

		public bool GetObject(IVersionSerializable instance)
		{
			if (payload == null)
			{
				return false;
			}

			return payload.GetObject(instance);
		}

		public T GetObject<T>() where T : new()
		{
			if (payload == null)
			{
				return default(T);
			}

			return payload.GetObject<T>();
		}

		#endregion

		#region GetCachingKey & GetExtendedKeyBytes

		public static byte[] GetStringBytes(string str)
		{
			byte[] extendedKeyBytes = null;
			if (str != null)
			{
				extendedKeyBytes = stringEncoder.GetBytes(str);
			}
			return extendedKeyBytes;
		}

		public static string GetBytesString(byte[] bytes)
		{
			if (bytes == null) return null;
			return RelayMessage.stringEncoder.GetString(bytes);
		}

		public static void GetExtendedInfo(ICacheParameter cacheObject, out byte[] extendedId, out DateTime? lastUpdatedDate)
		{

			IExtendedRawCacheParameter iercp = cacheObject as IExtendedRawCacheParameter;
			if (iercp != null)
			{
				extendedId = iercp.ExtendedId;
				lastUpdatedDate = iercp.LastUpdatedDate;
			}
			else
			{
				IExtendedCacheParameter iecp = cacheObject as IExtendedCacheParameter;
				if (iecp != null)
				{
					extendedId = GetStringBytes(iecp.ExtendedId);
					lastUpdatedDate = iecp.LastUpdatedDate;
				}
				else
				{
					extendedId = null;
					lastUpdatedDate = null;
				}
			}

		}

		public static string GetCachingKey(int objectType, int objectId)
		{
			return objectType + "_" + objectId;
		}

		public string GetCachingKey()
		{
			return GetCachingKey(this.TypeId, this.Id);
		}
		#endregion

		#region Other Public Methods

		internal void ExtractResponse(RelayMessage serverResponse)
		{
			if (AllowsReturnPayload)
			{
				Payload = serverResponse.Payload;
			}
			ResultOutcome = serverResponse.ResultOutcome;
			ResultDetails = serverResponse.ResultDetails;
			//freshness?
			if (ResultOutcome == null)
			{
				ResultOutcome = RelayOutcome.NotSupported;
			}
		}

		/// <summary>
		/// Gets a new <see cref="RelayMessage"/> from the given <see cref="Stream"/>.
		/// </summary>
		/// <param name="stream">The stream to deserialize from.</param>
		/// <returns></returns>
		public static RelayMessage GetInstanceFromStream(Stream stream)
		{
			RelayMessage message = new RelayMessage();
			try
			{
				Serializer.Deserialize<RelayMessage>(stream, message, SerializerFlags.Default, RelayCompressionImplementation);
			}
			catch (SerializationException exc)
			{
				if (stream == null) throw;
				else //provide context
				{
					//try and add some context to this object
					//Generally it's the payload that has problems so the 
					//Id, ExtendedId and TypeId most likely got correctly deserialized so 
					//we're providing that much as context
					string errorMessage = string.Format("Deserialization failed for RelayMessage of Id='{0}', ExtendedId='{1}', TypeId='{2}' and StreamLength='{3}'",
						message.Id, Algorithm.ToHex(message.ExtendedId), message.TypeId, stream.Length);
					SerializationException newException = new SerializationException(errorMessage, exc);
					throw newException;
				}
			}
			return message;
		}

		public static RelayMessage CreateInterZoneMessageFrom(RelayMessage oldMessage)
		{
			RelayMessage InterZoneMsg = new RelayMessage(oldMessage, oldMessage.MessageType);

			// Incrementing RelayTTL is required to properly propagate a message across zone boundaries
			InterZoneMsg.RelayTTL = (short)(oldMessage.RelayTTL + 1);

			// copy all other info without modification
			InterZoneMsg.Payload = oldMessage.Payload;
			InterZoneMsg.SourceZone = oldMessage.SourceZone;
			InterZoneMsg.AddressHistory.AddRange(new List<System.Net.IPAddress>(oldMessage.AddressHistory));

			return InterZoneMsg;
		}

		public void Reset()
		{
			Id = 0;
			ExtendedId = null;
			TypeId = 0;
			MessageType = MessageType.Undefined;
			SetError(RelayErrorType.None);
			QueryId = 0;
			QueryData = null;
			SerializerFlags = SerializerFlags.Default;
			payload = null;
			Priority = 0;

			RelayTTL = 2;
			SourceZone = 0;
		}

		public bool OriginatesDirectlyFromClient(ushort ServerZone)
		{
			return (this.RelayTTL > 0 && this.SourceZone == ServerZone);
		}

		#endregion

		#region Compression

		// high Byte: SourceZone
		// low Byte : relayTTL
		private static ushort Compress(ushort SourceZone, short RelayTTL)
		{
			if (SourceZone > 255)
			{
				throw new Exception("Invalid SourceZone value " + SourceZone + ".  Legal range = [0..255]");
			}
			else if (RelayTTL < -128 || RelayTTL > 127)
			{
				throw new Exception("Invalid RelayTTL value " + RelayTTL + ".  Legal range = [-128..127]");
			}

			// Shift Source Zone into High Byte 
			// Keep RelayTTL in Low Byte, but clear out it's High Byte bits
			return (ushort)((uint)(SourceZone << 8) | ((uint)(RelayTTL & 0x00FF)));
		}

		private static void Decompress(ushort ZoneAndRelayTTL, out ushort SourceZone, out short RelayTTL)
		{
			SourceZone = (ushort)(ZoneAndRelayTTL >> 8);

			if ((0x0080 & ZoneAndRelayTTL) > 0)
			{
				// handle negative number by setting hight byte to FF
				RelayTTL = (short)(0xFF00 | ((uint)(ZoneAndRelayTTL & 0x00FF)));
			}
			else
			{
				RelayTTL = ((short)(ZoneAndRelayTTL & 0x00FF));
			}
		}

		internal static CompressionImplementation RelayCompressionImplementation = Compressor.DefaultCompressionImplementation;
		public static void SetCompressionImplementation(CompressionImplementation compressionImplementation)
		{
			RelayCompressionImplementation = compressionImplementation;
		}

		public static CompressionImplementation GetCompressionImplementation()
		{
			return RelayCompressionImplementation;
		}

		#endregion

		#region ToString

		public override string ToString()
		{
			StringBuilder desc = new StringBuilder();
			desc.Append("RelayMessage ");
			desc.Append(MessageType.ToString());
			desc.Append(" TTL ");
			desc.Append(RelayTTL);
			desc.Append(" Source Zone ");
			desc.Append(SourceZone);

			switch (MessageType)
			{
				case MessageType.Undefined:
				case MessageType.DeleteAll:
					break;
				case MessageType.Update:
				case MessageType.Delete:
				case MessageType.Get:
				case MessageType.Save:
				case MessageType.Query:
				case MessageType.Invoke:
				case MessageType.Notification:
					desc.Append(" ID ");
					desc.Append(Id);
					desc.Append(" TypeId ");
					desc.Append(TypeId);

					AppendByteString(desc, "ExtendedId", ExtendedId);

					if (MessageType == MessageType.Query)
					{
						desc.Append(" QueryId ");
						desc.Append(QueryId);
						desc.Append(" QueryData ");
						if (QueryData == null)
						{
							desc.Append("null");
						}
						else
						{
							AppendByteString(desc, "QueryData", QueryData);
						}
						desc.Append(" QueryDataCompressed ").Append(QueryDataCompressed.ToString());
					}
					else if (MessageType == MessageType.Notification)
					{
						desc.Append(" NotificationId ");
						desc.Append(NotificationId);
					}
					break;

				case MessageType.DeleteAllInType:
					desc.Append(" TypeId ");
					desc.Append(TypeId);
					break;
				case MessageType.DeleteInAllTypes:
					desc.Append(" ID ");
					desc.Append(Id);
					AppendByteString(desc, "ExtendedId", ExtendedId);
					break;
			}
			if (Payload != null && Payload.ByteArray != null)
			{
				AppendByteString(desc, "Payload", Payload.ByteArray);
			}
			if (addressHistory != null && addressHistory.Count > 0)
			{
				desc.Append(" From");
				for (int i = 0; i < addressHistory.Count; i++)
				{
					desc.Append(" ");
					desc.Append(addressHistory[i].ToString());
					if (i < addressHistory.Count - 1)
					{
						desc.Append(",");
					}
				}
			}
			return desc.ToString();
		}

		static private void AppendByteString(StringBuilder desc, string bytesName, byte[] bytes)
		{
			const int maxLengthToPrint = 32;
			if (bytes != null)
			{
				desc.AppendFormat(" {0} ", bytesName);

				if (bytes.Length > maxLengthToPrint)
				{

					desc.Append(bytes.Length);
					desc.Append(" Bytes.");
				}
				else
				{
					for (int i = 0; i < bytes.Length; i++)
					{
						desc.Append(bytes[i].ToString("X2"));
					}
				}
			}
		}

		#endregion

		#region IVersionSerializable Members

		public void Serialize(IPrimitiveWriter writer)
		{
			Serialize(writer, this.CurrentVersion);
		}

		private void Serialize(IPrimitiveWriter writer, int version)
		{
			int currentVersion = version;

			writer.Write(this.TypeId);
			writer.Write(this.Id);
			writer.Write((Int32)MessageType);
			writer.Write(Compress(this.SourceZone, this.RelayTTL));
			//this is to indicate new serialization that supports reading a newer version
			//without corrupting the stream
			//formerly there was a private list serialized here that wasn't used
			//and it always wrote -1 for a null list.
			writer.Write(0); //we're saying that anyone who deserializes us, that we can read a response that supports new serialization

			if (addressHistory != null)
			{
				writer.Write(true);
				writer.Write((short)addressHistory.Count);
				for (int i = 0; i < addressHistory.Count; i++)
				{
					writer.Write(addressHistory[i].GetAddressBytes());
				}
			}
			else
			{
				writer.Write(false);
			}

			if (payload != null)
			{
				writer.Write(true);
				writer.Write<RelayPayload>(payload);
			}
			else
			{
				writer.Write(false);
			}

			if (ExtendedId != null)
			{
				if (currentVersion >= 4) writer.Write(true);
				writer.Write(ExtendedId.Length);
				writer.Write(ExtendedId);
			}
			else if (currentVersion >= 4)
			{
				writer.Write(false);
			}
			else if (currentVersion >= 2)
			{
				//  Write out a zero-length extended ID to prevent
				//  deserialization error.

				// this causes an inconsistency where a null ExtendedId will be 
				// deserialized into an empty byte array (byte[0]), all other cases
				// the null extended Id is preserved. fwise 5/2009
				writer.Write(0);
			}

			if ((MessageType == MessageType.Query) || (MessageType == MessageType.Invoke))
			{
				writer.Write(QueryId);
				writer.Write(QueryDataCompressed);

				if (QueryData == null)
				{
					writer.Write(-1);
				}
				else
				{
					writer.Write(QueryData.Length);
					writer.Write(QueryData);
				}
			}
			else if (MessageType == MessageType.Notification)
			{
				writer.Write(NotificationId);
			}

			Int32 length = 0;
			long lengthPosition = writer.BaseStream.Position;

			if (usingLegacySerialization == false)
			{
				if (version >= 5)
				{
					writer.Write(length); //we're going to go back and overwrite this with the correct length

					long? dt = null;
					if (this.Freshness != null)
					{
						dt = this.Freshness.Value.Ticks;
					}

					writer.Write(dt);
					writer.Write((byte?)ResultOutcome);
					writer.Write(ResultDetails);
				}

				if (version == 6)
				{
					writer.Write(HydrationPolicy == null ? false : (HydrationPolicy.Options & RelayHydrationOptions.HydrateOnMiss) == RelayHydrationOptions.HydrateOnMiss);
				}
				else if (version >= 7)
				{
					if (HydrationPolicy != null)
					{
						writer.Write(true);
						writer.WriteVarInt32((int)HydrationPolicy.KeyType);
						writer.WriteVarInt32((int)HydrationPolicy.Options);
					}
					else
					{
						writer.Write(false);
					}
				}

				//version 8+ goes here

				//keep at end
				if (version >= 5)
				{
					long endPosition = writer.BaseStream.Position;
					writer.BaseStream.Seek(lengthPosition, SeekOrigin.Begin);
					length = (Int32)(endPosition - lengthPosition - sizeof(Int32));
					writer.Write(length);
					writer.BaseStream.Seek(endPosition, SeekOrigin.Begin);
				}
			}
		}

		public void Deserialize(IPrimitiveReader reader, int version)
		{
			this.TypeId = reader.ReadInt16();
			this.Id = reader.ReadInt32();
			this.MessageType = (MessageType)reader.ReadInt32();
			Decompress(reader.ReadUInt16(), out this.SourceZone, out this.RelayTTL);

			int legacySerializationCheck = reader.ReadInt32();
			usingLegacySerialization = (legacySerializationCheck == -1);

			if (reader.ReadBoolean())
			{
				short historyCount = reader.ReadInt16();
				addressHistory = new List<IPAddress>((int)historyCount);
				for (short i = 0; i < historyCount; i++)
				{
					byte[] addressBytes = reader.ReadBytes(4);
					addressHistory.Add(new IPAddress(addressBytes));
				}
			}

			if (reader.ReadBoolean())
			{
				payload = new RelayPayload();
				reader.Read<RelayPayload>(payload, false);
			}

			if (version > 1)
			{
				if ((version >= 4) && (reader.ReadBoolean() == false))
				{
					this.ExtendedId = null;
				}
				else
				{
					int keyLength = reader.ReadInt32();
					this.ExtendedId = reader.ReadBytes(keyLength);
				}
			}

			if (version > 2 && ((this.MessageType == MessageType.Query) || (MessageType == MessageType.Invoke)))
			{
				this.QueryId = reader.ReadByte();
				this.QueryDataCompressed = reader.ReadBoolean();
				int queryDataLength = reader.ReadInt32();
				if (queryDataLength >= 0)
				{
					QueryData = reader.ReadBytes(queryDataLength);
				}
			}

			if (version > 3 && this.MessageType == MessageType.Notification)
			{
				this.NotificationId = reader.ReadByte();
			}

			Int32 length = 0;
			long endPosition = 0;

			if (version >= 5)
			{
				length = reader.ReadInt32();
				endPosition = reader.BaseStream.Position + length;

				long? dt = reader.ReadNullableInt64();
				if (dt != null)
				{
					this.Freshness = new DateTime(dt.Value);
				}
				else
				{
					this.Freshness = null;
				}
				byte? outcome = reader.ReadNullableByte();
				if (outcome != null)
				{
					this.ResultOutcome = (RelayOutcome)outcome;
				}
				else
				{
					this.ResultOutcome = null;
				}
				this.ResultDetails = reader.ReadString();
			}

			if (version == 6)
			{
				HydrationPolicy =
					reader.ReadBoolean()
					? SimpleHydrationPolicy.LegacyDefault
					: null;
			}
			else if (version >= 7)
			{
				if (reader.ReadBoolean())
				{
					HydrationPolicy = new SimpleHydrationPolicy
					{
						KeyType = (RelayKeyType)reader.ReadVarInt32(),
						Options = (RelayHydrationOptions)reader.ReadVarInt32()
					};
				}
				else
				{
					HydrationPolicy = null;
				}
			}

			//version 8+ goes here

			//leave at end of method
			if (version >= 5)
			{
				if (reader.BaseStream.Position != endPosition)
				{
					reader.BaseStream.Seek(endPosition, SeekOrigin.Begin);
				}
			}
		}

		public void Deserialize(IPrimitiveReader reader)
		{
			Deserialize(reader, CurrentVersion);
		}

		/// <summary>
		/// Gets the current serialization version.
		/// </summary>
		/// <remarks>
		/// <para>The reason this property is so bizarre is because the client and server
		/// will get updated at different times.  The new stream is only sent to the server
		/// that supports this new <see cref="MessageType"/>.</para>
		/// </remarks>
		public int CurrentVersion
		{
			get
			{
				if (usingLegacySerialization == false)
				{
					//we introduce forward safe code in version 5
					return 7;
				}

				switch (this.MessageType)
				{
					case MessageType.Notification:
						return 4;

					case MessageType.Query:
					case MessageType.Invoke:
						return 3;

					default:
						return (this.ExtendedId == null) ? 1 : 2;
				}
			}
		}

		public bool Volatile
		{
			get { return false; }
		}

		#endregion

		private class SimpleHydrationPolicy : IRelayHydrationPolicy
		{
			private static readonly IRelayHydrationPolicy _legacyDefault = new SimpleHydrationPolicy
			{
				KeyType = RelayKeyType.Int32,
				Options = RelayHydrationOptions.HydrateAll
			};

			public static IRelayHydrationPolicy LegacyDefault
			{
				get { return _legacyDefault; }
			}

			public RelayKeyType KeyType { get; set; }

			public RelayHydrationOptions Options { get; set; }
		}
	}
}
