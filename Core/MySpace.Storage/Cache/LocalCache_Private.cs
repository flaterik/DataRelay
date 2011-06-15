using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;
using MySpace.Storage;
using MySpace.Storage.Cache.Configuration;
using MySpace.Logging;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// API for storing objects. Supports time to live, and object dependencies. Uses
	/// an <see cref="IObjectStorage"/> instance to support operations.
	/// </summary>
	public static partial class LocalCache
	{
		#region Fields
		private static IObjectStorage _storage;

		/// <summary>
		/// Gets the storage used to for fetching and retrieving objects.
		/// Is provided for testing purposes only.
		/// </summary>
		/// <value>The underlying <see cref="IObjectStorage"/>.</value>
		internal static IObjectStorage Storage { get { return _storage; } }

		/// <summary>
		/// Gets the type policy used to provide type specific settings.
		/// </summary>
		/// <value>The configured <see cref="ITypePolicy"/>.</value>
		internal static ITypePolicy Policy { get; private set; }

		private static LogWrapper _log;
		#endregion

		private static List<ObjectDependency> ProcessDependencies(ObjectReference header,
			IEnumerable<ObjectDependency> dependencyList, OperationType op)
		{
			List<ObjectDependency> retained = null;
			foreach (var dependency in dependencyList)
			{
				var retain = false;
				try
				{
					retain = dependency.Notify(header, op);
				}
				catch (Exception exc)
				{
					if (_log.IsErrorEnabled)
					{
						_log.ErrorFormat("Dependent '{0}' for object id='{1}' threw {2}: {3}",
							 dependency, header, exc.GetType().FullName,
							 exc.Message);
					}
				}
				if (retain)
				{
					if (retained == null)
					{
						retained = new List<ObjectDependency>();
					}
					retained.Add(dependency);
				}
			}
			return retained;
		}

		private static DataBuffer GetKeySpace<T>(T instance, IVirtualCacheType virtualCacheObject)
			where T : ICacheParameter
		{
			return TypeStatic<T>.GetDescription(instance, virtualCacheObject).KeySpace;
		}

		private static DataBuffer GetKeySpace<T>()
			where T : ICacheParameter
		{
			if (TypeStatic<T>.IsVirtual)
			{
				throw new ApplicationException(string.Format(
					"Cannot be called for virtual cache type {0} without a virtual cache instance, please use an overload that supplies an instance",
					TypeStatic<T>.TypeName));
			}
			TypeStatic<T>.AssertDescriptionFound(TypeStatic<T>.Description,
				TypeStatic<T>.TypeName);
			return TypeStatic<T>.Description.KeySpace;
		}

		private static DataBuffer GetKeySpace<T>(IVirtualCacheType virtualCacheObject)
			where T : ICacheParameter
		{
			return virtualCacheObject != null ?
				GetKeySpace(default(T), virtualCacheObject) :
				GetKeySpace<T>();
		}

		private static DateTime GetRefreshExpires<T>(T instance, IVirtualCacheType virtualCacheObject)
			where T : ICacheParameter
		{
			var ttl = TypeStatic<T>.GetDescription(instance, virtualCacheObject).Ttl;
			return DateTime.Now.AddSeconds(ttl);
		}

		private static DateTime GetRefreshExpires(DataBuffer typeId)
		{
			var description = Policy.GetDescription(typeId);
			return DateTime.Now.AddSeconds(description.Ttl);
		}

		private static void ProcessDependencies(IObjectList<ObjectDependency,
			ObjectReference> dependencies, OperationType op)
		{
			var retained = ProcessDependencies(dependencies.Header,
				dependencies, op);
			if (retained == null)
			{
				DeleteDependencies(dependencies.Header.TypeId, dependencies.Header.ObjectId);
			}
			else
			{
				dependencies.Clear();
				dependencies.AddRange(retained);
			}
		}

		private static void ProcessObjectDependencies(DataBuffer typeId, StorageKey key, OperationType op)
		{
			var dependencies = GetDependencies(typeId, key, false);
			if (!dependencies.IsFound) return;
			using (var dependencyList = dependencies.Instance)
			{
				ProcessDependencies(dependencyList, op);
			}
		}

		private static DataBuffer GetDependencyKey(DataBuffer typeId, StorageKey key)
		{
			return Utility.CombineHashCodes(typeId.GetHashCode(), key.GetHashCode());
		}

		private static DataBuffer ListTypeId { get { return TypeStatic<ObjectDependency>.GetDescription(null, null).KeySpace ; } }

		private static readonly Func<ObjectDependency> CreateBlankDependency =  () => new ObjectDependency();

		private static readonly Func<ObjectReference> CreateBlankReference = () => new ObjectReference();

		private static StorageEntry<IObjectList<ObjectDependency, ObjectReference>>
			GetDependencies(DataBuffer typeId, StorageKey key, bool create)
		{
			var listKey = GetDependencyKey(typeId, key);
			if (create)
			{
				var ttl = TypeStatic<ObjectDependency>.GetDescription(null, null).Ttl;
				var expires = DateTime.Now.AddSeconds(ttl);
				return _storage.GetOrCreateList(ListTypeId, listKey,
					new ObjectReference(typeId, key), expires, CreateBlankDependency,
					CreateBlankReference);
			}
			return _storage.GetList(ListTypeId, listKey, CreateBlankDependency,
				CreateBlankReference);
		}

		private static void ThrowUpdatedDateTimeNotAllowed()
		{
			throw new ArgumentOutOfRangeException("options.updated");
		}

		/// <summary>
		/// Deletes an entry if it is not newer than a cutoff.
		/// </summary>
		/// <param name="typeId">The <see cref="DataBuffer"/> type
		/// identifier.</param>
		/// <param name="key">The <see cref="StorageKey"/> entry
		/// identifier.</param>
		/// <param name="updated">The <see cref="DateTime"/> version
		/// cut off.</param>
		/// <returns><see langword="true"/> if the entry existed prior and was
		/// deleted; otherwise <see langword="false"/>.</returns>
		internal static bool DeleteVersion(DataBuffer typeId, StorageKey key, DateTime updated)
		{
			if (!_storage.DeleteVersion(typeId, key, updated))
			{
				return false;
			}
			ProcessObjectDependencies(typeId, key, OperationType.Delete);
			return true;
		}

		private static void SaveDependency(DataBuffer typeId, ObjectReference reference,
			StorageKey key, DateTime updated, DependencyType type)
		{
			var dependencies = GetDependencies(reference.TypeId, reference.ObjectId, true);
			using (var dependencyList = dependencies.Instance)
			{
				dependencyList.Add(new ObjectDependency(new ObjectReference(
					typeId, key), updated, type));
			}
		}

		private static void SaveDependencies(DataBuffer typeId, StorageKey key,
			DateTime updated, IEnumerable<ObjectReference> contentDependencies,
			IEnumerable<ObjectReference> existenceDependencies)
		{
			ProcessObjectDependencies(typeId, key, OperationType.Save);
			if (contentDependencies != null)
			{
				foreach (var reference in contentDependencies)
				{
					SaveDependency(typeId, reference, key, updated, DependencyType.Content);
				}
			}
			if (existenceDependencies != null)
			{
				foreach (var reference in existenceDependencies)
				{
					SaveDependency(typeId, reference, key, updated, DependencyType.Existence);
				}
			}
		}

		private static void DeleteDependencies(DataBuffer typeId, StorageKey key)
		{
			_storage.DeleteList(ListTypeId, GetDependencyKey(typeId, key));
		}

		private static void StorageItemDropped(object sender, ObjectEventArgs e)
		{
			if (e.IsList)
			{
				var dependencies = e.Instance as
					IObjectList<ObjectDependency, ObjectReference>;
				if (dependencies != null)
				{
					using (dependencies)
					{
						ProcessDependencies(dependencies, OperationType.Delete);
					}
					return;
				}
				var objectDependencies = e.Instance as IObjectList<object, object>;
				if (objectDependencies != null)
				{
					using(objectDependencies)
					{
						var reference = (ObjectReference) objectDependencies.Header;
						var retained = ProcessDependencies(reference,
							objectDependencies.Select(o => (ObjectDependency) o), OperationType.Delete);
						if (retained == null) return;
						var entry = GetDependencies(reference.TypeId, reference.ObjectId,
							true);
						using (var dependencyList = entry.Instance)
						{
							foreach(var dep in retained)
							{
								dependencyList.Add(dep);							
							}
						}
					}
				}
			}
			else
			{
				ProcessObjectDependencies(e.Reference.TypeId, e.Reference.ObjectId,
				                          OperationType.Delete);
			}
		}

		static LocalCache()
		{
			_log = new LogWrapper();
			ITypePolicy policy = null;
			IObjectStorage storage = null;
			var section = LocalCacheConfigurationSection.GetInstance();
			if (section != null)
			{
				if (section.TypePolicy != null)
				{
					policy = section.TypePolicy.ObtainInstance();
				}
				if (section.Storage != null)
				{
					storage = section.Storage.ObtainInstance();
				}
			}

			if (policy == null)
			{
				_log.WarnFormat("ITypePolicy implementation not found.  Local caching disabled.");
				policy = new ByNameTypePolicyFactory().ObtainInstance(); // We need a policy as TypeStace and CacheTypeStatic calls need one.
			}

			if (storage == null)
			{
				_log.WarnFormat("IObjectStorage implementation not found.  Local caching disabled.");
			}

			Policy = policy;

			if (storage != null)
			{
				_storage = storage;
				_storage.Dropped += StorageItemDropped;
			}
			else
			{
				_storage = null;
			}
		}

		private static bool IsLocalCachingConfigured()
		{
			return _storage != null;
		}
	}
}
