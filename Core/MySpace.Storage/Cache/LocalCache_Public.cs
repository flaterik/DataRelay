using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;

using DataSource = MySpace.Common.Framework.DataSource;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// API for storing objects. Supports time to live, and object dependencies. Uses
	/// an <see cref="IObjectStorage"/> instance to support operations.
	/// </summary>
	public static partial class LocalCache
	{
		#region Get
		/// <summary>
		/// Retrieves an object from cache.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="options">The <see cref="LocalCacheOptions"/> to use for this
		/// operation.</param>
		/// <returns><see langword="true"/> if the object was found, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static StorageEntry<T> Get<T>(StorageKey key,
			LocalCacheOptions options) where T : ICacheParameter
		{
			if (!IsLocalCachingConfigured()) return StorageEntry<T>.NotFound;

			CacheTypeStatic<T>.AssertProperStorageKey(key);
			var entry = _storage.Get(GetKeySpace<T>(options.VirtualCacheObject),
				key, TypeStatic<T>.Creator);
			if (entry.IsFound)
			{
				entry.Instance.DataSource = DataSource.Cache;
			}
			return entry;
		}

		/// <summary>
		/// Retrieves an object from cache.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <returns><see langword="true"/> if the object was found, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static StorageEntry<T> Get<T>(StorageKey key) where T : ICacheParameter
		{
			return Get<T>(key, LocalCacheOptions.None);
		}

		/// <summary>
		/// Retrieves an object from cache.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. Instance to supply key, and virtual cache
		/// type if any.</param>
		/// <returns><see langword="true"/> if the object was found, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static StorageEntry<T> Get<T>(T instance)
			where T : ICacheParameter
		{
			if (!IsLocalCachingConfigured()) return StorageEntry<T>.NotFound;

			TypeStatic<T>.AssertNotNull("instance", instance);

			var entry = _storage.Get(GetKeySpace(instance, null), GetKey(instance),
				instance);
			if (entry.IsFound)
			{
				entry.Instance.DataSource = DataSource.Cache;
			}
			return entry;
		}

		/// <summary>
		/// Retrieves an object from cache.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		/// <param name="key">The key of the object.</param>
		/// <param name="creator">Delegate that provides an empty instance for
		/// unerlying stores that use serialization. May be <see langword="null"/>
		/// for underlying stores if serialization not used or if store can create
        /// empty instances of <see cref="ICacheParameter"/> on its own.</param>
		/// <returns><see langword="true"/> if the object was found, otherwise
		/// <see langword="false"/>.</returns>
		public static StorageEntry<ICacheParameter> Get(DataBuffer typeId,
			StorageKey key, Func<ICacheParameter> creator)
		{
			if (!IsLocalCachingConfigured()) return StorageEntry<ICacheParameter>.NotFound;

			var entry = _storage.Get(typeId, key, creator);
			if (entry.IsFound)
			{
				entry.Instance.DataSource = DataSource.Cache;
			}
			return entry;
		}
		#endregion

		#region Save
		/// <summary>
		/// Stores an object to cache.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to store.</param>
		/// <param name="options">The <see cref="LocalCacheOptions"/> to use for this
		/// operation.</param>
		/// <returns>The <see cref="StorageEntry{T}"/> containing the data
		/// saved.  <see cref="StorageEntry{T}.NotFound"/> if local caching is disabled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para><see cref="LocalCacheOptions.Updated"/> of <paramref name="options"/>
		/// has a value specified, which is not allowed for the cache type of
		/// <typeparamref name="T"/>.</para>
		/// </exception>
		public static StorageEntry<T> Save<T>(T instance, LocalCacheOptions options)
			where T : ICacheParameter
		{
			if (!IsLocalCachingConfigured()) return StorageEntry<T>.NotFound;

			TypeStatic<T>.AssertNotNull("instance", instance);
			DateTime? updated = null;
			if (CacheTypeStatic<T>.IsExtendedCache)
			{
				var ext = (IExtendedCacheParameter)instance;
				updated = ext.LastUpdatedDate;
			}
			else if (CacheTypeStatic<T>.IsExtendedRawCache)
			{
				var extRaw = (IExtendedRawCacheParameter)instance;
				updated = extRaw.LastUpdatedDate;
			}
			if (updated.HasValue)
			{
				if (options.Updated.HasValue && updated.Value !=
					options.Updated.Value)
				{
					ThrowUpdatedDateTimeNotAllowed();
				}
			}
			else
			{
				updated = options.Updated ?? DateTime.Now;
			}
			var key = CacheTypeStatic<T>.GetKey(instance);
			var entry = new StorageEntry<T>(instance, updated.Value,
				GetRefreshExpires(instance, null));
			var typeId = GetKeySpace(instance, null);
			_storage.Put(typeId, key, entry);
			SaveDependencies(typeId, key, updated.Value, options.ContentDependencies,
				options.ExistenceDependencies);
			return entry;
		}

		/// <summary>
		/// Stores an object to cache with a default last updated date.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to store.</param>
		/// <param name="updated">The updated <see cref="DateTime"/> for
		/// the object.</param>
		/// <returns>The <see cref="StorageEntry{T}"/> containing the data
		/// saved.  <see cref="StorageEntry{T}.NotFound"/> if local caching is disabled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>The cache type of <typeparamref name="T"/> does not allow
		/// an updated <see cref="DateTime"/> to be provided.</para>
		/// </exception>
		/// <param name="contentDependencies">Optional array of
		/// <see cref="ObjectReference"/> specifying the cached objects whose
		/// dependencies <paramref name="instance"/> is added.</param>
		public static StorageEntry<T> Save<T>(T instance, DateTime updated,
			params ObjectReference[] contentDependencies)
			where T : ICacheParameter
		{
			return Save(instance, new LocalCacheOptions
           	{
           		Updated = updated,
				ContentDependencies = contentDependencies
           	});
		}

		/// <summary>
		/// Stores an object to cache with a default last updated date.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to store.</param>
		/// <returns>The <see cref="StorageEntry{T}"/> containing the data
		/// saved.  <see cref="StorageEntry{T}.NotFound"/> if local caching is disabled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <param name="contentDependencies">Optional array of
		/// <see cref="ObjectReference"/> specifying the cached objects whose
		/// dependencies <paramref name="instance"/> is added.</param>
		public static StorageEntry<T> Save<T>(T instance,
			params ObjectReference[] contentDependencies)
			where T : ICacheParameter
		{
			return Save(instance, new LocalCacheOptions
			{
				ContentDependencies = contentDependencies
			});
		}

		/// <summary>
		/// Stores an object to cache.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to store.</param>
		/// <returns>The <see cref="StorageEntry{T}"/> containing the data
		/// saved.  <see cref="StorageEntry{T}.NotFound"/> if local caching is disabled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static StorageEntry<T> Save<T>(T instance) where T : ICacheParameter
		{
			return Save(instance, LocalCacheOptions.None);
		}

		/// <summary>
		/// Stores an object to cache.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		/// <param name="instance">Required. The object to store.</param>
		/// <param name="options">The <see cref="LocalCacheOptions"/> to use for this
		/// operation.</param>
		/// <returns>The <see cref="StorageEntry{ICacheParameter}"/> containing the data
		/// saved. <see cref="StorageEntry{T}.NotFound"/> if local caching is disabled.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static StorageEntry<ICacheParameter> Save(DataBuffer typeId, ICacheParameter instance, LocalCacheOptions options)
		{
			if (instance == null) throw new ArgumentNullException("instance");

			if (!IsLocalCachingConfigured()) return StorageEntry<ICacheParameter>.NotFound;

			DateTime? updated = null;
			StorageKey key;
			var ext = instance as IExtendedCacheParameter;
			if (ext != null)
			{
				key = new StorageKey(ext.ExtendedId, ext.PrimaryId);
				updated = ext.LastUpdatedDate;
			}
			else
			{
				var extRaw = instance as IExtendedRawCacheParameter;
				if (extRaw != null)
				{
					key = new StorageKey(extRaw.ExtendedId, extRaw.PrimaryId);
					updated = extRaw.LastUpdatedDate;
				}
				else
				{
					key = instance.PrimaryId;
				}
			}
			if (updated.HasValue)
			{
				if (options.Updated.HasValue && updated.Value !=
					options.Updated.Value)
				{
					ThrowUpdatedDateTimeNotAllowed();
				}
			}
			else
			{
				updated = options.Updated ?? DateTime.Now;
			}
			var entry = new StorageEntry<ICacheParameter>(instance, updated.Value,
				GetRefreshExpires(typeId));
			_storage.Put(typeId, key, entry);
			SaveDependencies(typeId, key, updated.Value, options.ContentDependencies,
				options.ExistenceDependencies);
			return entry;
		}
		#endregion

		#region Refresh Expires
		/// <summary>
		/// Refreshes the expiration time of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to refresh.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the new expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static DateTime? RefreshExpires<T>(T instance) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);

			return RefreshExpires(
				GetKeySpace(instance, null),
				CacheTypeStatic<T>.GetKey(instance));
		}

		/// <summary>
		/// Refreshes the expiration time of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="options">Supplies virtual cache type, if any.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the new expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static DateTime? RefreshExpires<T>(StorageKey key, LocalCacheOptions options) where T : ICacheParameter
		{
			CacheTypeStatic<T>.AssertProperStorageKey(key);
			return RefreshExpires(
				GetKeySpace<T>(options.VirtualCacheObject),
				key);
		}

		/// <summary>
		/// Refreshes the expiration time of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the new expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static DateTime? RefreshExpires<T>(StorageKey key) where T : ICacheParameter
		{
			return RefreshExpires<T>(key, LocalCacheOptions.None);
		}

		/// <summary>
		/// Refreshes the expiration time of a cached object.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		/// <param name="key">The key of the object.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the new expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		public static DateTime? RefreshExpires(DataBuffer typeId, StorageKey key)
		{
			if (!IsLocalCachingConfigured()) return null;

			var expires = GetRefreshExpires(typeId);
			if (_storage.SetExpires(typeId, key, expires))
			{
				return expires;
			}
			return null;
		}
		#endregion

		#region Get Expires
		/// <summary>
		/// Gets the expiration time of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to refresh.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static DateTime? GetExpires<T>(T instance) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);

			return GetExpires(
				GetKeySpace(instance, null),
				CacheTypeStatic<T>.GetKey(instance));
		}

		/// <summary>
		/// Gets the time to live of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="options">Supplies virtual cache type, if any.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static DateTime? GetExpires<T>(StorageKey key, LocalCacheOptions options) where T : ICacheParameter
		{
			CacheTypeStatic<T>.AssertProperStorageKey(key);
			return GetExpires(
				GetKeySpace<T>(options.VirtualCacheObject),
				key);
		}

		/// <summary>
		/// Gets the time to live of a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static DateTime? GetExpires<T>(StorageKey key) where T : ICacheParameter
		{
			return GetExpires<T>(key, LocalCacheOptions.None);
		}

		/// <summary>
		/// Gets the time to live of a cached object.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		/// <param name="key">The key of the object.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <paramref name="typeId"/>.
		/// </para>
		/// </exception>
		public static DateTime? GetExpires(DataBuffer typeId, StorageKey key)
		{
			if (!IsLocalCachingConfigured()) return null;

			return _storage.GetExpires(typeId, key);			
		}
		#endregion

		#region Delete
		/// <summary>
		/// Deletes a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">Required. The object to refresh.</param>
		/// <returns><see langword="true"/> if the object existed prior, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static bool Delete<T>(T instance) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);
			return Delete(
				GetKeySpace(instance, null),
				CacheTypeStatic<T>.GetKey(instance));
		}

		/// <summary>
		/// Deletes a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <param name="options">Supplies virtual cache type, if any.</param>
		/// <returns><see langword="true"/> if the object existed prior, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static bool Delete<T>(StorageKey key, LocalCacheOptions options) where T : ICacheParameter
		{
			CacheTypeStatic<T>.AssertProperStorageKey(key);
			return Delete(GetKeySpace<T>(options.VirtualCacheObject), key);
		}

		/// <summary>
		/// Deletes a cached object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="key">The key of the object.</param>
		/// <returns><see langword="true"/> if the object existed prior, otherwise
		/// <see langword="false"/>.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="key"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static bool Delete<T>(StorageKey key) where T : ICacheParameter
		{
			return Delete<T>(key, LocalCacheOptions.None);
		}

		/// <summary>
		/// Deletes a cached object.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		/// <param name="key">The key of the object.</param>
		/// <returns><see langword="true"/> if the object existed prior, otherwise
		/// <see langword="false"/>.</returns>
		public static bool Delete(DataBuffer typeId, StorageKey key)
		{
			if (!IsLocalCachingConfigured()) return false;

			if (!_storage.Delete(typeId, key))
			{
				return false;
			}
			ProcessObjectDependencies(typeId, key, OperationType.Delete);
			return true;
		}
		#endregion

		#region Clear
		/// <summary>
		/// Deletes all cached objects of a type.
		/// </summary>
		/// <typeparam name="T">The type of the objects.</typeparam>
		/// <param name="options">Supplies virtual cache type, if any.</param>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static void Clear<T>(LocalCacheOptions options) where T : ICacheParameter
		{
			Clear(GetKeySpace<T>(options.VirtualCacheObject));
		}

		/// <summary>
		/// Deletes all cached objects of a type.
		/// </summary>
		/// <typeparam name="T">The type of the objects.</typeparam>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static void Clear<T>() where T : ICacheParameter
		{
			Clear<T>(LocalCacheOptions.None);
		}

		/// <summary>
		/// Deletes all cached objects of a type.
		/// </summary>
		/// <param name="typeId">The type identifier of the object.</param>
		public static void Clear(DataBuffer typeId)
		{
			if (!IsLocalCachingConfigured()) return;

			_storage.Clear(typeId);
		}
		#endregion

		#region Dependencies
		/// <summary>
		/// Adds a custom object dependency to the dependencies of a list of cached objects.
		/// </summary>
		/// <param name="dependent">The <see cref="IObjectDependency"/> to be added.</param>
		/// <param name="references">Array of <see cref="ObjectReference"/> specifying the
		/// cached objects whose dependencies <paramref name="dependent"/> is added.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="dependent"/> is <see langword="null"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="references"/> is <see langword="null"/>.</para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="dependent"/> is empty.</para>
		/// </exception>
		public static void AddDependent(IObjectDependency dependent, params ObjectReference[] references)
		{
			if (dependent == null)
			{
				throw new ArgumentNullException("dependent");
			}
			if (references == null)
			{
				throw new ArgumentNullException("references");			
			}
			var len = references.Length;
			if (len == 0)
			{
				throw new ArgumentOutOfRangeException("references");							
			}
			for(var idx = 0; idx < len; ++idx)
			{
				var reference = references[idx];
				var dependencies = GetDependencies(reference.TypeId, reference.ObjectId, true);
				using (var dependencyList = dependencies.Instance)
				{
					dependencyList.Add(new ObjectDependency(dependent));
				}
			}
		}
		#endregion

		#region References
		/// <summary>
		/// Generates a reference to an object.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="instance">The <typeparamref name="T"/>
		/// being referred to.</param>
		/// <returns>The <see cref="ObjectReference"/> that specifies
		/// <paramref name="instance"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>		
		public static ObjectReference CreateReference<T>(T instance) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);
			return new ObjectReference(GetKeySpace(instance, null),
				CacheTypeStatic<T>.GetKey(instance));
		}

		/// <summary>
		/// Generates a array of references to an array of objects.
		/// </summary>
		/// <typeparam name="T">The type of the objects.</typeparam>
		/// <param name="instances">The <typeparamref name="T"/> array
		/// being referred to.</param>
		/// <returns>The array of <see cref="ObjectReference"/>s that
		/// contains references specified by <paramref name="instances"/>.</returns>
		/// <exception cref="ArgumentNullException">
		/// 	<para><paramref name="instances"/> is <see langword="null"/>.</para>
		/// </exception>		
		public static ObjectReference[] CreateReferences<T>(params T[] instances) where T : ICacheParameter
		{
			if (instances == null)
			{
				throw new ArgumentNullException("instances");
			}
			var ret = new ObjectReference[instances.Length];
			var idx = 0;
			foreach (var instance in instances)
			{
				ret[idx] = new ObjectReference(GetKeySpace(instance, null),
					CacheTypeStatic<T>.GetKey(instance));
				++idx;
			}
			return ret;
		}

		/// <summary>
		/// Generates a reference to an object by identifier.
		/// </summary>
		/// <typeparam name="T">The type of the object. Must not
		/// implement <see cref="IExtendedCacheParameter"/> or
		/// <see cref="IExtendedRawCacheParameter"/>.</typeparam>
		/// <param name="objectId">The <see cref="StorageKey"/>
		/// of the object.</param>
		/// <param name="options">Supplies virtual cache type, if any.</param>
		/// <returns>The <see cref="ObjectReference"/> that specifies
		/// the object.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="objectId"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type and <paramref name="options"/>
		/// has a null <see cref="LocalCacheOptions.VirtualCacheObject"/>.
		/// </exception>
		public static ObjectReference CreateReference<T>(StorageKey objectId,
			LocalCacheOptions options)
			where T : ICacheParameter
		{
			CacheTypeStatic<T>.AssertProperStorageKey(objectId);
			return new ObjectReference(GetKeySpace<T>(options.VirtualCacheObject),
				objectId);
		}

		/// <summary>
		/// Generates a reference to an object by identifier.
		/// </summary>
		/// <typeparam name="T">The type of the object. Must not
		/// implement <see cref="IExtendedCacheParameter"/> or
		/// <see cref="IExtendedRawCacheParameter"/>.</typeparam>
		/// <param name="objectId">The <see cref="StorageKey"/>
		/// of the object.</param>
		/// <returns>The <see cref="ObjectReference"/> that specifies
		/// the object.</returns>
		/// <exception cref="ArgumentException">
		/// <para>Type of <see cref="StorageKey.Key"/> of <paramref name="objectId"/>
		/// is wrong for the cache type of <typeparamref name="T"/>.
		/// </para>
		/// </exception>
		/// <exception cref="ApplicationException">
		/// <typeparamref name="T"/> is a virtual cache type.
		/// </exception>
		public static ObjectReference CreateReference<T>(StorageKey objectId)
			where T : ICacheParameter
		{
			return CreateReference<T>(objectId, LocalCacheOptions.None);
		}
		#endregion

		#region Get and Set Key
		/// <summary>
		/// Gets the key corresponding to an object.
		/// </summary>
		/// <typeparam name="T">The type of <paramref name="instance"/>.</typeparam>
		/// <param name="instance">The object.</param>
		/// <returns>The <see cref="StorageKey"/> identifier key.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static StorageKey GetKey<T>(T instance) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);
			return CacheTypeStatic<T>.GetKey(instance);
		}

		/// <summary>
		/// Gets the key corresponding to an object.
		/// </summary>
		/// <param name="instance">The object.</param>
		/// <returns>The <see cref="StorageKey"/> identifier key.</returns>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static StorageKey GetKey(ICacheParameter instance)
		{
			if (instance == null) throw new ArgumentNullException("instance");
			var ext = instance as IExtendedCacheParameter;
			if (ext != null)
			{
				return new StorageKey(ext.ExtendedId, ext.PrimaryId);
			}
			var extRaw = instance as IExtendedRawCacheParameter;
			if (extRaw != null)
			{
				return new StorageKey(extRaw.ExtendedId, extRaw.PrimaryId);
			}
			return instance.PrimaryId;
		}

		/// <summary>
		/// Sets an object's key properties.
		/// </summary>
		/// <typeparam name="T">The type of <paramref name="instance"/>.</typeparam>
		/// <param name="instance">The object.</param>
		/// <param name="key">The <see cref="StorageKey"/> identifier key.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void SetKey<T>(T instance, StorageKey key) where T : ICacheParameter
		{
			TypeStatic<T>.AssertNotNull("instance", instance);
			instance.PrimaryId = key.PartitionId;
			if (CacheTypeStatic<T>.IsExtendedCache)
			{
				((IExtendedCacheParameter)instance).ExtendedId =
					key.Key.ToString();
			}
			else if (CacheTypeStatic<T>.IsExtendedRawCache)
			{
				((IExtendedRawCacheParameter)instance).ExtendedId =
					key.Key.GetBinary();				
			}
		}

		/// <summary>
		/// Sets an object's key properties.
		/// </summary>
		/// <param name="instance">The object.</param>
		/// <param name="key">The <see cref="StorageKey"/> identifier key.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public static void SetKey(ICacheParameter instance, StorageKey key)
		{
			if (instance == null) throw new ArgumentNullException("instance");
			instance.PrimaryId = key.PartitionId;
			var ext = instance as IExtendedCacheParameter;
			if (ext != null)
			{
				ext.ExtendedId = key.Key.ToString();
				return;
			}
			var extRaw = instance as IExtendedRawCacheParameter;
			if (extRaw != null)
			{
				extRaw.ExtendedId = key.Key.GetBinary();
			}
		}

		/// <summary>
		/// Gets the type identifier for an instance.
		/// </summary>
		/// <typeparam name="T">The type of <paramref name="instance"/>.</typeparam>
		/// <param name="instance">The instance used to obtain a type
		/// identifier. Ignored if <typeparamref name="T"/> is not
		/// <see cref="IVirtualCacheType"/>.</param>
		/// <returns>The <see cref="DataBuffer"/> type identifier for
		/// <paramref name="instance"/>.</returns>
		public static DataBuffer GetTypeId<T>(T instance)
			where T : ICacheParameter
		{
			return GetKeySpace(instance, null);
		}
		#endregion

		#region Cache Properties
		/// <summary>
		/// Gets whether <typeparamref name="T"/> does not implement any
		/// interfaces besides <see cref="ICacheParameter"/>.
		/// </summary>
		/// <typeparam name="T">The type of cache parameter.</typeparam>
		/// <returns><see langword="true"/> if <typeparamref name="T"/> does
		/// not implement any interfaces besides <see cref="ICacheParameter"/>;
		/// otherwise <see langword="false"/>.</returns>
		public static bool GetIsPlainCache<T>() where T : ICacheParameter
		{
			return CacheTypeStatic<T>.IsPlainCache;
		}

		/// <summary>
		/// Gets whether <typeparamref name="T"/> implements
		/// <see cref="IExtendedCacheParameter"/>.
		/// </summary>
		/// <typeparam name="T">The type of cache parameter.</typeparam>
		/// <returns><see langword="true"/> if <typeparamref name="T"/>
		/// implements <see cref="IExtendedCacheParameter"/>; otherwise
		/// <see langword="false"/>.</returns>
		public static bool GetIsExtendedCache<T>() where T : ICacheParameter
		{
			return CacheTypeStatic<T>.IsExtendedCache;
		}

		/// <summary>
		/// Gets whether <typeparamref name="T"/> implements
		/// <see cref="IExtendedRawCacheParameter"/>.
		/// </summary>
		/// <typeparam name="T">The type of cache parameter.</typeparam>
		/// <returns><see langword="true"/> if <typeparamref name="T"/>
		/// implements <see cref="IExtendedRawCacheParameter"/>; otherwise
		/// <see langword="false"/>.</returns>
		public static bool GetIsExtendedRawCache<T>() where T : ICacheParameter
		{
			return CacheTypeStatic<T>.IsExtendedRawCache;
		}
		#endregion
	}
}
