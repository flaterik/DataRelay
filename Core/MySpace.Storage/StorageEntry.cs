using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MySpace.Storage
{
	/// <summary>
	/// Provides information for a storage entry.
	/// </summary>
	/// <typeparam name="T">The type of the entry object.</typeparam>
	public struct StorageEntry<T>
	{
		/// <summary>
		/// Gets whether the entry was found.
		/// </summary>
		/// <value><see langword="true"/> if found; otherwise
		/// <see langword="false"/>.</value>
		public bool IsFound { get; private set; }

		/// <summary>
		/// Gets the instance in the storage entry, if any.
		/// </summary>
		/// <value>The stored <typeparamref name="T"/> instance if
		/// <see cref="IsFound"/> is <see langword="true"/>; otherwise the
		/// default value of <typeparamref name="T"/>.</value>
		public T Instance { get; private set; }

		/// <summary>
		/// Gets the time that the entry object was updated.
		/// </summary>
		/// <value>The <see cref="DateTime"/> that <see cref="Instance"/>
		/// was last updated if <see cref="IsFound"/> is <see langword="true"/>,
		/// otherwise <see cref="DateTime.MinValue"/>.</value>
		public DateTime Updated { get; private set; }

		/// <summary>
		/// Gets the time that the entry will expire.
		/// </summary>
		/// <value>The <see cref="DateTime"/> that the entry will expire
		/// if <see cref="IsFound"/> is <see langword="true"/>,
		/// otherwise <see cref="DateTime.MinValue"/>.</value>
		public DateTime Expires { get; private set; }

		private static readonly bool _isRef = !typeof(T).IsValueType;

		/// <summary>
		/// 	<para>Initializes a new instance of the <see cref="StorageEntry{T}"/> structure
		///		for entry found condition.</para>
		/// </summary>
		/// <param name="instance">
		/// 	<para>The entry <typeparamref name="T"/> instance.</para>
		/// </param>
		/// <param name="updated">
		/// 	<para>The <see cref="DateTime"/> <paramref name="instance"/> was last updated.</para>
		/// </param>
		/// <param name="expires">
		/// 	<para>The <see cref="DateTime"/> the entry will expire.</para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="instance"/> is <see langword="null"/>.</para>
		/// </exception>
		public StorageEntry(T instance, DateTime updated, DateTime expires) : this()
		{
			if (_isRef && ReferenceEquals(instance, null))
			{
				throw new ArgumentNullException("instance");
			}
			IsFound = true;
			Instance = instance;
			Updated = updated;
			Expires = expires;
		}

		private static readonly StorageEntry<T> _notFound = new StorageEntry<T>();

		/// <summary>
		/// Gets the <see cref="StorageEntry{T}"/> corresponding to entry
		/// not found.
		/// </summary>
		public static StorageEntry<T> NotFound { get { return _notFound; } }
	}
}
