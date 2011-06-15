using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySpace.Common.Storage;
using MySpace.Storage.Cache;

namespace MySpace.Storage
{
	/// <summary>
	/// An <see cref="IStorage"/> for objects.
	/// </summary>
	public interface IObjectStorage : IStorage
	{
		#region Data Operations

		#region Atomic
		/// <summary>
		/// Reads an object from the store.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="creator">Delegate that provides an empty instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="T"/> on its own.</param>
		/// <returns>A <see cref="StorageEntry{T}"/> containing the entry.</returns>
		StorageEntry<T> Get<T>(DataBuffer keySpace, StorageKey key, Func<T> creator);

		/// <summary>
		/// Reads an object from the store.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="instance">An empty instance for stores that use serialization.
		/// May be <see langword="null"/> for stores if serialization not used or if
		/// store can create empty instances of <typeparamref name="T"/> on its own.</param>
		/// <returns>A <see cref="StorageEntry{T}"/> containing the entry.</returns>
		StorageEntry<T> Get<T>(DataBuffer keySpace, StorageKey key, T instance);

		/// <summary>
		/// Reads an object from the store, or creates and stores an object if not found.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="creator">A <see cref="Func{StorageEntry}"/> that returns the
		/// <see cref="StorageEntry{T}"/> to store and return if not found. Can also
		/// be called to provide instance for deserialization for serializing stores.</param>
		/// <returns>The object read or created.</returns>
		StorageEntry<T> GetOrCreate<T>(DataBuffer keySpace, StorageKey key, Func<StorageEntry<T>> creator);
		
		/// <summary>
		/// Writes an object to the store.
		/// </summary>
		/// <typeparam name="T">The type of the object.</typeparam>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="entry">The entry to write.</param>
		void Put<T>(DataBuffer keySpace, StorageKey key, StorageEntry<T> entry);

		/// <summary>
		/// Deletes an object from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns><see langword="true"/> if the entry existed prior; otherwise
		/// <see langword="false"/>.</returns>
		bool Delete(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Deletes an object from the store by version.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="updated">The last updated <see cref="DateTime"/> of the
		/// version to be deleted. If the entry's last updated date is not newer
		/// than this value, then the entry will be deleted, otherwise not.</param>
		/// <returns><see langword="true"/> if the entry was deleted; otherwise
		/// <see langword="false"/>. The entry is deleted if it existed prior and
		/// was as old or older than <paramref name="updated"/>.</returns>
		bool DeleteVersion(DataBuffer keySpace, StorageKey key, DateTime updated);

		/// <summary>
		/// Gets whether or not the object exists within the store.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns><see langword="true"/> if the entry exists; otherwise
		/// <see langword="false"/>.</returns>
		bool Exists(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Gets the expiration time of an object.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time if found; otherwise <see langword="null"/>.</returns>
		DateTime? GetExpires(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Sets the expiration time of an object.
		/// </summary>
		/// <param name="keySpace">The key space of the entry.</param>
		/// <param name="key">The key of the entry.</param>
		/// <param name="expires">The expires <see cref="DateTime"/>.</param>
		/// <returns><see langword="true"/> if the entry exists so its time to live
		/// could be set; otherwise <see langword="false"/>.</returns>
		bool SetExpires(DataBuffer keySpace, StorageKey key, DateTime expires);

		/// <summary>
		/// Clears all entries of a particular key space.
		/// </summary>
		/// <param name="keySpace">The key space of the entries to be cleared.</param>
		void Clear(DataBuffer keySpace);
		#endregion

		#region List

		/// <summary>
		/// Gets a list of objects from the store.
		/// </summary>
		/// <typeparam name="T">The type of the objects in the list.</typeparam>
		/// <typeparam name="THeader">The type of the list's header information.</typeparam>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <param name="creator">Delegate that provides an empty object instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="T"/> on its own.</param>
		/// <param name="headerCreator">Delegate that provides an empty header instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="THeader"/> on its own.</param>
		/// <returns>The <see cref="StorageEntry{IObjectList}"/> containing
		/// the fetched list.</returns>
		StorageEntry<IObjectList<T, THeader>> GetList<T, THeader>(DataBuffer keySpace, StorageKey key,
			Func<T> creator, Func<THeader> headerCreator);

		/// <summary>
		/// Creates a new empty list of objects and writes it to the store.
		/// </summary>
		/// <typeparam name="T">The type of the objects in the list.</typeparam>
		/// <typeparam name="THeader">The type of the list's header information.</typeparam>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <param name="header">A <typeparamref name="THeader"/> containing the
		/// list's header information.</param>
		/// <param name="expires">The expiration <see cref="DateTime"/> of the list.</param>
		/// <param name="creator">Delegate that provides an empty object instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="T"/> on its own.</param>
		/// <returns>The created <see cref="IObjectList{T, THeader}"/>. If a list
		/// already exists in the specified <paramref name="keySpace"/> and
		/// <paramref name="key"/>, that list is discarded.</returns>
		IObjectList<T, THeader> CreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime expires,
			Func<T> creator);
		
		/// <summary>
		/// Gets a list of objects from the store, or creates one if the list is
		/// not found.
		/// </summary>
		/// <typeparam name="T">The type of the objects in the list.</typeparam>
		/// <typeparam name="THeader">The type of the list's header information.</typeparam>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <param name="header">A <typeparamref name="THeader"/> containing the
		/// header information of the new list, if one is created.</param>
		/// <param name="expires">The expiration <see cref="DateTime"/> to use
		/// if the list must be created.</param>
		/// <param name="creator">Delegate that provides an empty object instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="T"/> on its own.</param>
		/// <param name="headerCreator">Delegate that provides an empty header instance for
		/// stores that use serialization. May be <see langword="null"/> for
		/// stores if serialization not used or if store can create empty
		/// instances of <typeparamref name="T"/> on its own.</param>
		/// <returns>The <see cref="StorageEntry{IObjectList}"/> read or created.</returns>
		StorageEntry<IObjectList<T, THeader>> GetOrCreateList<T, THeader>(DataBuffer keySpace, StorageKey key, THeader header, DateTime expires,
			Func<T> creator, Func<THeader> headerCreator);

		/// <summary>
		/// Deletes a list from the store.
		/// </summary>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <returns><see langword="true"/> if the list existed prior; otherwise
		/// <see langword="false"/>.</returns>		
		bool DeleteList(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Gets the expiration time of a list.
		/// </summary>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <returns>A <see cref="Nullable{DateTime}"/> containing the expiration
		/// time of the list if found; otherwise <see langword="null"/>.</returns>
		DateTime? GetListExpires(DataBuffer keySpace, StorageKey key);

		/// <summary>
		/// Sets the expiration time of a list.
		/// </summary>
		/// <param name="keySpace">The key space of the list.</param>
		/// <param name="key">The key of the list.</param>
		/// <param name="expires">The expiration <see cref="DateTime"/>.</param>
		/// <returns><see langword="true"/> if the list exists so its expiration time
		/// could be set; otherwise <see langword="false"/>.</returns>
		bool SetListExpires(DataBuffer keySpace, StorageKey key, DateTime expires);
		#endregion

		#endregion

		#region Notifications
		/// <summary>
		/// Occurs when an object or list is dropped from the store. Does not occur
		/// for deletions initiated by client called deletions, but only when the
		/// store itself drops the entry, for example due to lack of space or time
		/// based expiration.
		/// </summary>
		event EventHandler<ObjectEventArgs> Dropped;
		#endregion
	}
}
