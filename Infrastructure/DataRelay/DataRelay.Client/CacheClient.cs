using System;
using MySpace.Common;
using MySpace.Common.IO;
using MySpace.DataRelay.Configuration;

namespace MySpace.DataRelay.Client
{
    /// <summary>
    /// A Data Relay client based on the Future pattern.
    /// </summary>
    public class CacheClient : RelayMessageClient
    {
        private static readonly CacheClient _defaultInstance = new CacheClient();

        /// <summary>
        /// Gets the default cache client instance.
        /// </summary>
        /// <value>The default.</value>
        /// <remarks>Use the default instance for general purpose CacheClient instances.  The default instance will use the Data Relay configuation
        /// system to determine type settings and group configuration.</remarks>
        public static CacheClient Default { get { return _defaultInstance; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheClient"/> class.
        /// </summary>
        /// <remarks>The constructor is responsible for acquiring an appropirate configuration for the relay client.</remarks>
        private CacheClient() 
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheClient"/> class, whit a specified relay node config.  Used in unit tests.
        /// </summary>
        /// <param name="nodeConfig">The node config.</param>
        internal CacheClient(RelayNodeConfig nodeConfig) : base(nodeConfig)
        {
        }

        /// <summary>
		/// Gets a domain object from the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain object type to get.</typeparam>
		/// <param name="key">The object key.</param>
		/// <returns>A <see cref="Future{T}"/> that will contain the object when it has been returned from Data Relay.</returns>
		public Future<CacheResult<T>> GetObject<T>(int key) 
		{
			return GetObject<T>(key, null);
		}

		/// <summary>
		/// Gets a domain object from the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain object type to get.</typeparam>
		/// <param name="key">The object key.</param>
		/// <returns>A <see cref="Future{T}"/> that will contain the object when it has been returned from Data Relay.</returns>
        public Future<CacheResult<T>> GetObject<T>(string key)
		{
			byte[] byteKey = RelayMessage.GetStringBytes(key);
			return GetObject<T>(GetIntFromBytes(byteKey), byteKey);
		}

		/// <summary>
		/// Gets a domain object from the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain object type to get.</typeparam>
		/// <param name="key">The object key.</param>
		/// <returns>A <see cref="Future{T}"/> that will contain the object when it has been returned from Data Relay.</returns>
        public Future<CacheResult<T>> GetObject<T>(byte[] key)
		{
			return GetObject<T>(GetIntFromBytes(key), key);
		}

    	/// <summary>
        /// Saves a domain object to the Data Relay infrastructure.
        /// </summary>
        /// <typeparam name="T">The domain type of the object to save, this type is inferred from the paramter passed in.</typeparam>
        /// <param name="key">The object key.</param>
		/// <param name="putObject">The object to save.</param>
        /// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the save operation.  Will have a Value if successful, an error otherwise.</returns>
        public Future<RelayOutcome> PutObject<T>(int key, T putObject)
        {
			return PutObject(key, null, putObject);
        }

		/// <summary>
		/// Saves a domain object to the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain type of the object to save, this type is inferred from the paramter passed in.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="putObject">The object to save.</param>
		/// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the save operation.  Will have a Value if successful, an error otherwise.</returns>
		public Future<RelayOutcome> PutObject<T>(string key, T putObject)
		{
			byte[] extendedId = RelayMessage.GetStringBytes(key);
			int primaryId = GetIntFromBytes(extendedId);
			return PutObject(primaryId, extendedId, putObject);
		}

		/// <summary>
		/// Saves a domain object to the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain type of the object to save, this type is inferred from the paramter passed in.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="putObject">The object to save.</param>
		/// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the save operation.  Will have a Value if successful, an error otherwise.</returns>
		public Future<RelayOutcome> PutObject<T>(byte[] key, T putObject)
		{
			byte[] extendedId = key;
			int primaryId = GetIntFromBytes(extendedId);
			return PutObject(primaryId, extendedId, putObject);
		}

        /// <summary>
        /// Saves a domain object to the Data Relay infrastructure.
        /// </summary>
        /// <typeparam name="T">The domain type of the object to save, this type is inferred from the paramter passed in.</typeparam>
        /// <param name="putObject">The ICacheParameter object to save.</param>
        /// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the save operation.  Will have a Value if successful, an error otherwise.</returns>
        public Future<RelayOutcome> PutObject<T>(T putObject) where T : ICacheParameter
        {
            byte[] extendedId;
            DateTime? lastUpdatedDate;
            RelayMessage.GetExtendedInfo(putObject, out extendedId, out lastUpdatedDate);
            return PutObject(putObject.PrimaryId, extendedId, putObject);
        }

    	/// <summary>
        /// Deletes a domain object in the Data Relay infrastructure.
        /// </summary>
        /// <typeparam name="T">The domain type of the object to delete, this type is inferred from the paramter passed in.</typeparam>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the delete operation.  Will have a Value if successful, an error otherwise.</returns>
        public Future<RelayOutcome> DeleteObject<T>(int key)
    	{
    		return DeleteObject<T>(key, null);
    	}

		/// <summary>
		/// Deletes a domain object in the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain type of the object to delete, this type is inferred from the paramter passed in.</typeparam>
		/// <param name="key">The key.</param>
		/// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the delete operation.  Will have a Value if successful, an error otherwise.</returns>
		public Future<RelayOutcome> DeleteObject<T>(string key)
		{
			byte[] byteKey = RelayMessage.GetStringBytes(key);
			return DeleteObject<T>(GetIntFromBytes(byteKey), byteKey);
		}

		/// <summary>
		/// Deletes a domain object in the Data Relay infrastructure.
		/// </summary>
		/// <typeparam name="T">The domain type of the object to delete, this type is inferred from the paramter passed in.</typeparam>
		/// <param name="key">The key.</param>
		/// <returns>A <see cref="Future{T}"/> of the <see cref="RelayOutcome"/> of the delete operation.  Will have a Value if successful, an error otherwise.</returns>
		public Future<RelayOutcome> DeleteObject<T>(byte[] key)
		{
			return DeleteObject<T>(GetIntFromBytes(key), key);
		}

		private Future<CacheResult<T>> GetObject<T>(int primaryId, byte[] extendedId)
		{
			var typeSetting = GetTypeSetting<T>();
			if (typeSetting == null) return RelayTypeSettings.InvalidTypeFuture<CacheResult<T>>();

			var relayMessage = new RelayMessage(typeSetting.TypeId, primaryId, extendedId, MessageType.Get);

			var future = SendRelayMessage(typeSetting, relayMessage);
			return future.Convert(replyMessage =>
			{
                if (replyMessage == null || replyMessage.Payload == null) return new CacheResult<T>();
                return new CacheResult<T>(replyMessage.Payload.GetObject<T>());
			});
		}

    	private Future<RelayOutcome> DeleteObject<T>(int primaryId, byte[] extendedId)
    	{
    	    var typeSetting = GetTypeSetting<T>();
    	    if (typeSetting == null) return RelayTypeSettings.InvalidTypeFuture<RelayOutcome>();
    	    var relayMessage = new RelayMessage(typeSetting.TypeId, primaryId, extendedId, MessageType.Delete);
            return SendRelayMessage(typeSetting, relayMessage).Convert(replyMessage => replyMessage == null ? RelayOutcome.Success : replyMessage.ResultOutcome ?? RelayOutcome.Success);
        }

        private Future<RelayOutcome> PutObject<T>(int primaryId, byte[] extendedId, T saveObject)
        {
            var typeSetting = base.GetTypeSetting<T>();
            if (typeSetting == null) return RelayTypeSettings.InvalidTypeFuture<RelayOutcome>();

            var payload = new RelayPayload(typeSetting.TypeId, primaryId, Serializer.Serialize(saveObject, typeSetting.Compress), typeSetting.Compress);
            var relayMessage = new RelayMessage(typeSetting.TypeId, primaryId, extendedId, MessageType.Save);
            relayMessage.Payload = payload;
            return SendRelayMessage(typeSetting, relayMessage).Convert(replyMessage => replyMessage == null ? RelayOutcome.Success : replyMessage.ResultOutcome ?? RelayOutcome.Success);
        }

        private static int GetIntFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return 0;
            if (bytes.Length >= 4) return BitConverter.ToInt32(bytes, bytes.Length - 4);

            var result = new byte[4];
            for (int i = bytes.Length - 1; i >= 0; i--)
            {
                result[i] = bytes[i];
            }
            return BitConverter.ToInt32(result, 0);
        }
    }
}
