using System;
using System.IO;
using MySpace.Common;
using MySpace.Common.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using MySpace.DataRelay.Formatters;
using MySpace.DataRelay.Common.Interfaces.Query;

namespace MySpace.DataRelay
{
	/// <summary>
	/// Encapsulates the primary data of a <see cref="RelayMessage"/> and provides functionality
	/// to convert the transport representation of the data into the client representation.
	/// </summary>
    [Serializable()]
    public class RelayPayload : IVersionSerializable
	{
		#region Fields
		
		public byte[] ByteArray;
		public int Id;
		public short TypeId;
		public byte[] ExtendedId;

		#endregion 

		#region Ctor

		/// <summary>
		/// Initializes a <see cref="RelayPayload"/>.
		/// </summary>
		public RelayPayload()
        {
        }

        public RelayPayload(short typeId, int id)
        {
            this.TypeId = typeId;
            this.Id = id;
        }

        /// <summary>
        /// Creates a cache message with no defined TTL.
        /// </summary>
        /// <param name="typeId">The type of the object to be cached.</param>
        /// <param name="id">The id of the object to be cached.</param>
        /// <param name="byteArray">The bytes that make up the object to be cached.</param>
        /// <param name="isCompressed">Whether byteArray is compressed.</param>
        public RelayPayload(short typeId, int id, byte[] byteArray, bool isCompressed)
        {
            this.ByteArray = byteArray;
            this.Id = id;
            this.TypeId = typeId;
            this.LastUpdatedTicks = DateTime.Now.Ticks;
            this.Compressed = isCompressed;
        }

        /// <summary>
        /// Creates a cache message with no defined TTL.
        /// </summary>
        /// <param name="typeId">The type of the object to be cached.</param>
        /// <param name="id">The id of the object to be cached.</param>
        /// <param name="extendedId">The extended id of the object.</param>
        /// <param name="byteArray">The bytes that make up the object to be cached.</param>
        /// <param name="isCompressed">Whether byteArray is compressed.</param>
        public RelayPayload(short typeId, int id, byte[] extendedId, DateTime? lastUpdatedDate, byte[] byteArray, bool isCompressed)
        {
            this.ByteArray = byteArray;
            this.Id = id;
            this.ExtendedId = extendedId;
            this.TypeId = typeId;
            this.LastUpdatedTicks = lastUpdatedDate.GetValueOrDefault(DateTime.Now).Ticks;
            this.Compressed = isCompressed;
        }

        /// <summary>
        /// Creates a cache message.
        /// </summary>
        /// <param name="typeId">The type of the object to be cached.</param>
        /// <param name="id">The id of the object to be cached.</param>
        /// <param name="byteArray">The bytes that make up the object to be cached.</param>
        /// <param name="isCompressed">Whether byteArray is compressed.</param>
        /// <param name="ttlSeconds">The number of seconds from now the object should remain in cache. -1 indicates no expiration.</param>
        public RelayPayload(short typeId, int id, byte[] byteArray, bool isCompressed, int ttlSeconds)
        {
            this.ByteArray = byteArray;
            this.Id = id;
            this.TypeId = typeId;
            this.LastUpdatedTicks = DateTime.Now.Ticks;
            this.Compressed = isCompressed;
            this.TTL = ttlSeconds;
        }

        /// <summary>
        /// Creates a cache message.
        /// </summary>
        /// <param name="typeId">The type of the object to be cached.</param>
        /// <param name="id">The id of the object to be cached.</param>
        /// <param name="extendedId">The extended id of the object.</param>
        /// <param name="byteArray">The bytes that make up the object to be cached.</param>
        /// <param name="isCompressed">Whether byteArray is compressed.</param>
        /// <param name="ttlSeconds">The number of seconds from now the object should remain in cache. -1 indicates no expiration.</param>
        public RelayPayload(short typeId, int id, byte[] extendedId, DateTime? lastUpdatedDate, byte[] byteArray, bool isCompressed, int ttlSeconds)
        {
            this.ByteArray = byteArray;
            this.Id = id;
            this.ExtendedId = extendedId;
            this.TypeId = typeId;
            this.LastUpdatedTicks = lastUpdatedDate.GetValueOrDefault(DateTime.Now).Ticks;
            this.Compressed = isCompressed;
            this.TTL = ttlSeconds;
        }

        public RelayPayload(short typeId, int id, byte[] extendedId, byte[] byteArray, bool isCompressed, int ttlSeconds, long lastUpdatedTicks, long expiration)
        {
            this.TypeId = typeId;
            this.Id = id;
            this.ExtendedId = extendedId;
            this.ByteArray = byteArray;
            this.Compressed = isCompressed;
            this.TTL = ttlSeconds;
            this.LastUpdatedTicks = lastUpdatedTicks;
            this.expirationTicks = expiration;
        }

        public RelayPayload(short typeId, int id, byte[] byteArray, bool isCompressed, int ttlSeconds, long lastUpdatedTicks, long expiration)
        {
            this.TypeId = typeId;
            this.Id = id;
            this.ByteArray = byteArray;
            this.Compressed = isCompressed;
            this.TTL = ttlSeconds;
            this.LastUpdatedTicks = lastUpdatedTicks;
            this.expirationTicks = expiration;
		}

		#endregion

		public bool Compressed
        {
            get { return (this.SerializerFlags & SerializerFlags.Compress) != 0; }
            set
            {
                this.SerializerFlags &= ~SerializerFlags.Compress;
                if (value) this.SerializerFlags |= SerializerFlags.Compress;
            }
        }
        private SerializerFlags SerializerFlags = SerializerFlags.Default;

        /// <summary>
        /// Get a value indicating at what time the object will be force purged from the cache.
        /// Setting the TTL value sets this value.
        /// </summary>
		/// <value>Purges occur approximately every 5 minutes.
		/// -1 indicates no forced expiration.
		/// </value>
        public long ExpirationTicks
        {
            get
            {
                return this.expirationTicks;
            }
        }
		private long expirationTicks = -1;

        
        public DateTime LastUpdatedDate
        {
            get
            {
                return new DateTime(this.LastUpdatedTicks);
            }
            set
            {
                this.LastUpdatedTicks = value.Ticks;
            }
        }
		public long LastUpdatedTicks;

        
        /// <summary>
        /// The number of seconds from now the object should remain in the cache. -1 indicates no forced expiration.
        /// </summary>
        public int TTL
        {
            set
            {
                this.ttl = value;
                if (this.ttl == -1)
                {
                    this.expirationTicks = -1;
                }
                else
                {
                    this.expirationTicks = DateTime.Now.AddSeconds(value).Ticks;
                }
            }
            get
            {
                return ttl;
            }
		}
		private int ttl = -1;

		#region GetObject

		/// <summary>
		/// Loads the given object with data from the contained payload.
		/// </summary>
		/// <typeparam name="T">Any type.</typeparam>
		/// <param name="instance">The given instance to load from the payload.</param>
		/// <returns>True if successful; otherwise false.</returns>
        public bool GetObject<T>(T instance)
        {
            if (this.ByteArray == null || this.ByteArray.Length == 0)
            {
                return false;
            }

            MemoryStream stream = new MemoryStream(this.ByteArray);
            Serializer.Deserialize<T>(stream, instance, this.SerializerFlags, RelayMessage.RelayCompressionImplementation);
            SetLastUpdatedDate<T>(instance);           
            return true;
        }

		/// <summary>
		/// Creates a loads new instance of type <typeparamref name="T"/> that implements an empty 
		/// constructor.
		/// </summary>
		/// <typeparam name="T">Any type that implements and empty constructor.</typeparam>
		/// <returns>The loaded new instance if successful; otherwise returns a default empty value.</returns>
        public T GetObject<T>() where T : new()
        {
            if (this.ByteArray == null || this.ByteArray.Length == 0)
            {
                return default(T);
            }

            MemoryStream stream = new MemoryStream(this.ByteArray);
            T instance = Serializer.Deserialize<T>(stream, this.Compressed);
            SetLastUpdatedDate<T>(instance);
            return instance;
		}

		#endregion

        private void SetLastUpdatedDate<T>(T instance)
        {
            IExtendedRawCacheParameter iercp = instance as IExtendedRawCacheParameter;
            if (iercp != null)
            {
                iercp.LastUpdatedDate = LastUpdatedDate;
            }
            else
            {
                IExtendedCacheParameter iecp = instance as IExtendedCacheParameter;
                if (iecp != null)
                {
                    iecp.LastUpdatedDate = LastUpdatedDate;
                }
            }
        }

		#region IVersionSerializable Members

		/// <summary>
		/// Serializes a <see cref="RelayPayload"/> into the <see cref="IPrimitiveWriter"/>,
		/// </summary>
		/// <param name="writer">The <see cref="IPrimitiveWriter"/> to serialize into.</param>
		public void Serialize(IPrimitiveWriter writer)
        {
            writer.Write(this.TypeId);
            writer.Write(this.Id);
            writer.Write(this.LastUpdatedTicks);
            writer.Write(this.Compressed);
            writer.Write(this.ttl);
            writer.Write(this.expirationTicks);
            if (this.ByteArray != null)
            {
                writer.Write(this.ByteArray.Length);
                writer.Write(this.ByteArray);
            }
            else
            {
                writer.Write(-1);
            }

            if (ExtendedId != null) //not serializing at all if not null. version will also be 1.
            {
                writer.Write(ExtendedId.Length);
                writer.Write(ExtendedId);
            }
		}

		/// <summary>
		/// Deserializes a <see cref="RelayPayload"/> from an <see cref="IPrimitiveReader"/>.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="version">The version of the <paramref name="reader"/> data.</param>
		public void Deserialize(IPrimitiveReader reader, int version)
        {
            this.TypeId = reader.ReadInt16();
            this.Id = reader.ReadInt32();
            this.LastUpdatedTicks = reader.ReadInt64();
            this.Compressed = reader.ReadBoolean();
            this.ttl = reader.ReadInt32();
            this.expirationTicks = reader.ReadInt64();
            int byteLength = reader.ReadInt32();
            if (byteLength > 0)
            {
                this.ByteArray = reader.ReadBytes(byteLength);
            }
            if (version > 1)
            {
                int keyLength = reader.ReadInt32();
                this.ExtendedId = reader.ReadBytes(keyLength);
            }
        }

		/// <summary>
		/// Deserializes a <see cref="RelayPayload"/> from an <see cref="IPrimitiveReader"/>.
		/// </summary>
		/// <param name="reader">The reader</param>
        public void Deserialize(IPrimitiveReader reader)
        {
            Deserialize(reader, CurrentVersion);
		}

		/// <summary>
		/// Gets the data version of this instance for serialization purposes.
		/// </summary>
		/// <value>A version number is used as the number and type of serializable data in the class change, 
		/// so that serialization can successfully manage between different version.</value>
		public int CurrentVersion
        {
            get { return (ExtendedId == null) ? 1 : 2; }
        }

		/// <summary>
		/// Gets a value indicating if the data is Volatile
		/// </summary>
		/// <value>If this value is true exceptions will be thrown to the client if anything
		/// other than a successful result is returned.
		/// </value>
        public bool Volatile
        {
            get { return false; }
        }

        #endregion
    }

}
