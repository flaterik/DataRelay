using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common;
using MySpace.Common.Storage;

namespace MySpace.Storage.Cache
{
	/// <summary>
	/// API for storing objects. Supports time to live, and object dependencies. Uses
	/// an <see cref="IObjectStorage"/> instance to support operations.
	/// </summary>
	public static partial class LocalCache
	{
		/// <summary>
		/// Supplies object type dependent information and calculations.
		/// </summary>
		/// <typeparam name="T">The type of cached objects.</typeparam>
		private static class TypeStatic<T>
		{
			/// <summary>
			/// Whether <typeparamref name="T"/> is <see cref="IVirtualCacheType"/>.
			/// </summary>
			public static readonly bool IsVirtual;

			/// <summary>
			/// Whether <typeparamref name="T"/> is reference type.
			/// </summary>
			private static readonly bool _isReferenceType;

			/// <summary>
			/// The full name of <typeparamref name="T"/>.
			/// </summary>
			public static readonly string TypeName;

			/// <summary>
			/// The <see cref="TypeDescription"/> corresponding to <typeparamref name="T"/>.
			/// </summary>
			public static TypeDescription Description { get; private set; }

			/// <summary>
			/// The <see cref="Func{T}"/> that creates blank instances for
			/// optional object storage usage.
			/// </summary>
			public static Func<T> Creator { get; private set; }

			private static readonly Factory<string, TypeDescription> _virtualDescriptions;

			static TypeStatic()
			{
				var type = typeof(T);
				TypeName = type.FullName;
				Description = Policy.GetDescription(type);
				_isReferenceType = !type.IsValueType;
				IsVirtual = typeof(IVirtualCacheType).IsAssignableFrom(type);
				if (IsVirtual)
				{
					_virtualDescriptions = Algorithm.LazyIndexer<string, TypeDescription>(
						virtualCacheType => Policy.GetDescription(virtualCacheType)
                  	);
				}
				var factoryCreator = Description.GetCreator<T>();
				if (factoryCreator != null)
				{
					Creator = () => factoryCreator();
				}
			}

			/// <summary>
			/// Gets the type description for an instance.
			/// </summary>
			/// <param name="instance">Instance used to supply virtual cache type,
			/// if any. Ignored if <typeparamref name="T"/> doesn't implement
			/// <see cref="IVirtualCacheType"/> or <paramref name="virtualCacheObject"/>
			/// is not null.</param>
			/// <param name="virtualCacheObject">Object used to supply custom
			/// description. Ignored if <typeparamref name="T"/> doesn't implement
			/// <see cref="IVirtualCacheType"/>.</param>
			/// <returns>The <see cref="TypeDescription"/></returns>
			public static TypeDescription GetDescription(T instance,
				IVirtualCacheType virtualCacheObject)
			{
				var description = Description;
				var typeName = TypeName;
				if (IsVirtual)
				{
					string cacheType = null;
					if (virtualCacheObject != null)
					{
						cacheType = virtualCacheObject.CacheTypeName;
					}
					if (string.IsNullOrEmpty(cacheType) &&
						(!_isReferenceType || instance != null))
					{
						cacheType = ((IVirtualCacheType)instance).CacheTypeName;
					}
					if (!string.IsNullOrEmpty(cacheType))
					{
						typeName = cacheType;
						description = _virtualDescriptions(cacheType);
					}
				}
				AssertDescriptionFound(description, typeName);
				return description;
			}

			/// <summary>
			/// Asserts that a parameter instance of <typeparamref name="T"/>
			/// isn't <see langword="null"/>.
			/// </summary>
			/// <param name="paramName">The name of the parameter.</param>
			/// <param name="instance">The instance to test.</param>
			/// <exception cref="ArgumentNullException">
			/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
			/// </exception>
			public static void AssertNotNull(string paramName, T instance)
			{
				if (_isReferenceType && ReferenceEquals(instance, null))
				{
					throw new ArgumentNullException(paramName);
				}
			}

			/// <summary>
			/// Asserts that a type description was found.
			/// </summary>
			/// <param name="description">The type description.</param>
			/// <param name="typeName">The type name corresponding to
			/// <paramref name="description"/>.</param>
			public static void AssertDescriptionFound(TypeDescription description,
				string typeName)
			{
				if (description.IsNotFound)
				{
					throw new ApplicationException(string.Format(
						"Type description for {0} not found", typeName));
				}
			}
 
		}

		/// <summary>
		/// Supplies cached object type dependent information and calculations.
		/// </summary>
		/// <typeparam name="T">The type of cached objects.</typeparam>
		private static class CacheTypeStatic<T> where T : ICacheParameter
		{
			/// <summary>
			/// Whether <typeparamref name="T"/> implements
			/// <see cref="IExtendedCacheParameter"/>.
			/// </summary>
			public static readonly bool IsExtendedCache;

			/// <summary>
			/// Whether <typeparamref name="T"/> implements
			/// <see cref="IExtendedRawCacheParameter"/>.
			/// </summary>
			public static readonly bool IsExtendedRawCache;

			/// <summary>
			/// Whether <typeparamref name="T"/> doesn't implement
			/// <see cref="IExtendedRawCacheParameter"/> or
			/// <see cref="IExtendedRawCacheParameter"/>.
			/// </summary>
			public static readonly bool IsPlainCache;

			static CacheTypeStatic()
			{
				var type = typeof(T);
				IsExtendedCache = typeof(IExtendedCacheParameter).IsAssignableFrom(
					type);
				IsExtendedRawCache = typeof(IExtendedRawCacheParameter).IsAssignableFrom(
					type);
				IsPlainCache = !(IsExtendedCache || IsExtendedRawCache);
			}

			/// <summary>
			/// Gets the key corresponding to an object.
			/// </summary>
			/// <param name="instance">The object.</param>
			/// <returns>The <see cref="StorageKey"/> identifier key.</returns>
			public static StorageKey GetKey(T instance)
			{
				DataBuffer buffer;
				if (IsExtendedCache)
				{
					buffer = ((IExtendedCacheParameter)instance).ExtendedId;
				}
				else if (IsExtendedRawCache)
				{
					buffer = ((IExtendedRawCacheParameter)instance).ExtendedId;
				}
				else
				{
					buffer = instance.PrimaryId;
				}
				return new StorageKey(buffer, instance.PrimaryId);
			}

			/// <summary>
			/// Asserts that a storage key is the proper type for
			/// <typeparamref name="T"/>.
			/// </summary>
			/// <param name="key">The <see cref="StorageKey"/>.</param>
			/// <exception cref="ArgumentException">
			/// <para>
			/// <typeparamref name="T"/> is a plain cache type, but <paramref name="key"/>
			/// doesn't contain an <see cref="Int32"/> <see cref="StorageKey.Key"/>.
			/// </para>
			/// <para>-or-</para>
			/// <para>
			/// <typeparamref name="T"/> is an extended cache type, but <paramref name="key"/>
			/// doesn't contain a <see cref="String"/> or empty <see cref="StorageKey.Key"/>.
			/// </para>
			/// <para>-or-</para>
			/// <para>
			/// <typeparamref name="T"/> is an extended raw cache type, but <paramref name="key"/>
			/// doesn't contain a <see cref="Byte"/> array or empty <see cref="StorageKey.Key"/>.
			/// </para>
			/// </exception>
			public static void AssertProperStorageKey(StorageKey key)
			{
				var keyType = key.Key.Type;
				if (IsPlainCache)
				{
					if (keyType != DataBufferType.Int32)
					{
						throw new ArgumentException(string.Format(
							"Key type {0} not allowed for plain cache parameter type",
							keyType), "key");
					}
				}
				else if (IsExtendedCache)
				{
					switch (keyType)
					{
						case DataBufferType.Empty:
						case DataBufferType.String:
							break;
						default:
							throw new ArgumentException(string.Format(
								"Key type {0} not allowed for extended cache parameter type",
								keyType), "key");
					}
				}
				else if (IsExtendedRawCache)
				{
					switch (keyType)
					{
						case DataBufferType.Empty:
						case DataBufferType.ByteArraySegment:
							break;
						default:
							throw new ArgumentException(string.Format(
								"Key type {0} not allowed for extended raw cache parameter type",
								keyType), "key");
					}
				}
			}
		}
	}
}
